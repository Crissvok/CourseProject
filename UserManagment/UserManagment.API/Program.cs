using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using UserManagement.Data;
using UserManagment.API.Services;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.AspNetCore.OpenApi;
using Mysqlx.Datatypes;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(); // Регистрация контроллеров

builder.Services.AddOpenApi();

builder.Services.AddScoped<IUserService, UserService>(); // Регистрация сервисов

//1. Настройка jwt токеннов авторизации
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForUserManagementCourseProject2026!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "UserManagementAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "UserManagementClient";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = ClaimTypes.Role
    };
});

builder.Services.AddAuthorization();

//2. Проверяем доступность локального MySQL
bool isMySqlRunning = IsPortOpen("localhost", 3306);
var mySqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (isMySqlRunning)
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySQL(mySqlConnectionString!));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=usermanagement_db.sqlite"));
}



var app = builder.Build();

//3. Инициализация и автоматический перенос из SQLite в MySQL
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();

        // Если мы дома (MySQL активен) и существует локальный файл SQLite
        string sqlitePath = "usermanagement_db.sqlite";
        if (isMySqlRunning && File.Exists(sqlitePath))
        {
            logger.LogInformation("[СИНХРОНИЗАЦИЯ] Обнаружен файл SQLite. Проверяем перенос данных в MySQL...");

            // Создаем временный контекст для чтения из SQLite файла
            var sqliteOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={sqlitePath}")
                .Options;

            using (var sqliteContext = new AppDbContext(sqliteOptions))
            {
                var sqliteUsers = sqliteContext.Users.AsNoTracking().ToList();

                // Получаем ID пользователей, которые уже есть в MySQL
                var existingMySqlIds = context.Users.Select(u => u.Id).ToHashSet();

                var newUsers = sqliteUsers
                    .Where(u => !existingMySqlIds.Contains(u.Id))
                    .ToList();

                if (newUsers.Any())
                {
                    // Для явной вставки конкретных ID из SQLite
                    using var transaction = context.Database.BeginTransaction();

                    context.Users.AddRange(newUsers);
                    context.SaveChanges();

                    transaction.Commit();
                    logger.LogInformation($"[СИНХРОНИЗАЦИЯ] Успешно перенесено {newUsers.Count} новых пользователей из SQLite в MySQL!");
                }
                else
                {
                    logger.LogInformation("[СИНХРОНИЗАЦИЯ] Все пользователи из SQLite уже есть в MySQL.");
                }
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError($"Ошибка при инициализации/миграции БД: {ex.Message}");
    }
}
//4. Запус всего и вся
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapOpenApi();
app.MapScalarApiReference();
app.Run();

static bool IsPortOpen(string host, int port)
{
    try
    {
        using var client = new TcpClient();
        var result = client.BeginConnect(host, port, null, null);
        bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(400));
        return success && client.Connected;
    }
    catch
    {
        return false;
    }
}

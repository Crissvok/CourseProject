using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using UserManagement.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// 1. Проверяем доступность локального MySQL
bool isMySqlRunning = IsPortOpen("localhost", 3306);
var mySqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var serverVersion = new MySqlServerVersion(new Version(8, 0, 30));

if (isMySqlRunning)
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(mySqlConnectionString, serverVersion));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=usermanagement_db.sqlite"));
}

var app = builder.Build();

// 2. Инициализация и автоматический перенос из SQLite в MySQL
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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
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

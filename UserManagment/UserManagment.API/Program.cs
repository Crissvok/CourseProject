using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using UserManagement.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// 1. Проверяем, запущен ли локальный сервер MySQL (порт 3306)
bool isMySqlRunning = IsPortOpen("localhost", 3306);

if (isMySqlRunning)
{
    // ДОМА: подключаемся к MySQL из appsettings.json
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var serverVersion = new MySqlServerVersion(new Version(8, 0, 30));

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseMySql(connectionString, serverVersion));
}
else
{
    // В КОЛЛЕДЖЕ: автоматически переключаемся на локальный файл SQLite в папке проекта
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=usermanagement_db.sqlite"));
}

var app = builder.Build();

// 2. Автоматическое создание БД, таблицы Users и начальных записей при старте
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<AppDbContext>();

        // Создаёт базу и таблицу, если их ещё нет
        bool isCreated = context.Database.EnsureCreated();

        string dbMode = isMySqlRunning ? "MySQL (сервер)" : "SQLite (файл usermanagement_db.sqlite)";
        if (isCreated)
        {
            logger.LogInformation($"[АВТОРАЗВЕРТЫВАНИЕ] База данных успешно создана с нуля! Режим: {dbMode}");
        }
        else
        {
            logger.LogInformation($"[АВТОРАЗВЕРТЫВАНИЕ] База данных уже существует и подключена. Режим: {dbMode}");
        }
    }
    catch (Exception ex)
    {
        logger.LogError($"Ошибка при автоматическом развертывании БД: {ex.Message}");
    }
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Вспомогательная функция проверки доступности MySQL (таймаут 400 мс)
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
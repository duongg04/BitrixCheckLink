using BitrixChecker.Data;
using BitrixChecker.Services;
using BitrixChecker.Workers;
using Hangfire;
using Hangfire.MySql;
using Microsoft.EntityFrameworkCore;
using System.Net; 

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("System", LogLevel.Warning); 
builder.Logging.AddFilter("Microsoft", LogLevel.Warning); 
builder.Logging.AddFilter("Hangfire", LogLevel.Warning); 
builder.Logging.AddFilter("BitrixChecker", LogLevel.Information);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var connectionStringWithPool = connectionString + ";Pooling=true;Min Pool Size=0;Max Pool Size=100;Connection Lifetime=0;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionStringWithPool,
        ServerVersion.AutoDetect(connectionStringWithPool),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure(10, TimeSpan.FromSeconds(2), null)
    ));

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseStorage(new MySqlStorage(connectionStringWithPool, new MySqlStorageOptions
    {
        TransactionIsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
        QueuePollInterval = TimeSpan.FromSeconds(1),
        JobExpirationCheckInterval = TimeSpan.FromHours(1),
        CountersAggregateInterval = TimeSpan.FromMinutes(1),
        PrepareSchemaIfNecessary = true,
        DashboardJobListLimit = 20000,
        TransactionTimeout = TimeSpan.FromMinutes(5),
        TablesPrefix = "hangfire"
    })));

builder.Services.AddHangfireServer(options =>
{
    // [CHUẨN] 6 Worker x 80 Thread = 480 request/s (Rất mạnh)
    options.WorkerCount = 6; 
});

builder.Services.AddHttpClient("BitrixClient", client =>
{
    // [CHUẨN] Timeout 10s. Đủ thời gian cho web load, không chờ chết quá lâu.
    client.Timeout = TimeSpan.FromSeconds(10); 
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.ConnectionClose = false;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = false, 
    UseCookies = false,
    AutomaticDecompression = DecompressionMethods.All,
    PooledConnectionLifetime = TimeSpan.FromMinutes(10), 
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2), 
    MaxConnectionsPerServer = 1000, // Mở rộng kết nối
    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
    {
        RemoteCertificateValidationCallback = delegate { return true; }
    },
    ConnectTimeout = TimeSpan.FromSeconds(5)
});

builder.Services.AddSingleton<LinkBufferService>();
builder.Services.AddHostedService<DbWriterBackgroundService>();
builder.Services.AddScoped<BitrixService>();
builder.Services.AddControllers();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire");
app.MapControllers();

RecurringJob.AddOrUpdate<BitrixService>("daily-recheck", x => x.RecheckActiveLinks(), Cron.Daily);

app.Run();
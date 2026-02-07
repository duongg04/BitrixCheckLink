using BitrixChecker.Data;
using BitrixChecker.Services;
using BitrixChecker.Workers;
using Hangfire;
using Hangfire.Dashboard; 
using Hangfire.MySql;
using Microsoft.EntityFrameworkCore;
using System.Net; 

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Log cho gọn nhẹ
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("System", LogLevel.Warning); 
builder.Logging.AddFilter("Microsoft", LogLevel.Warning); 
builder.Logging.AddFilter("Hangfire", LogLevel.Warning); 
builder.Logging.AddFilter("BitrixChecker", LogLevel.Information);

// 2. Xử lý chuỗi kết nối (Thêm dấu ; nếu thiếu)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString) && !connectionString.EndsWith(";"))
{
    connectionString += ";";
}
var connectionStringWithPool = connectionString + "Pooling=true;Min Pool Size=0;Max Pool Size=100;Connection Lifetime=0;";

// 3. Kết nối Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionStringWithPool,
        ServerVersion.AutoDetect(connectionStringWithPool),
        mysqlOptions => mysqlOptions.EnableRetryOnFailure(10, TimeSpan.FromSeconds(2), null)
    ));

// 4. Cấu hình Hangfire (Database MySQL)
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
    options.WorkerCount = 6; 
});

// 5. Cấu hình HTTP Client (Mạng)
builder.Services.AddHttpClient("BitrixClient", client =>
{
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
    MaxConnectionsPerServer = 1000,
    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
    {
        RemoteCertificateValidationCallback = delegate { return true; }
    },
    ConnectTimeout = TimeSpan.FromSeconds(5)
});

// 6. Đăng ký các Service
builder.Services.AddSingleton<LinkBufferService>();
builder.Services.AddHostedService<DbWriterBackgroundService>();
builder.Services.AddScoped<BitrixService>();
builder.Services.AddControllers();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// [QUAN TRỌNG] Cấu hình Dashboard cho phép truy cập từ Docker (Sửa lỗi 401)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AllowAllAuthorizationFilter() }
});

app.MapControllers();

// Đăng ký Job chạy định kỳ (Theo múi giờ Việt Nam)
RecurringJob.AddOrUpdate<BitrixService>("daily-recheck", x => x.RecheckActiveLinks(), Cron.Daily, TimeZoneInfo.Local);

app.Run();

// [CLASS MỚI] Bộ lọc cho phép tất cả mọi người truy cập Dashboard
public class AllowAllAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true; // Luôn trả về true để bỏ qua bảo mật (Dùng cho nội bộ/Docker)
    }
}
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BitrixChecker.Configuration;
using BitrixChecker.Data;
using BitrixChecker.Middleware;
using BitrixChecker.Models;
using BitrixChecker.Services;
using BitrixChecker.Services.ScanEngine;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.MySql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("Hangfire", LogLevel.Warning);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<ScanOptions>(builder.Configuration.GetSection(ScanOptions.SectionName));
builder.Services.Configure<SeedAdminOptions>(builder.Configuration.GetSection(SeedAdminOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32 || string.IsNullOrWhiteSpace(jwt.Issuer) ||
    string.IsNullOrWhiteSpace(jwt.Audience) || jwt.ExpiryMinutes is < 5 or > 1440)
{
    throw new InvalidOperationException("JWT configuration is invalid. Set Jwt__Key (at least 32 characters), Jwt__Issuer, Jwt__Audience and Jwt__ExpiryMinutes.");
}

var scan = builder.Configuration.GetSection(ScanOptions.SectionName).Get<ScanOptions>() ?? new ScanOptions();
if (scan.HttpTimeoutSeconds is < 1 or > 120 || scan.ConnectTimeoutSeconds is < 1 or > 60 ||
    scan.WorkerCount is < 1 or > 64 || scan.Parallelism is < 1 or > 200 || scan.BatchSize is < 1 or > 5000 || scan.MaxConnectionsPerServer is < 1 or > 500 ||
    scan.MinLength is < 1 or > 63 || scan.MaxLength is < 1 or > 63 || scan.MaxLength < scan.MinLength ||
    scan.RetryCount is < 0 or > 5 || scan.RetryDelayMs is < 0 or > 10000 ||
    string.IsNullOrWhiteSpace(scan.TargetBaseDomain) || scan.TargetBaseDomain.Contains('/') || scan.TargetBaseDomain.Contains(':'))
{
    throw new InvalidOperationException("Scan configuration is outside its safe limits.");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings__DefaultConnection must be configured.");
}

if (!connectionString.EndsWith(';')) connectionString += ";";
var databaseConnectionString = connectionString + "Pooling=true;Min Pool Size=0;Max Pool Size=100;Connection Lifetime=0;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(databaseConnectionString, ServerVersion.AutoDetect(databaseConnectionString),
        mysql => mysql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null)));

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
    {
        // AddIdentity installs the Identity cookie as the default authenticate/challenge
        // scheme. Without these overrides every [Authorize] endpoint challenges via the
        // cookie scheme and redirects to /Account/Login (which has no implementation),
        // so JWT bearer tokens issued by /api/auth/login were never validated.
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
        .Build();
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("AdminOrUser", policy => policy.RequireRole("Admin", "User"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("auth", limiter =>
        {
            limiter.PermitLimit = 10;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
            limiter.AutoReplenishment = true;
        });
        options.AddFixedWindowLimiter("scan", limiter =>
        {
            limiter.PermitLimit = 30;
            limiter.Window = TimeSpan.FromMinutes(1);
            limiter.QueueLimit = 0;
            limiter.AutoReplenishment = true;
        });
    });

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BitrixChecker API", Version = "v1", Description = "Internal Bitrix24 link management API." });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});

builder.Services.AddHangfire(configuration => configuration.SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings()
    .UseStorage(new MySqlStorage(databaseConnectionString, new MySqlStorageOptions { PrepareSchemaIfNecessary = true, QueuePollInterval = TimeSpan.FromSeconds(1), TablesPrefix = "hangfire" })));
builder.Services.AddHangfireServer(options => options.WorkerCount = scan.WorkerCount);

builder.Services.AddHttpClient("BitrixClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(scan.HttpTimeoutSeconds);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("BitrixChecker/1.0");
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    UseCookies = false,
    AutomaticDecompression = DecompressionMethods.All,
    PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
    MaxConnectionsPerServer = scan.MaxConnectionsPerServer,
    ConnectTimeout = TimeSpan.FromSeconds(scan.ConnectTimeoutSeconds)
});

builder.Services.AddScoped<IdentitySeedService>();

builder.Services.AddScoped<ISubdomainGenerator, SubdomainGenerator>();
builder.Services.AddScoped<ILinkDetector, LinkDetector>();
builder.Services.AddScoped<ILinkResultSaver, LinkResultSaver>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IScanJobService, ScanJobService>();
builder.Services.AddScoped<ScanJobs>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
    {
        policy.WithOrigins("https://bitrix24.vn")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseCors("AdminPolicy");
app.UseMiddleware<CorrelationIdMiddleware>();
app.Use((context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    return next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "BitrixChecker API v1"));

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = new[] { new AdminOnlyDashboardAuthorizationFilter() } });

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<IdentitySeedService>().InitializeAsync();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapGet("/error", () => Results.Problem("An unexpected server error occurred.")).AllowAnonymous();
RecurringJob.AddOrUpdate<ScanJobs>("daily-recheck", x => x.RecheckJob(), scan.RecheckCron, new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
app.Run();

public sealed class AdminOnlyDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext?.User?.IsInRole("Admin") == true;
    }
}

public partial class Program { }

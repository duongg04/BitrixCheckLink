using BitrixChecker.Data;
using BitrixChecker.Services;
using Microsoft.EntityFrameworkCore;

namespace BitrixChecker.Workers
{
    public class DbWriterBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly LinkBufferService _buffer;
        private readonly ILogger<DbWriterBackgroundService> _logger;

        public DbWriterBackgroundService(IServiceProvider serviceProvider, LinkBufferService buffer, ILogger<DbWriterBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _buffer = buffer;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            GlobalState.Load(); 
            _logger.LogInformation($"[SYSTEM] Khôi phục: {GlobalState.TotalScanned}/{GlobalState.TotalExpected}");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    GlobalState.Save();

                    // LOG TIẾN ĐỘ Ở ĐÂY (3 giây hiện 1 lần)
                    // Đây là log duy nhất bạn sẽ thấy, giúp bạn biết tool vẫn chạy mà không làm chậm máy
                    double percent = GlobalState.TotalExpected > 0 ? (double)GlobalState.TotalScanned / GlobalState.TotalExpected * 100 : 0;
                    _logger.LogInformation($"[TIẾN ĐỘ] Đã quét: {GlobalState.TotalScanned:N0} / {GlobalState.TotalExpected:N0} ({percent:F2}%)");

                    if (_buffer.Count > 0)
                    {
                        var linksToSave = _buffer.DequeueChunk(500); 
                        if (linksToSave.Any())
                        {
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                                var domains = linksToSave.Select(x => x.Subdomain).ToList();
                                var existing = await db.CheckedLinks.AsNoTracking().Where(x => domains.Contains(x.Subdomain)).Select(x => x.Subdomain).ToListAsync(stoppingToken);
                                var newLinks = linksToSave.Where(x => !existing.Contains(x.Subdomain)).ToList();

                                if (newLinks.Any())
                                {
                                    await db.CheckedLinks.AddRangeAsync(newLinks, stoppingToken);
                                    await db.SaveChangesAsync(stoppingToken);
                                    _logger.LogInformation($"[DATABASE] Đã lưu {newLinks.Count} active mới!");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Lỗi Writer: {ex.Message}");
                }
                
                // Nghỉ 3 giây trước khi báo cáo tiếp
                await Task.Delay(3000, stoppingToken);
            }
        }
    }
}
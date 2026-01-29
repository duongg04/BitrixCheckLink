using Hangfire;
using BitrixChecker.Data;
using BitrixChecker.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Net.Http; 
using System.IO; 

namespace BitrixChecker.Services
{
    public class BitrixService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BitrixService> _logger;
        private readonly LinkBufferService _bufferService;

        // [CHUẨN] Batch 5.000: Cân bằng tải tốt nhất cho Database
        public const int BATCH_SIZE = 5000; 
        private const string CHARS = "abcdefghijklmnopqrstuvwxyz0123456789";
        public static bool IsSystemPaused { get; set; } = false;

        public BitrixService(IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory, ILogger<BitrixService> logger, LinkBufferService bufferService)
        {
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _bufferService = bufferService;
        }

        [AutomaticRetry(Attempts = 0)]
        [JobDisplayName("Master: Sinh Link ({0} - {1})")]
        public async Task GenerateRangeJob(int minLength, int maxLength)
        {
            // Test thử aasc đầu tiên để yên tâm
            BackgroundJob.Enqueue<BitrixService>(x => x.CheckBatchJob(new List<string> { "aasc" }));

            string? currentString = new string(CHARS[0], minLength);
            List<string> batchToSend = new List<string>(BATCH_SIZE);

            _logger.LogInformation($"[START] Bắt đầu quét từ: {currentString}");

            while (currentString != null && currentString.Length <= maxLength)
            {
                while (IsSystemPaused) await Task.Delay(1000);
                batchToSend.Add(currentString);

                if (batchToSend.Count >= BATCH_SIZE)
                {
                    Interlocked.Add(ref GlobalState.TotalExpected, batchToSend.Count);
                    GlobalState.Save();
                    var batchCopy = new List<string>(batchToSend);
                    BackgroundJob.Enqueue<BitrixService>(x => x.CheckBatchJob(batchCopy));
                    batchToSend.Clear(); 
                    await Task.Delay(50); 
                }
                currentString = GetNextString(currentString, maxLength);
            }

            if (batchToSend.Count > 0)
            {
                Interlocked.Add(ref GlobalState.TotalExpected, batchToSend.Count);
                GlobalState.Save();
                BackgroundJob.Enqueue<BitrixService>(x => x.CheckBatchJob(batchToSend));
            }
        }

        [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
        [JobDisplayName("Worker: Lọc ({0} links)")]
        public async Task CheckBatchJob(List<string> subdomains)
        {
            if (GlobalState.TotalExpected == 0) return;

            while (IsSystemPaused) await Task.Delay(1000);

            var client = _httpClientFactory.CreateClient("BitrixClient");
            
            // [CHUẨN] 80 Luồng: Đủ nhanh để lấp đầy băng thông nhưng không làm đơ máy
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 80 };

            await Parallel.ForEachAsync(subdomains, parallelOptions, async (sub, cancellationToken) =>
            {
                if (IsSystemPaused || GlobalState.TotalExpected == 0) return;
                
                long current = Interlocked.Increment(ref GlobalState.TotalScanned);
                
                // Log mỗi 1000: Đủ để theo dõi mà không bị spam
                if (current % 1000 == 0) 
                {
                    _logger.LogInformation($"[STANDARD] Đã quét: {current:N0}...");
                }

                try
                {
                    var url = $"https://{sub}.bitrix24.com";
                    
                    // [TỐI ƯU] Chỉ Retry 1 lần (Tổng 2 lần thử). 
                    // Nếu 2 lần đều lỗi thì 99% là link chết, bỏ qua luôn cho nhanh.
                    for (int attempt = 1; attempt <= 2; attempt++)
                    {
                        try
                        {
                            using var response = await client.GetAsync(url, cancellationToken);
                            int code = (int)response.StatusCode;
                            bool isActive = false;

                            // 1. REDIRECT 
                            if (code == 301 || code == 302)
                            {
                                var location = response.Headers.Location?.ToString() ?? "";
                                if (location.Contains("/auth/") || 
                                    location.Contains("login=yes") || 
                                    location.Contains("oauth") ||          
                                    location.Contains("bitrix24.net"))     
                                {
                                    isActive = true; 
                                }
                            }
                            // 2. 200 OK
                            else if (code == 200)
                            {
                                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                                if (content.Contains("name=\"USER_LOGIN\"") || 
                                    content.Contains("name='USER_LOGIN'") ||
                                    content.Contains("login-item") ||
                                    content.Contains("b24-network-auth-form"))
                                {
                                    if (!content.Contains("Create new Bitrix24", StringComparison.OrdinalIgnoreCase))
                                    {
                                        isActive = true;
                                    }
                                }
                            }

                            if (isActive)
                            {
                                // Ghi file ngay lập tức
                                try { await File.AppendAllTextAsync("active_found.txt", $"{sub}|{url}|{code}\n"); } catch {}

                                _bufferService.Enqueue(new CheckedLink
                                {
                                    Subdomain = sub,
                                    FullUrl = url,
                                    Status = "ACTIVE",
                                    HttpCode = code,
                                    SaleStatus = "New"
                                });
                                _logger.LogInformation($"\n✅ [TÌM THẤY] {sub} (Code: {code})\n");
                                break; 
                            }
                            
                            // Nếu lỗi 404/403/500 rõ ràng thì dừng luôn, không retry
                            if (code >= 400) break;
                            break; 
                        }
                        catch 
                        { 
                            // Retry sau 1.5s
                            if (attempt < 2) await Task.Delay(1500, cancellationToken);
                        }
                    }
                }
                catch { }
            });
        }

        private string? GetNextString(string input, int totalLimit)
        {
            char[] result = input.ToCharArray();
            int index = result.Length - 1;
            while (index >= 0)
            {
                int charIndex = CHARS.IndexOf(result[index]);
                if (charIndex < CHARS.Length - 1) { result[index] = CHARS[charIndex + 1]; return new string(result); }
                result[index] = CHARS[0]; index--;
            }
            if (input.Length < totalLimit) return new string(CHARS[0], input.Length + 1);
            return null;
        }
        public void RecheckActiveLinks() {}
    }
}
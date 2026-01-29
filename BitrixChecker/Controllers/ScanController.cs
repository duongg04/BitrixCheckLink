using Microsoft.AspNetCore.Mvc;
using BitrixChecker.Services;
using Hangfire;

namespace BitrixChecker.Controllers
{
    [Route("api/scan")]
    [ApiController]
    public class ScanController : ControllerBase
    {
        // API chỉ nhận nhiệm vụ kích hoạt Job, trả về ngay lập tức
        [HttpPost("generate")]
        public IActionResult Generate([FromBody] ScanRequest req)
        {
            if (req == null) return BadRequest("Dữ liệu rỗng");
            
            // [FIX] Reset bộ đếm trước khi chạy mới
            GlobalState.Reset(0); 

            BackgroundJob.Enqueue<BitrixService>(x => x.GenerateRangeJob(req.MinLength, req.MaxLength));
            
            return Ok(new { message = $"Đã kích hoạt quét từ {req.MinLength} đến {req.MaxLength}." });
        }
    }

    public class ScanRequest
    {
        public int MinLength { get; set; }
        public int MaxLength { get; set; }
    }
}
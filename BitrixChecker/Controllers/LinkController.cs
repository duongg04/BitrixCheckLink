using Microsoft.AspNetCore.Mvc;
using BitrixChecker.Data;
using BitrixChecker.Models;
using BitrixChecker.Services;
using Hangfire; 
using Hangfire.Storage;
using System.Text;
using Microsoft.EntityFrameworkCore; 

namespace BitrixChecker.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LinkController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LinkController(AppDbContext context) { _context = context; }

        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            long pending = GlobalState.TotalExpected - GlobalState.TotalScanned;
            if (pending < 0) pending = 0;

            return Ok(new
            {
                Total = _context.CheckedLinks.Count(), 
                Active = _context.CheckedLinks.Count(x => x.Status == "ACTIVE"),
                Inactive = 0, 
                Pending = pending, 
                Scanned = GlobalState.TotalScanned,
                Processed = _context.CheckedLinks.Count(x => x.SaleStatus != "New"),
                IsPaused = BitrixService.IsSystemPaused
            });
        }

        [HttpPost("pause")]
        public IActionResult TogglePause([FromQuery] bool pause)
        {
            BitrixService.IsSystemPaused = pause;
            return Ok(new { message = pause ? "ĐÃ TẠM DỪNG HỆ THỐNG" : "HỆ THỐNG TIẾP TỤC CHẠY", isPaused = BitrixService.IsSystemPaused });
        }

        [HttpGet("list")]
        public IActionResult GetList([FromQuery] string status, [FromQuery] int page = 1)
        {
            int pageSize = 50;
            var query = _context.CheckedLinks.AsQueryable();
            if (!string.IsNullOrEmpty(status) && status != "ALL") query = query.Where(x => x.Status == status);
            int totalRecords = query.Count();
            var data = query.OrderBy(x => x.Subdomain).Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Ok(new { data = data, total = totalRecords });
        }

        [HttpPut("update-sales/{id}")]
        public IActionResult UpdateSales(int id, [FromBody] SalesUpdateModel req)
        {
            var link = _context.CheckedLinks.Find(id);
            if (link == null) return NotFound();
            link.SaleNote = req.Note;
            link.SaleStatus = req.Status;
            link.AssignedUser = req.User;
            _context.SaveChanges();
            return Ok(new { message = "Đã cập nhật" });
        }

        [HttpDelete("delete")]
        public IActionResult DeleteByStatus([FromQuery] string status)
        {
            if (string.IsNullOrEmpty(status)) return BadRequest("Cần chọn hành động");

            // 1. XÓA PENDING (Nút màu Vàng)
            if (status == "PENDING") 
            {
                // Bước 1: Dừng hệ thống ngay lập tức
                BitrixService.IsSystemPaused = true; 
                
                // Bước 2: Reset bộ đếm về 0 để các Worker tự hủy việc
                GlobalState.Reset(0); 

                // Bước 3: Dọn sạch hàng đợi trong Hangfire (Để không còn việc tồn đọng)
                var monitor = JobStorage.Current.GetMonitoringApi();
                var queues = monitor.Queues();
                foreach (var queue in queues)
                {
                    // Lấy tối đa 100.000 job để xóa cho sạch (tăng số lượng lên để chắc chắn)
                    var jobs = monitor.EnqueuedJobs(queue.Name, 0, 100000); 
                    foreach (var job in jobs)
                    {
                        BackgroundJob.Delete(job.Key);
                    }
                }

                // Xóa cả các job đang lên lịch (Scheduled) nếu có
                // (Thường dùng cho RecurringJob, nhưng xóa luôn cho sạch)
                
                return Ok(new { message = "✅ Đã xóa sạch Pending và dừng toàn bộ tiến trình quét!" });
            }
            
            // 2. XÓA ACTIVE (Nút màu Đỏ)
            else if (status == "ACTIVE")
            {
                // Chỉ xóa dữ liệu trong bảng, giữ nguyên bộ đếm tiến độ
                _context.Database.ExecuteSqlRaw("TRUNCATE TABLE checkedlinks;");
                
                return Ok(new { message = "✅ Đã xóa toàn bộ dữ liệu Active trong Database!" });
            }
            
            // 3. STOP GENERATING (Nút Tím cũ - Giữ lại nếu cần dùng API, dù giao diện đã bỏ)
            else if (status == "STOP_GENERATING")
            {
                 // Logic cũ: Chỉ dừng sinh thêm, vẫn chạy nốt pending
                 return Ok(new { message = "Chức năng này đã được gộp vào Tạm Dừng/Xóa Pending." });
            }

            return BadRequest("Lệnh không hợp lệ");
        }

        [HttpGet("export")]
        public IActionResult ExportCsv()
        {
            var links = _context.CheckedLinks.Where(x => x.Status == "ACTIVE").OrderBy(x => x.Id).ToList();
            var sb = new StringBuilder();
            
            // Thêm BOM để Excel hiển thị đúng tiếng Việt
            sb.Append('\uFEFF'); 
            
            sb.AppendLine("Id,Subdomain,FullUrl,Status,SaleStatus,SaleNote,LastChecked");
            foreach (var link in links) 
            {
                // Xử lý csv escape nếu cần (đơn giản hóa ở đây)
                sb.AppendLine($"{link.Id},{link.Subdomain},{link.FullUrl},{link.Status},{link.SaleStatus},{link.SaleNote?.Replace(",", " ")},{link.LastChecked}");
            }
            
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"active_links_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }
    }
    public class SalesUpdateModel { public string Note { get; set; } = ""; public string Status { get; set; } = ""; public string User { get; set; } = ""; }
}
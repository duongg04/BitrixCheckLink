using System.ComponentModel.DataAnnotations;

namespace BitrixChecker.Models
{
    public class CheckedLink
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Subdomain { get; set; } = string.Empty;

        public string FullUrl { get; set; } = string.Empty;

        public string Status { get; set; } = "INACTIVE"; 

        public int? HttpCode { get; set; }

        public DateTime LastChecked { get; set; } = DateTime.Now;

        // --- CÁC TRƯỜNG MỚI CHO SALES TEAM ---
        public string? SaleNote { get; set; } 
        
        public string SaleStatus { get; set; } = "New"; 
        
        public string? AssignedUser { get; set; } 
    }
}
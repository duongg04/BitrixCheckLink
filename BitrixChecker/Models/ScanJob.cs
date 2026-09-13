using System.ComponentModel.DataAnnotations;

namespace BitrixChecker.Models
{
    public class ScanJob
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string JobType { get; set; } = "Manual";

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public int TotalExpected { get; set; }

        public int TotalScanned { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string? CreatedByUserId { get; set; }

        public ApplicationUser? CreatedByUser { get; set; }

        public ICollection<LinkResult> LinkResults { get; set; } = [];
    }
}

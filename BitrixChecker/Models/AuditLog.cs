using System.ComponentModel.DataAnnotations;

namespace BitrixChecker.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string EntityName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? EntityId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        public string? PerformedById { get; set; }
        public ApplicationUser? PerformedBy { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public string? Details { get; set; }
    }
}
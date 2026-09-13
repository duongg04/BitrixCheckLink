using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BitrixChecker.Models
{
    public class LinkProcessing
    {
        [Key]
        public int Id { get; set; }

        [Column("CheckedLinkId")]
        public int LinkResultId { get; set; }

        public LinkResult? LinkResult { get; set; }

        public string? AssignedUserId { get; set; }

        public ApplicationUser? AssignedUser { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "New";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? UpdatedByUserId { get; set; }
        public ApplicationUser? UpdatedByUser { get; set; }
    }
}

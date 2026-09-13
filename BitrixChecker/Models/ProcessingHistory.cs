using System.ComponentModel.DataAnnotations;

namespace BitrixChecker.Models;

/// <summary>Lịch sử thay đổi của LinkProcessing.</summary>
public class ProcessingHistory
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int LinkProcessingId { get; set; }
    public LinkProcessing? LinkProcessing { get; set; }

    public string? AssignedUserId { get; set; }
    public ApplicationUser? AssignedUser { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    [Required, MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? ChangedByUserId { get; set; }
    public ApplicationUser? ChangedByUser { get; set; }

    [MaxLength(200)]
    public string? ChangeReason { get; set; }
}

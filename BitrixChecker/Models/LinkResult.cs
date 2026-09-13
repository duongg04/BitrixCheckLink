using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BitrixChecker.Models;

/// <summary>One durable result produced by a scan or a later re-check.</summary>
[Table("CheckedLinks")]
public class LinkResult
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Subdomain { get; set; } = string.Empty;

    [Required, MaxLength(2048)]
    public string FullUrl { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = LinkStatuses.Inactive;

    public int? HttpCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
    [MaxLength(2000)] public string? ResponseFingerprint { get; set; }
    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
    public int? ScanJobId { get; set; }
    public ScanJob? ScanJob { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsTracked { get; set; }
    public DateTime? DeletedAt { get; set; }
    public ICollection<LinkProcessing> Processings { get; set; } = [];
}

public static class LinkStatuses
{
    public const string Active = "ACTIVE";
    public const string Inactive = "INACTIVE";
}

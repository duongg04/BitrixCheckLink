using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BitrixChecker.Models;

[Table("Notifications")]
public class Notification
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Type { get; set; } = "Info"; // Info, Warning, Success, Error

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? UserId { get; set; }
    [ForeignKey("UserId")]
    public ApplicationUser? User { get; set; }
}

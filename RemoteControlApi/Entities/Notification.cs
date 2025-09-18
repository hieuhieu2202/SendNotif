using System.ComponentModel.DataAnnotations;

namespace RemoteControlApi.Entities;

public class Notification
{
    [Key]
    public int NotificationId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = default!;

    [Required]
    public string Message { get; set; } = default!;

    [MaxLength(255)]
    public string? Link { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public int ApplicationId { get; set; }

    public int? AppVersionId { get; set; }

    [MaxLength(255)]
    public string? FileUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public Application Application { get; set; } = default!;

    public AppVersion? AppVersion { get; set; }
}

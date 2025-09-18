using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RemoteControlApi.Entities;

public class Application
{
    [Key]
    public int ApplicationId { get; set; }

    [Required]
    [MaxLength(100)]
    public string AppKey { get; set; } = default!;

    [Required]
    [MaxLength(150)]
    public string DisplayName { get; set; } = default!;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public ICollection<AppVersion> AppVersions { get; set; } = [];

    public ICollection<Notification> Notifications { get; set; } = [];
}

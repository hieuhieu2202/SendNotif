using System.ComponentModel.DataAnnotations;

namespace RemoteControlApi.Entities;

public class AppVersion
{
    [Key]
    public int AppVersionId { get; set; }

    [Required]
    [MaxLength(50)]
    public string VersionName { get; set; } = default!;

    public string? ReleaseNotes { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileUrl { get; set; } = default!;

    [MaxLength(128)]
    public string? FileChecksum { get; set; }

    public DateTime ReleaseDate { get; set; }

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

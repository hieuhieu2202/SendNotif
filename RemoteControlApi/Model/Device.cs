using System.ComponentModel.DataAnnotations;

namespace RemoteControlApi.Model;

public class Device
{
    [Key]
    [StringLength(100)]
    public string DeviceId { get; set; } = default!;

    [StringLength(100)]
    public string? CardCode { get; set; }

    [StringLength(50)]
    public string? CurrentVersion { get; set; }

    public DateTimeOffset LastSeen { get; set; }

    public ICollection<DeviceNotification> Notifications { get; set; } = new List<DeviceNotification>();
}

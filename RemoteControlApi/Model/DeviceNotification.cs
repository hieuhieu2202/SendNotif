using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RemoteControlApi.Model;

public class DeviceNotification
{
    [Key, Column(Order = 0)]
    public string DeviceId { get; set; } = default!;
    public Device Device { get; set; } = default!;

    [Key, Column(Order = 1)]
    public string NotificationId { get; set; } = default!;
    public NotificationMessage Notification { get; set; } = default!;

    [StringLength(20)]
    public string Status { get; set; } = "Pending";
    public DateTimeOffset? ReadAt { get; set; }
}

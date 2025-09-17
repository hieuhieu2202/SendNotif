using System;

namespace RemoteControlApi.Model;

public class DeviceNotificationDto
{
    public string NotificationId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTimeOffset DeliveredAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public NotificationDto Notification { get; set; } = default!;
}

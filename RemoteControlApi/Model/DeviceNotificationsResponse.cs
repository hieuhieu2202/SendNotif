using System.Collections.Generic;

namespace RemoteControlApi.Model;

public class DeviceNotificationsResponse
{
    public string DeviceId { get; set; } = default!;
    public string? CardCode { get; set; }
    public string? CurrentVersion { get; set; }
    public List<DeviceNotificationDto> Notifications { get; set; } = new();
}

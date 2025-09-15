using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemoteControlApi.Data;
using RemoteControlApi.Model;
using System.Linq;

namespace RemoteControlApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly NotificationDbContext _db;
    public DevicesController(NotificationDbContext db)
    {
        _db = db;
    }

    [HttpPost("{id}/version")]
    public async Task<IActionResult> ReportVersion(string id, [FromBody] DeviceVersionDto dto)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device == null)
        {
            device = new Device { DeviceId = id, CurrentVersion = dto.Version, LastSeen = DateTimeOffset.UtcNow, CardCode = dto.CardCode };
            _db.Devices.Add(device);
        }
        else
        {
            device.CurrentVersion = dto.Version;
            device.CardCode = dto.CardCode ?? device.CardCode;
            device.LastSeen = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync();
        return Ok(new { status = "updated" });
    }

    [HttpGet("{id}/notifications")]
    public async Task<IActionResult> FetchNotifications(string id)
    {
        var device = await _db.Devices.FindAsync(id);
        if (device == null)
        {
            device = new Device { DeviceId = id, LastSeen = DateTimeOffset.UtcNow };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync();
        }
        else
        {
            device.LastSeen = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }

        var delivered = _db.DeviceNotifications
            .Where(dn => dn.DeviceId == id)
            .Select(dn => dn.NotificationId);

        var pending = await _db.Notifications
            .Where(n => !delivered.Contains(n.Id!))
            .OrderBy(n => n.TimestampUtc)
            .ToListAsync();

        foreach (var n in pending)
        {
            _db.DeviceNotifications.Add(new DeviceNotification
            {
                DeviceId = id,
                NotificationId = n.Id!,
                Status = "Sent"
            });
        }
        await _db.SaveChangesAsync();
        return Ok(pending);
    }

    [HttpPost("{deviceId}/notifications/{notificationId}/read")]
    public async Task<IActionResult> MarkRead(string deviceId, string notificationId)
    {
        var device = await _db.Devices.FindAsync(deviceId);
        if (device == null) return NotFound(new { error = "Device not found" });

        var now = DateTimeOffset.UtcNow;
        device.LastSeen = now;

        var entry = await _db.DeviceNotifications.FindAsync(deviceId, notificationId);
        if (entry == null)
        {
            entry = new DeviceNotification
            {
                DeviceId = deviceId,
                NotificationId = notificationId,
                Status = "Read",
                ReadAt = now
            };
            _db.DeviceNotifications.Add(entry);
        }
        else
        {
            entry.Status = "Read";
            entry.ReadAt = now;
        }

        await _db.SaveChangesAsync();
        return Ok(new { status = "read", readAt = entry.ReadAt, cardCode = device.CardCode, version = device.CurrentVersion });
    }

    public class DeviceVersionDto
    {
        public string Version { get; set; } = string.Empty;
        public string? CardCode { get; set; }
    }
}

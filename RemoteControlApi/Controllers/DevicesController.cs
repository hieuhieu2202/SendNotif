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
            device = new Device { DeviceId = id, CurrentVersion = dto.Version, LastSeen = DateTimeOffset.UtcNow };
            _db.Devices.Add(device);
        }
        else
        {
            device.CurrentVersion = dto.Version;
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

    public class DeviceVersionDto
    {
        public string Version { get; set; } = string.Empty;
    }
}

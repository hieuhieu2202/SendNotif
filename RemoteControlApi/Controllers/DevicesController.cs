using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RemoteControlApi.Data;
using RemoteControlApi.Model;

namespace RemoteControlApi.Controllers;

[ApiController]
[Route("api/devices")]
public class DevicesController : ControllerBase
{
    private readonly NotificationDbContext _db;

    public DevicesController(NotificationDbContext db)
    {
        _db = db;
    }

    [HttpPost("{deviceId}/version")]
    public async Task<IActionResult> ReportVersion(string deviceId, [FromBody] DeviceVersionRequest request)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            ModelState.AddModelError(nameof(deviceId), "DeviceId is required.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        deviceId = deviceId.Trim();
        var version = request.Version.Trim();
        var now = DateTimeOffset.UtcNow;

        var device = await _db.Devices.FindAsync(deviceId);
        if (device == null)
        {
            device = new Device
            {
                DeviceId = deviceId,
                CurrentVersion = version,
                CardCode = NormalizeCardCode(request.CardCode),
                LastSeen = now
            };
            _db.Devices.Add(device);
        }
        else
        {
            device.CurrentVersion = version;
            device.LastSeen = now;
            if (request.CardCode != null)
            {
                device.CardCode = NormalizeCardCode(request.CardCode);
            }
        }

        var latestWithVersion = await _db.Notifications
            .Where(n => n.TargetVersion != null)
            .OrderByDescending(n => n.TimestampUtc)
            .FirstOrDefaultAsync();

        var latestVersion = latestWithVersion?.TargetVersion;
        var updateRequired = latestVersion != null && NeedsUpgrade(version, latestVersion);

        await _db.SaveChangesAsync();

        return Ok(new
        {
            deviceId = device.DeviceId,
            cardCode = device.CardCode,
            currentVersion = device.CurrentVersion,
            latestVersion,
            updateRequired
        });
    }

    [HttpGet("{deviceId}/notifications")]
    public async Task<IActionResult> GetNotifications(string deviceId, [FromQuery] bool includeRead = false)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            ModelState.AddModelError(nameof(deviceId), "DeviceId is required.");
            return ValidationProblem(ModelState);
        }

        deviceId = deviceId.Trim();
        var device = await _db.Devices.FindAsync(deviceId);
        if (device == null)
        {
            device = new Device
            {
                DeviceId = deviceId,
                LastSeen = DateTimeOffset.UtcNow
            };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync();
        }

        var missingIds = await _db.Notifications
            .Where(n => !_db.DeviceNotifications.Any(dn => dn.DeviceId == deviceId && dn.NotificationId == n.Id))
            .Select(n => n.Id!)
            .ToListAsync();

        if (missingIds.Count > 0)
        {
            foreach (var notificationId in missingIds)
            {
                _db.DeviceNotifications.Add(new DeviceNotification
                {
                    DeviceId = deviceId,
                    NotificationId = notificationId,
                    Status = "Pending"
                });
            }
            await _db.SaveChangesAsync();
        }

        var query = _db.DeviceNotifications
            .Include(dn => dn.Notification)
            .Where(dn => dn.DeviceId == deviceId);

        if (!includeRead)
        {
            query = query.Where(dn => dn.Status != "Read");
        }

        var records = await query
            .OrderByDescending(dn => dn.Notification!.TimestampUtc)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        foreach (var record in records)
        {
            if (record.Status == "Pending")
            {
                record.Status = "Delivered";
            }
        }
        device.LastSeen = now;
        await _db.SaveChangesAsync();

        var notifications = records.Select(record => new DeviceNotificationDto
        {
            NotificationId = record.NotificationId,
            Status = record.Status,
            DeliveredAt = record.Notification!.TimestampUtc,
            ReadAt = record.ReadAt,
            Notification = NotificationDto.FromEntity(record.Notification!)
        }).ToList();

        return Ok(new DeviceNotificationsResponse
        {
            DeviceId = device.DeviceId,
            CardCode = device.CardCode,
            CurrentVersion = device.CurrentVersion,
            Notifications = notifications
        });
    }

    [HttpPost("{deviceId}/notifications/{notificationId}/read")]
    public async Task<IActionResult> MarkAsRead(string deviceId, string notificationId)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(notificationId))
        {
            ModelState.AddModelError("keys", "DeviceId và NotificationId là bắt buộc.");
            return ValidationProblem(ModelState);
        }

        deviceId = deviceId.Trim();
        notificationId = notificationId.Trim();

        var notificationExists = await _db.Notifications.AnyAsync(n => n.Id == notificationId);
        if (!notificationExists)
        {
            return NotFound(new { message = "Không tìm thấy thông báo." });
        }

        var device = await _db.Devices.FindAsync(deviceId);
        var now = DateTimeOffset.UtcNow;
        if (device == null)
        {
            device = new Device
            {
                DeviceId = deviceId,
                LastSeen = now
            };
            _db.Devices.Add(device);
        }
        else
        {
            device.LastSeen = now;
        }

        var record = await _db.DeviceNotifications.FindAsync(deviceId, notificationId);
        if (record == null)
        {
            record = new DeviceNotification
            {
                DeviceId = deviceId,
                NotificationId = notificationId,
                Status = "Read",
                ReadAt = now
            };
            _db.DeviceNotifications.Add(record);
        }
        else
        {
            record.Status = "Read";
            record.ReadAt = now;
        }

        await _db.SaveChangesAsync();

        return Ok(new
        {
            deviceId,
            notificationId,
            status = record.Status,
            readAt = record.ReadAt
        });
    }

    private static string? NormalizeCardCode(string? cardCode)
        => string.IsNullOrWhiteSpace(cardCode) ? null : cardCode.Trim();

    private static bool NeedsUpgrade(string current, string latest)
    {
        if (Version.TryParse(current, out var currentVersion) && Version.TryParse(latest, out var latestVersion))
        {
            return currentVersion < latestVersion;
        }

        return string.CompareOrdinal(current, latest) < 0;
    }
}

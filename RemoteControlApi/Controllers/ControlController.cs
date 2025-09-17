using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RemoteControlApi.Data;
using RemoteControlApi.Model;

namespace RemoteControlApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ControlController : ControllerBase
{
    private const int MaxNotifications = 20;
    private readonly NotificationDbContext _db;
    private static readonly ConcurrentDictionary<Guid, Channel<NotificationMessage>> _streams = new();

    public ControlController(NotificationDbContext db)
    {
        _db = db;
    }

    // Notification endpoints
    [HttpPost("send-notification-json")]
    [Consumes("application/json")]
    [RequestSizeLimit(1_500_000_000)]
    public async Task<IActionResult> SendNotificationJson([FromBody] NotificationMessage msg)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(msg.FileBase64) && !string.IsNullOrWhiteSpace(msg.FileName))
            {
                await SaveBase64(msg);
            }
            return Handle(msg);
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    [HttpPost("send-notification")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1_500_000_000)]
    public async Task<IActionResult> SendNotification([FromForm] SendNotificationFormData data)
    {
        try
        {
            var msg = new NotificationMessage
            {
                Id = data.Id,
                Title = data.Title,
                Body = data.Body,
                Link = data.Link,
                TargetVersion = data.TargetVersion
            };
            if (data.File != null && data.File.Length > 0)
            {
                await SaveFormFile(data.File, msg);
            }
            return Handle(msg);
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    [HttpGet("get-notifications")]
    public IActionResult GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var items = _db.Notifications
            .OrderByDescending(x => x.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        SetNoCache();
        return Ok(new { total = _db.Notifications.Count(), page, pageSize, items });
    }

    [HttpPost("clear-notifications")]
    public IActionResult ClearNotifications()
    {
        _db.DeviceNotifications.RemoveRange(_db.DeviceNotifications);
        _db.Notifications.RemoveRange(_db.Notifications);
        _db.SaveChanges();
        SetNoCache();
        return Ok(new { status = "Cleared" });
    }

    [HttpGet("notifications-stream")]
    public async Task NotificationsStream(CancellationToken cancellationToken)
    {
        SetNoCache();
        Response.Headers[HeaderNames.ContentType] = "text/event-stream";
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<NotificationMessage>();
        _streams[id] = channel;
        try
        {
            await foreach (var msg in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var json = JsonSerializer.Serialize(msg);
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            _streams.TryRemove(id, out _);
        }
    }

    private async Task SaveFormFile(IFormFile file, NotificationMessage msg)
    {
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var webroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var uploads = Path.Combine(webroot, "uploads");
        Directory.CreateDirectory(uploads);
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10) ext = ".bin";
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploads, fileName);
        await using (var fs = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(fs);
        }
        var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
        msg.FileBase64 = Convert.ToBase64String(bytes);
        msg.FileName = file.FileName;
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        msg.FileUrl = $"{baseUrl}/uploads/{fileName}";
    }

    private async Task SaveBase64(NotificationMessage msg)
    {
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var webroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var uploads = Path.Combine(webroot, "uploads");
        Directory.CreateDirectory(uploads);
        var ext = Path.GetExtension(msg.FileName);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10) ext = ".bin";
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploads, fileName);
        var bytes = Convert.FromBase64String(msg.FileBase64!);
        await System.IO.File.WriteAllBytesAsync(fullPath, bytes);
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        msg.FileUrl = $"{baseUrl}/uploads/{fileName}";
    }

    private IActionResult Handle(NotificationMessage msg)
    {
        if (!TryValidateModel(msg)) return ValidationProblem(ModelState);
        msg.TimestampUtc = DateTimeOffset.UtcNow;
        msg.Id = string.IsNullOrWhiteSpace(msg.Id) ? Guid.NewGuid().ToString("n") : msg.Id;

        var deviceIds = _db.Devices.Select(d => d.DeviceId).ToList();

        _db.Notifications.Add(msg);

        if (deviceIds.Count > 0)
        {
            foreach (var deviceId in deviceIds)
            {
                _db.DeviceNotifications.Add(new DeviceNotification
                {
                    DeviceId = deviceId,
                    NotificationId = msg.Id!,
                    Status = "Pending"
                });
            }
        }

        _db.SaveChanges();

        var excess = _db.Notifications
            .OrderByDescending(n => n.TimestampUtc)
            .Skip(MaxNotifications)
            .ToList();
        if (excess.Count > 0)
        {
            _db.Notifications.RemoveRange(excess);
            _db.SaveChanges();
        }

        foreach (var pair in _streams.ToArray())
        {
            if (!pair.Value.Writer.TryWrite(msg))
                _streams.TryRemove(pair.Key, out _);
        }
        SetNoCache();
        return Ok(new { status = "Notification received", message = msg });
    }

    private void SetNoCache()
    {
        var headers = Response.GetTypedHeaders();
        headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MustRevalidate = true
        };
        Response.Headers[HeaderNames.Pragma] = "no-cache";
        Response.Headers[HeaderNames.Expires] = "0";
    }

    // App version endpoints
    private const string VersionDir = "builds";
    private const string ManifestName = "manifest.json";

    [HttpGet("app-version")]
    public IActionResult GetAppVersion()
    {
        var manifest = ReadManifest();
        if (manifest == null) return NotFound();
        SetNoCache();
        return Ok(manifest);
    }

    [HttpPost("app-version/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1_500_000_000)]
    public async Task<IActionResult> UploadAppVersion([FromForm] AppVersionUpload data)
    {
        try
        {
            var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var webroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
            var dir = Path.Combine(webroot, VersionDir);
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(data.File.FileName);
            if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10) ext = ".bin";
            var storedName = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(dir, storedName);
            await using (var fs = System.IO.File.Create(path))
            {
                await data.File.CopyToAsync(fs);
            }
            string sha256;
            await using (var fs = System.IO.File.OpenRead(path))
            {
                using var sha = SHA256.Create();
                sha256 = Convert.ToHexString(sha.ComputeHash(fs));
            }
            var size = new FileInfo(path).Length;
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var fileUrl = $"{baseUrl}/{VersionDir}/{storedName}";
            var info = new AppVersionInfo
            {
                Version = data.Version,
                FileName = data.File.FileName,
                FileUrl = fileUrl,
                Size = size,
                Sha256 = sha256,
                ContentType = data.File.ContentType,
                UploadedAt = DateTimeOffset.UtcNow
            };
            var manifestPath = Path.Combine(dir, ManifestName);
            await System.IO.File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(info));
            SetNoCache();
            return Ok(info);
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    [HttpGet("app-version/download")]
    public IActionResult DownloadAppVersion()
    {
        var manifest = ReadManifest();
        if (manifest == null) return NotFound();
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var webroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webroot, VersionDir);
        var fileName = manifest.FileUrl.Split('/').Last();
        var path = Path.Combine(dir, fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        SetNoCache();
        return PhysicalFile(path, manifest.ContentType, manifest.FileName, enableRangeProcessing: true);
    }

    [HttpHead("app-version/download")]
    public IActionResult HeadAppVersion()
    {
        var manifest = ReadManifest();
        if (manifest == null) return NotFound();
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var webroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webroot, VersionDir);
        var fileName = manifest.FileUrl.Split('/').Last();
        var path = Path.Combine(dir, fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        var fi = new FileInfo(path);
        Response.ContentLength = fi.Length;
        Response.ContentType = manifest.ContentType;
        Response.Headers["X-SHA256"] = manifest.Sha256;
        SetNoCache();
        return Ok();
    }

    private AppVersionInfo? ReadManifest()
    {
        var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var webroot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var path = Path.Combine(webroot, VersionDir, ManifestName);
        if (!System.IO.File.Exists(path)) return null;
        var json = System.IO.File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppVersionInfo>(json);
    }
}

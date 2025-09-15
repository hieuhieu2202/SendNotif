using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using RemoteControlApi.Data;
using RemoteControlApi.Model;

namespace RemoteControlApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private const int MaxNotifications = 20;
    private static readonly ConcurrentDictionary<Guid, Channel<NotificationMessage>> _streams = new();
    private readonly NotificationDbContext _db;

    public NotificationsController(NotificationDbContext db)
    {
        _db = db;
    }

    [HttpPost("form")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(1_500_000_000)]
    public async Task<IActionResult> SendForm([FromForm] SendNotificationFormData data)
    {
        try
        {
            var msg = new NotificationMessage
            {
                Id = data.Id,
                Title = data.Title,
                Body = data.Body,
                Link = data.Link
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

    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(1_500_000_000)]
    public async Task<IActionResult> SendJson([FromBody] NotificationMessage msg)
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

    [HttpGet]
    public IActionResult List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
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

    [HttpPost("clear")]
    public IActionResult Clear()
    {
        _db.DeviceNotifications.RemoveRange(_db.DeviceNotifications);
        _db.Notifications.RemoveRange(_db.Notifications);
        _db.SaveChanges();
        SetNoCache();
        return Ok(new { status = "Cleared" });
    }

    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
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
                var json = System.Text.Json.JsonSerializer.Serialize(msg);
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

    private IActionResult Handle(NotificationMessage msg)
    {
        if (!TryValidateModel(msg)) return ValidationProblem(ModelState);
        msg.TimestampUtc = DateTimeOffset.UtcNow;
        msg.Id = string.IsNullOrWhiteSpace(msg.Id) ? Guid.NewGuid().ToString("n") : msg.Id;
        _db.Notifications.Add(msg);
        _db.SaveChanges();

        // trim to MaxNotifications
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
}

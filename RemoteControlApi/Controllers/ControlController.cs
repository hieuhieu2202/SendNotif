using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using RemoteControlApi.Data;
using RemoteControlApi.Entities;
using RemoteControlApi.Services;
using static RemoteControlApi.Model.NotiModel;

namespace RemoteControlApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ControlController(AppDbContext dbContext, IWebHostEnvironment environment, INotificationStream notificationStream) : ControllerBase
{
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly char[] AppKeySeparators = { ',', ';', '\n', '\r' };

    private readonly AppDbContext _dbContext = dbContext;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly INotificationStream _notificationStream = notificationStream;

    #region Applications

    [HttpGet("applications")]
    public async Task<IActionResult> GetApplications()
    {
        var applications = await _dbContext.Applications
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.ApplicationId,
                a.AppKey,
                a.DisplayName,
                a.Description,
                a.IsActive,
                a.CreatedAt,
                versionCount = a.AppVersions.Count(),
                notificationCount = a.Notifications.Count()
            })
            .ToListAsync();

        SetNoCache();
        return Ok(applications);
    }

    [HttpPost("applications")]
    public async Task<IActionResult> CreateApplication([FromBody] CreateApplicationRequest request)
    {
        request.AppKey = NormalizeAppKey(request.AppKey) ?? string.Empty;
        request.DisplayName = request.DisplayName?.Trim() ?? string.Empty;
        request.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        ModelState.Clear();
        if (!TryValidateModel(request))
        {
            return ValidationProblem(ModelState);
        }

        var exists = await _dbContext.Applications
            .AnyAsync(a => a.AppKey == request.AppKey);
        if (exists)
        {
            ModelState.AddModelError(nameof(request.AppKey), "AppKey đã tồn tại.");
            return ValidationProblem(ModelState);
        }

        var application = new Application
        {
            AppKey = request.AppKey,
            DisplayName = request.DisplayName,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _dbContext.Applications.Add(application);
        await _dbContext.SaveChangesAsync();

        SetNoCache();
        return Ok(new
        {
            application.ApplicationId,
            application.AppKey,
            application.DisplayName,
            application.Description,
            application.IsActive,
            application.CreatedAt
        });
    }

    #endregion

    #region App Versions

    [HttpGet("app-versions")]
    public async Task<IActionResult> ListAppVersions([FromQuery] string? appKey = null)
    {
        var normalizedKey = NormalizeAppKey(appKey);

        IQueryable<AppVersion> query = _dbContext.AppVersions
            .AsNoTracking();

        if (!string.IsNullOrEmpty(normalizedKey))
        {
            query = query.Where(v => v.Application.AppKey == normalizedKey);
        }

        var versions = await query
            .OrderByDescending(v => v.ReleaseDate)
            .ThenByDescending(v => v.AppVersionId)
            .Select(v => new
            {
                v.AppVersionId,
                v.VersionName,
                v.Platform,
                v.ReleaseNotes,
                v.FileUrl,
                v.FileChecksum,
                v.ReleaseDate,
                appKey = v.Application.AppKey,
                appName = v.Application.DisplayName
            })
            .ToListAsync();

        SetNoCache();
        return Ok(versions);
    }

    [HttpGet("app-versions/{id:int}")]
    public async Task<IActionResult> GetAppVersionById(int id)
    {
        var version = await _dbContext.AppVersions
            .AsNoTracking()
            .Where(v => v.AppVersionId == id)
            .Select(v => new
            {
                v.AppVersionId,
                v.VersionName,
                v.Platform,
                v.ReleaseNotes,
                v.FileUrl,
                v.FileChecksum,
                v.ReleaseDate,
                appKey = v.Application.AppKey,
                appName = v.Application.DisplayName
            })
            .FirstOrDefaultAsync();

        if (version is null)
        {
            return NotFound();
        }

        SetNoCache();
        return Ok(version);
    }

    [HttpPost("app-versions")]
    public async Task<IActionResult> CreateAppVersion([FromBody] CreateAppVersionRequest request)
    {
        request.AppKey = NormalizeAppKey(request.AppKey) ?? string.Empty;
        request.VersionName = request.VersionName?.Trim() ?? string.Empty;
        request.Platform = string.IsNullOrWhiteSpace(request.Platform)
            ? null
            : request.Platform.Trim();
        request.ReleaseNotes = string.IsNullOrWhiteSpace(request.ReleaseNotes)
            ? null
            : request.ReleaseNotes.Trim();
        request.FileUrl = request.FileUrl?.Trim() ?? string.Empty;
        request.FileChecksum = string.IsNullOrWhiteSpace(request.FileChecksum)
            ? null
            : request.FileChecksum.Trim();

        ModelState.Clear();
        if (!TryValidateModel(request))
        {
            return ValidationProblem(ModelState);
        }

        var application = await _dbContext.Applications
            .FirstOrDefaultAsync(a => a.AppKey == request.AppKey);
        if (application is null)
        {
            ModelState.AddModelError(nameof(request.AppKey), "AppKey không tồn tại.");
            return ValidationProblem(ModelState);
        }

        var releaseDate = NormalizeToUtc(request.ReleaseDate);

        var duplicate = await _dbContext.AppVersions
            .AnyAsync(v => v.ApplicationId == application.ApplicationId && v.VersionName == request.VersionName);
        if (duplicate)
        {
            ModelState.AddModelError(nameof(request.VersionName), "Phiên bản đã tồn tại cho ứng dụng này.");
            return ValidationProblem(ModelState);
        }

        var version = new AppVersion
        {
            ApplicationId = application.ApplicationId,
            VersionName = request.VersionName,
            Platform = request.Platform,
            ReleaseNotes = request.ReleaseNotes,
            FileUrl = request.FileUrl,
            FileChecksum = request.FileChecksum,
            ReleaseDate = releaseDate
        };

        _dbContext.AppVersions.Add(version);
        await _dbContext.SaveChangesAsync();

        SetNoCache();
        return Ok(new
        {
            version.AppVersionId,
            version.VersionName,
            version.Platform,
            version.ReleaseNotes,
            version.FileUrl,
            version.FileChecksum,
            version.ReleaseDate,
            appKey = application.AppKey,
            appName = application.DisplayName
        });
    }

    [HttpGet("check-app-version")]
    public async Task<IActionResult> CheckAppVersion([FromQuery] string appKey, [FromQuery] string currentVersion)
    {
        var normalizedKey = NormalizeAppKey(appKey);
        if (string.IsNullOrEmpty(normalizedKey))
        {
            ModelState.AddModelError(nameof(appKey), "appKey là bắt buộc.");
            return ValidationProblem(ModelState);
        }

        currentVersion = currentVersion?.Trim() ?? string.Empty;

        var application = await _dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AppKey == normalizedKey);

        if (application is null)
        {
            return NotFound(new { message = "Ứng dụng không tồn tại." });
        }

        var latest = await _dbContext.AppVersions
            .AsNoTracking()
            .Where(v => v.ApplicationId == application.ApplicationId)
            .OrderByDescending(v => v.ReleaseDate)
            .ThenByDescending(v => v.AppVersionId)
            .FirstOrDefaultAsync();

        if (latest is null)
        {
            return Ok(new
            {
                appKey = application.AppKey,
                currentVersion,
                updateAvailable = false,
                message = "Chưa có bản phát hành nào.",
                latestRelease = (object?)null
            });
        }

        var hasCurrent = TryParseVersionString(currentVersion, out var currentVer);
        var hasLatest = TryParseVersionString(latest.VersionName, out var latestVer);

        bool updateAvailable = false;
        string? comparisonNote = null;

        if (hasCurrent && hasLatest)
        {
            updateAvailable = currentVer < latestVer;
        }
        else if (!hasCurrent)
        {
            comparisonNote = "currentVersion không đúng định dạng x.y.z";
        }
        else if (!hasLatest)
        {
            comparisonNote = "VersionName trên máy chủ không đúng định dạng.";
        }

        return Ok(new
        {
            appKey = application.AppKey,
            currentVersion,
            serverVersion = latest.VersionName,
            updateAvailable,
            comparisonNote,
            latestRelease = new
            {
                latest.AppVersionId,
                latest.VersionName,
                latest.Platform,
                latest.ReleaseNotes,
                latest.FileUrl,
                latest.FileChecksum,
                latest.ReleaseDate
            }
        });
    }

    #endregion

    #region Notifications

    [HttpPost("send-notification-json")]
    [Consumes("application/json")]
    public Task<IActionResult> SendNotificationJson([FromBody] NotificationMessage message)
    {
        return PersistNotificationAsync(message);
    }

    [HttpPost("send-notification")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SendNotificationMultipart([FromForm] SendNotificationForm form)
    {
        var targets = NormalizeAppKeyList(form.Id);
        if (targets.Count == 0)
        {
            ModelState.AddModelError(nameof(form.Id), "Cần ít nhất một appKey trong trường id.");
            return ValidationProblem(ModelState);
        }

        var message = new NotificationMessage
        {
            Title = form.Title,
            Body = form.Body,
            Link = form.Link,
            Targets = targets.Select(key => new NotificationTarget
            {
                AppKey = key,
                AppVersionId = form.AppVersionId
            }).ToList(),
            FileName = form.File?.FileName
        };

        if (form.File is { Length: > 0 })
        {
            using var ms = new MemoryStream();
            await form.File.CopyToAsync(ms);
            message.FileBase64 = Convert.ToBase64String(ms.ToArray());
        }

        return await PersistNotificationAsync(message);
    }

    [HttpGet("get-notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] string appKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var normalizedKey = NormalizeAppKey(appKey);
        if (string.IsNullOrEmpty(normalizedKey))
        {
            ModelState.AddModelError(nameof(appKey), "appKey là bắt buộc.");
            return ValidationProblem(ModelState);
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var application = await _dbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AppKey == normalizedKey);

        if (application is null)
        {
            return NotFound(new { message = "Ứng dụng không tồn tại." });
        }

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.ApplicationId == application.ApplicationId && n.IsActive)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Include(n => n.AppVersion);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                n.NotificationId,
                n.Title,
                message = n.Message,
                n.CreatedAt,
                n.Link,
                n.FileUrl,
                appKey = application.AppKey,
                appName = application.DisplayName,
                appVersion = n.AppVersion == null
                    ? null
                    : new
                    {
                        n.AppVersion.AppVersionId,
                        n.AppVersion.VersionName,
                        n.AppVersion.Platform,
                        n.AppVersion.ReleaseNotes,
                        n.AppVersion.FileUrl,
                        n.AppVersion.FileChecksum,
                        n.AppVersion.ReleaseDate
                    }
            })
            .ToListAsync();

        SetNoCache();
        return Ok(new
        {
            total,
            page,
            pageSize,
            items
        });
    }

    [HttpPost("clear-notifications")]
    public async Task<IActionResult> ClearNotifications([FromQuery] string? appKey = null)
    {
        var normalizedKey = NormalizeAppKey(appKey);
        if (string.IsNullOrEmpty(normalizedKey))
        {
            await _dbContext.Notifications.ExecuteDeleteAsync();
            SetNoCache();
            return Ok(new { status = "Cleared" });
        }

        var application = await _dbContext.Applications
            .FirstOrDefaultAsync(a => a.AppKey == normalizedKey);

        if (application is null)
        {
            return NotFound(new { message = "Ứng dụng không tồn tại." });
        }

        await _dbContext.Notifications
            .Where(n => n.ApplicationId == application.ApplicationId)
            .ExecuteDeleteAsync();

        SetNoCache();
        return Ok(new { status = "Cleared", appKey = application.AppKey });
    }

    [HttpGet("notifications-stream")]
    public async Task NotificationsStream([FromQuery] string? appKey, CancellationToken cancellationToken)
    {
        Response.Headers[HeaderNames.CacheControl] = "no-cache";
        Response.Headers[HeaderNames.ContentType] = "text/event-stream";
        Response.Headers["X-Accel-Buffering"] = "no";

        await Response.Body.FlushAsync(cancellationToken);

        var normalizedKey = NormalizeAppKey(appKey);

        await foreach (var @event in _notificationStream.Subscribe(cancellationToken))
        {
            if (!string.IsNullOrEmpty(normalizedKey) && !string.Equals(@event.AppKey, normalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var payload = JsonSerializer.Serialize(@event, SseJsonOptions);
            await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    #endregion

    #region Helpers

    private async Task<IActionResult> PersistNotificationAsync(NotificationMessage message)
    {
        message.Title = message.Title?.Trim() ?? string.Empty;
        message.Body = message.Body?.Trim() ?? string.Empty;
        message.Link = string.IsNullOrWhiteSpace(message.Link)
            ? null
            : message.Link.Trim();

        message.Targets = message.Targets
            .Select(t => new NotificationTarget
            {
                AppKey = NormalizeAppKey(t.AppKey) ?? string.Empty,
                AppVersionId = t.AppVersionId
            })
            .Where(t => !string.IsNullOrEmpty(t.AppKey))
            .GroupBy(t => new { t.AppKey, t.AppVersionId })
            .Select(g => g.First())
            .ToList();

        ModelState.Clear();
        if (!TryValidateModel(message))
        {
            return ValidationProblem(ModelState);
        }

        var appKeys = message.Targets.Select(t => t.AppKey).Distinct().ToList();
        var applications = await _dbContext.Applications
            .Where(a => appKeys.Contains(a.AppKey))
            .ToListAsync();

        if (applications.Count != appKeys.Count)
        {
            var missing = appKeys.Except(applications.Select(a => a.AppKey)).ToList();
            ModelState.AddModelError(nameof(message.Targets), $"Không tìm thấy appKey: {string.Join(", ", missing)}.");
            return ValidationProblem(ModelState);
        }

        var versionIds = message.Targets
            .Where(t => t.AppVersionId.HasValue)
            .Select(t => t.AppVersionId!.Value)
            .Distinct()
            .ToList();

        var versions = await _dbContext.AppVersions
            .AsNoTracking()
            .Where(v => versionIds.Contains(v.AppVersionId))
            .ToListAsync();

        if (versions.Count != versionIds.Count)
        {
            var missing = versionIds.Except(versions.Select(v => v.AppVersionId)).ToList();
            ModelState.AddModelError(nameof(message.Targets), $"AppVersionId không tồn tại: {string.Join(", ", missing)}.");
            return ValidationProblem(ModelState);
        }

        var appsByKey = applications.ToDictionary(a => a.AppKey);
        var appsById = applications.ToDictionary(a => a.ApplicationId);
        var versionsById = versions.ToDictionary(v => v.AppVersionId);

        foreach (var target in message.Targets.Where(t => t.AppVersionId.HasValue))
        {
            var version = versionsById[target.AppVersionId!.Value];
            var application = appsByKey[target.AppKey];
            if (version.ApplicationId != application.ApplicationId)
            {
                ModelState.AddModelError(nameof(target.AppVersionId), $"AppVersionId {version.AppVersionId} không thuộc appKey {application.AppKey}.");
                return ValidationProblem(ModelState);
            }
        }

        string? storedFileUrl = null;
        if (!string.IsNullOrWhiteSpace(message.FileBase64))
        {
            try
            {
                storedFileUrl = await SaveAttachmentAsync(message.FileBase64, message.FileName);
            }
            catch (FormatException)
            {
                ModelState.AddModelError(nameof(message.FileBase64), "fileBase64 không hợp lệ.");
                return ValidationProblem(ModelState);
            }
        }

        var now = DateTime.UtcNow;
        List<Notification> notifications = [];

        foreach (var target in message.Targets)
        {
            var application = appsByKey[target.AppKey];
            var notification = new Notification
            {
                ApplicationId = application.ApplicationId,
                Title = message.Title,
                Message = message.Body,
                Link = message.Link,
                AppVersionId = target.AppVersionId,
                FileUrl = storedFileUrl,
                CreatedAt = now,
                IsActive = true
            };
            notifications.Add(notification);
        }

        _dbContext.Notifications.AddRange(notifications);
        await _dbContext.SaveChangesAsync();

        List<object> result = [];
        foreach (var notification in notifications)
        {
            var application = appsById[notification.ApplicationId];
            NotificationStreamVersion? versionPayload = null;
            if (notification.AppVersionId.HasValue)
            {
                var version = versionsById[notification.AppVersionId.Value];
                versionPayload = new NotificationStreamVersion(
                    version.AppVersionId,
                    version.VersionName,
                    version.Platform,
                    version.ReleaseNotes,
                    version.FileUrl,
                    version.FileChecksum,
                    version.ReleaseDate);
            }

            await _notificationStream.PublishAsync(new NotificationStreamEvent(
                application.AppKey,
                application.DisplayName,
                notification.NotificationId,
                notification.Title,
                notification.Message,
                notification.CreatedAt,
                notification.Link,
                notification.FileUrl,
                versionPayload));

            result.Add(new
            {
                appKey = application.AppKey,
                notificationId = notification.NotificationId,
                appVersionId = notification.AppVersionId
            });
        }

        SetNoCache();
        return Ok(new
        {
            status = "sent",
            fileUrl = storedFileUrl,
            notifications = result
        });
    }

    private async Task<string> SaveAttachmentAsync(string fileBase64, string? fileName)
    {
        var bytes = Convert.FromBase64String(fileBase64);
        var envRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploads = Path.Combine(envRoot, "uploads");
        Directory.CreateDirectory(uploads);

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10)
        {
            ext = ".bin";
        }

        var storedName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploads, storedName);
        await System.IO.File.WriteAllBytesAsync(fullPath, bytes);

        var basePath = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;
        return $"{basePath}/uploads/{storedName}";
    }

    private static bool TryParseVersionString(string value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 4)
        {
            return false;
        }

        var numbers = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out numbers[i]) || numbers[i] < 0)
            {
                return false;
            }
        }

        version = parts.Length switch
        {
            1 => new Version(numbers[0], 0),
            2 => new Version(numbers[0], numbers[1]),
            3 => new Version(numbers[0], numbers[1], numbers[2]),
            _ => new Version(numbers[0], numbers[1], numbers[2], numbers[3])
        };

        return true;
    }

    private void SetNoCache()
    {
        var headers = Response.GetTypedHeaders();
        headers.CacheControl = new()
        {
            NoCache = true,
            NoStore = true,
            MustRevalidate = true
        };
        Response.Headers[HeaderNames.Pragma] = "no-cache";
        Response.Headers[HeaderNames.Expires] = "0";
    }

    private static string? NormalizeAppKey(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private static List<string> NormalizeAppKeyList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(AppKeySeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeAppKey)
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .ToList()!;
    }

    private static DateTime NormalizeToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    public class SendNotificationForm
    {
        [Required, StringLength(100)]
        public string Title { get; set; } = default!;

        [Required, StringLength(4000)]
        public string Body { get; set; } = default!;

        [StringLength(255)]
        public string? Link { get; set; }

        [Required]
        public string Id { get; set; } = default!;

        [Range(1, int.MaxValue)]
        public int? AppVersionId { get; set; }

        public IFormFile? File { get; set; }
    }

    #endregion
}

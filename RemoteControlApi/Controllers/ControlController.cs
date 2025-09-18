using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using RemoteControlApi.Data;
using RemoteControlApi.Entities;
using static RemoteControlApi.Model.NotiModel;

namespace RemoteControlApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ControlController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public ControlController(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    #region Applications

    [HttpGet("applications")]
    public async Task<IActionResult> GetApplications()
    {
        var apps = await _dbContext.Applications
            .AsNoTracking()
            .OrderBy(a => a.DisplayName)
            .Select(a => new
            {
                a.ApplicationId,
                a.AppKey,
                a.DisplayName,
                a.Description,
                a.IsActive,
                a.CreatedAt,
                versionCount = a.AppVersions.Count,
                notificationCount = a.Notifications.Count
            })
            .ToListAsync();

        SetNoCache();
        return Ok(apps);
    }

    [HttpGet("applications/{appKey}")]
    public async Task<IActionResult> GetApplication(string appKey)
    {
        var normalized = NormalizeAppKey(appKey);
        var app = await _dbContext.Applications
            .AsNoTracking()
            .Where(a => a.AppKey == normalized)
            .Select(a => new
            {
                a.ApplicationId,
                a.AppKey,
                a.DisplayName,
                a.Description,
                a.IsActive,
                a.CreatedAt,
                versionCount = a.AppVersions.Count,
                notificationCount = a.Notifications.Count
            })
            .FirstOrDefaultAsync();

        if (app is null)
        {
            return NotFound();
        }

        SetNoCache();
        return Ok(app);
    }

    [HttpPost("applications")]
    public async Task<IActionResult> CreateApplication([FromBody] CreateApplicationRequest request)
    {
        request.AppKey = NormalizeAppKey(request.AppKey);
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

        var app = new Application
        {
            AppKey = request.AppKey,
            DisplayName = request.DisplayName,
            Description = request.Description,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _dbContext.Applications.Add(app);
        await _dbContext.SaveChangesAsync();

        SetNoCache();
        return CreatedAtAction(nameof(GetApplication), new { appKey = app.AppKey }, new
        {
            app.ApplicationId,
            app.AppKey,
            app.DisplayName,
            app.Description,
            app.IsActive,
            app.CreatedAt
        });
    }

    #endregion

    #region Notifications

    [HttpPost("send-notification-json")]
    [Consumes("application/json")]
    public async Task<IActionResult> SendNotificationJson([FromBody] NotificationMessage message)
    {
        message.Title = message.Title?.Trim() ?? string.Empty;
        message.Body = message.Body?.Trim() ?? string.Empty;
        message.Link = string.IsNullOrWhiteSpace(message.Link)
            ? null
            : message.Link.Trim();
        message.Targets ??= new List<NotificationTarget>();

        foreach (var target in message.Targets)
        {
            target.AppKey = NormalizeAppKey(target.AppKey);
        }

        ModelState.Clear();
        if (!TryValidateModel(message))
        {
            return ValidationProblem(ModelState);
        }

        var targetKeys = message.Targets
            .Select(t => t.AppKey)
            .Distinct()
            .ToList();

        var apps = await _dbContext.Applications
            .Where(a => targetKeys.Contains(a.AppKey) && a.IsActive)
            .ToDictionaryAsync(a => a.AppKey);

        for (var i = 0; i < message.Targets.Count; i++)
        {
            var target = message.Targets[i];
            if (!apps.TryGetValue(target.AppKey, out _))
            {
                ModelState.AddModelError($"Targets[{i}].AppKey", "Ứng dụng không tồn tại hoặc đã bị vô hiệu hoá.");
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var versionIds = message.Targets
            .Where(t => t.AppVersionId.HasValue)
            .Select(t => t.AppVersionId!.Value)
            .Distinct()
            .ToList();

        var versions = new Dictionary<int, AppVersion>();
        if (versionIds.Count > 0)
        {
            versions = await _dbContext.AppVersions
                .Where(v => versionIds.Contains(v.AppVersionId))
                .ToDictionaryAsync(v => v.AppVersionId);
        }

        for (var i = 0; i < message.Targets.Count; i++)
        {
            var target = message.Targets[i];
            if (!target.AppVersionId.HasValue)
            {
                continue;
            }

            if (!versions.TryGetValue(target.AppVersionId.Value, out var version))
            {
                ModelState.AddModelError($"Targets[{i}].AppVersionId", "AppVersionId không tồn tại.");
                continue;
            }

            var app = apps[target.AppKey];
            if (version.ApplicationId != app.ApplicationId)
            {
                ModelState.AddModelError($"Targets[{i}].AppVersionId", "Phiên bản không thuộc ứng dụng đã chọn.");
            }
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
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

        var utcNow = DateTime.UtcNow;
        var created = new List<(string AppKey, Notification Entity)>();

        foreach (var target in message.Targets)
        {
            var app = apps[target.AppKey];
            var notification = new Notification
            {
                Title = message.Title,
                Message = message.Body,
                Link = message.Link,
                CreatedAt = utcNow,
                ApplicationId = app.ApplicationId,
                AppVersionId = target.AppVersionId,
                FileUrl = storedFileUrl,
                IsActive = true
            };

            _dbContext.Notifications.Add(notification);
            created.Add((app.AppKey, notification));
        }

        await _dbContext.SaveChangesAsync();

        SetNoCache();
        return Ok(new
        {
            status = "sent",
            fileUrl = storedFileUrl,
            notifications = created.Select(c => new
            {
                c.AppKey,
                notificationId = c.Entity.NotificationId,
                c.Entity.AppVersionId
            })
        });
    }

    [HttpGet("get-notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] string appKey, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var normalized = NormalizeAppKey(appKey);
        var app = await _dbContext.Applications
            .AsNoTracking()
            .Where(a => a.AppKey == normalized && a.IsActive)
            .Select(a => new { a.ApplicationId, a.AppKey, a.DisplayName })
            .FirstOrDefaultAsync();

        if (app is null)
        {
            return NotFound(new { error = "Ứng dụng không tồn tại." });
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.ApplicationId == app.ApplicationId && n.IsActive)
            .OrderByDescending(n => n.CreatedAt)
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
                appKey = app.AppKey,
                appName = app.DisplayName,
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
        IQueryable<Notification> query = _dbContext.Notifications;

        if (!string.IsNullOrWhiteSpace(appKey))
        {
            var normalized = NormalizeAppKey(appKey);
            var app = await _dbContext.Applications
                .Where(a => a.AppKey == normalized)
                .Select(a => new { a.ApplicationId })
                .FirstOrDefaultAsync();

            if (app is null)
            {
                return NotFound(new { error = "Ứng dụng không tồn tại." });
            }

            query = query.Where(n => n.ApplicationId == app.ApplicationId);
        }

        await query.ExecuteDeleteAsync();
        SetNoCache();
        return Ok(new { status = "Cleared" });
    }

    #endregion

    #region App Versions

    [HttpGet("app-versions")]
    public async Task<IActionResult> ListAppVersions([FromQuery] string? appKey = null)
    {
        var query = _dbContext.AppVersions
            .AsNoTracking()
            .Include(v => v.Application)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(appKey))
        {
            var normalized = NormalizeAppKey(appKey);
            query = query.Where(v => v.Application.AppKey == normalized);
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
            .Include(v => v.Application)
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
        request.AppKey = NormalizeAppKey(request.AppKey);
        request.VersionName = request.VersionName?.Trim() ?? string.Empty;
        request.Platform = string.IsNullOrWhiteSpace(request.Platform)
            ? null
            : request.Platform.Trim().ToLowerInvariant();
        request.FileUrl = request.FileUrl?.Trim() ?? string.Empty;
        request.ReleaseNotes = string.IsNullOrWhiteSpace(request.ReleaseNotes)
            ? null
            : request.ReleaseNotes.Trim();
        request.FileChecksum = string.IsNullOrWhiteSpace(request.FileChecksum)
            ? null
            : request.FileChecksum.Trim();

        ModelState.Clear();
        if (!TryValidateModel(request))
        {
            return ValidationProblem(ModelState);
        }

        var app = await _dbContext.Applications
            .FirstOrDefaultAsync(a => a.AppKey == request.AppKey && a.IsActive);
        if (app is null)
        {
            ModelState.AddModelError(nameof(request.AppKey), "Ứng dụng không tồn tại hoặc đã bị vô hiệu hoá.");
            return ValidationProblem(ModelState);
        }

        var releaseDate = request.ReleaseDate;
        if (releaseDate.Kind == DateTimeKind.Local)
        {
            releaseDate = releaseDate.ToUniversalTime();
        }
        else if (releaseDate.Kind == DateTimeKind.Unspecified)
        {
            releaseDate = DateTime.SpecifyKind(releaseDate, DateTimeKind.Utc);
        }

        var duplicate = await _dbContext.AppVersions.AnyAsync(v =>
            v.ApplicationId == app.ApplicationId &&
            v.VersionName == request.VersionName &&
            v.Platform == request.Platform);

        if (duplicate)
        {
            ModelState.AddModelError(nameof(request.VersionName), "Phiên bản đã tồn tại cho ứng dụng này.");
            return ValidationProblem(ModelState);
        }

        var version = new AppVersion
        {
            ApplicationId = app.ApplicationId,
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
            appKey = app.AppKey,
            appName = app.DisplayName
        });
    }

    [HttpGet("check-app-version")]
    public async Task<IActionResult> CheckAppVersion([FromQuery] string appKey, [FromQuery] string currentVersion)
    {
        var normalized = NormalizeAppKey(appKey);
        var app = await _dbContext.Applications
            .AsNoTracking()
            .Where(a => a.AppKey == normalized && a.IsActive)
            .Select(a => new { a.ApplicationId, a.AppKey, a.DisplayName })
            .FirstOrDefaultAsync();

        if (app is null)
        {
            return NotFound(new { error = "Ứng dụng không tồn tại." });
        }

        var latest = await _dbContext.AppVersions
            .AsNoTracking()
            .Where(v => v.ApplicationId == app.ApplicationId)
            .OrderByDescending(v => v.ReleaseDate)
            .ThenByDescending(v => v.AppVersionId)
            .FirstOrDefaultAsync();

        SetNoCache();

        if (latest is null)
        {
            return Ok(new
            {
                currentVersion,
                updateAvailable = false,
                message = "Chưa có bản phát hành nào cho ứng dụng này.",
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

    #region Helpers

    private string NormalizeAppKey(string? appKey) => string.IsNullOrWhiteSpace(appKey)
        ? string.Empty
        : appKey.Trim().ToLowerInvariant();

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
        headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MustRevalidate = true
        };
        Response.Headers[HeaderNames.Pragma] = "no-cache";
        Response.Headers[HeaderNames.Expires] = "0";
    }

    #endregion
}

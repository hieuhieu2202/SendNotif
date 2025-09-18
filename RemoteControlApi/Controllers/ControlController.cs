using System.ComponentModel.DataAnnotations;
using System.IO;
using Microsoft.AspNetCore.Http;
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

    #region Notifications

    [HttpPost("send-notification-json")]
    [Consumes("application/json")]
    public Task<IActionResult> SendNotificationJson([FromBody] NotificationMessage message)
    {
        return PersistNotificationAsync(message);
    }

    [HttpPost("send-notification")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SendNotificationForm([FromForm] SendNotificationForm form)
    {
        var message = new NotificationMessage
        {
            Title = form.Title,
            Body = form.Body,
            Link = form.Link,
            AppVersionId = form.AppVersionId,
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
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.IsActive)
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
                n.IsActive,
                appVersion = n.AppVersion == null
                    ? null
                    : new
                    {
                        n.AppVersion.AppVersionId,
                        n.AppVersion.VersionName,
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
    public async Task<IActionResult> ClearNotifications()
    {
        await _dbContext.Notifications.ExecuteDeleteAsync();
        SetNoCache();
        return Ok(new { status = "Cleared" });
    }

    #endregion

    #region App Versions

    [HttpGet("app-versions")]
    public async Task<IActionResult> ListAppVersions()
    {
        var versions = await _dbContext.AppVersions
            .AsNoTracking()
            .OrderByDescending(v => v.ReleaseDate)
            .ThenByDescending(v => v.AppVersionId)
            .Select(v => new
            {
                v.AppVersionId,
                v.VersionName,
                v.ReleaseNotes,
                v.FileUrl,
                v.FileChecksum,
                v.ReleaseDate
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
                v.ReleaseNotes,
                v.FileUrl,
                v.FileChecksum,
                v.ReleaseDate
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
        request.VersionName = request.VersionName?.Trim() ?? string.Empty;
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

        var releaseDate = request.ReleaseDate;
        if (releaseDate.Kind == DateTimeKind.Local)
        {
            releaseDate = releaseDate.ToUniversalTime();
        }
        else if (releaseDate.Kind == DateTimeKind.Unspecified)
        {
            releaseDate = DateTime.SpecifyKind(releaseDate, DateTimeKind.Utc);
        }

        var duplicate = await _dbContext.AppVersions.AnyAsync(v => v.VersionName == request.VersionName);
        if (duplicate)
        {
            ModelState.AddModelError(nameof(request.VersionName), "Phiên bản đã tồn tại.");
            return ValidationProblem(ModelState);
        }

        var version = new AppVersion
        {
            VersionName = request.VersionName,
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
            version.ReleaseNotes,
            version.FileUrl,
            version.FileChecksum,
            version.ReleaseDate
        });
    }

    [HttpGet("check-app-version")]
    public async Task<IActionResult> CheckAppVersion([FromQuery] string currentVersion)
    {
        currentVersion = currentVersion?.Trim() ?? string.Empty;

        var latest = await _dbContext.AppVersions
            .AsNoTracking()
            .OrderByDescending(v => v.ReleaseDate)
            .ThenByDescending(v => v.AppVersionId)
            .FirstOrDefaultAsync();

        if (latest is null)
        {
            return Ok(new
            {
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
            currentVersion,
            serverVersion = latest.VersionName,
            updateAvailable,
            comparisonNote,
            latestRelease = new
            {
                latest.AppVersionId,
                latest.VersionName,
                latest.ReleaseNotes,
                latest.FileUrl,
                latest.FileChecksum,
                latest.ReleaseDate
            }
        });
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

        if (message.AppVersionId.HasValue && message.AppVersionId <= 0)
        {
            message.AppVersionId = null;
        }

        ModelState.Clear();
        if (!TryValidateModel(message))
        {
            return ValidationProblem(ModelState);
        }

        AppVersion? version = null;
        if (message.AppVersionId.HasValue)
        {
            version = await _dbContext.AppVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.AppVersionId == message.AppVersionId.Value);

            if (version is null)
            {
                ModelState.AddModelError(nameof(message.AppVersionId), "AppVersionId không tồn tại.");
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

        var entity = new Notification
        {
            Title = message.Title,
            Message = message.Body,
            Link = message.Link,
            CreatedAt = DateTime.UtcNow,
            AppVersionId = message.AppVersionId,
            FileUrl = storedFileUrl,
            IsActive = true
        };

        _dbContext.Notifications.Add(entity);
        await _dbContext.SaveChangesAsync();

        SetNoCache();
        return Ok(new
        {
            status = "sent",
            notificationId = entity.NotificationId,
            fileUrl = storedFileUrl,
            appVersion = version is null
                ? null
                : new
                {
                    version.AppVersionId,
                    version.VersionName,
                    version.ReleaseNotes,
                    version.FileUrl,
                    version.FileChecksum,
                    version.ReleaseDate
                }
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
        headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true,
            MustRevalidate = true
        };
        Response.Headers[HeaderNames.Pragma] = "no-cache";
        Response.Headers[HeaderNames.Expires] = "0";
    }

    public class SendNotificationForm
    {
        [Required, StringLength(100)]
        public string Title { get; set; } = default!;

        [Required, StringLength(4000)]
        public string Body { get; set; } = default!;

        [StringLength(255)]
        public string? Link { get; set; }

        [Range(1, int.MaxValue)]
        public int? AppVersionId { get; set; }

        public IFormFile? File { get; set; }
    }

    #endregion
}

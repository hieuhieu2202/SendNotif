using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
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

    private static readonly string StoreRoot =
        Path.Combine(AppContext.BaseDirectory, "Builds");
    private static readonly string ManifestPath =
        Path.Combine(StoreRoot, "manifest.json");

    private static readonly ConcurrentQueue<NotificationMessage> _notifications = new();
    private const int MaxNotifications = 1000;
    private static readonly ConcurrentDictionary<Guid, Channel<NotificationMessage>> _streams = new();

    private static AppVersionInfo _appVersion = LoadManifestOrDefault();

    public ControlController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private static AppVersionInfo LoadManifestOrDefault()
    {
        Directory.CreateDirectory(StoreRoot);
        if (System.IO.File.Exists(ManifestPath))
        {
            try
            {
                var json = System.IO.File.ReadAllText(ManifestPath);
                var data = JsonSerializer.Deserialize<AppVersionInfo>(json);
                if (data != null) return data;
            }
            catch
            {
                // ignore, fallback
            }
        }

        return new AppVersionInfo
        {
            Latest = string.Empty,
            MinSupported = string.Empty,
            Build = 0,
            UpdatedAt = DateTimeOffset.UtcNow,
            Files = new Dictionary<string, AppFileInfo>() // key: "android"/"ios"
        };
    }

    private static void SaveManifest()
    {
        var json = JsonSerializer.Serialize(_appVersion, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        System.IO.File.WriteAllText(ManifestPath, json);
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

    private static bool TryParseVersionString(string? value, out Version version)
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

    [HttpPost("send-notification-json")]
    [Consumes("application/json")]
    public async Task<IActionResult> SendNotificationJson([FromBody] NotificationMessage message)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(message.FileBase64))
            {
                var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var webrootPath = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
                Directory.CreateDirectory(webrootPath);
                var uploads = Path.Combine(webrootPath, "uploads");
                Directory.CreateDirectory(uploads);

                var ext = Path.GetExtension(message.FileName);
                if (string.IsNullOrWhiteSpace(ext) || ext.Length > 10) ext = ".bin";
                var fileName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(uploads, fileName);
                var bytes = Convert.FromBase64String(message.FileBase64);
                await System.IO.File.WriteAllBytesAsync(fullPath, bytes);

                var basePath = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;
                message.FileUrl = $"{basePath}/uploads/{fileName}";
            }

            return await HandleNotificationAsync(message);
        }
        catch (Exception ex)
        {
            return Problem(detail: ex.Message);
        }
    }

    [HttpPost("send-notification")]
    public async Task<IActionResult> SendNotificationUnified()
    {
        try
        {
            NotificationMessage msg;

            if (Request.HasFormContentType)
            {
                // multipart/form-data (hoặc x-www-form-urlencoded)
                var form = await Request.ReadFormAsync();

                msg = new NotificationMessage
                {
                    Id = string.IsNullOrWhiteSpace(form["id"]) ? null : form["id"].ToString(),
                    Title = form["title"],
                    Body = form["body"],
                    Link = string.IsNullOrWhiteSpace(form["link"]) ? null : form["link"].ToString().Trim()
                };

                if (int.TryParse(form["appVersionId"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedVersionId))
                {
                    msg.AppVersionId = parsedVersionId;
                }

                // file là tuỳ chọn
                var file = form.Files.GetFile("file");
                if (file is { Length: > 0 })
                {
                    var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                    var webrootPath = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
                    Directory.CreateDirectory(webrootPath);
                    var uploads = Path.Combine(webrootPath, "uploads");
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
                    var basePath = Request.PathBase.HasValue ? Request.PathBase.Value : string.Empty;
                    msg.FileUrl = $"{basePath}/uploads/{fileName}";
                }
            }
            else if (Request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
            {
                msg = await JsonSerializer.DeserializeAsync<NotificationMessage>(Request.Body)
                      ?? throw new JsonException("Payload is empty.");
            }
            else
            {
                return BadRequest(new { error = "Unsupported content type." });
            }

            return await HandleNotificationAsync(msg);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "JSON không hợp lệ." });
        }
        catch (Exception ex)
        {
            // Có thể log thêm tại đây
            return Problem(detail: ex.Message);
        }
    }

    private async Task<IActionResult> HandleNotificationAsync(NotificationMessage message)
    {
        if (message.AppVersionId.HasValue && message.AppVersionId.Value <= 0)
        {
            message.AppVersionId = null;
        }

        if (!TryValidateModel(message)) return ValidationProblem(ModelState);
        message.Link = string.IsNullOrWhiteSpace(message.Link) ? null : message.Link.Trim();

        if (message.AppVersionId.HasValue)
        {
            var exists = await _dbContext.AppVersions
                .AsNoTracking()
                .AnyAsync(v => v.AppVersionId == message.AppVersionId.Value);
            if (!exists)
            {
                ModelState.AddModelError(
                    nameof(NotificationMessage.AppVersionId),
                    "AppVersionId không tồn tại. Có thể bỏ trống trường này nếu không cần gắn với bản cập nhật."
                );
                return ValidationProblem(ModelState);
            }
        }

        message.TimestampUtc = DateTimeOffset.UtcNow;
        message.Id = string.IsNullOrWhiteSpace(message.Id)
            ? Guid.NewGuid().ToString("n")
            : message.Id;
        _notifications.Enqueue(message);
        while (_notifications.Count > MaxNotifications && _notifications.TryDequeue(out _)) { }
        foreach (var pair in _streams.ToArray())
        {
            if (!pair.Value.Writer.TryWrite(message))
                _streams.TryRemove(pair.Key, out _);
        }

        var entity = new Notification
        {
            Title = message.Title,
            Message = message.Body,
            CreatedAt = message.TimestampUtc.UtcDateTime,
            Link = message.Link,
            AppVersionId = message.AppVersionId,
            FileUrl = message.FileUrl,
            IsActive = true
        };
        _dbContext.Notifications.Add(entity);
        await _dbContext.SaveChangesAsync();

        SetNoCache();
        return Ok(new { status = "Notification received", message });
    }

    [HttpGet("get-notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.IsActive)
            .OrderByDescending(n => n.CreatedAt)
            .Include(n => n.AppVersion);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new
            {
                notificationId = n.NotificationId,
                title = n.Title,
                message = n.Message,
                createdAt = n.CreatedAt,
                link = n.Link,
                fileUrl = n.FileUrl,
                appVersion = n.AppVersion == null
                    ? null
                    : new
                    {
                        appVersionId = n.AppVersion.AppVersionId,
                        versionName = n.AppVersion.VersionName,
                        releaseNotes = n.AppVersion.ReleaseNotes,
                        fileUrl = n.AppVersion.FileUrl,
                        fileChecksum = n.AppVersion.FileChecksum,
                        releaseDate = n.AppVersion.ReleaseDate
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
    public async Task<IActionResult> Clear()
    {
        while (_notifications.TryDequeue(out _)) { }
        await _dbContext.Notifications.ExecuteDeleteAsync();
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
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _streams.TryRemove(id, out _);
        }
    }

    [HttpGet("app-version")]
    public IActionResult GetAppVersion()
    {
        SetNoCache();
        return Ok(_appVersion);
    }

    [HttpGet("check-app-version")]
    public async Task<IActionResult> CheckAppVersion([FromQuery][Required] string currentVersion)
    {
        var latestVersion = await _dbContext.AppVersions
            .AsNoTracking()
            .OrderByDescending(v => v.ReleaseDate)
            .ThenByDescending(v => v.AppVersionId)
            .FirstOrDefaultAsync();

        SetNoCache();

        if (latestVersion is null)
        {
            return Ok(new
            {
                currentVersion,
                updateAvailable = false,
                message = "Chưa có thông tin phiên bản nào trên máy chủ",
                latestVersion = (object?)null
            });
        }

        var hasCurrent = TryParseVersionString(currentVersion, out var currentVer);
        var hasLatest = TryParseVersionString(latestVersion.VersionName, out var latestVer);

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
            comparisonNote = "Dữ liệu phiên bản trên máy chủ không hợp lệ";
        }

        return Ok(new
        {
            currentVersion,
            serverVersion = latestVersion.VersionName,
            updateAvailable,
            comparisonNote,
            latestRelease = new
            {
                latestVersion.AppVersionId,
                latestVersion.VersionName,
                latestVersion.ReleaseNotes,
                latestVersion.FileUrl,
                latestVersion.FileChecksum,
                latestVersion.ReleaseDate
            }
        });
    }

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

        if (!TryValidateModel(request)) return ValidationProblem(ModelState);

        var releaseDate = request.ReleaseDate;
        if (releaseDate.Kind == DateTimeKind.Unspecified)
        {
            releaseDate = DateTime.SpecifyKind(releaseDate, DateTimeKind.Utc);
        }
        else if (releaseDate.Kind == DateTimeKind.Local)
        {
            releaseDate = releaseDate.ToUniversalTime();
        }

        var duplicate = await _dbContext.AppVersions
            .AnyAsync(v => v.VersionName == request.VersionName);
        if (duplicate)
        {
            ModelState.AddModelError(nameof(request.VersionName), "VersionName đã tồn tại.");
            return ValidationProblem(ModelState);
        }

        var entity = new AppVersion
        {
            VersionName = request.VersionName,
            ReleaseNotes = request.ReleaseNotes,
            FileUrl = request.FileUrl,
            FileChecksum = request.FileChecksum,
            ReleaseDate = releaseDate
        };

        _dbContext.AppVersions.Add(entity);
        await _dbContext.SaveChangesAsync();

        var response = new
        {
            entity.AppVersionId,
            entity.VersionName,
            entity.ReleaseNotes,
            entity.FileUrl,
            entity.FileChecksum,
            entity.ReleaseDate
        };

        SetNoCache();
        return CreatedAtAction(nameof(GetAppVersionById), new { id = entity.AppVersionId }, response);
    }

    [HttpPost("app-version/upload")]
    [RequestSizeLimit(1_500_000_000)]
    [DisableRequestSizeLimit]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadBuild(
        [FromForm][Required] string latest,
        [FromForm][Required] string minSupported,
        [FromForm] string? notesVi,
        [FromForm] string? notesEn,
        [FromForm][Required] string platform,
        [FromForm] int? build,
        [Required] IFormFile file)
    {
        platform = platform.Trim().ToLowerInvariant();
        if (platform is not ("android" or "ios"))
            return BadRequest(new { error = "platform must be 'android' or 'ios'" });

        // Kiểm tra version format
        bool validVer(string v) => System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d+(\.\d+){0,2}$");
        if (!validVer(latest) || !validVer(minSupported))
            return BadRequest(new { error = "Invalid version format. Use 'x.y.z'." });

        if (file.Length <= 0) return BadRequest(new { error = "Empty file" });

        // Đặt tên file: app-{platform}-{latest}-{ticks}.ext
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = platform == "android" ? ".apk" : ".ipa";
        var ticks = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var safeName = $"app-{platform}-{latest}-{ticks}{ext}";
        var fullPath = Path.Combine(StoreRoot, safeName);

        Directory.CreateDirectory(StoreRoot);

        // Lưu file & tính SHA256/size
        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            await file.CopyToAsync(fs);
        }
        var size = new FileInfo(fullPath).Length;
        string sha256;
        await using (var fs = System.IO.File.OpenRead(fullPath))
        using (var sha = SHA256.Create())
        {
            var hash = await sha.ComputeHashAsync(fs);
            sha256 = Convert.ToHexString(hash).ToLowerInvariant();
        }

        // Cập nhật manifest
        _appVersion.Latest = latest;
        _appVersion.MinSupported = minSupported;
        _appVersion.NotesVi = notesVi;
        _appVersion.NotesEn = notesEn;
        _appVersion.Build = build ?? Math.Max(_appVersion.Build + 1, 10001);
        _appVersion.UpdatedAt = DateTimeOffset.UtcNow;

        _appVersion.Files ??= new Dictionary<string, AppFileInfo>();
        _appVersion.Files[platform] = new AppFileInfo
        {
            FileName = safeName,
            RelativePath = safeName,
            SizeBytes = size,
            Sha256 = sha256,
            ContentType = GetContentTypeByPlatform(platform, ext)
        };

        SaveManifest();

        SetNoCache();
        return Ok(new
        {
            status = "uploaded",
            version = _appVersion.Latest,
            platform,
            file = _appVersion.Files[platform]
        });
    }

    [HttpGet("app-version/download")]
    public IActionResult DownloadLatest([FromQuery] string platform = "android")
    {
        platform = platform.Trim().ToLowerInvariant();
        if (_appVersion.Files == null || !_appVersion.Files.TryGetValue(platform, out var fileInfo))
            return NotFound(new { error = "No build available for this platform" });

        var relPath = fileInfo.RelativePath;
        if (string.IsNullOrEmpty(relPath))
            return NotFound(new { error = "File path not specified" });
        var path = Path.Combine(StoreRoot, relPath);
        if (!System.IO.File.Exists(path))
            return NotFound(new { error = "File not found on server" });

        var contentType = fileInfo.ContentType ?? "application/octet-stream";
        var fileName = fileInfo.FileName ?? Path.GetFileName(path);

        // ETag dựa trên SHA256 để client cache/validate
        Response.Headers[HeaderNames.ETag] = $"\"{fileInfo.Sha256}\"";
        // Cho phép resume
        var result = PhysicalFile(path, contentType, fileDownloadName: fileName, enableRangeProcessing: true);
        // Gợi ý không cache lâu
        SetNoCache();
        return result;
    }

    [HttpHead("app-version/download")]
    public IActionResult HeadLatest([FromQuery] string platform = "android")
    {
        platform = platform.Trim().ToLowerInvariant();
        if (_appVersion.Files == null || !_appVersion.Files.TryGetValue(platform, out var fileInfo))
            return NotFound();

        var relPathHead = fileInfo.RelativePath;
        if (string.IsNullOrEmpty(relPathHead)) return NotFound();
        var path = Path.Combine(StoreRoot, relPathHead);
        if (!System.IO.File.Exists(path)) return NotFound();

        Response.Headers[HeaderNames.ContentLength] = fileInfo.SizeBytes.ToString();
        Response.Headers[HeaderNames.ETag] = $"\"{fileInfo.Sha256}\"";
        Response.Headers[HeaderNames.ContentType] = fileInfo.ContentType ?? "application/octet-stream";
        SetNoCache();
        return Ok();
    }

    private static string GetContentTypeByPlatform(string platform, string ext)
    {
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType("x" + ext, out var ct))
            ct = "application/octet-stream";
        if (platform == "android") ct = "application/vnd.android.package-archive";
        if (platform == "ios") ct = "application/octet-stream"; // .ipa
        return ct;
    }
}

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace RemoteControlApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ControlController : ControllerBase
    {
        // ================== Storage paths ==================
        private static readonly string StoreRoot =
            Path.Combine(AppContext.BaseDirectory, "Builds");
        private static readonly string ManifestPath =
            Path.Combine(StoreRoot, "manifest.json");

        // thread-safe queue cho thông báo
        private static readonly ConcurrentQueue<NotificationMessage> _notifications = new();
        private const int MaxNotifications = 1000;

        // Thông tin version hiện tại (được nạp từ manifest)
        private static AppVersionInfo _appVersion = LoadManifestOrDefault();

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
                catch { /* ignore, fallback */ }
            }
            return new AppVersionInfo
            {
                Latest = "1.0.0",
                MinSupported = "1.0.0",
                NotesVi = "Khởi tạo.",
                Build = 10000,
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

        // ================== Notifications ==================

        [HttpPost("send-notification")]
        public IActionResult SendNotification([FromBody] NotificationMessage message)
        {
            if (!ModelState.IsValid) return ValidationProblem(ModelState);
            message.TimestampUtc = DateTimeOffset.UtcNow;
            message.Id = Guid.NewGuid().ToString("n");
            _notifications.Enqueue(message);
            while (_notifications.Count > MaxNotifications && _notifications.TryDequeue(out _)) { }
            SetNoCache();
            return Ok(new { status = "Notification received", message });
        }

        [HttpGet("get-notifications")]
        public IActionResult GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);
            var items = _notifications.ToArray()
                .OrderByDescending(x => x.TimestampUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            SetNoCache();
            return Ok(new
            {
                total = _notifications.Count,
                page,
                pageSize,
                items
            });
        }

        [HttpPost("clear-notifications")]
        public IActionResult Clear()
        {
            while (_notifications.TryDequeue(out _)) { }
            SetNoCache();
            return Ok(new { status = "Cleared" });
        }

        // ================== App Version – Metadata ==================

        /// Flutter gọi endpoint này để biết latest/minSupported/notes + hash/size
        [HttpGet("app-version")]
        public IActionResult GetAppVersion() { SetNoCache(); return Ok(_appVersion); }

        // ================== App Version – Upload binary ==================
        // multipart/form-data: fields (latest, minSupported, notesVi, notesEn, platform, build?) + file
        // platform: "android" | "ios"
        [HttpPost("app-version/upload")]
        [RequestSizeLimit(1_500_000_000)] // ~1.5GB, điều chỉnh tuỳ nhu cầu
        [DisableRequestSizeLimit]
        public async Task<IActionResult> UploadBuild(
            [FromForm][Required] string latest,
            [FromForm][Required] string minSupported,
            [FromForm] string? notesVi,
            [FromForm] string? notesEn,
            [FromForm][Required] string platform,
            [FromForm] int? build,
            [FromForm][Required] IFormFile file)
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

        // ================== App Version – Download latest binary ==================
        // Trả thẳng file (hỗ trợ Range resume)
        // GET /api/control/app-version/download?platform=android
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

        // HEAD để lấy size/hash trước khi tải
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

        // ================== Utils ==================
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

    // ================== Models ==================
    public class NotificationMessage
    {
        public string Id { get; set; } = default!;
        [Required, StringLength(120)] public string Title { get; set; } = default!;
        [Required, StringLength(4000)] public string Body { get; set; } = default!;
        public DateTimeOffset TimestampUtc { get; set; }
    }

    public class AppVersionInfo
    {
        [Required] public string Latest { get; set; } = default!;
        [Required] public string MinSupported { get; set; } = default!;
        public string? NotesVi { get; set; }
        public string? NotesEn { get; set; }
        public int Build { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        // "android" | "ios" -> file info
        public Dictionary<string, AppFileInfo>? Files { get; set; }
    }

    public class AppFileInfo
    {
        public string? FileName { get; set; }
        public string? RelativePath { get; set; }
        public long SizeBytes { get; set; }
        public string? Sha256 { get; set; }
        public string? ContentType { get; set; }
    }
}

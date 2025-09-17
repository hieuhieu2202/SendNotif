using System.ComponentModel.DataAnnotations;

namespace RemoteControlApi.Model
{
    public class NotiModel
    {
        public class NotificationMessage
        {
            public string? Id { get; set; }
            [Required, StringLength(120)] public string Title { get; set; } = default!;
            [Required, StringLength(4000)] public string Body { get; set; } = default!;
            [StringLength(255)] public string? Link { get; set; }
            [Range(1, int.MaxValue)] public int? AppVersionId { get; set; }
            public DateTimeOffset TimestampUtc { get; set; }
            public string? FileUrl { get; set; }
            public string? FileBase64 { get; set; }
            public string? FileName { get; set; }
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

        public class CreateAppVersionRequest
        {
            [Required, StringLength(50)] public string VersionName { get; set; } = default!;
            public string? ReleaseNotes { get; set; }
            [Required, StringLength(255)] public string FileUrl { get; set; } = default!;
            [StringLength(128)] public string? FileChecksum { get; set; }
            [Required] public DateTime ReleaseDate { get; set; }
        }
    }
}


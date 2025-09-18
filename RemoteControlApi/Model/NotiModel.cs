using System.ComponentModel.DataAnnotations;

namespace RemoteControlApi.Model;

public static class NotiModel
{
    public class NotificationMessage
    {
        [Required, StringLength(100)]
        public string Title { get; set; } = default!;

        [Required, StringLength(4000)]
        public string Body { get; set; } = default!;

        [StringLength(255)]
        public string? Link { get; set; }

        [Range(1, int.MaxValue)]
        public int? AppVersionId { get; set; }

        public string? FileBase64 { get; set; }

        public string? FileName { get; set; }
    }

    public class CreateAppVersionRequest
    {
        [Required, StringLength(50)]
        public string VersionName { get; set; } = default!;

        public string? ReleaseNotes { get; set; }

        [Required, StringLength(255)]
        public string FileUrl { get; set; } = default!;

        [StringLength(128)]
        public string? FileChecksum { get; set; }

        [Required]
        public DateTime ReleaseDate { get; set; }
    }
}

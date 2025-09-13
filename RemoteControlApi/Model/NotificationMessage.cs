using System.ComponentModel.DataAnnotations;

namespace RemoteControlApi.Model;

public class NotificationMessage
{
    public string? Id { get; set; }

    [Required, StringLength(120)]
    public string Title { get; set; } = default!;

    [Required, StringLength(4000)]
    public string Body { get; set; } = default!;

    [StringLength(2048)]
    public string? Link { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }
    public string? FileUrl { get; set; }
    public string? FileBase64 { get; set; }
    public string? FileName { get; set; }
}

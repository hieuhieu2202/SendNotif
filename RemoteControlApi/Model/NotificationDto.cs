using System;

namespace RemoteControlApi.Model;

public class NotificationDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Link { get; set; }
    public string? TargetVersion { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public string? FileUrl { get; set; }
    public string? FileBase64 { get; set; }
    public string? FileName { get; set; }

    public static NotificationDto FromEntity(NotificationMessage entity) => new()
    {
        Id = entity.Id ?? string.Empty,
        Title = entity.Title,
        Body = entity.Body,
        Link = entity.Link,
        TargetVersion = entity.TargetVersion,
        TimestampUtc = entity.TimestampUtc,
        FileUrl = entity.FileUrl,
        FileBase64 = entity.FileBase64,
        FileName = entity.FileName
    };
}

namespace RemoteControlApi.Model;

public class AppVersionInfo
{
    public string Version { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FileUrl { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public DateTimeOffset UploadedAt { get; set; }
}

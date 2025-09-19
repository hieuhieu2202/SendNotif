using System.ComponentModel.DataAnnotations;

namespace RemoteControlApi.Model;

public class AppVersionUpload
{
    [Required]
    public string Version { get; set; } = "";

    [Required]
    public IFormFile File { get; set; } = default!;
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RemoteControlApi.Model;

public class SendNotificationFormData
{
    public string? Id { get; set; }

    [Required, StringLength(120)]
    public string Title { get; set; } = default!;

    [Required, StringLength(4000)]
    public string Body { get; set; } = default!;

    [StringLength(2048)]
    public string? Link { get; set; }

    [StringLength(50)]
    public string? TargetVersion { get; set; }

    public IFormFile? File { get; set; }
}

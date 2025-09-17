using System.ComponentModel.DataAnnotations;

namespace RemoteControlApi.Model;

public class DeviceVersionRequest
{
    [Required]
    [StringLength(50)]
    public string Version { get; set; } = default!;

    [StringLength(100)]
    public string? CardCode { get; set; }
}

using Microsoft.EntityFrameworkCore;
using RemoteControlApi.Model;

namespace RemoteControlApi.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationMessage> Notifications => Set<NotificationMessage>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceNotification> DeviceNotifications => Set<DeviceNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<NotificationMessage>().ToTable("Notifications");
        modelBuilder.Entity<Device>().ToTable("Devices");
        modelBuilder.Entity<DeviceNotification>().ToTable("DeviceNotifications");
        modelBuilder.Entity<DeviceNotification>().HasKey(dn => new { dn.DeviceId, dn.NotificationId });
        modelBuilder.Entity<DeviceNotification>()
            .HasOne(dn => dn.Device)
            .WithMany(d => d.Notifications)
            .HasForeignKey(dn => dn.DeviceId);
        modelBuilder.Entity<DeviceNotification>()
            .HasOne(dn => dn.Notification)
            .WithMany()
            .HasForeignKey(dn => dn.NotificationId);
    }
}

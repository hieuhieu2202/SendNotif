using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using RemoteControlApi.Data;

#nullable disable

namespace RemoteControlApi.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.6")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        modelBuilder.Entity("RemoteControlApi.Entities.AppVersion", b =>
        {
            b.Property<int>("AppVersionId")
                .ValueGeneratedOnAdd()
                .HasColumnType("int")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            b.Property<string>("FileChecksum")
                .HasMaxLength(128)
                .HasColumnType("nvarchar(128)");

            b.Property<string>("FileUrl")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("nvarchar(255)");

            b.Property<DateTime>("ReleaseDate")
                .HasColumnType("datetime2");

            b.Property<string>("ReleaseNotes")
                .HasColumnType("nvarchar(max)");

            b.Property<string>("VersionName")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("nvarchar(50)");

            b.HasKey("AppVersionId");

            b.ToTable("AppVersions");

        });

        modelBuilder.Entity("RemoteControlApi.Entities.Notification", b =>
        {
            b.Property<int>("NotificationId")
                .ValueGeneratedOnAdd()
                .HasColumnType("int")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            b.Property<int?>("AppVersionId")
                .HasColumnType("int");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("datetime2");

            b.Property<string>("FileUrl")
                .HasMaxLength(255)
                .HasColumnType("nvarchar(255)");

            b.Property<bool>("IsActive")
                .HasColumnType("bit");

            b.Property<string>("Link")
                .HasMaxLength(255)
                .HasColumnType("nvarchar(255)");

            b.Property<string>("Message")
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            b.Property<string>("Title")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.HasKey("NotificationId");

            b.HasIndex("AppVersionId");

            b.ToTable("Notifications");

        });

        modelBuilder.Entity("RemoteControlApi.Entities.Notification", b =>
        {
            b.HasOne("RemoteControlApi.Entities.AppVersion", "AppVersion")
                .WithMany("Notifications")
                .HasForeignKey("AppVersionId")
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Notifications_AppVersions_AppVersionId");

            b.Navigation("AppVersion");
        });

        modelBuilder.Entity("RemoteControlApi.Entities.AppVersion", b =>
        {
            b.Navigation("Notifications");
        });
    }
}

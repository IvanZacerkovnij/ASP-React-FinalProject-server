using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class MediaConfigurator : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        builder.ToTable("Media");

        builder.HasKey(media => media.Id);

        builder.Property(media => media.StorageKey)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(media => media.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(media => media.ContentType)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(media => media.SizeInBytes)
            .IsRequired();

        builder.Property(media => media.SortOrder)
            .IsRequired();

        builder.Property(media => media.CreatedAt)
            .IsRequired();

        builder.HasIndex(media => media.StorageKey)
            .IsUnique();

        builder.HasIndex(media => media.UploadedByUserId);
        builder.HasIndex(media => media.PostId);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Media_SizeInBytes",
                "\"SizeInBytes\" >= 0"));

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Media_SortOrder",
                "\"SortOrder\" >= 0"));

        builder.HasOne(media => media.UploadedByUser)
            .WithMany(user => user.UploadedMedia)
            .HasForeignKey(media => media.UploadedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(media => media.Post)
            .WithMany(post => post.Media)
            .HasForeignKey(media => media.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

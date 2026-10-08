using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class CommentVersionConfigurator : IEntityTypeConfiguration<CommentVersion>
{
    public void Configure(EntityTypeBuilder<CommentVersion> builder)
    {
        builder.ToTable("CommentVersions");
        
        builder.HasKey(version => version.Id);
        
        builder.Property(version => version.SnapshotJson)
            .HasColumnType("jsonb")
            .IsRequired();
        
        builder.Property(version => version.SchemaVersion)
            .IsRequired();
        
        builder.Property(version => version.CreatedAt)
            .IsRequired();

        builder.HasIndex(version => new
        {
            version.CommentId,
            version.CreatedAt
        });
        
        builder.HasOne(version => version.Comment)
            .WithMany(comment => comment.Versions)
            .HasForeignKey(version => version.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_CommentVersions_SchemaVersion",
                "\"SchemaVersion\" > 0"));
    }
}
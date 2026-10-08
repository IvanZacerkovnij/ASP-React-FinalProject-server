using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class PostVersionConfigurator : IEntityTypeConfiguration<PostVersion>
{
    public void Configure(EntityTypeBuilder<PostVersion> builder)
    {
        builder.ToTable("PostVersions");
        
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
            version.PostId,
            version.CreatedAt
        });
        
        builder.HasOne(version => version.Post)
            .WithMany(post => post.Versions)
            .HasForeignKey(version => version.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_PostVersions_SchemaVersion",
                "\"SchemaVersion\" > 0"));
    }
}
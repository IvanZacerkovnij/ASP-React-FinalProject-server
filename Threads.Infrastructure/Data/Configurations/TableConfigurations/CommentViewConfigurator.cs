using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class CommentViewConfigurator : IEntityTypeConfiguration<CommentView>
{
    public void Configure(EntityTypeBuilder<CommentView> builder)
    {
        builder.ToTable("CommentViews");

        builder.HasKey(cv => new { cv.CommentId, cv.UserId });

        builder.HasIndex(cv => cv.UserId);

        builder.Property(cv => cv.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasOne(cv => cv.User)
            .WithMany(u => u.CommentViews)
            .HasForeignKey(cv => cv.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cv => cv.Comment)
            .WithMany(c => c.CommentViews)
            .HasForeignKey(cv => cv.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

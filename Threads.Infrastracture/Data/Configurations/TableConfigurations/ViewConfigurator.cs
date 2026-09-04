using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class ViewConfigurator : IEntityTypeConfiguration<View>
{
    public void Configure(EntityTypeBuilder<View> builder)
    {
        builder.ToTable("Views");

        builder.HasKey(view => view.Id);

        builder.Property(view => view.CreatedAt)
            .IsRequired();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Views_ExactlyOneTarget",
                "(\"PostId\" IS NOT NULL AND \"CommentId\" IS NULL) OR (\"PostId\" IS NULL AND \"CommentId\" IS NOT NULL)"));

        builder.HasIndex(view => new { view.PostId, view.ViewerId })
            .IsUnique()
            .HasFilter("\"PostId\" IS NOT NULL");

        builder.HasIndex(view => new { view.CommentId, view.ViewerId })
            .IsUnique()
            .HasFilter("\"CommentId\" IS NOT NULL");

        builder.HasOne(view => view.Post)
            .WithMany(post => post.Views)
            .HasForeignKey(view => view.PostId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(view => view.Comment)
            .WithMany(comment => comment.Views)
            .HasForeignKey(view => view.CommentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(view => view.Viewer)
            .WithMany(user => user.Views)
            .HasForeignKey(view => view.ViewerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

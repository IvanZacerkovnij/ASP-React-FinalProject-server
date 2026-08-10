using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class PostViewConfigurator : IEntityTypeConfiguration<PostView>
{
    public void Configure(EntityTypeBuilder<PostView> builder)
    {
        builder.ToTable("PostViews");

        builder.HasKey(postView => postView.Id);

        builder.Property(postView => postView.CreatedAt)
            .IsRequired();

        builder.HasIndex(postView => new { postView.PostId, postView.ViewerId })
            .IsUnique();

        builder.HasOne(postView => postView.Post)
            .WithMany(post => post.Views)
            .HasForeignKey(postView => postView.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(postView => postView.Viewer)
            .WithMany(user => user.PostViews)
            .HasForeignKey(postView => postView.ViewerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

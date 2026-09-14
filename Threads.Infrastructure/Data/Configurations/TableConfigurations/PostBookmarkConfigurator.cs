using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class PostBookmarkConfigurator : IEntityTypeConfiguration<PostBookmark>
{
    public void Configure(EntityTypeBuilder<PostBookmark> builder)
    {
        builder.ToTable("PostBookmarks");

        builder.HasKey(postBookmark => new { postBookmark.PostId, postBookmark.UserId });

        builder.HasIndex(postBookmark => new
        {
            postBookmark.UserId,
            postBookmark.CreatedAt,
            postBookmark.PostId
        });

        builder.Property(postBookmark => postBookmark.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasOne(postBookmark => postBookmark.User)
            .WithMany(user => user.PostBookmarks)
            .HasForeignKey(postBookmark => postBookmark.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(postBookmark => postBookmark.Post)
            .WithMany(post => post.PostBookmarks)
            .HasForeignKey(postBookmark => postBookmark.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

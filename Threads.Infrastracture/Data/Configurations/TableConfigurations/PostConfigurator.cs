using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class PostConfigurator : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts");

        builder.HasKey(post => post.Id);

        builder.Property(post => post.Content)
            .HasMaxLength(2000);

        builder.Property(post => post.LocationName)
            .HasMaxLength(255);

        builder.Property(post => post.LocationPlaceId)
            .HasMaxLength(1024);

        builder.Property(post => post.LocationCountry)
            .HasMaxLength(255);

        builder.Property(post => post.EmbedUrl)
            .HasMaxLength(2048);

        builder.Property(post => post.EmbedTitle)
            .HasMaxLength(255);

        builder.Property(post => post.EmbedDescription)
            .HasMaxLength(1000);

        builder.Property(post => post.EmbedThumbnailUrl)
            .HasMaxLength(2048);

        builder.Property(post => post.ViewsCount)
            .IsRequired();

        builder.Property(post => post.RepostsCount)
            .IsRequired();

        builder.Property(post => post.CreatedAt)
            .IsRequired();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Posts_ViewsCount",
                "\"ViewsCount\" >= 0"));

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Posts_RepostsCount",
                "\"RepostsCount\" >= 0"));

        builder.HasIndex(post => post.AuthorId);

        builder.HasOne(post => post.Author)
            .WithMany(user => user.Posts)
            .HasForeignKey(post => post.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(post => post.Comments)
            .WithOne(comment => comment.Post)
            .HasForeignKey(comment => comment.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(post => post.Likes)
            .WithOne(like => like.Post)
            .HasForeignKey(like => like.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(post => post.Views)
            .WithOne(view => view.Post)
            .HasForeignKey(view => view.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(post => post.Media)
            .WithOne(media => media.Post)
            .HasForeignKey(media => media.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(post => post.Poll)
            .WithOne(poll => poll.Post)
            .HasForeignKey<Poll>(poll => poll.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

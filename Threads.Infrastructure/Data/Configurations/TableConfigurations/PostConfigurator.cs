using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

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

        builder.Property(post => post.CreatedAt)
            .IsRequired();

        builder.HasIndex(post => new { post.AuthorId, post.CreatedAt, post.Id });

        builder.Property<NpgsqlTsVector>(PostgresSearch.VectorProperty)
            .IsGeneratedTsVectorColumn(
                PostgresSearch.Configuration,
                nameof(Post.Content),
                nameof(Post.LocationName),
                nameof(Post.EmbedTitle));

        builder.HasIndex(PostgresSearch.VectorProperty)
            .HasMethod("gin")
            .HasDatabaseName("IX_Posts_SearchVector");

        builder.HasOne(post => post.Author)
            .WithMany(user => user.Posts)
            .HasForeignKey(post => post.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(post => post.Comments)
            .WithOne(comment => comment.Post)
            .HasForeignKey(comment => comment.PostId)
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

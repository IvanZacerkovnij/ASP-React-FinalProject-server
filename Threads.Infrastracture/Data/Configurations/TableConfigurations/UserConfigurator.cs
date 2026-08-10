using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class UserConfigurator : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Username)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(user => user.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(user => user.PasswordHash)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(user => user.PasswordResetCodeHash)
            .HasMaxLength(128);

        builder.Property(user => user.DisplayName)
            .HasMaxLength(100);

        builder.Property(user => user.Bio)
            .HasMaxLength(500);

        builder.Property(user => user.Location)
            .HasMaxLength(255);

        builder.Property(user => user.LocationPlaceId)
            .HasMaxLength(1024);

        builder.Property(user => user.LocationCountry)
            .HasMaxLength(255);

        builder.Property(user => user.AvatarObjectKey)
            .HasMaxLength(512);

        builder.Property(user => user.BannerObjectKey)
            .HasMaxLength(512);

        builder.Property(user => user.CreatedAt)
            .IsRequired();

        builder.HasIndex(user => user.Username)
            .IsUnique();

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.HasMany(user => user.Posts)
            .WithOne(post => post.Author)
            .HasForeignKey(post => post.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.Comments)
            .WithOne(comment => comment.Author)
            .HasForeignKey(comment => comment.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.Likes)
            .WithOne(like => like.User)
            .HasForeignKey(like => like.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.PostViews)
            .WithOne(postView => postView.Viewer)
            .HasForeignKey(postView => postView.ViewerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.PollVotes)
            .WithOne(vote => vote.User)
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.FollowingRelations)
            .WithOne(follow => follow.Follower)
            .HasForeignKey(follow => follow.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.FollowerRelations)
            .WithOne(follow => follow.Following)
            .HasForeignKey(follow => follow.FollowingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.RefreshTokens)
            .WithOne(token => token.User)
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.UploadedMedia)
            .WithOne(media => media.UploadedByUser)
            .HasForeignKey(media => media.UploadedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

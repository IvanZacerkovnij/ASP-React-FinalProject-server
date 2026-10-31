using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class NotificationConfigurator : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", table =>
        {
            table.HasCheckConstraint("CK_Notifications_Type",
                "\"Type\" IN ('Follow', 'PostLike', 'CommentLike', 'PostRepost', 'CommentRepost', 'PostComment', 'CommentReply', 'Quote')");
            table.HasCheckConstraint("CK_Notifications_DifferentUsers", "\"ActorId\" <> \"RecipientId\"");
        });

        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(notification => notification.CreatedAt).IsRequired();

        builder.HasIndex(notification => new { notification.Type, notification.ActorId, notification.SourceId }).IsUnique();
        builder.HasIndex(notification => new { notification.RecipientId, notification.CreatedAt, notification.Id });
        builder.HasIndex(notification => notification.RecipientId).HasFilter("\"ReadAt\" IS NULL");
        builder.HasIndex(notification => notification.SourceId);

        builder.HasOne(notification => notification.Recipient).WithMany()
            .HasForeignKey(notification => notification.RecipientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(notification => notification.Actor).WithMany()
            .HasForeignKey(notification => notification.ActorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(notification => notification.Post).WithMany()
            .HasForeignKey(notification => notification.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(notification => notification.Comment).WithMany()
            .HasForeignKey(notification => notification.CommentId).OnDelete(DeleteBehavior.Cascade);
    }
}

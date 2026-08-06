using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class FollowConfigurator : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> builder)
    {
        builder.ToTable("Follows");

        builder.HasKey(follow => follow.Id);

        builder.Property(follow => follow.CreatedAt)
            .IsRequired();

        builder.HasIndex(follow => new { follow.FollowerId, follow.FollowingId })
            .IsUnique();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Follows_FollowerId_FollowingId",
                "\"FollowerId\" <> \"FollowingId\""));

        builder.HasOne(follow => follow.Follower)
            .WithMany(user => user.FollowingRelations)
            .HasForeignKey(follow => follow.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(follow => follow.Following)
            .WithMany(user => user.FollowerRelations)
            .HasForeignKey(follow => follow.FollowingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

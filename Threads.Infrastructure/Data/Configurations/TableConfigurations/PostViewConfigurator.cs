using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class PostViewConfigurator : IEntityTypeConfiguration<PostView>
{
    public void Configure(EntityTypeBuilder<PostView> builder)
    {
        builder.ToTable("PostViews");

        builder.HasKey(pv => new { pv.PostId, pv.UserId });

        builder.HasIndex(pv => pv.UserId);

        builder.Property(pv => pv.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasOne(pv => pv.User)
            .WithMany(u => u.PostViews)
            .HasForeignKey(pv => pv.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pv => pv.Post)
            .WithMany(c => c.PostViews)
            .HasForeignKey(pv => pv.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

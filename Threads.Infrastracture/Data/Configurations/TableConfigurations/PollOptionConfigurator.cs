using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class PollOptionConfigurator : IEntityTypeConfiguration<PollOption>
{
    public void Configure(EntityTypeBuilder<PollOption> builder)
    {
        builder.ToTable("PollOptions");

        builder.HasKey(option => option.Id);

        builder.Property(option => option.Text)
            .IsRequired()
            .HasMaxLength(280);

        builder.Property(option => option.CreatedAt)
            .IsRequired();

        builder.HasIndex(option => new { option.PollId, option.Position })
            .IsUnique();

        builder.HasOne(option => option.Poll)
            .WithMany(poll => poll.Options)
            .HasForeignKey(option => option.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(option => option.Votes)
            .WithOne(vote => vote.PollOption)
            .HasForeignKey(vote => vote.PollOptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

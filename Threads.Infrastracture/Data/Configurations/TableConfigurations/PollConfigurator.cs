using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class PollConfigurator : IEntityTypeConfiguration<Poll>
{
    public void Configure(EntityTypeBuilder<Poll> builder)
    {
        builder.ToTable("Polls");

        builder.HasKey(poll => poll.Id);

        builder.Property(poll => poll.CreatedAt)
            .IsRequired();

        builder.HasIndex(poll => poll.PostId)
            .IsUnique();

        builder.HasOne(poll => poll.Post)
            .WithOne(post => post.Poll)
            .HasForeignKey<Poll>(poll => poll.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(poll => poll.Options)
            .WithOne(option => option.Poll)
            .HasForeignKey(option => option.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(poll => poll.Votes)
            .WithOne(vote => vote.Poll)
            .HasForeignKey(vote => vote.PollId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

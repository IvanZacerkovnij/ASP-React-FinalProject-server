using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class PollVoteConfigurator : IEntityTypeConfiguration<PollVote>
{
    public void Configure(EntityTypeBuilder<PollVote> builder)
    {
        builder.ToTable("PollVotes");

        builder.HasKey(vote => vote.Id);

        builder.Property(vote => vote.CreatedAt)
            .IsRequired();

        builder.HasIndex(vote => new { vote.PollId, vote.UserId })
            .IsUnique();

        builder.HasOne(vote => vote.Poll)
            .WithMany(poll => poll.Votes)
            .HasForeignKey(vote => vote.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(vote => vote.PollOption)
            .WithMany(option => option.Votes)
            .HasForeignKey(vote => vote.PollOptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(vote => vote.User)
            .WithMany(user => user.PollVotes)
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

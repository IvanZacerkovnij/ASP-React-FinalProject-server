using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

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

        builder.HasIndex(poll => poll.CommentId)
            .IsUnique();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Polls_SingleTarget",
                "(\"PostId\" IS NOT NULL)::int + (\"CommentId\" IS NOT NULL)::int = 1"));

        builder.HasOne(poll => poll.Post)
            .WithOne(post => post.Poll)
            .HasForeignKey<Poll>(poll => poll.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(poll => poll.Comment)
            .WithOne(comment => comment.Poll)
            .HasForeignKey<Poll>(poll => poll.CommentId)
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

using Microsoft.EntityFrameworkCore;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data;

public class ThreadsDbContext : DbContext
{
    public DbSet<Comment> Comments { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Follow> Follows { get; set; }
    public DbSet<Like> Likes { get; set; }
    public DbSet<Media> Medias { get; set; }
    public DbSet<Poll> Polls { get; set; }
    public DbSet<PollOption> PollOptions { get; set; }
    public DbSet<PollVote> PollVotes { get; set; }
    public DbSet<PostView> PostViews { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public ThreadsDbContext(DbContextOptions<ThreadsDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ThreadsDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

}

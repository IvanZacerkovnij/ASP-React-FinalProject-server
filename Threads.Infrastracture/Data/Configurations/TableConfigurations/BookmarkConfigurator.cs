using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class BookmarkConfigurator: IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.ToTable("Bookmarks");
        
        builder.HasKey(bookmark => bookmark.Id);
        
        builder.Property(bookmark => bookmark.CreatedAt)
            .IsRequired();
        
        builder.HasIndex(bookmark => new {bookmark.UserId, bookmark.PostId})
            .IsUnique();
        
        builder.HasOne(bookmark => bookmark.User)
            .WithMany(user => user.Bookmarks)
            .HasForeignKey(bookmark => bookmark.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(bookmark => bookmark.Post)
            .WithMany(post => post.Bookmarks)
            .HasForeignKey(bookmark => bookmark.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
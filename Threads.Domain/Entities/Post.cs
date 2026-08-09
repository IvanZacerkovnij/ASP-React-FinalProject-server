using System.ComponentModel.DataAnnotations;
using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class Post : BaseEntity
{
    public string? Content { get; set; }

    public Guid AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public ICollection<Media> Media { get; set; } =
        new List<Media>();

    public ICollection<Comment> Comments { get; set; } =
        new List<Comment>();

    public ICollection<Like> Likes { get; set; } =
        new List<Like>();

    public Poll? Poll { get; set; }
}

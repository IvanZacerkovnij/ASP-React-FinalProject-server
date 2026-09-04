using AutoMapper;
using Threads.Application.DTOs.Comments;
using Threads.Domain.Entities;

namespace Threads.Application.Mapping;

public class CommentProfile : Profile
{
    public CommentProfile()
    {
        CreateMap<Comment, CommentResponse>()
            .ForMember(destination => destination.LikesCount, options => options.MapFrom(source => source.Likes.Count))
            .ForMember(destination => destination.IsLikedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.RepliesCount, options => options.MapFrom(source => source.Replies.Count))
            .ForMember(destination => destination.IsBookmarkedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.RepostsCount, options => options.MapFrom(source => source.Reposts.Count))
            .ForMember(destination => destination.IsRepostedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.ViewsCount, options => options.MapFrom(source => source.Views.Count))
            .ForMember(destination => destination.CreatedAt, options => options.MapFrom(source => source.CreatedAt.UtcDateTime))
            .ForMember(destination => destination.UpdatedAt, options => options.MapFrom(source =>
                source.UpdatedAt.HasValue ? source.UpdatedAt.Value.UtcDateTime : (DateTime?)null));

        CreateMap<CreateCommentRequest, Comment>()
            .ForMember(destination => destination.AuthorId, options => options.Ignore())
            .ForMember(destination => destination.Author, options => options.Ignore())
            .ForMember(destination => destination.Post, options => options.Ignore())
            .ForMember(destination => destination.ParentComment, options => options.Ignore())
            .ForMember(destination => destination.Replies, options => options.Ignore())
            .ForMember(destination => destination.Likes, options => options.Ignore())
            .ForMember(destination => destination.Bookmarks, options => options.Ignore())
            .ForMember(destination => destination.Reposts, options => options.Ignore())
            .ForMember(destination => destination.Views, options => options.Ignore());

        CreateMap<UpdateCommentRequest, Comment>()
            .ForMember(destination => destination.PostId, options => options.Ignore())
            .ForMember(destination => destination.AuthorId, options => options.Ignore())
            .ForMember(destination => destination.Author, options => options.Ignore())
            .ForMember(destination => destination.Post, options => options.Ignore())
            .ForMember(destination => destination.ParentCommentId, options => options.Ignore())
            .ForMember(destination => destination.ParentComment, options => options.Ignore())
            .ForMember(destination => destination.Replies, options => options.Ignore())
            .ForMember(destination => destination.Likes, options => options.Ignore())
            .ForMember(destination => destination.Bookmarks, options => options.Ignore())
            .ForMember(destination => destination.Reposts, options => options.Ignore())
            .ForMember(destination => destination.Views, options => options.Ignore());
    }
}

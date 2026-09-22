using AutoMapper;
using Threads.Application.DTOs.Comments;
using Threads.Domain.Entities;

namespace Threads.Application.Mapping;

public class CommentProfile : Profile
{
    public CommentProfile()
    {
        CreateMap<Comment, CommentResponse>()
            .ForMember(destination => destination.LikesCount, options => options.MapFrom(source => source.CommentLikes.Count))
            .ForMember(destination => destination.IsLikedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.RepliesCount, options => options.MapFrom(source => source.Replies.Count))
            .ForMember(destination => destination.IsBookmarkedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.RepostsCount, options => options.MapFrom(source => source.CommentReposts.Count))
            .ForMember(destination => destination.IsRepostedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.ViewsCount, options => options.Ignore())
            .ForMember(destination => destination.ActionAt, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.MapFrom(source => source.CreatedAt))
            .ForMember(destination => destination.UpdatedAt, options => options.MapFrom(source => source.UpdatedAt));

        CreateMap<CreateCommentRequest, Comment>()
            .ForMember(destination => destination.AuthorId, options => options.Ignore())
            .ForMember(destination => destination.Author, options => options.Ignore())
            .ForMember(destination => destination.Post, options => options.Ignore())
            .ForMember(destination => destination.ParentComment, options => options.Ignore())
            .ForMember(destination => destination.Replies, options => options.Ignore())
            .ForMember(destination => destination.CommentLikes, options => options.Ignore())
            .ForMember(destination => destination.CommentBookmarks, options => options.Ignore())
            .ForMember(destination => destination.CommentReposts, options => options.Ignore())
            .ForMember(destination => destination.CommentViews, options => options.Ignore());

        CreateMap<UpdateCommentRequest, Comment>()
            .ForMember(destination => destination.PostId, options => options.Ignore())
            .ForMember(destination => destination.AuthorId, options => options.Ignore())
            .ForMember(destination => destination.Author, options => options.Ignore())
            .ForMember(destination => destination.Post, options => options.Ignore())
            .ForMember(destination => destination.ParentCommentId, options => options.Ignore())
            .ForMember(destination => destination.ParentComment, options => options.Ignore())
            .ForMember(destination => destination.Replies, options => options.Ignore())
            .ForMember(destination => destination.CommentLikes, options => options.Ignore())
            .ForMember(destination => destination.CommentBookmarks, options => options.Ignore())
            .ForMember(destination => destination.CommentReposts, options => options.Ignore())
            .ForMember(destination => destination.CommentViews, options => options.Ignore());
    }
}

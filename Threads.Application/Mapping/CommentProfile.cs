using AutoMapper;
using Threads.Application.DTOs.Comments;
using Threads.Domain.Entities;

namespace Threads.Application.Mapping;

public class CommentProfile : Profile
{
    public CommentProfile()
    {
        CreateMap<Comment, CommentResponse>()
            .ForMember(destination => destination.LikesCount, options => options.MapFrom(_ => 0))
            .ForMember(destination => destination.IsLikedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.MapFrom(source => source.CreatedAt.UtcDateTime))
            .ForMember(destination => destination.UpdatedAt, options => options.MapFrom(source =>
                source.UpdatedAt.HasValue ? source.UpdatedAt.Value.UtcDateTime : (DateTime?)null));

        CreateMap<CreateCommentRequest, Comment>()
            .ForMember(destination => destination.AuthorId, options => options.Ignore())
            .ForMember(destination => destination.Author, options => options.Ignore())
            .ForMember(destination => destination.Post, options => options.Ignore())
            .ForMember(destination => destination.ParentCommentId, options => options.Ignore())
            .ForMember(destination => destination.ParentComment, options => options.Ignore())
            .ForMember(destination => destination.Replies, options => options.Ignore());

        CreateMap<UpdateCommentRequest, Comment>()
            .ForMember(destination => destination.AuthorId, options => options.Ignore())
            .ForMember(destination => destination.Author, options => options.Ignore())
            .ForMember(destination => destination.Post, options => options.Ignore())
            .ForMember(destination => destination.ParentCommentId, options => options.Ignore())
            .ForMember(destination => destination.ParentComment, options => options.Ignore())
            .ForMember(destination => destination.Replies, options => options.Ignore());
    }
}

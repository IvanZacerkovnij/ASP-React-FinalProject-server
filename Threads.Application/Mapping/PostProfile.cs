using AutoMapper;
using Threads.Application.DTOs.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Mapping;

public class PostProfile : Profile
{
    public PostProfile()
    {
        CreateMap<Post, PostResponse>()
            .ForMember(destination => destination.Content, options => options.MapFrom(source => source.Content ?? string.Empty))
            .ForMember(destination => destination.MediaUrls, options => options.MapFrom(source => source.Media
                .OrderBy(media => media.SortOrder)
                .Select(media => media.StorageKey)))
            .ForMember(destination => destination.LikesCount, options => options.MapFrom(source => source.Likes.Count))
            .ForMember(destination => destination.CommentsCount, options => options.MapFrom(source => source.Comments.Count))
            .ForMember(destination => destination.RepostsCount, options => options.MapFrom(_ => 0))
            .ForMember(destination => destination.IsLikedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.MapFrom(source => source.CreatedAt.UtcDateTime))
            .ForMember(destination => destination.UpdatedAt, options => options.MapFrom(source =>
                source.UpdatedAt.HasValue ? source.UpdatedAt.Value.UtcDateTime : (DateTime?)null));

        CreateMap<CreatePostRequest, Post>()
            .ForMember(destination => destination.AuthorId, options => options.Ignore())
            .ForMember(destination => destination.Author, options => options.Ignore())
            .ForMember(destination => destination.Media, options => options.Ignore())
            .ForMember(destination => destination.Comments, options => options.Ignore())
            .ForMember(destination => destination.Likes, options => options.Ignore());

        CreateMap<UpdatePostRequest, Post>()
            .ForMember(destination => destination.AuthorId, options => options.Ignore())
            .ForMember(destination => destination.Author, options => options.Ignore())
            .ForMember(destination => destination.Media, options => options.Ignore())
            .ForMember(destination => destination.Comments, options => options.Ignore())
            .ForMember(destination => destination.Likes, options => options.Ignore());
    }
}

using AutoMapper;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Domain.Entities;

namespace Threads.Application.Mapping;

public class PostProfile : Profile
{
    public PostProfile()
    {
        CreateMap<Post, PostResponse>()
            .ForMember(destination => destination.Content, options => options.MapFrom(source => source.Content ?? string.Empty))
            .ForMember(destination => destination.Media, options => options.Ignore())
            .ForMember(destination => destination.Poll, options => options.Ignore())
            .ForMember(destination => destination.Location, options => options.Ignore())
            .ForMember(destination => destination.Embed, options => options.Ignore())
            .ForMember(destination => destination.LikesCount, options => options.MapFrom(source => source.PostLikes.Count))
            .ForMember(destination => destination.CommentsCount, options => options.MapFrom(source => source.Comments.Count))
            .ForMember(destination => destination.RepostsCount, options => options.MapFrom(source => source.PostReposts.Count))
            .ForMember(destination => destination.BookmarksCount, options => options.MapFrom(source => source.PostBookmarks.Count))
            .ForMember(destination => destination.ViewsCount, options => options.Ignore())
            .ForMember(destination => destination.IsLikedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.IsRepostedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.IsBookmarkedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.ActionAt, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.MapFrom(source => source.CreatedAt))
            .ForMember(destination => destination.UpdatedAt, options => options.MapFrom(source => source.UpdatedAt));

        CreateMap<CreatePostRequest, Post>()
            .ForMember(destination => destination.Content, options => options.Ignore())
            .ForMember(destination => destination.LocationPlaceId, options => options.Ignore())
            .ForMember(destination => destination.LocationName, options => options.Ignore())
            .ForMember(destination => destination.LocationCountry, options => options.Ignore())
            .ForMember(destination => destination.LocationLatitude, options => options.Ignore())
            .ForMember(destination => destination.LocationLongitude, options => options.Ignore())
            .ForMember(destination => destination.EmbedUrl, options => options.Ignore())
            .ForMember(destination => destination.EmbedTitle, options => options.Ignore())
            .ForMember(destination => destination.EmbedDescription, options => options.Ignore())
            .ForMember(destination => destination.EmbedThumbnailUrl, options => options.Ignore())
            .ForMember(destination => destination.PostViews, options => options.Ignore())
            .ForMember(destination => destination.AuthorId, options => options.Ignore())
            .ForMember(destination => destination.Author, options => options.Ignore())
            .ForMember(destination => destination.Media, options => options.Ignore())
            .ForMember(destination => destination.Comments, options => options.Ignore())
            .ForMember(destination => destination.PostLikes, options => options.Ignore())
            .ForMember(destination => destination.PostReposts, options => options.Ignore())
            .ForMember(destination => destination.PostBookmarks, options => options.Ignore())
            .ForMember(destination => destination.Poll, options => options.Ignore());

        CreateMap<UpdatePostRequest, Post>()
            .ForMember(destination => destination.Content, options => options.Ignore())
            .ForMember(destination => destination.LocationPlaceId, options => options.Ignore())
            .ForMember(destination => destination.LocationName, options => options.Ignore())
            .ForMember(destination => destination.LocationCountry, options => options.Ignore())
            .ForMember(destination => destination.LocationLatitude, options => options.Ignore())
            .ForMember(destination => destination.LocationLongitude, options => options.Ignore())
            .ForMember(destination => destination.EmbedUrl, options => options.Ignore())
            .ForMember(destination => destination.EmbedTitle, options => options.Ignore())
            .ForMember(destination => destination.EmbedDescription, options => options.Ignore())
            .ForMember(destination => destination.EmbedThumbnailUrl, options => options.Ignore())
            .ForMember(destination => destination.PostViews, options => options.Ignore())
            .ForMember(destination => destination.AuthorId, options => options.Ignore())
            .ForMember(destination => destination.Author, options => options.Ignore())
            .ForMember(destination => destination.Media, options => options.Ignore())
            .ForMember(destination => destination.Comments, options => options.Ignore())
            .ForMember(destination => destination.PostLikes, options => options.Ignore())
            .ForMember(destination => destination.PostReposts, options => options.Ignore())
            .ForMember(destination => destination.PostBookmarks, options => options.Ignore())
            .ForMember(destination => destination.Poll, options => options.Ignore());
    }
}

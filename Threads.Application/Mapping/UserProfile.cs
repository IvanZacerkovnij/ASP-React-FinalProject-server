using AutoMapper;
using Threads.Application.DTOs.Users;
using Threads.Domain.Entities;

namespace Threads.Application.Mapping;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserShortResponse>()
            .ForMember(destination => destination.Location, options => options.Ignore())
            .ForMember(destination => destination.AvatarUrl, options => options.MapFrom(source => source.AvatarObjectKey));

        CreateMap<User, UserResponse>()
            .ForMember(destination => destination.Location, options => options.Ignore())
            .ForMember(destination => destination.AvatarUrl, options => options.MapFrom(source => source.AvatarObjectKey))
            .ForMember(destination => destination.BannerUrl, options => options.MapFrom(source => source.BannerObjectKey))
            .ForMember(destination => destination.FollowersCount, options => options.MapFrom(source => source.FollowerRelations.Count))
            .ForMember(destination => destination.FollowingCount, options => options.MapFrom(source => source.FollowingRelations.Count))
            .ForMember(destination => destination.PostsCount, options => options.MapFrom(source => source.Posts.Count))
            .ForMember(destination => destination.IsFollowedByCurrentUser, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.MapFrom(source => source.CreatedAt.UtcDateTime));

        CreateMap<UpdateUserRequest, User>()
            .ForMember(destination => destination.Id, options => options.Ignore())
            .ForMember(destination => destination.Username, options => options.Ignore())
            .ForMember(destination => destination.Email, options => options.Ignore())
            .ForMember(destination => destination.PasswordHash, options => options.Ignore())
            .ForMember(destination => destination.PasswordResetCode, options => options.Ignore())
            .ForMember(destination => destination.PasswordResetCodeExpiresAt, options => options.Ignore())
            .ForMember(destination => destination.PendingPasswordHash, options => options.Ignore())
            .ForMember(destination => destination.AvatarObjectKey, options => options.Ignore())
            .ForMember(destination => destination.BannerObjectKey, options => options.Ignore())
            .ForMember(destination => destination.Location, options => options.Ignore())
            .ForMember(destination => destination.LocationPlaceId, options => options.Ignore())
            .ForMember(destination => destination.LocationCountry, options => options.Ignore())
            .ForMember(destination => destination.LocationLatitude, options => options.Ignore())
            .ForMember(destination => destination.LocationLongitude, options => options.Ignore())
            .ForMember(destination => destination.IsVerified, options => options.Ignore())
            .ForMember(destination => destination.IsActive, options => options.Ignore())
            .ForMember(destination => destination.Posts, options => options.Ignore())
            .ForMember(destination => destination.Comments, options => options.Ignore())
            .ForMember(destination => destination.Likes, options => options.Ignore())
            .ForMember(destination => destination.Reposts, options => options.Ignore())
            .ForMember(destination => destination.Bookmarks, options => options.Ignore())
            .ForMember(destination => destination.PostViews, options => options.Ignore())
            .ForMember(destination => destination.PollVotes, options => options.Ignore())
            .ForMember(destination => destination.FollowingRelations, options => options.Ignore())
            .ForMember(destination => destination.FollowerRelations, options => options.Ignore())
            .ForMember(destination => destination.RefreshTokens, options => options.Ignore())
            .ForMember(destination => destination.UploadedMedia, options => options.Ignore())
            .ForMember(destination => destination.CreatedAt, options => options.Ignore())
            .ForMember(destination => destination.UpdatedAt, options => options.Ignore());
    }
}

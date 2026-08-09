using AutoMapper;
using Threads.Application.DTOs.Auth;
using Threads.Domain.Entities;

namespace Threads.Application.Mapping;

public class AuthProfile : Profile
{
    public AuthProfile()
    {
        CreateMap<RegisterRequest, User>()
            .ForMember(destination => destination.PasswordHash, options => options.Ignore())
            .ForMember(destination => destination.PasswordResetCodeHash, options => options.Ignore())
            .ForMember(destination => destination.PasswordResetCodeExpiresAt, options => options.Ignore())
            .ForMember(destination => destination.Bio, options => options.Ignore())
            .ForMember(destination => destination.AvatarObjectKey, options => options.Ignore())
            .ForMember(destination => destination.IsVerified, options => options.Ignore())
            .ForMember(destination => destination.IsActive, options => options.Ignore())
            .ForMember(destination => destination.Posts, options => options.Ignore())
            .ForMember(destination => destination.Comments, options => options.Ignore())
            .ForMember(destination => destination.Likes, options => options.Ignore())
            .ForMember(destination => destination.FollowingRelations, options => options.Ignore())
            .ForMember(destination => destination.FollowerRelations, options => options.Ignore())
            .ForMember(destination => destination.RefreshTokens, options => options.Ignore())
            .ForMember(destination => destination.UploadedMedia, options => options.Ignore());
    }
}

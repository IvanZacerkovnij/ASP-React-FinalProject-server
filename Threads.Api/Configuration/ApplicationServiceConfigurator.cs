using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Admin;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Polls;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Application.Interfaces.Recommendations;
using Threads.Application.Interfaces.ScheduledPosts;
using Threads.Application.Interfaces.Users;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Mapping;
using Threads.Application.Interfaces.Notifications;
using Threads.Application.Services.Notifications;
using Threads.Application.Recommendations;
using Threads.Application.Services.Auth;
using Threads.Application.Services.Admin;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Follows;
using Threads.Application.Services.Interactions;
using Threads.Application.Services.Media;
using Threads.Application.Services.Posts;
using Threads.Application.Services.Recommendations;
using Threads.Application.Services.ScheduledPosts;
using Threads.Application.Services.Users;

using Threads.Infrastructure.Security;

namespace Threads.Api.Configuration;

public static class ApplicationServiceConfigurator
{
    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddDataProtection();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<INotificationService, NotificationService>();
        builder.Services.AddOptions<RecommendationOptions>()
            .Bind(builder.Configuration.GetSection("Recommendations"))
            .ValidateDataAnnotations()
            .Validate(options => options.HasValidWeights(), "View weight must be weaker than explicit signals and ResultLimit must not exceed CandidateLimit.")
            .ValidateOnStart();
        builder.Services.AddScoped<IRecommendationService, RecommendationService>();
        builder.Services.AddSingleton<IRecommendationCursorProtector, RecommendationCursorProtector>();
        builder.Services.AddScoped<UserQueryService>();
        builder.Services.AddScoped<IAdministrationService, AdministrationService>();
        builder.Services.AddScoped<UserProfileService>();
        builder.Services.AddScoped<UserDeletionService>();
        builder.Services.AddScoped<UserResponseFactory>();
        builder.Services.AddScoped<ModerationResponseFactory>();
        builder.Services.AddScoped<ProfileImageManager>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<PostQueryService>();
        builder.Services.AddScoped<PostManagementService>();
        builder.Services.AddScoped<PostInteractionService>();
        builder.Services.AddScoped<PostMediaManager>();
        builder.Services.AddScoped<PostResponseFactory>();
        builder.Services.AddScoped<IPostService, PostService>();
        builder.Services.AddScoped<ScheduledPostMediaManager>();
        builder.Services.AddScoped<ScheduledPostResponseFactory>();
        builder.Services.AddScoped<IScheduledPostService, ScheduledPostService>();
        builder.Services.AddScoped<IPollService, PollService>();
        builder.Services.AddScoped<CommentQueryService>();
        builder.Services.AddScoped<CommentManagementService>();
        builder.Services.AddScoped<CommentInteractionService>();
        builder.Services.AddScoped<CommentMediaManager>();
        builder.Services.AddScoped<CommentResponseFactory>();
        builder.Services.AddScoped<CommentThreadService>();
        builder.Services.AddScoped<ICommentService, CommentService>();
        builder.Services.AddScoped<IFollowService, FollowService>();
        builder.Services.AddScoped<ILikeService, LikeService>();
        builder.Services.AddScoped<IRepostService, RepostService>();
        builder.Services.AddScoped<IBookmarkService, BookmarkService>();
        builder.Services.AddScoped<IMediaService, MediaService>();
        builder.Services.AddScoped<RegistrationService>();
        builder.Services.AddScoped<SessionService>();
        builder.Services.AddScoped<GoogleAuthService>();
        builder.Services.AddScoped<PasswordRecoveryService>();
        builder.Services.AddScoped<PasswordChangeService>();
        builder.Services.AddScoped<RefreshTokenManager>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddAutoMapper(cfg => { }, typeof(UserProfile));

        return builder;
    }
}

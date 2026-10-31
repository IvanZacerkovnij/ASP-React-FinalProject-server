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
using Threads.Application.Interfaces.Versions;
using Threads.Application.Interfaces.Notifications;
using Threads.Infrastructure.Data.Repositories.Notifications;
using Threads.Application.Services.Posts;
using Threads.Application.Services.Versions;
using Threads.Infrastructure.Data.Repositories.Bookmarks;
using Threads.Infrastructure.Data.Repositories.Admin;
using Threads.Infrastructure.Data.Repositories.Comments;
using Threads.Infrastructure.Data.Repositories.Follows;
using Threads.Infrastructure.Data.Repositories.Likes;
using Threads.Infrastructure.Data.Repositories.Media;
using Threads.Infrastructure.Data.Repositories.PendingRegistrations;
using Threads.Infrastructure.Data.Repositories.Polls;
using Threads.Infrastructure.Data.Repositories.Posts;
using Threads.Infrastructure.Data.Repositories.Recommendations;
using Threads.Infrastructure.Data.Repositories.Reposts;
using Threads.Infrastructure.Data.Repositories.ScheduledPosts;
using Threads.Infrastructure.Data.Repositories.RefreshTokens;
using Threads.Infrastructure.Data.Repositories.Users;
using Threads.Infrastructure.Data.Transactions;

using Threads.Application.Services.Comments;

namespace Threads.Api.Configuration;

public static class RepositoryConfigurator
{
    public static WebApplicationBuilder AddRepositories(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
        builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IAdministrationRepository, AdministrationRepository>();
        builder.Services.AddScoped<IPostRepository, PostRepository>();
        builder.Services.AddScoped<ICommentRepository, CommentRepository>();
        builder.Services.AddScoped<IFollowRepository, FollowRepository>();
        builder.Services.AddScoped<ILikeRepository, LikeRepository>();
        builder.Services.AddScoped<IRepostRepository, RepostRepository>();
        builder.Services.AddScoped<IBookmarkRepository, BookmarkRepository>();
        builder.Services.AddScoped<IMediaRepository, MediaRepository>();
        builder.Services.AddScoped<IPollRepository, PollRepository>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<IPendingRegistrationRepository, PendingRegistrationRepository>();
        builder.Services.AddScoped<IScheduledPostRepository, ScheduledPostRepository>();
        builder.Services.AddScoped<IAuthTransaction, AuthTransaction>();

        builder.Services.AddSingleton<IVersionSnapshotSerializer, VersionSnapshotSerializer>();
        builder.Services.AddScoped<PostVersionFactory>();
        builder.Services.AddScoped<CommentVersionFactory>();
        builder.Services.AddScoped<PostVersionResponseFactory>();
        builder.Services.AddScoped<CommentVersionResponseFactory>();
        builder.Services.AddScoped<PostVersionService>();
        builder.Services.AddScoped<CommentVersionService>();
        builder.Services.AddScoped<PostQuoteService>();

        return builder;
    }
}

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Resend;
using Threads.Api.ExceptionHandling;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Bookmarks;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Follows;
using Threads.Application.Interfaces.Gifs;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Locations;
using Threads.Application.Interfaces.Polls;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Reposts;
using Threads.Application.Interfaces.Security;
using Threads.Application.Interfaces.Users;
using Threads.Application.Interfaces.Likes;
using Threads.Application.Mapping;
using Threads.Application.Services;
using Threads.Infrastracture.Data;
using Threads.Infrastracture.Data.Configurations;
using Threads.Infrastracture.Data.Repositories.Bookmarks;
using Threads.Infrastracture.Data.Repositories.Comments;
using Threads.Infrastracture.Data.Repositories.Follows;
using Threads.Infrastracture.Data.Repositories.Likes;
using Threads.Infrastracture.Data.Repositories.Media;
using Threads.Infrastracture.Data.Repositories.PendingRegistrations;
using Threads.Infrastracture.Data.Repositories.Polls;
using Threads.Infrastracture.Data.Repositories.Posts;
using Threads.Infrastracture.Data.Repositories.Reposts;
using Threads.Infrastracture.Data.Repositories.RefreshTokens;
using Threads.Infrastracture.Data.Repositories.Users;
using Threads.Infrastracture.Security;
using Threads.Infrastracture.Services;

namespace Threads.Api;

public class Program
{
    private static void AddRepositories(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IUserRepository, UserRepository>();
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
    }

    private static void AddServices(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IPostService, PostService>();
        builder.Services.AddScoped<IPollService, PollService>();
        builder.Services.AddScoped<ICommentService, CommentService>();
        builder.Services.AddScoped<IFollowService, FollowService>();
        builder.Services.AddScoped<ILikeService, LikeService>();
        builder.Services.AddScoped<IRepostService, RepostService>();
        builder.Services.AddScoped<IBookmarkService, BookmarkService>();
        builder.Services.AddScoped<IMediaService, MediaService>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddAutoMapper(cfg => { }, typeof(UserProfile));
    }

    private static void AddInfrastructureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IMediaProcessingService, FfmpegMediaProcessingService>();
        builder.Services.AddScoped<IObjectStorageService, S3ObjectStorageService>();
        builder.Services.AddScoped<IAuthEmailService, AuthEmailService>();
        builder.Services.AddResend(options => ResendConfigurator.Configure(options, builder.Configuration));
    }

    private static void AddExternalApi(WebApplicationBuilder builder)
    {
        builder.Services.AddHttpClient<IGifSearchService, GiphyGifSearchService>(client => GifConfigurator.Configure(client, builder.Configuration));
        builder.Services.AddHttpClient<ILocationSearchService, GeoapifyLocationSearchService>(client => LocationConfigurator.Configure(client, builder.Configuration));
    }

    private static void AddCache(WebApplicationBuilder builder)
    {
        builder.Services.AddStackExchangeRedisCache(options => RedisConfigurator.Configure(options, builder.Configuration));
        builder.Services.AddHybridCache(HybridCacheConfigurator.Configure);
    }

    private static void AddSecurityServices(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
        builder.Services.AddScoped<ITokenService, JwtTokenService>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => JwtBearerConfigurator.Configure(options, builder.Configuration));

        builder.Services.AddAuthorization(AuthorizationConfigurator.Configure);

        builder.Services.AddCors(options => CORSConfigurator.Configure(options, builder.Configuration));
    }

    private static void AddUploadConfiguration(WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options => UploadConfigurator.Configure(options));
        builder.Services.Configure<FormOptions>(options => UploadConfigurator.Configure(options));
    }

    private static void AddDbConfiguration(WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<ThreadsDbContext>(options => DbConfigurator.Configure(options, builder.Configuration));
    }

    private static void AddGlobalExceptionHandler(WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    }

    private static void ConfigureApplication(WebApplication app)
    {
        app.UseExceptionHandler();
        
        app.UseCors("AllowAll");
        
        app.UseHttpsRedirection();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
    }

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        AddUploadConfiguration(builder);
        AddDbConfiguration(builder);

        AddRepositories(builder);
        AddServices(builder);
        AddInfrastructureServices(builder);
        AddSecurityServices(builder);

        AddExternalApi(builder);

        AddCache(builder);
        AddGlobalExceptionHandler(builder);

        builder.Services.AddControllers();

        var app = builder.Build();

        ConfigureApplication(app);

        app.Run();
    }
}

using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Media;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Quotes;
using Threads.Application.DTOs.Versions;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Common;
using Threads.Domain.Enums;
using Threads.Domain.Models.Versions;
using CommentVersionEntity = Threads.Domain.Entities.CommentVersion;
using MediaEntity = Threads.Domain.Entities.Media;
using PostVersionEntity = Threads.Domain.Entities.PostVersion;

namespace Threads.Application.Services.Posts;

public sealed class PostVersionService
{
    private readonly IPostRepository _postRepository;
    private readonly ICommentRepository _commentRepository;
    private readonly IMediaRepository _mediaRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly PostVersionResponseFactory _versionResponseFactory;
    private readonly CommentVersionResponseFactory _commentVersionResponseFactory;

    public PostVersionService(
        IPostRepository postRepository,
        ICommentRepository commentRepository,
        IMediaRepository mediaRepository,
        IObjectStorageService objectStorageService,
        PostVersionResponseFactory versionResponseFactory,
        CommentVersionResponseFactory commentVersionResponseFactory)
    {
        _postRepository = postRepository;
        _commentRepository = commentRepository;
        _mediaRepository = mediaRepository;
        _objectStorageService = objectStorageService;
        _versionResponseFactory = versionResponseFactory;
        _commentVersionResponseFactory = commentVersionResponseFactory;
    }

    public async Task<EditHistoryResponse<PostResponse>?> GetEditHistoryAsync(
        Guid postId,
        CursorPageRequest pagination,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var currentPosts = await _postRepository.GetSummariesByIdsAsync(
            [postId],
            currentUserId,
            cancellationToken);
        var currentPost = currentPosts.SingleOrDefault(post => post.Id == postId);

        if (currentPost is null)
        {
            return null;
        }

        var cursor = CursorCodec.Decode(pagination.Cursor);
        var versions = await _postRepository.GetVersionsAsync(
            postId,
            pagination.Limit,
            cursor,
            cancellationToken);
        var hasMore = versions.Count > pagination.Limit;
        var pageVersions = versions.Take(pagination.Limit).ToList();
        var versionSnapshots = pageVersions
            .Select(version => (
                Version: version,
                Snapshot: _versionResponseFactory.DeserializeSnapshot(version)))
            .ToList();
        var quotes = versionSnapshots
            .Select(item => item.Snapshot.Quote)
            .Where(quote => quote is not null)
            .Select(quote => quote!)
            .ToList();
        var quotedPosts = await GetQuotedPostsAsync(quotes, currentUserId, cancellationToken);
        var quotedComments = await GetQuotedCommentsAsync(quotes, currentUserId, cancellationToken);
        var quotedPostVersions = await GetQuotedPostVersionsAsync(quotes, cancellationToken);
        var quotedCommentVersions = await GetQuotedCommentVersionsAsync(quotes, cancellationToken);
        var mediaIds = versionSnapshots
            .SelectMany(item => item.Snapshot.MediaIds)
            .Concat(quotedPostVersions.Values.SelectMany(item => item.Snapshot.MediaIds))
            .Concat(quotedCommentVersions.Values.SelectMany(item => item.Snapshot.MediaIds))
            .Distinct()
            .ToArray();
        var mediaById = await GetMediaByIdAsync(mediaIds, cancellationToken);

        var responses = versionSnapshots
            .Select(item => _versionResponseFactory.Create(
                currentPost,
                item.Version,
                item.Snapshot,
                mediaById,
                ResolveQuote(
                    item.Snapshot.Quote,
                    quotedPosts,
                    quotedComments,
                    quotedPostVersions,
                    quotedCommentVersions,
                    mediaById)))
            .ToList();

        return new EditHistoryResponse<PostResponse>
        {
            TargetType = ContentTargetType.Post,
            TargetId = postId,
            Versions = responses,
            HasMore = hasMore,
            NextCursor = hasMore
                ? CursorCodec.Encode(pageVersions[^1].CreatedAt, pageVersions[^1].Id)
                : null
        };
    }

    private async Task<IReadOnlyDictionary<Guid, MediaAttachmentResponse>> GetMediaByIdAsync(
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        if (mediaIds.Count == 0)
        {
            return new Dictionary<Guid, MediaAttachmentResponse>();
        }

        var media = await _mediaRepository.GetByIdsAsync(mediaIds, cancellationToken);

        return media.ToDictionary(item => item.Id, CreateMediaResponse);
    }

    private async Task<IReadOnlyDictionary<Guid, PostSummaryReadModel>> GetQuotedPostsAsync(
        IReadOnlyCollection<QuoteVersionSnapshot> quotes,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var ids = quotes
            .Where(quote => quote.TargetType == ContentTargetType.Post)
            .Select(quote => quote.TargetId)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, PostSummaryReadModel>();
        }

        var posts = await _postRepository.GetSummariesByIdsAsync(
            ids,
            currentUserId,
            cancellationToken);

        return posts.ToDictionary(post => post.Id);
    }

    private async Task<IReadOnlyDictionary<Guid, CommentSummaryReadModel>> GetQuotedCommentsAsync(
        IReadOnlyCollection<QuoteVersionSnapshot> quotes,
        Guid? currentUserId,
        CancellationToken cancellationToken)
    {
        var ids = quotes
            .Where(quote => quote.TargetType == ContentTargetType.Comment)
            .Select(quote => quote.TargetId)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, CommentSummaryReadModel>();
        }

        var comments = await _commentRepository.GetSummariesByIdsAsync(
            ids,
            currentUserId,
            cancellationToken);

        return comments.ToDictionary(comment => comment.Id);
    }

    private QuoteResponse? ResolveQuote(
        QuoteVersionSnapshot? quote,
        IReadOnlyDictionary<Guid, PostSummaryReadModel> quotedPosts,
        IReadOnlyDictionary<Guid, CommentSummaryReadModel> quotedComments,
        IReadOnlyDictionary<Guid, (PostVersionEntity Version, PostVersionSnapshot Snapshot)> quotedPostVersions,
        IReadOnlyDictionary<Guid, (CommentVersionEntity Version, CommentVersionSnapshot Snapshot)> quotedCommentVersions,
        IReadOnlyDictionary<Guid, MediaAttachmentResponse> mediaById)
    {
        if (quote is null)
        {
            return null;
        }

        object? target = quote.TargetType switch
        {
            ContentTargetType.Post when
                quotedPosts.TryGetValue(quote.TargetId, out var post) &&
                quotedPostVersions.TryGetValue(quote.TargetVersionId, out var postVersion) =>
                CreateQuotedPost(post, postVersion, mediaById),
            ContentTargetType.Comment when
                quotedComments.TryGetValue(quote.TargetId, out var comment) &&
                quotedCommentVersions.TryGetValue(quote.TargetVersionId, out var commentVersion) =>
                _commentVersionResponseFactory.Create(
                    comment,
                    commentVersion.Version,
                    commentVersion.Snapshot,
                    mediaById),
            _ => null
        };
        var currentVersionId = quote.TargetType switch
        {
            ContentTargetType.Post when quotedPosts.TryGetValue(quote.TargetId, out var post) =>
                post.VersionId,
            ContentTargetType.Comment when quotedComments.TryGetValue(quote.TargetId, out var comment) =>
                comment.VersionId,
            _ => (Guid?)null
        };

        return new QuoteResponse
        {
            TargetType = quote.TargetType,
            TargetId = quote.TargetId,
            TargetVersionId = quote.TargetVersionId,
            HasNewVersion = currentVersionId.HasValue &&
                currentVersionId.Value != quote.TargetVersionId,
            Target = target
        };
    }

    private async Task<IReadOnlyDictionary<Guid, (PostVersionEntity Version, PostVersionSnapshot Snapshot)>>
        GetQuotedPostVersionsAsync(
            IReadOnlyCollection<QuoteVersionSnapshot> quotes,
            CancellationToken cancellationToken)
    {
        var versionIds = quotes
            .Where(quote => quote.TargetType == ContentTargetType.Post)
            .Select(quote => quote.TargetVersionId)
            .Distinct()
            .ToArray();
        var versions = await _postRepository.GetVersionsByIdsAsync(
            versionIds,
            cancellationToken);

        return versions.ToDictionary(
            version => version.Id,
            version => (version, _versionResponseFactory.DeserializeSnapshot(version)));
    }

    private async Task<IReadOnlyDictionary<Guid, (CommentVersionEntity Version, CommentVersionSnapshot Snapshot)>>
        GetQuotedCommentVersionsAsync(
            IReadOnlyCollection<QuoteVersionSnapshot> quotes,
            CancellationToken cancellationToken)
    {
        var versionIds = quotes
            .Where(quote => quote.TargetType == ContentTargetType.Comment)
            .Select(quote => quote.TargetVersionId)
            .Distinct()
            .ToArray();
        var versions = await _commentRepository.GetVersionsByIdsAsync(
            versionIds,
            cancellationToken);

        return versions.ToDictionary(
            version => version.Id,
            version => (version, _commentVersionResponseFactory.DeserializeSnapshot(version)));
    }

    private PostResponse CreateQuotedPost(
        PostSummaryReadModel currentPost,
        (PostVersionEntity Version, PostVersionSnapshot Snapshot) version,
        IReadOnlyDictionary<Guid, MediaAttachmentResponse> mediaById)
    {
        var response = _versionResponseFactory.Create(
            currentPost,
            version.Version,
            version.Snapshot,
            mediaById);
        response.Quote = null;

        return response;
    }

    private MediaAttachmentResponse CreateMediaResponse(MediaEntity media)
    {
        var url = _objectStorageService.GetReadUrl(media.StorageKey);
        var type = ResolveMediaType(media);

        return new MediaAttachmentResponse
        {
            Id = media.Id,
            Type = type,
            Url = url,
            ThumbnailUrl = !string.IsNullOrWhiteSpace(media.ThumbnailStorageKey)
                ? _objectStorageService.GetReadUrl(media.ThumbnailStorageKey)
                : type is "image" or "gif"
                    ? url
                    : null,
            Width = media.Width,
            Height = media.Height,
            Duration = media.DurationSeconds,
            MimeType = media.ContentType,
            FileName = media.FileName,
            SizeInBytes = media.SizeInBytes,
            SortOrder = media.SortOrder
        };
    }

    private static string ResolveMediaType(MediaEntity media)
    {
        if (media.ContentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
        {
            return "gif";
        }

        return media.Type == MediaType.Video
            ? "video"
            : "image";
    }
}

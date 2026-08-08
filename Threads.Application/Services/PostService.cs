using AutoMapper;
using Threads.Application.DTOs.Posts;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Services;

public class PostService : IPostService
{
    private readonly IPostRepository _postRepository;
    private readonly IMediaRepository _mediaRepository;
    private readonly IMapper _mapper;

    public PostService(
        IPostRepository postRepository,
        IMediaRepository mediaRepository,
        IMapper mapper)
    {
        _postRepository = postRepository;
        _mediaRepository = mediaRepository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var posts = await _postRepository.GetAllAsync(cancellationToken);

        return posts
            .Select(_mapper.Map<PostResponse>)
            .ToList();
    }

    public async Task<PostResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(id, cancellationToken);

        return post is null
            ? null
            : _mapper.Map<PostResponse>(post);
    }

    public async Task<PostResponse> CreateAsync(
        Guid authorId,
        CreatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        var post = _mapper.Map<Post>(request);
        post.AuthorId = authorId;

        if (request.MediaIds.Count > 0)
        {
            var media = await _mediaRepository.GetByIdsAsync(request.MediaIds, cancellationToken);
            post.Media = media.ToList();
        }

        await _postRepository.AddAsync(post, cancellationToken);

        var createdPost = await _postRepository.GetByIdAsync(post.Id, cancellationToken);

        return _mapper.Map<PostResponse>(createdPost ?? post);
    }

    public async Task<PostResponse?> UpdateAsync(Guid id, UpdatePostRequest request, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(id, cancellationToken);

        if (post is null)
        {
            return null;
        }

        post.Content = request.Content;
        post.UpdatedAt = DateTimeOffset.UtcNow;

        await _postRepository.UpdateAsync(post, cancellationToken);

        var updatedPost = await _postRepository.GetByIdAsync(post.Id, cancellationToken);

        return _mapper.Map<PostResponse>(updatedPost ?? post);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(id, cancellationToken);

        if (post is null)
        {
            return false;
        }

        await _postRepository.DeleteAsync(post, cancellationToken);

        return true;
    }
}

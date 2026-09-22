using Threads.Application.DTOs.Polls;
using Threads.Application.Interfaces.Polls;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Interactions;

public class PollService : IPollService
{
    private readonly IPollRepository _pollRepository;
    private readonly IPostRepository _postRepository;

    public PollService(IPollRepository pollRepository, IPostRepository postRepository)
    {
        _pollRepository = pollRepository;
        _postRepository = postRepository;
    }

    public async Task<PollVoteResult> VoteAsync(
        Guid userId,
        Guid postId,
        VotePollRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await _postRepository.ExistsAsync(postId, cancellationToken))
        {
            return new PollVoteResult
            {
                Status = PollVoteStatus.PostNotFound
            };
        }

        var poll = await _pollRepository.GetByPostIdAsync(postId, cancellationToken);

        if (poll is null)
        {
            return new PollVoteResult
            {
                Status = PollVoteStatus.PollNotFound
            };
        }

        if (poll.EndsAt.HasValue && poll.EndsAt.Value <= DateTimeOffset.UtcNow)
        {
            return new PollVoteResult
            {
                Status = PollVoteStatus.PollClosed,
                Poll = MapPollResponse(poll, userId)
            };
        }

        var selectedOption = poll.Options.FirstOrDefault(option => option.Id == request.OptionId);

        if (selectedOption is null)
        {
            return new PollVoteResult
            {
                Status = PollVoteStatus.InvalidOption,
                Poll = MapPollResponse(poll, userId)
            };
        }

        var existingVote = poll.Votes.FirstOrDefault(vote => vote.UserId == userId);

        if (existingVote is not null)
        {
            return new PollVoteResult
            {
                Status = PollVoteStatus.AlreadyVoted,
                Poll = MapPollResponse(poll, userId)
            };
        }

        var vote = new PollVote
        {
            PollId = poll.Id,
            PollOptionId = selectedOption.Id,
            UserId = userId
        };

        var wasAdded = await _pollRepository.TryAddVoteAsync(vote, cancellationToken);

        if (!wasAdded)
        {
            var persistedPoll = await _pollRepository.GetByPostIdAsync(postId, cancellationToken) ?? poll;

            return new PollVoteResult
            {
                Status = PollVoteStatus.AlreadyVoted,
                Poll = MapPollResponse(persistedPoll, userId)
            };
        }

        poll.Votes.Add(vote);
        selectedOption.Votes.Add(vote);

        return new PollVoteResult
        {
            Status = PollVoteStatus.Success,
            Poll = MapPollResponse(poll, userId)
        };
    }

    private static PollResponse MapPollResponse(Poll poll, Guid currentUserId)
    {
        var currentVote = poll.Votes.FirstOrDefault(vote => vote.UserId == currentUserId);

        return new PollResponse
        {
            Id = poll.Id,
            PostId = poll.PostId,
            EndsAt = poll.EndsAt,
            TotalVotes = poll.Votes.Count,
            HasVotedByCurrentUser = currentVote is not null,
            SelectedOptionId = currentVote?.PollOptionId,
            Options = poll.Options
                .OrderBy(option => option.Position)
                .Select(option => new PollOptionResponse
                {
                    Id = option.Id,
                    Text = option.Text,
                    Position = option.Position,
                    VotesCount = option.Votes.Count
                })
                .ToList()
        };
    }

}

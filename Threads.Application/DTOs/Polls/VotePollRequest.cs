using Threads.Application.DTOs.Validation;

namespace Threads.Application.DTOs.Polls;

public class VotePollRequest
{
    [NotEmptyGuid]
    public Guid OptionId { get; init; }
}

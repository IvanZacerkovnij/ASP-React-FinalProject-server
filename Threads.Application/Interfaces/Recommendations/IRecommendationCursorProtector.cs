namespace Threads.Application.Interfaces.Recommendations;

public interface IRecommendationCursorProtector
{
    string Protect(string value);
    string Unprotect(string value);
}

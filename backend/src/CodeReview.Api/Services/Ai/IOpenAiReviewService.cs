using CodeReview.Core.Models;

namespace CodeReview.Api.Services.Ai;

public interface IOpenAiReviewService
{
    Task<LlmReviewResult> ReviewAsync(PullRequestContext context, IReadOnlyList<SonarFinding> sonarFindings, CancellationToken cancellationToken = default);
}

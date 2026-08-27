using CodeReview.Core.Models;

namespace CodeReview.Api.Services.GitHub;

public interface IGitHubService
{
    /// <summary>Fetches the PR metadata, unified diff, and (if referenced) the linked issue text.</summary>
    Task<PullRequestContext> BuildPullRequestContextAsync(string owner, string repository, int pullRequestNumber);

    /// <summary>Posts the rendered review report back to the pull request as a single comment.</summary>
    Task PostReviewCommentAsync(string owner, string repository, int pullRequestNumber, string markdownComment);
}

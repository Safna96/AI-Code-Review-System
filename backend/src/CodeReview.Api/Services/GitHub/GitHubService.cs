using System.Text;
using System.Text.RegularExpressions;
using CodeReview.Core.Models;
using CodeReview.Core.Services;
using Octokit;

namespace CodeReview.Api.Services.GitHub;

public partial class GitHubService(IGitHubClient client, ILogger<GitHubService> logger) : IGitHubService
{
    // Matches "closes #12", "fixes: #34", "resolves #56" etc. per GitHub's linking keywords.
    [GeneratedRegex(@"(close[sd]?|fix(e[sd])?|resolve[sd]?)\s*:?\s*#(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LinkedIssueRegex();

    public async Task<PullRequestContext> BuildPullRequestContextAsync(string owner, string repository, int pullRequestNumber)
    {
        var pullRequest = await client.PullRequest.Get(owner, repository, pullRequestNumber);
        var files = await client.PullRequest.Files(owner, repository, pullRequestNumber);

        var diffBuilder = new StringBuilder();
        var changedPaths = new List<string>();
        foreach (var file in files)
        {
            changedPaths.Add(file.FileName);
            if (string.IsNullOrEmpty(file.Patch))
            {
                continue; // binary files or files too large for GitHub to return a patch for
            }

            diffBuilder.AppendLine($"--- a/{file.FileName}");
            diffBuilder.AppendLine($"+++ b/{file.FileName}");
            diffBuilder.AppendLine(file.Patch);
        }

        string? linkedTicket = await TryFetchLinkedIssueAsync(owner, repository, pullRequest.Body);

        return new PullRequestContext
        {
            Owner = owner,
            Repository = repository,
            PullRequestNumber = pullRequestNumber,
            Title = pullRequest.Title,
            Description = pullRequest.Body,
            HeadSha = pullRequest.Head.Sha,
            BaseSha = pullRequest.Base.Sha,
            Diff = diffBuilder.ToString(),
            LinkedTicketDescription = linkedTicket,
            ChangedFilePaths = changedPaths
        };
    }

    public async Task PostReviewCommentAsync(string owner, string repository, int pullRequestNumber, string markdownComment)
    {
        // A pull request is addressable as an "issue" for general (non-inline) comments.
        //
        // The webhook fires again on every push to an open PR ("synchronize"), so creating
        // a new comment each time would stack one review per commit. Instead, look for the
        // comment this system left previously — identified by the invisible marker that
        // GitHubCommentRenderer writes as the first line — and edit it in place.
        var existing = await TryFindPreviousReviewCommentAsync(owner, repository, pullRequestNumber);
        if (existing is not null)
        {
            await client.Issue.Comment.Update(owner, repository, existing.Id, markdownComment);
            logger.LogInformation(
                "Updated existing review comment {CommentId} on {Owner}/{Repository}#{PullRequestNumber}",
                existing.Id, owner, repository, pullRequestNumber);
            return;
        }

        await client.Issue.Comment.Create(owner, repository, pullRequestNumber, markdownComment);
    }

    private async Task<IssueComment?> TryFindPreviousReviewCommentAsync(string owner, string repository, int pullRequestNumber)
    {
        try
        {
            var comments = await client.Issue.Comment.GetAllForIssue(owner, repository, pullRequestNumber);
            return comments.LastOrDefault(c =>
                c.Body?.Contains(GitHubCommentRenderer.CommentMarker, StringComparison.Ordinal) == true);
        }
        catch (ApiException ex)
        {
            // Listing comments is only an optimisation — if it fails, fall back to posting
            // a fresh comment rather than losing the review entirely.
            logger.LogWarning(ex, "Could not list existing comments on {Owner}/{Repository}#{PullRequestNumber}; will post a new comment",
                owner, repository, pullRequestNumber);
            return null;
        }
    }

    private async Task<string?> TryFetchLinkedIssueAsync(string owner, string repository, string? pullRequestBody)
    {
        if (string.IsNullOrWhiteSpace(pullRequestBody))
        {
            return null;
        }

        var match = LinkedIssueRegex().Match(pullRequestBody);
        if (!match.Success)
        {
            return null;
        }

        var issueNumber = int.Parse(match.Groups[3].Value);
        try
        {
            var issue = await client.Issue.Get(owner, repository, issueNumber);
            return $"[#{issue.Number}] {issue.Title}\n\n{issue.Body}";
        }
        catch (NotFoundException)
        {
            logger.LogWarning("Linked issue #{IssueNumber} referenced by PR body was not found", issueNumber);
            return null;
        }
    }
}

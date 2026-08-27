using System.Text;
using System.Text.RegularExpressions;
using CodeReview.Core.Models;
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
        await client.Issue.Comment.Create(owner, repository, pullRequestNumber, markdownComment);
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

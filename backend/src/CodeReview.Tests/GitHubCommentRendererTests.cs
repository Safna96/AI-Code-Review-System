using CodeReview.Core.Models;
using CodeReview.Core.Services;

namespace CodeReview.Tests;

public class GitHubCommentRendererTests
{
    [Fact]
    public void Render_WithNoFindings_ShowsPositiveMessage()
    {
        var report = new ReviewReport
        {
            Owner = "moresand",
            Repository = "demo-repo",
            PullRequestNumber = 1,
            HeadSha = "abc",
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = "All good.",
            RequirementCoverage = [],
            Findings = []
        };

        var markdown = GitHubCommentRenderer.Render(report);

        Assert.Contains("No static-analysis or requirement issues were found", markdown);
    }

    [Fact]
    public void Render_AlwaysStartsWithTheCommentMarker()
    {
        // The marker is what lets GitHubService find and update its own previous
        // comment on a re-run, instead of posting a second one for every push.
        var report = new ReviewReport
        {
            Owner = "moresand",
            Repository = "demo-repo",
            PullRequestNumber = 1,
            HeadSha = "abc",
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = "All good.",
            RequirementCoverage = [],
            Findings = []
        };

        var markdown = GitHubCommentRenderer.Render(report);

        Assert.StartsWith(GitHubCommentRenderer.CommentMarker, markdown);
    }

    [Fact]
    public void Render_WithFindings_ProducesMarkdownTableRow()
    {
        var report = new ReviewReport
        {
            Owner = "moresand",
            Repository = "demo-repo",
            PullRequestNumber = 1,
            HeadSha = "abc",
            GeneratedAtUtc = DateTime.UtcNow,
            Summary = "Needs work.",
            RequirementCoverage = [],
            Findings =
            [
                new ReviewFinding
                {
                    Source = FindingSource.StaticAnalysis,
                    Category = FindingCategory.Security,
                    Severity = FindingSeverity.Critical,
                    Message = "SQL injection risk",
                    FilePath = "Db.cs",
                    Line = 12
                }
            ]
        };

        var markdown = GitHubCommentRenderer.Render(report);

        Assert.Contains("Db.cs:12", markdown);
        Assert.Contains("SQL injection risk", markdown);
        Assert.Contains("Critical", markdown);
    }
}

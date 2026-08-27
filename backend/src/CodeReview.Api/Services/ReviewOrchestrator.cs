using CodeReview.Api.Data;
using CodeReview.Api.Services.Ai;
using CodeReview.Api.Services.GitHub;
using CodeReview.Api.Services.SonarQube;
using CodeReview.Core.Models;
using CodeReview.Core.Services;

namespace CodeReview.Api.Services;

/// <summary>
/// Coordinates a single end-to-end review run: fetch PR context → static analysis →
/// LLM analysis → aggregate → persist → post comment. This is the class the webhook
/// controller calls into; every step is designed to degrade gracefully rather than
/// throw, so one failing dependency (e.g. SonarQube down) doesn't stop the others.
/// </summary>
public class ReviewOrchestrator(
    IGitHubService gitHubService,
    ISonarQubeService sonarQubeService,
    IOpenAiReviewService openAiReviewService,
    AppDbContext dbContext,
    ILogger<ReviewOrchestrator> logger)
{
    public async Task<ReviewReport> RunAsync(string owner, string repository, int pullRequestNumber, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting review for {Owner}/{Repository}#{PullRequestNumber}", owner, repository, pullRequestNumber);

        var context = await gitHubService.BuildPullRequestContextAsync(owner, repository, pullRequestNumber);

        var sonarFindings = await sonarQubeService.GetFindingsForPullRequestAsync(pullRequestNumber, context.ChangedFilePaths);

        var llmResult = await openAiReviewService.ReviewAsync(context, sonarFindings, cancellationToken);

        var report = ReportAggregator.Aggregate(context, sonarFindings, llmResult, DateTime.UtcNow);

        dbContext.ReviewReports.Add(ReviewReportMapper.ToEntity(report));
        await dbContext.SaveChangesAsync(cancellationToken);

        var comment = GitHubCommentRenderer.Render(report);
        await gitHubService.PostReviewCommentAsync(owner, repository, pullRequestNumber, comment);

        logger.LogInformation(
            "Completed review for {Owner}/{Repository}#{PullRequestNumber}: {Critical} critical, {Major} major, {Minor} minor findings",
            owner, repository, pullRequestNumber, report.CriticalCount, report.MajorCount, report.MinorCount);

        return report;
    }
}

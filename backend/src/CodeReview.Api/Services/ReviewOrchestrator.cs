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
    /// <param name="ticketOverride">
    /// Requirements supplied by hand, replacing whatever the pull request links to.
    /// Used by the manual run form to test how the review responds to different
    /// wordings of the same requirements. Null for the normal webhook-driven path.
    /// </param>
    /// <param name="ticketUrl">
    /// Optional link recorded alongside a manual override - e.g. the Jira ticket the
    /// requirements were copied from. Stored as provenance only; never fetched.
    /// </param>
    public async Task<ReviewReport> RunAsync(
        string owner,
        string repository,
        int pullRequestNumber,
        string? ticketOverride = null,
        string? ticketUrl = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting review for {Owner}/{Repository}#{PullRequestNumber}", owner, repository, pullRequestNumber);

        var context = await gitHubService.BuildPullRequestContextAsync(owner, repository, pullRequestNumber);

        if (!string.IsNullOrWhiteSpace(ticketOverride))
        {
            logger.LogInformation("Using a manually supplied ticket description for {Owner}/{Repository}#{PullRequestNumber}",
                owner, repository, pullRequestNumber);
            context = context with
            {
                LinkedTicketDescription = ticketOverride,
                TicketSource = TicketSource.ManualOverride,
                TicketUrl = ticketUrl
            };
        }
        else if (!string.IsNullOrWhiteSpace(ticketUrl))
        {
            context = context with { TicketUrl = ticketUrl };
        }

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

using CodeReview.Core.Models;
using CodeReview.Core.Services;

namespace CodeReview.Tests;

public class ReportAggregatorTests
{
    private static PullRequestContext SampleContext() => new()
    {
        Owner = "moresand",
        Repository = "demo-repo",
        PullRequestNumber = 42,
        Title = "Add token refresh to login endpoint",
        HeadSha = "abc123",
        BaseSha = "def456",
        Diff = "--- a/Login.cs\n+++ b/Login.cs\n@@ -1,2 +1,3 @@\n+// token refresh\n",
        LinkedTicketDescription = "Login endpoint must refresh expired tokens and reject null user IDs."
    };

    [Fact]
    public void Aggregate_MapsUncoveredRequirement_ToMajorRequirementGapFinding()
    {
        var llmResult = new LlmReviewResult
        {
            Summary = "Token refresh implemented but null user ID is not handled.",
            RequirementCoverage =
            [
                new RequirementCoverageItem { Requirement = "Refresh expired tokens", Covered = true },
                new RequirementCoverageItem { Requirement = "Reject null user IDs", Covered = false, Evidence = "No null check found in diff" }
            ]
        };

        var report = ReportAggregator.Aggregate(SampleContext(), [], llmResult, DateTime.UtcNow);

        var gap = Assert.Single(report.Findings, f => f.Category == FindingCategory.RequirementGap);
        Assert.Equal(FindingSeverity.Major, gap.Severity);
        Assert.Equal(FindingSource.Llm, gap.Source);
        Assert.Equal(1, report.UncoveredRequirementCount);
    }

    [Fact]
    public void Aggregate_MapsBlockerSonarFinding_ToCriticalSeverity()
    {
        var sonarFindings = new List<SonarFinding>
        {
            new()
            {
                RuleKey = "csharpsquid:S2259",
                Type = SonarIssueType.Bug,
                Severity = SonarSeverity.Blocker,
                Message = "Possible null reference exception",
                FilePath = "Login.cs",
                Line = 42
            }
        };
        var llmResult = new LlmReviewResult { Summary = "Looks fine." };

        var report = ReportAggregator.Aggregate(SampleContext(), sonarFindings, llmResult, DateTime.UtcNow);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(FindingSeverity.Critical, finding.Severity);
        Assert.Equal(FindingCategory.CodeQuality, finding.Category);
        Assert.Equal(1, report.CriticalCount);
    }

    [Fact]
    public void Aggregate_OrdersFindings_BySeverityDescendingThenRequirementGapsFirst()
    {
        var sonarFindings = new List<SonarFinding>
        {
            new() { RuleKey = "r1", Type = SonarIssueType.CodeSmell, Severity = SonarSeverity.Minor, Message = "Long method", FilePath = "A.cs" }
        };
        var llmResult = new LlmReviewResult
        {
            Summary = "Missing requirement",
            RequirementCoverage = [new RequirementCoverageItem { Requirement = "X", Covered = false }]
        };

        var report = ReportAggregator.Aggregate(SampleContext(), sonarFindings, llmResult, DateTime.UtcNow);

        Assert.Equal(FindingCategory.RequirementGap, report.Findings[0].Category);
        Assert.Equal(FindingSeverity.Major, report.Findings[0].Severity);
    }

    [Fact]
    public void Aggregate_WithNoFindings_ProducesEmptyReport()
    {
        var llmResult = new LlmReviewResult
        {
            Summary = "All requirements covered, no static issues.",
            RequirementCoverage = [new RequirementCoverageItem { Requirement = "X", Covered = true }]
        };

        var report = ReportAggregator.Aggregate(SampleContext(), [], llmResult, DateTime.UtcNow);

        Assert.Empty(report.Findings);
        Assert.Equal(0, report.UncoveredRequirementCount);
    }
}

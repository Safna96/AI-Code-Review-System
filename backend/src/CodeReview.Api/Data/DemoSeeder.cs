using CodeReview.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CodeReview.Api.Data;

/// <summary>
/// Inserts a couple of realistic-looking <see cref="ReviewReport"/> rows so the dashboard
/// has something to show even if live GitHub/OpenAI/SonarQube calls aren't possible during
/// a demo (bad wifi, rate limits, a service being down, etc.). This never runs unless
/// explicitly enabled — see "Demo:SeedOnStartup" in appsettings.json / the DEMO_SEED_ON_STARTUP
/// environment variable — and it only inserts data if the table is empty, so it never
/// clobbers real review history.
/// </summary>
public static class DemoSeeder
{
    public static async Task SeedIfEmptyAsync(AppDbContext db)
    {
        if (await db.ReviewReports.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;

        var reportWithIssues = new ReviewReport
        {
            Owner = "your-username",
            Repository = "code-review-sandbox",
            PullRequestNumber = 12,
            HeadSha = "a1b2c3d",
            GeneratedAtUtc = now.AddMinutes(-20),
            Summary = "The login endpoint now refreshes expired tokens as required, but the " +
                      "ticket's second requirement (reject requests with a null user ID) is not " +
                      "handled anywhere in the diff.",
            RequirementCoverage =
            [
                new RequirementCoverageItem { Requirement = "Refresh expired access tokens on login", Covered = true, Evidence = "AuthController.cs lines 40-58" },
                new RequirementCoverageItem { Requirement = "Reject requests with a null user ID", Covered = false, Evidence = "No null check found in AuthController.cs or AuthService.cs" }
            ],
            Findings =
            [
                new ReviewFinding
                {
                    Source = FindingSource.Llm,
                    Category = FindingCategory.RequirementGap,
                    Severity = FindingSeverity.Major,
                    Message = "Requirement not clearly covered: Reject requests with a null user ID",
                    Suggestion = "Add a guard clause in AuthService.RefreshToken before the token lookup"
                },
                new ReviewFinding
                {
                    Source = FindingSource.StaticAnalysis,
                    Category = FindingCategory.CodeQuality,
                    Severity = FindingSeverity.Critical,
                    Message = "Possible null reference exception",
                    FilePath = "AuthService.cs",
                    Line = 42
                },
                new ReviewFinding
                {
                    Source = FindingSource.Llm,
                    Category = FindingCategory.LogicGap,
                    Severity = FindingSeverity.Minor,
                    Message = "Token expiry check assumes UTC but DateTime.Now (local time) is used for comparison",
                    FilePath = "AuthService.cs",
                    Line = 51,
                    Suggestion = "Use DateTime.UtcNow consistently to avoid timezone-dependent bugs"
                }
            ]
        };

        var cleanReport = new ReviewReport
        {
            Owner = "your-username",
            Repository = "code-review-sandbox",
            PullRequestNumber = 9,
            HeadSha = "9f8e7d6",
            GeneratedAtUtc = now.AddDays(-1),
            Summary = "All stated requirements are covered by the diff, and no static analysis issues were found.",
            RequirementCoverage =
            [
                new RequirementCoverageItem { Requirement = "Add pagination to the /orders endpoint", Covered = true, Evidence = "OrdersController.cs lines 15-33" }
            ],
            Findings = []
        };

        db.ReviewReports.AddRange(
            ReviewReportMapper.ToEntity(reportWithIssues),
            ReviewReportMapper.ToEntity(cleanReport));

        await db.SaveChangesAsync();
    }
}

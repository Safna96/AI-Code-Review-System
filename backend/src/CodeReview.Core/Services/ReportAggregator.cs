using CodeReview.Core.Models;

namespace CodeReview.Core.Services;

/// <summary>
/// Merges SonarQube's static findings and the LLM's requirement-aware findings into a
/// single prioritised <see cref="ReviewReport"/>. Pure logic, no I/O — this is the
/// piece of the system most worth unit-testing thoroughly (see objective 6 of the
/// proposal: measuring defects detected / quality of the aggregated report).
/// </summary>
public static class ReportAggregator
{
    public static ReviewReport Aggregate(
        PullRequestContext context,
        IReadOnlyList<SonarFinding> sonarFindings,
        LlmReviewResult llmResult,
        DateTime generatedAtUtc)
    {
        var findings = new List<ReviewFinding>();

        foreach (var sf in sonarFindings)
        {
            findings.Add(new ReviewFinding
            {
                Source = FindingSource.StaticAnalysis,
                Category = sf.Type == SonarIssueType.Vulnerability || sf.Type == SonarIssueType.SecurityHotspot
                    ? FindingCategory.Security
                    : FindingCategory.CodeQuality,
                Severity = MapSonarSeverity(sf.Severity),
                Message = sf.Message,
                FilePath = sf.FilePath,
                Line = sf.Line,
                Suggestion = null
            });
        }

        foreach (var uncovered in llmResult.RequirementCoverage.Where(r => !r.Covered))
        {
            findings.Add(new ReviewFinding
            {
                Source = FindingSource.Llm,
                Category = FindingCategory.RequirementGap,
                Severity = FindingSeverity.Major,
                Message = $"Requirement not clearly covered: {uncovered.Requirement}",
                Suggestion = uncovered.Evidence
            });
        }

        foreach (var gap in llmResult.LogicGaps)
        {
            findings.Add(new ReviewFinding
            {
                Source = FindingSource.Llm,
                Category = FindingCategory.LogicGap,
                Severity = FindingSeverity.Minor,
                Message = gap.Description,
                FilePath = gap.FilePath,
                Line = gap.Line,
                Suggestion = gap.SuggestedFix
            });
        }

        // Most severe, then requirement gaps, then by file for readability.
        var prioritised = findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Category == FindingCategory.RequirementGap ? 0 : 1)
            .ThenBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ReviewReport
        {
            Owner = context.Owner,
            Repository = context.Repository,
            PullRequestNumber = context.PullRequestNumber,
            HeadSha = context.HeadSha,
            GeneratedAtUtc = generatedAtUtc,
            Summary = llmResult.Summary,
            RequirementCoverage = llmResult.RequirementCoverage,
            Findings = prioritised,
            TicketSource = context.TicketSource,
            TicketUrl = context.TicketUrl,
            ModelName = llmResult.ModelName
        };
    }

    private static FindingSeverity MapSonarSeverity(SonarSeverity severity) => severity switch
    {
        SonarSeverity.Blocker or SonarSeverity.Critical => FindingSeverity.Critical,
        SonarSeverity.Major => FindingSeverity.Major,
        _ => FindingSeverity.Minor
    };
}

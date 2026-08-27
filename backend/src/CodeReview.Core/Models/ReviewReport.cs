namespace CodeReview.Core.Models;

/// <summary>
/// The final, aggregated output of one review run: everything posted back to the
/// pull request and everything persisted for the dashboard.
/// </summary>
public class ReviewReport
{
    public required string Owner { get; init; }
    public required string Repository { get; init; }
    public required int PullRequestNumber { get; init; }
    public required string HeadSha { get; init; }
    public required DateTime GeneratedAtUtc { get; init; }
    public required string Summary { get; init; }
    public required List<RequirementCoverageItem> RequirementCoverage { get; init; }
    public required List<ReviewFinding> Findings { get; init; }

    public int CriticalCount => Findings.Count(f => f.Severity == FindingSeverity.Critical);
    public int MajorCount => Findings.Count(f => f.Severity == FindingSeverity.Major);
    public int MinorCount => Findings.Count(f => f.Severity == FindingSeverity.Minor);
    public int UncoveredRequirementCount => RequirementCoverage.Count(r => !r.Covered);
}

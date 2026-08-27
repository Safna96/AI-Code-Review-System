namespace CodeReview.Api.Data.Entities;

/// <summary>EF Core persistence model for a completed review run. Kept separate from
/// CodeReview.Core.Models.ReviewReport so the domain model stays free of ORM concerns.</summary>
public class ReviewReportEntity
{
    public int Id { get; set; }
    public required string Owner { get; set; }
    public required string Repository { get; set; }
    public required int PullRequestNumber { get; set; }
    public required string HeadSha { get; set; }
    public required DateTime GeneratedAtUtc { get; set; }
    public required string Summary { get; set; }

    /// <summary>Requirement coverage and findings stored as JSON columns (Postgres jsonb via EF Core).</summary>
    public required string RequirementCoverageJson { get; set; }
    public required string FindingsJson { get; set; }

    public int CriticalCount { get; set; }
    public int MajorCount { get; set; }
    public int MinorCount { get; set; }
}

using System.Text.Json;
using CodeReview.Api.Data.Entities;
using CodeReview.Core.Models;

namespace CodeReview.Api.Data;

public static class ReviewReportMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ReviewReportEntity ToEntity(ReviewReport report) => new()
    {
        Owner = report.Owner,
        Repository = report.Repository,
        PullRequestNumber = report.PullRequestNumber,
        HeadSha = report.HeadSha,
        GeneratedAtUtc = report.GeneratedAtUtc,
        Summary = report.Summary,
        RequirementCoverageJson = JsonSerializer.Serialize(report.RequirementCoverage, JsonOptions),
        FindingsJson = JsonSerializer.Serialize(report.Findings, JsonOptions),
        CriticalCount = report.CriticalCount,
        MajorCount = report.MajorCount,
        MinorCount = report.MinorCount
    };

    public static ReviewReport ToDomain(ReviewReportEntity entity) => new()
    {
        Owner = entity.Owner,
        Repository = entity.Repository,
        PullRequestNumber = entity.PullRequestNumber,
        HeadSha = entity.HeadSha,
        GeneratedAtUtc = entity.GeneratedAtUtc,
        Summary = entity.Summary,
        RequirementCoverage = JsonSerializer.Deserialize<List<RequirementCoverageItem>>(entity.RequirementCoverageJson, JsonOptions) ?? new(),
        Findings = JsonSerializer.Deserialize<List<ReviewFinding>>(entity.FindingsJson, JsonOptions) ?? new()
    };
}

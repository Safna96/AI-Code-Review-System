using System.Globalization;
using System.Text;
using CodeReview.Api.Data;
using CodeReview.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeReview.Api.Controllers;

/// <summary>Read-only API consumed by the React dashboard (Section 5, Step 8 of the design).</summary>
[ApiController]
[Route("api/reviews")]
public class ReviewsController(
    AppDbContext dbContext,
    ReviewOrchestrator orchestrator,
    ILogger<ReviewsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] string? owner, [FromQuery] string? repository, [FromQuery] int take = 50)
    {
        var query = dbContext.ReviewReports.AsNoTracking().OrderByDescending(r => r.GeneratedAtUtc).AsQueryable();

        if (!string.IsNullOrWhiteSpace(owner))
        {
            query = query.Where(r => r.Owner == owner);
        }

        if (!string.IsNullOrWhiteSpace(repository))
        {
            query = query.Where(r => r.Repository == repository);
        }

        var entities = await query.Take(Math.Clamp(take, 1, 200)).ToListAsync();
        var reports = entities.Select(ReviewReportMapper.ToDomain);
        return Ok(reports);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var entity = await dbContext.ReviewReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        return entity is null ? NotFound() : Ok(ReviewReportMapper.ToDomain(entity));
    }

    /// <summary>
    /// Runs a review on demand, without waiting for GitHub to deliver a webhook.
    /// This is the endpoint to use when building the evaluation set (Objective 4-6):
    /// it lets the same pull request be re-reviewed as many times as needed, and
    /// removes ngrok from the loop entirely.
    /// </summary>
    /// <remarks>
    /// Unlike the webhook endpoint this one runs the review synchronously and returns
    /// the finished report, so expect it to take 10-30 seconds (mostly the OpenAI call).
    /// </remarks>
    [HttpPost("run")]
    public async Task<IActionResult> RunNow([FromBody] RunReviewRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Owner) || string.IsNullOrWhiteSpace(request.Repository) || request.PullRequestNumber <= 0)
        {
            return BadRequest(new { message = "owner, repository and a positive pullRequestNumber are all required." });
        }

        try
        {
            var report = await orchestrator.RunAsync(request.Owner, request.Repository, request.PullRequestNumber, cancellationToken);
            return Ok(report);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Manual review run failed for {Owner}/{Repository}#{PullRequestNumber}",
                request.Owner, request.Repository, request.PullRequestNumber);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Exports every stored review as CSV, one row per finding, for the evaluation
    /// spreadsheet described in objective 6 of the proposal (AI findings vs. the
    /// human baseline). Reviews that produced no findings still get one row, so a
    /// clean pull request is not silently missing from the data set.
    /// </summary>
    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv()
    {
        var entities = await dbContext.ReviewReports.AsNoTracking()
            .OrderBy(r => r.GeneratedAtUtc)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("ReviewId,GeneratedAtUtc,Owner,Repository,PullRequestNumber,HeadSha," +
                       "RequirementsTotal,RequirementsCovered,CriticalCount,MajorCount,MinorCount," +
                       "FindingSource,FindingCategory,FindingSeverity,FilePath,Line,Message,Suggestion");

        foreach (var entity in entities)
        {
            var report = ReviewReportMapper.ToDomain(entity);
            var prefix = string.Join(',', new[]
            {
                entity.Id.ToString(CultureInfo.InvariantCulture),
                report.GeneratedAtUtc.ToString("o", CultureInfo.InvariantCulture),
                Csv(report.Owner),
                Csv(report.Repository),
                report.PullRequestNumber.ToString(CultureInfo.InvariantCulture),
                Csv(report.HeadSha),
                report.RequirementCoverage.Count.ToString(CultureInfo.InvariantCulture),
                report.RequirementCoverage.Count(c => c.Covered).ToString(CultureInfo.InvariantCulture),
                report.CriticalCount.ToString(CultureInfo.InvariantCulture),
                report.MajorCount.ToString(CultureInfo.InvariantCulture),
                report.MinorCount.ToString(CultureInfo.InvariantCulture)
            });

            if (report.Findings.Count == 0)
            {
                csv.AppendLine($"{prefix},,,,,,,");
                continue;
            }

            foreach (var f in report.Findings)
            {
                csv.AppendLine(string.Join(',', new[]
                {
                    prefix,
                    f.Source.ToString(),
                    f.Category.ToString(),
                    f.Severity.ToString(),
                    Csv(f.FilePath),
                    f.Line?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    Csv(f.Message),
                    Csv(f.Suggestion)
                }));
            }
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "ai-code-review-export.csv");
    }

    /// <summary>Quotes a value for CSV: wrap in quotes and double any embedded quote.</summary>
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"")
                            .Replace('\r', ' ')
                            .Replace('\n', ' ');
        return "\"" + escaped + "\"";
    }
}

/// <summary>Body of a manual <c>POST /api/reviews/run</c> request.</summary>
public record RunReviewRequest(string Owner, string Repository, int PullRequestNumber);

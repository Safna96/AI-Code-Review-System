using CodeReview.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CodeReview.Api.Controllers;

/// <summary>Read-only API consumed by the React dashboard (Section 5, Step 8 of the design).</summary>
[ApiController]
[Route("api/reviews")]
public class ReviewsController(AppDbContext dbContext) : ControllerBase
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
}

namespace CodeReview.Core.Models;

/// <summary>
/// The structured output requested from GPT-4o for a single pull request review.
/// This shape is what the LLM is instructed (via the prompt's JSON schema) to return,
/// so that the response can be parsed deterministically rather than free-text.
/// </summary>
public class LlmReviewResult
{
    /// <summary>Short plain-language summary of whether the change fulfils the ticket.</summary>
    public required string Summary { get; init; }

    /// <summary>One entry per requirement the LLM could identify in the ticket text.</summary>
    public List<RequirementCoverageItem> RequirementCoverage { get; init; } = new();

    /// <summary>Logic gaps / edge cases the LLM believes are not handled by the diff.</summary>
    public List<LogicGap> LogicGaps { get; init; } = new();

    /// <summary>Overall confidence the model has in its own assessment, 0.0-1.0.</summary>
    public double Confidence { get; init; }
}

public class RequirementCoverageItem
{
    public required string Requirement { get; init; }
    public required bool Covered { get; init; }
    public string? Evidence { get; init; }
}

public class LogicGap
{
    public required string Description { get; init; }
    public string? FilePath { get; init; }
    public int? Line { get; init; }
    public string? SuggestedFix { get; init; }
}

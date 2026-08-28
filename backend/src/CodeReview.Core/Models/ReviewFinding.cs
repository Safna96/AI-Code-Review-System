namespace CodeReview.Core.Models;

public enum FindingSource
{
    StaticAnalysis,
    Llm
}

public enum FindingCategory
{
    RequirementGap,
    LogicGap,
    CodeQuality,
    Security
}

public enum FindingSeverity
{
    Minor,
    Major,
    Critical
}

/// <summary>
/// A single, unified review finding produced after merging SonarQube's static
/// analysis output with the LLM's requirement-aware reasoning. This is the type
/// that both the GitHub comment renderer and the dashboard operate on.
/// </summary>
public class ReviewFinding
{
    public required FindingSource Source { get; init; }
    public required FindingCategory Category { get; init; }
    public required FindingSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string? FilePath { get; init; }
    public int? Line { get; init; }
    public string? Suggestion { get; init; }
}

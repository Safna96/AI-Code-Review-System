namespace CodeReview.Core.Models;

public enum SonarSeverity
{
    Info,
    Minor,
    Major,
    Critical,
    Blocker
}

public enum SonarIssueType
{
    Bug,
    Vulnerability,
    CodeSmell,
    SecurityHotspot
}

/// <summary>A single issue reported by SonarQube's static analysis for the changed code.</summary>
public class SonarFinding
{
    public required string RuleKey { get; init; }
    public required SonarIssueType Type { get; init; }
    public required SonarSeverity Severity { get; init; }
    public required string Message { get; init; }
    public required string FilePath { get; init; }
    public int? Line { get; init; }
}

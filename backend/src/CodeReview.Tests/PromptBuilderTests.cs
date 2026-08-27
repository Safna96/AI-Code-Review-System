using CodeReview.Core.Models;
using CodeReview.Core.Services;

namespace CodeReview.Tests;

public class PromptBuilderTests
{
    private static PullRequestContext SampleContext(string? ticket = "Ticket text goes here.") => new()
    {
        Owner = "moresand",
        Repository = "demo-repo",
        PullRequestNumber = 1,
        Title = "Sample PR",
        HeadSha = "abc",
        BaseSha = "def",
        Diff = "diff --git a/File.cs b/File.cs\n+added line",
        LinkedTicketDescription = ticket
    };

    [Fact]
    public void BuildUserPrompt_IncludesTicketDiffAndSonarFindings()
    {
        var findings = new List<SonarFinding>
        {
            new() { RuleKey = "S1", Type = SonarIssueType.Bug, Severity = SonarSeverity.Major, Message = "Null check missing", FilePath = "File.cs", Line = 10 }
        };

        var prompt = PromptBuilder.BuildUserPrompt(SampleContext(), findings);

        Assert.Contains("Ticket text goes here.", prompt);
        Assert.Contains("added line", prompt);
        Assert.Contains("Null check missing", prompt);
        Assert.Contains("File.cs:10", prompt);
    }

    [Fact]
    public void BuildUserPrompt_WithNoTicket_ExplicitlyStatesTicketIsMissing()
    {
        var prompt = PromptBuilder.BuildUserPrompt(SampleContext(ticket: null), []);

        Assert.Contains("No linked ticket description was found", prompt);
    }

    [Fact]
    public void SystemPrompt_InstructsModelToRespondWithJsonOnly()
    {
        Assert.Contains("JSON object", PromptBuilder.SystemPrompt);
        Assert.Contains("requirementCoverage", PromptBuilder.SystemPrompt);
    }
}

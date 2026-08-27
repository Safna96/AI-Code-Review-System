using System.Text;
using CodeReview.Core.Models;

namespace CodeReview.Core.Services;

/// <summary>
/// Builds the structured prompt sent to GPT-4o. Kept as pure string-building logic
/// (no HTTP/SDK dependency) so it can be unit tested without calling OpenAI.
/// </summary>
public static class PromptBuilder
{
    public const string SystemPrompt =
        """
        You are a meticulous senior software engineer performing a code review.
        You are given: (1) a ticket/issue description stating what the change should do,
        (2) the unified diff of a pull request, and (3) a list of static-analysis findings
        already reported by SonarQube for the same diff.

        Your job is NOT to repeat what SonarQube already found. Instead:
        1. Decide whether the diff actually satisfies every requirement stated in the ticket.
        2. Identify logic gaps or unhandled edge cases that a careful human reviewer would flag,
           which SonarQube's structural rules would not catch.
        3. Be concise, specific, and cite file paths and line numbers from the diff whenever possible.
        4. If the ticket description is missing or empty, say so explicitly in "summary" and leave
           requirementCoverage empty rather than inventing requirements.
        5. Do not hallucinate code that is not present in the diff.

        Respond with ONLY a single JSON object matching exactly this shape (no markdown fences):
        {
          "summary": "string",
          "confidence": 0.0,
          "requirementCoverage": [
            { "requirement": "string", "covered": true, "evidence": "string|null" }
          ],
          "logicGaps": [
            { "description": "string", "filePath": "string|null", "line": 0, "suggestedFix": "string|null" }
          ]
        }
        """;

    public static string BuildUserPrompt(
        PullRequestContext context,
        IReadOnlyList<SonarFinding> sonarFindings)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## Ticket / Issue Description");
        sb.AppendLine(string.IsNullOrWhiteSpace(context.LinkedTicketDescription)
            ? "(No linked ticket description was found for this pull request.)"
            : context.LinkedTicketDescription);
        sb.AppendLine();

        sb.AppendLine("## Pull Request");
        sb.AppendLine($"Title: {context.Title}");
        if (!string.IsNullOrWhiteSpace(context.Description))
        {
            sb.AppendLine("Description:");
            sb.AppendLine(context.Description);
        }
        sb.AppendLine();

        sb.AppendLine("## Code Diff");
        sb.AppendLine("```diff");
        sb.AppendLine(context.Diff);
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Existing Static Analysis Findings (SonarQube)");
        if (sonarFindings.Count == 0)
        {
            sb.AppendLine("(No static analysis findings were reported.)");
        }
        else
        {
            foreach (var finding in sonarFindings)
            {
                sb.AppendLine(
                    $"- [{finding.Severity}] {finding.Type} in {finding.FilePath}" +
                    (finding.Line is not null ? $":{finding.Line}" : string.Empty) +
                    $" — {finding.Message} ({finding.RuleKey})");
            }
        }

        return sb.ToString();
    }
}

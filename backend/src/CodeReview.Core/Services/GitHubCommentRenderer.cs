using System.Text;
using CodeReview.Core.Models;

namespace CodeReview.Core.Services;

/// <summary>
/// Renders a <see cref="ReviewReport"/> as Markdown suitable for posting back to a
/// GitHub pull request as a single review comment. Kept separate from the GitHub
/// API client so the formatting logic can be unit tested without mocking Octokit.
/// </summary>
public static class GitHubCommentRenderer
{
    public static string Render(ReviewReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("### 🤖 AI-Augmented Code Review");
        sb.AppendLine();
        sb.AppendLine("> This is an automated first-pass review. It does **not** replace a human " +
                       "approval and does not block merging. Treat every item below as a starting " +
                       "point for investigation, not a verdict.");
        sb.AppendLine();
        sb.AppendLine($"**Summary:** {report.Summary}");
        sb.AppendLine();

        if (report.RequirementCoverage.Count > 0)
        {
            sb.AppendLine("#### Requirement Coverage");
            foreach (var item in report.RequirementCoverage)
            {
                sb.AppendLine($"- {(item.Covered ? "✅" : "⚠️")} {item.Requirement}" +
                              (string.IsNullOrWhiteSpace(item.Evidence) ? string.Empty : $" — {item.Evidence}"));
            }
            sb.AppendLine();
        }

        if (report.Findings.Count == 0)
        {
            sb.AppendLine("No static-analysis or requirement issues were found. 🎉");
            return sb.ToString();
        }

        sb.AppendLine("#### Findings");
        sb.AppendLine("| Severity | Category | Location | Message | Suggestion |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var f in report.Findings)
        {
            var location = f.FilePath is null
                ? "-"
                : f.Line is null ? f.FilePath : $"{f.FilePath}:{f.Line}";
            sb.AppendLine($"| {f.Severity} | {f.Category} | {location} | {Escape(f.Message)} | {Escape(f.Suggestion ?? "-")} |");
        }

        return sb.ToString();
    }

    private static string Escape(string text) => text.Replace("|", "\\|").Replace("\n", " ");
}

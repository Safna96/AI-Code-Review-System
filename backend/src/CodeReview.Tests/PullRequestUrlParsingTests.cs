using CodeReview.Api.Controllers;

namespace CodeReview.Tests;

/// <summary>
/// The manual run form sends whatever URL was pasted, so the parser has to cope with
/// the shapes a browser address bar actually produces - trailing tabs like /files,
/// query strings, and the scheme being absent when copied from a link.
/// </summary>
public class PullRequestUrlParsingTests
{
    [Theory]
    [InlineData("https://github.com/Safna96/code-review-sandbox/pull/2")]
    [InlineData("http://github.com/Safna96/code-review-sandbox/pull/2")]
    [InlineData("github.com/Safna96/code-review-sandbox/pull/2")]
    [InlineData("https://github.com/Safna96/code-review-sandbox/pull/2/files")]
    [InlineData("https://github.com/Safna96/code-review-sandbox/pull/2#issuecomment-1")]
    [InlineData("  https://github.com/Safna96/code-review-sandbox/pull/2  ")]
    public void ParsesTheShapesAPastedUrlActuallyTakes(string url)
    {
        Assert.True(ReviewsController.TryParsePullRequestUrl(url, out var owner, out var repo, out var number));
        Assert.Equal("Safna96", owner);
        Assert.Equal("code-review-sandbox", repo);
        Assert.Equal(2, number);
    }

    [Theory]
    [InlineData("https://github.com/Safna96/code-review-sandbox")]          // repo, not a PR
    [InlineData("https://github.com/Safna96/code-review-sandbox/issues/1")] // an issue
    [InlineData("https://yourorg.atlassian.net/browse/PROJ-123")]           // a Jira ticket
    [InlineData("not a url at all")]
    [InlineData("")]
    public void RejectsAnythingThatIsNotAPullRequest(string url)
    {
        Assert.False(ReviewsController.TryParsePullRequestUrl(url, out _, out _, out _));
    }
}

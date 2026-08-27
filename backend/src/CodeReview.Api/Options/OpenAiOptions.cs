namespace CodeReview.Api.Options;

public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public required string ApiKey { get; set; }

    public string Model { get; set; } = "gpt-4o";
}

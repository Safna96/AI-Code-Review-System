namespace CodeReview.Api.Options;

public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public required string ApiKey { get; set; }

    public string Model { get; set; } = "gpt-4o";

    /// <summary>
    /// Optional override for the API endpoint. Leave null/empty to use OpenAI itself.
    /// Set it to any OpenAI-compatible endpoint (Groq, Google Gemini's compatibility
    /// layer, OpenRouter, a local Ollama) to run the pipeline without an OpenAI
    /// account -- the request/response shape is identical, only the host changes.
    /// </summary>
    public string? BaseUrl { get; set; }
}

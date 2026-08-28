using System.Text.Json.Serialization;
using CodeReview.Api.Data;
using CodeReview.Api.Options;
using CodeReview.Api.Services;
using CodeReview.Api.Services.Ai;
using CodeReview.Api.Services.GitHub;
using CodeReview.Api.Services.SonarQube;
using Microsoft.EntityFrameworkCore;
using Octokit;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration (see appsettings.json / environment variables / .env) ----
builder.Services.AddOptions<GitHubOptions>()
    .Bind(builder.Configuration.GetSection(GitHubOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<OpenAiOptions>()
    .Bind(builder.Configuration.GetSection(OpenAiOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<SonarQubeOptions>()
    .Bind(builder.Configuration.GetSection(SonarQubeOptions.SectionName))
    .ValidateDataAnnotations();

// ---- Database (Section 4/6 of the proposal: PostgreSQL via EF Core) ----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// ---- GitHub client (Octokit.NET) ----
builder.Services.AddSingleton<IGitHubClient>(sp =>
{
    var ghOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GitHubOptions>>().Value;
    // Trim the token: a trailing newline picked up when the value was pasted or
    // piped into configuration makes Octokit reject the Authorization header with
    // an opaque "format of value is invalid" error that points nowhere near the cause.
    var client = new GitHubClient(new ProductHeaderValue("ai-augmented-code-review"))
    {
        Credentials = new Credentials(ghOptions.AccessToken?.Trim())
    };
    return client;
});
builder.Services.AddScoped<IGitHubService, GitHubService>();

// ---- SonarQube client ----
builder.Services.AddHttpClient<ISonarQubeService, SonarQubeService>((sp, http) =>
{
    var sonarOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SonarQubeOptions>>().Value;
    http.BaseAddress = new Uri(sonarOptions.BaseUrl.TrimEnd('/') + "/");
    var basicAuth = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{sonarOptions.ApiToken}:"));
    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);
});

// ---- OpenAI (GPT-4o) client ----
builder.Services.AddScoped<IOpenAiReviewService, OpenAiReviewService>();

// ---- Orchestration ----
builder.Services.AddScoped<ReviewOrchestrator>();

// Serialise enums as their names, not their integer values. The dashboard's
// types.ts declares them as string unions ("Minor" | "Major" | "Critical"), and
// the default integer form makes severity.toLowerCase() throw in ReviewList.tsx.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- CORS for the React dashboard (Section 5, Step 8) during local development ----
const string DashboardCorsPolicy = "DashboardCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(DashboardCorsPolicy, policy =>
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Create the database schema directly from the current model on startup — no
// `dotnet ef migrations` step required, which suits this prototype where the
// schema isn't expected to change often. (A production deployment, or a
// dissertation appendix that wants proper migration history, would swap this
// for `db.Database.Migrate()` plus a generated migration — see
// IMPLEMENTATION_GUIDE.md's "Optional: proper EF Core migrations" note.)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Demo safety net (see DEMO_SCRIPT.md): populates the dashboard with sample
    // data so a live demo still has something to show if a live GitHub/OpenAI/
    // SonarQube call fails. Off by default; enable with Demo__SeedOnStartup=true.
    if (builder.Configuration.GetValue<bool>("Demo:SeedOnStartup"))
    {
        await DemoSeeder.SeedIfEmptyAsync(db);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(DashboardCorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();

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
    var client = new GitHubClient(new ProductHeaderValue("ai-augmented-code-review"))
    {
        Credentials = new Credentials(ghOptions.AccessToken)
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

builder.Services.AddControllers();
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

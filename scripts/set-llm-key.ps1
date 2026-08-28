<#
.SYNOPSIS
  Stores the LLM API key (and matching endpoint/model) in dotnet user-secrets for
  CodeReview.Api, prompting for the key rather than taking it as an argument.

.DESCRIPTION
  Prompting avoids the two failure modes that bite when copying commands from a
  guide: pasting the placeholder text literally, and the key ending up in shell
  history. Run it from the repository root:

    powershell -ExecutionPolicy Bypass -File .\scripts\set-llm-key.ps1

.NOTES
  Model names and free-tier availability change. gemini-2.0-flash and
  gemini-2.5-flash-lite have both already been retired; if a model is rejected
  with 404, list the current ones from the provider's console and pass -Model.
#>
param(
    [ValidateSet('openai', 'gemini', 'groq', 'ollama')]
    [string]$Provider,
    [string]$Model
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\backend\src\CodeReview.Api'

function Fail($message) {
    Write-Host ""
    Write-Host "  $message" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

if (-not $Provider) {
    Write-Host ""
    Write-Host "Which provider?" -ForegroundColor Cyan
    Write-Host "  1. openai  - paid, ~1.3 cents/review, matches the proposal's design"
    Write-Host "  2. gemini  - free tier, key from https://aistudio.google.com/apikey"
    Write-Host "  3. groq    - free tier, key from https://console.groq.com/keys"
    Write-Host "  4. ollama  - fully local, no key needed"
    Write-Host ""
    $choice = Read-Host "Enter 1-4"
    $Provider = @{ '1' = 'openai'; '2' = 'gemini'; '3' = 'groq'; '4' = 'ollama' }[$choice]
    if (-not $Provider) { Fail "Invalid choice '$choice'." }
}

$config = @{
    openai = @{ BaseUrl = '';                                                          Model = 'gpt-4o' }
    gemini = @{ BaseUrl = 'https://generativelanguage.googleapis.com/v1beta/openai/';  Model = 'gemini-3.5-flash' }
    groq   = @{ BaseUrl = 'https://api.groq.com/openai/v1';                            Model = 'llama-3.3-70b-versatile' }
    ollama = @{ BaseUrl = 'http://localhost:11434/v1';                                 Model = 'qwen2.5-coder:7b' }
}[$Provider]

if ($Provider -eq 'ollama') {
    $plain = 'ollama'   # Ollama ignores the key, but the SDK requires a non-empty one.
} else {
    Write-Host ""
    Write-Host "  Paste your $Provider API key below, then press Enter." -ForegroundColor Cyan
    Write-Host "  The key IS shown as you paste - that is deliberate. A masked prompt" -ForegroundColor DarkGray
    Write-Host "  silently swallows Ctrl+V in the Windows console and stores junk." -ForegroundColor DarkGray
    Write-Host "  If Ctrl+V does nothing, use right-click to paste instead." -ForegroundColor DarkGray
    Write-Host ""
    $plain = Read-Host "  Key"
}

# Strip anything the console may have injected: control characters left by a failed
# Ctrl+V, surrounding whitespace, or quotes pasted along with the key. \p{C} is the
# Unicode "other/control" category, which avoids hex escapes in this source file.
$plain = ($plain -replace '\p{C}', '').Trim().Trim('"').Trim("'")

if ([string]::IsNullOrWhiteSpace($plain)) { Fail "No key entered." }
if ($plain -match '^(YOUR_|PASTE_|<)')    { Fail "That is placeholder text, not a real key. Copy the actual key from the provider's console." }
if ($plain.Length -lt 20) {
    Fail ("That key is only $($plain.Length) character(s) long, which is too short to be real." + [Environment]::NewLine + [Environment]::NewLine +
          "  The paste probably did not register. Try again and use RIGHT-CLICK to paste," + [Environment]::NewLine +
          "  or Ctrl+Shift+V if you are in Windows Terminal.")
}

if (-not $Model) {
    Write-Host ""
    $Model = Read-Host "  Model [$($config.Model)] (press Enter to accept)"
}
if ([string]::IsNullOrWhiteSpace($Model)) { $Model = $config.Model }

dotnet user-secrets set "OpenAI:ApiKey" $plain --project $project | Out-Null
dotnet user-secrets set "OpenAI:Model"  $Model --project $project | Out-Null

if ([string]::IsNullOrWhiteSpace($config.BaseUrl)) {
    # Clearing the override is what sends requests back to OpenAI itself.
    dotnet user-secrets remove "OpenAI:BaseUrl" --project $project 2>&1 | Out-Null
} else {
    dotnet user-secrets set "OpenAI:BaseUrl" $config.BaseUrl --project $project | Out-Null
}

$endpoint = if ($config.BaseUrl) { $config.BaseUrl } else { 'https://api.openai.com/v1 (default)' }
Write-Host ""
Write-Host "Stored: provider=$Provider model=$Model endpoint=$endpoint" -ForegroundColor Green
Write-Host "Verify with: powershell -ExecutionPolicy Bypass -File .\scripts\test-llm-key.ps1"

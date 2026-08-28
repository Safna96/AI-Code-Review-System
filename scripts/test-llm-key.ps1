<#
.SYNOPSIS
  Sends one tiny chat completion using whatever is currently in user-secrets, to
  confirm the key/endpoint/model combination actually works before running a real
  review. Costs a fraction of a cent on a paid provider, nothing on a free tier.
#>
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\backend\src\CodeReview.Api'

$id = ([xml](Get-Content (Join-Path $project 'CodeReview.Api.csproj'))).Project.PropertyGroup.UserSecretsId | Where-Object { $_ }
$path = Join-Path $env:APPDATA "Microsoft\UserSecrets\$id\secrets.json"
function Fail($message) {
    Write-Host ""
    Write-Host "  $message" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

if (-not (Test-Path $path)) { Fail "No secrets stored yet. Run this first:`n`n     powershell -ExecutionPolicy Bypass -File .\scripts\set-llm-key.ps1" }

$s = Get-Content $path -Raw | ConvertFrom-Json
$key = $s.'OpenAI:ApiKey'
$model = $s.'OpenAI:Model'
$base = $s.'OpenAI:BaseUrl'
if ([string]::IsNullOrWhiteSpace($base)) { $base = 'https://api.openai.com/v1' }

if ([string]::IsNullOrWhiteSpace($key)) { Fail "OpenAI:ApiKey is not set. Run this first:`n`n     powershell -ExecutionPolicy Bypass -File .\scripts\set-llm-key.ps1" }
if ($key -match '^(YOUR_|PASTE_|<)') {
    Fail ("The stored key is still placeholder text: '$key'`n`n" +
          "  Nothing is broken - you just have not set a real key yet.`n`n" +
          "  1. Get a free key at https://aistudio.google.com/apikey`n" +
          "  2. Run: powershell -ExecutionPolicy Bypass -File .\scripts\set-llm-key.ps1`n" +
          "  3. Then run this script again")
}

Write-Host "Endpoint: $base"
Write-Host "Model:    $model"
Write-Host ""

# Ask for JSON specifically: the review pipeline depends on JSON mode working on
# this provider, so a plain-text success here would be a misleading pass.
$body = @{
    model = $model
    messages = @(
        @{ role = 'system'; content = 'Respond with only a JSON object.' },
        @{ role = 'user';   content = 'Return {"status":"ok"}' }
    )
    response_format = @{ type = 'json_object' }
    max_tokens = 20
} | ConvertTo-Json -Depth 6

# Free-tier endpoints return 503 under load often enough that a single attempt is
# not a fair test of the credentials - mirror the retry the app itself now does.
for ($attempt = 1; $attempt -le 5; $attempt++) {
    try {
        $r = Invoke-RestMethod -Uri "$($base.TrimEnd('/'))/chat/completions" -Method Post `
             -Headers @{ Authorization = "Bearer $key" } -ContentType 'application/json' -Body $body -ErrorAction Stop
        Write-Host "LLM OK - JSON mode works. Replied: $($r.choices[0].message.content)" -ForegroundColor Green
        if ($attempt -gt 1) { Write-Host "(succeeded on attempt $attempt - transient errors are normal on a free tier)" -ForegroundColor DarkGray }
        exit 0
    } catch {
        $status = $null
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($status -in 408,429,500,502,503,504 -and $attempt -lt 5) {
            Write-Host "  attempt $attempt : transient $status, retrying..." -ForegroundColor DarkGray
            Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
            continue
        }
        Write-Host "LLM FAILED: $($_.Exception.Message)" -ForegroundColor Red
        if ($_.ErrorDetails.Message) {
            Write-Host $_.ErrorDetails.Message.Substring(0, [Math]::Min(500, $_.ErrorDetails.Message.Length))
        }
        exit 1
    }
}

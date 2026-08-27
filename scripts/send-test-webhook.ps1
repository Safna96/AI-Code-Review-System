<#
.SYNOPSIS
  Triggers a review run by posting a signed pull_request webhook payload straight to
  your locally-running backend, without needing GitHub to actually deliver it (i.e.
  without ngrok in the loop). The backend still makes real calls to GitHub, SonarQube,
  and OpenAI for the PR number you give it — this script only replaces the "GitHub ->
  ngrok -> your machine" delivery hop, which is the flakiest part of a live demo.

.EXAMPLE
  $env:GITHUB_WEBHOOK_SECRET = "your-secret-from-.env"
  ./scripts/send-test-webhook.ps1 -Owner your-username -Repo code-review-sandbox -Number 12
#>
param(
    [Parameter(Mandatory = $true)][string]$Owner,
    [Parameter(Mandatory = $true)][string]$Repo,
    [Parameter(Mandatory = $true)][int]$Number,
    [string]$Action = "opened",
    [string]$ApiUrl = "http://localhost:8080/api/webhook/github",
    [string]$Secret = $env:GITHUB_WEBHOOK_SECRET
)

if ([string]::IsNullOrWhiteSpace($Secret)) {
    Write-Error "No webhook secret found. Set `$env:GITHUB_WEBHOOK_SECRET (same value as GitHub:WebhookSecret / GITHUB_WEBHOOK_SECRET in .env) or pass -Secret."
    exit 1
}

# Minimal shape of a real GitHub 'pull_request' webhook event — WebhookController only
# reads action/number/repository.owner.login/repository.name; everything else (diff,
# ticket text, etc.) is fetched fresh from the GitHub API using these three values.
$payload = '{"action":"' + $Action + '","number":' + $Number + ',"repository":{"name":"' + $Repo + '","owner":{"login":"' + $Owner + '"}}}'

$hmac = New-Object System.Security.Cryptography.HMACSHA256
$hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($Secret)
$hashBytes = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($payload))
$signature = "sha256=" + (($hashBytes | ForEach-Object { $_.ToString("x2") }) -join "")

Write-Host "POST $ApiUrl  (PR $Owner/$Repo#$Number, action=$Action)"

$response = Invoke-RestMethod -Uri $ApiUrl -Method Post -Body $payload -ContentType "application/json" -Headers @{
    "X-GitHub-Event"       = "pull_request"
    "X-Hub-Signature-256"  = $signature
}

$response | ConvertTo-Json
Write-Host "`nQueued. Watch the API console for 'Starting review for ...' and check GET $($ApiUrl -replace '/webhook/github$','/reviews') shortly after."

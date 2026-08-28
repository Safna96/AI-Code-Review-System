<#
.SYNOPSIS
  Stores the GitHub personal access token in dotnet user-secrets, after checking it
  actually works.

.DESCRIPTION
  Prompts for the token rather than taking it as an argument, so a placeholder from a
  copied command can never be stored by mistake, and the token does not end up in shell
  history. The token is validated against the GitHub API and the sandbox repository's
  permissions are checked before anything is written, so a bad token fails here rather
  than surfacing later as "Bad credentials" in the dashboard.

    powershell -ExecutionPolicy Bypass -File .\scripts\set-github-token.ps1

.PARAMETER Repository
  owner/name of the repository to verify access against. Defaults to the sandbox.
#>
param(
    [string]$Repository = 'Safna96/code-review-sandbox'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\backend\src\CodeReview.Api'

function Fail($message) {
    Write-Host ""
    Write-Host "  $message" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "  Create or copy a fine-grained token at:" -ForegroundColor Cyan
Write-Host "    https://github.com/settings/personal-access-tokens"
Write-Host ""
Write-Host "  It needs, on $Repository :" -ForegroundColor Cyan
Write-Host "    Contents:      Read and write"
Write-Host "    Issues:        Read and write"
Write-Host "    Pull requests: Read and write"
Write-Host ""
Write-Host "  Paste it below and press Enter. The token IS shown as you paste - a masked" -ForegroundColor DarkGray
Write-Host "  prompt swallows Ctrl+V in the Windows console. Right-click also pastes." -ForegroundColor DarkGray
Write-Host ""

$token = Read-Host "  Token"

# Strip whatever the console may have added: control characters from a failed paste,
# surrounding whitespace, or quotes pasted along with the value. A trailing newline
# here is what makes Octokit reject the Authorization header outright.
$token = ($token -replace '\p{C}', '').Trim().Trim('"').Trim("'")

if ([string]::IsNullOrWhiteSpace($token)) { Fail "No token entered." }
if ($token -match '^(NEW_|YOUR_|PASTE_|<)')  { Fail "That is placeholder text, not a real token. Copy the actual value from GitHub." }
if ($token.Length -lt 20) { Fail "That token is only $($token.Length) characters, which is too short to be real. Try again, using right-click to paste." }

$headers = @{ Authorization = "Bearer $token"; 'User-Agent' = 'ai-augmented-code-review'; Accept = 'application/vnd.github+json' }

Write-Host ""
Write-Host "  Checking the token against GitHub..." -ForegroundColor DarkGray
try {
    $user = Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers $headers -ErrorAction Stop
} catch {
    Fail "GitHub rejected the token (HTTP $([int]$_.Exception.Response.StatusCode)). Nothing was stored."
}
Write-Host "  Authenticated as $($user.login)" -ForegroundColor Green

try {
    $repo = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository" -Headers $headers -ErrorAction Stop
    Write-Host "  Can see $($repo.full_name)" -ForegroundColor Green
} catch {
    Fail "The token authenticates, but cannot see $Repository (HTTP $([int]$_.Exception.Response.StatusCode)). Add that repository under the token's Repository access. Nothing was stored."
}

# Probing with a deliberately invalid body distinguishes "no permission" (403) from
# "permission fine, body rejected" (422) without creating anything.
function Test-Write($name, $url, $method, $body) {
    try {
        Invoke-RestMethod -Uri $url -Method $method -Headers $headers -ContentType 'application/json' -Body $body -ErrorAction Stop | Out-Null
        return $true
    } catch {
        $code = [int]$_.Exception.Response.StatusCode
        if ($code -eq 422) { return $true }
        Write-Host "  $name is NOT writable (HTTP $code)" -ForegroundColor Yellow
        return $false
    }
}

$api = "https://api.github.com/repos/$Repository"
$ok  = Test-Write 'Pull requests' "$api/pulls"  'Post' '{}'
$ok  = (Test-Write 'Issues       ' "$api/issues" 'Post' '{}') -and $ok
$ok  = (Test-Write 'Contents     ' "$api/contents/probe.txt" 'Put' '{"message":"","content":""}') -and $ok

if (-not $ok) {
    Write-Host ""
    Write-Host "  Storing anyway - reviews will still run, but posting the review comment" -ForegroundColor Yellow
    Write-Host "  back to the pull request needs Pull requests: Read and write." -ForegroundColor Yellow
} else {
    Write-Host "  Contents, Issues and Pull requests are all writable" -ForegroundColor Green
}

dotnet user-secrets set "GitHub:AccessToken" $token --project $project | Out-Null

Write-Host ""
Write-Host "Stored. Restart the API for it to take effect." -ForegroundColor Green
Write-Host "  dotnet run --project backend/src/CodeReview.Api --launch-profile https"

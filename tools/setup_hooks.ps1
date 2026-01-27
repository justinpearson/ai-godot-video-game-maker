# Install git hooks for the project
# Usage: pwsh ./tools/setup_hooks.ps1
#
# This installs:
#   - pre-push: Runs smoke tests before each push

param(
    [switch]$Uninstall
)

$hooksDir = ".git/hooks"
$sourceDir = "tools/hooks"

if (-not (Test-Path $hooksDir)) {
    Write-Host "ERROR: Not in a git repository (no .git/hooks directory)" -ForegroundColor Red
    exit 1
}

$hooks = @("pre-push")

if ($Uninstall) {
    foreach ($hook in $hooks) {
        $target = Join-Path $hooksDir $hook
        if (Test-Path $target) {
            Remove-Item $target
            Write-Host "Removed: $hook" -ForegroundColor Yellow
        }
    }
    Write-Host "Git hooks uninstalled." -ForegroundColor Green
} else {
    foreach ($hook in $hooks) {
        $source = Join-Path $sourceDir $hook
        $target = Join-Path $hooksDir $hook

        if (-not (Test-Path $source)) {
            Write-Host "WARNING: Hook source not found: $source" -ForegroundColor Yellow
            continue
        }

        Copy-Item $source $target -Force
        Write-Host "Installed: $hook" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Git hooks installed successfully!" -ForegroundColor Green
    Write-Host "Smoke tests will now run before each push." -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To bypass (use sparingly): git push --no-verify" -ForegroundColor DarkGray
}

# Run GUT (Godot Unit Test) tests with timeout protection
# Usage: pwsh ./tools/test.ps1 [-TimeoutSeconds N] [-Select "pattern"] [-Test "res://path"]
# Default: runs all tests in res://test with 60 second timeout
#
# Exit codes:
#   0   = All tests passed
#   1   = One or more test failures
#   124 = Timeout (process killed)
#
# Examples:
#   pwsh ./tools/test.ps1                              # Run all tests
#   pwsh ./tools/test.ps1 -Select "player"             # Run tests with "player" in filename
#   pwsh ./tools/test.ps1 -Test "res://test/unit/test_inventory.gd"  # Run specific test file

param(
    [int]$TimeoutSeconds = 60,
    [string]$Select = "",
    [string]$Test = ""
)

$ErrorActionPreference = "Stop"

Write-Host "Running GUT tests (timeout: ${TimeoutSeconds}s)" -ForegroundColor Cyan

# Resolve Godot path (same logic as godot.ps1)
$envCandidate = $env:GODOT4_MONO_EXE
$version = if ($env:GODOT_VERSION) { $env:GODOT_VERSION } else { "4.6" }
$standardPath = "C:\Projects\Godot\Godot_v${version}-stable_mono_win64\Godot_v${version}-stable_mono_win64.exe"

if ($envCandidate -and (Test-Path -LiteralPath $envCandidate)) { $godot = $envCandidate }
elseif (Test-Path -LiteralPath $standardPath) { $godot = $standardPath }
else { throw "Godot executable not found. Set GODOT4_MONO_EXE or install to '$standardPath'." }

# Build argument list for GUT CLI
$gutArgs = @("-s", "res://addons/gut/gut_cmdln.gd", "-gexit")

# Add selection filter if provided
if ($Select) {
    $gutArgs += @("-gselect=$Select")
    Write-Host "  Filtering: $Select" -ForegroundColor DarkGray
}

# Add specific test file if provided
if ($Test) {
    $gutArgs += @("-gtest=$Test")
    Write-Host "  Test file: $Test" -ForegroundColor DarkGray
}

# Use Start-Process with file redirection to capture output
$stdoutFile = [System.IO.Path]::GetTempFileName()
$stderrFile = [System.IO.Path]::GetTempFileName()

$process = Start-Process -FilePath $godot `
    -ArgumentList $gutArgs `
    -PassThru -NoNewWindow `
    -RedirectStandardOutput $stdoutFile `
    -RedirectStandardError $stderrFile

try {
    $process | Wait-Process -Timeout $TimeoutSeconds -ErrorAction Stop
    # Show output after process completes
    $stdout = Get-Content $stdoutFile -ErrorAction SilentlyContinue
    $stderr = Get-Content $stderrFile -ErrorAction SilentlyContinue
    if ($stdout) { $stdout | ForEach-Object { Write-Host $_ } }
    if ($stderr) { $stderr | ForEach-Object { Write-Host $_ -ForegroundColor Yellow } }
    Remove-Item $stdoutFile, $stderrFile -ErrorAction SilentlyContinue

    # GUT exit codes: 0 = pass, 1 = failures
    exit $process.ExitCode
} catch [System.TimeoutException] {
    Write-Host "TIMEOUT: Tests exceeded ${TimeoutSeconds}s limit, killing process..." -ForegroundColor Red
    Get-Content $stdoutFile -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
    Get-Content $stderrFile -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
    Remove-Item $stdoutFile, $stderrFile -ErrorAction SilentlyContinue
    taskkill /T /F /PID $process.Id 2>$null | Out-Null
    exit 124
} catch {
    if ($_.Exception.Message -match "timed out") {
        Write-Host "TIMEOUT: Tests exceeded ${TimeoutSeconds}s limit, killing process..." -ForegroundColor Red
        Get-Content $stdoutFile -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ }
        Get-Content $stderrFile -ErrorAction SilentlyContinue | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
        Remove-Item $stdoutFile, $stderrFile -ErrorAction SilentlyContinue
        taskkill /T /F /PID $process.Id 2>$null | Out-Null
        exit 124
    }
    throw
}

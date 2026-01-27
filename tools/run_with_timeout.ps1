param(
    [int]$TimeoutSeconds = 8,
    [string]$Scene = "res://scenes/TestSandbox.tscn"
)

$godotExe = "C:\Projects\Godot\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe"
$projectPath = "C:\Projects\Godot\mansion-bespoke"
$outFile = Join-Path $projectPath "godot_output.log"

# Remove old log
Remove-Item $outFile -ErrorAction SilentlyContinue

$errFile = Join-Path $projectPath "godot_error.log"
Remove-Item $errFile -ErrorAction SilentlyContinue

$process = Start-Process -FilePath $godotExe -ArgumentList "--path", $projectPath, $Scene -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile -WindowStyle Hidden

try {
    $process | Wait-Process -Timeout $TimeoutSeconds -ErrorAction Stop
    Write-Host "Process completed within timeout."
} catch {
    Write-Host "Timeout reached, killing process..."
    taskkill /T /F /PID $process.Id 2>$null | Out-Null
}

# Output the logs
if (Test-Path $outFile) {
    Get-Content $outFile
}
if (Test-Path $errFile) {
    Get-Content $errFile
}

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [int]$DebounceMs = 400
)

$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $PSScriptRoot
$csproj = Get-ChildItem -LiteralPath $projectDir -Filter *.csproj -File | Select-Object -First 1
if (-not $csproj) {
    throw "Tidak ada file .csproj di $projectDir"
}

$watchedExtensions = @(".cs", ".xaml", ".csproj", ".manifest", ".resx", ".ico", ".png")
$ignoredDirectories = @("bin", "obj", ".git", ".vs", ".idea", "node_modules", "TestResults")

$script:lastGoodExe = $null
$script:watchers = @()

function Write-Step {
    param([string]$Message)
    Write-Host "[nusa] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[nusa] $Message" -ForegroundColor Green
}

function Write-Note {
    param([string]$Message)
    Write-Host "[nusa] $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([string]$Message)
    Write-Host "[nusa] $Message" -ForegroundColor Red
}

function Get-AppExePath {
    $target = & dotnet msbuild $csproj.FullName -nologo -getProperty:TargetPath -p:Configuration=$Configuration 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $target) {
        return $null
    }

    $targetPath = ([string]$target).Trim()
    $exe = [IO.Path]::ChangeExtension($targetPath, ".exe")
    if (Test-Path -LiteralPath $exe) {
        return $exe
    }

    if (Test-Path -LiteralPath $targetPath) {
        return $targetPath
    }

    return $null
}

function Stop-App {
    $processes = @(Get-Process -Name NusaLite -ErrorAction SilentlyContinue)
    if ($processes.Count -eq 0) {
        return
    }

    Write-Step "Menghentikan NusaLite (PID: $($processes.Id -join ', '))..."
    foreach ($process in $processes) {
        try {
            if ($process.MainWindowHandle -ne 0) {
                [void]$process.CloseMainWindow()
            }
        }
        catch {
        }
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(3)
    while ([DateTime]::UtcNow -lt $deadline) {
        $alive = @(Get-Process -Name NusaLite -ErrorAction SilentlyContinue)
        if ($alive.Count -eq 0) {
            return
        }
        Start-Sleep -Milliseconds 100
    }

    foreach ($process in @(Get-Process -Name NusaLite -ErrorAction SilentlyContinue)) {
        try {
            $process.Kill()
        }
        catch {
        }
    }

    Start-Sleep -Milliseconds 200
}

function Invoke-AppBuild {
    $arguments = @("build", $csproj.FullName, "-c", $Configuration, "-nologo", "-v", "minimal")

    $raw = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $lines = @($raw | ForEach-Object { "$_" })

    if ($exitCode -ne 0 -and ($lines -join "`n") -match "being used by another process|locked by") {
        Start-Sleep -Milliseconds 800
        $raw = & dotnet @arguments 2>&1
        $exitCode = $LASTEXITCODE
        $lines = @($raw | ForEach-Object { "$_" })
    }

    if ($exitCode -eq 0) {
        Write-Ok "Build berhasil."
        return $true
    }

    Write-Fail "Build gagal."
    $errors = @($lines | Where-Object { $_ -match ":\s*error\s+[A-Za-z]+\d+" } | Select-Object -Unique)
    if ($errors.Count -eq 0) {
        $errors = @($lines | Where-Object { $_ -match "error" } | Select-Object -Unique)
    }

    if ($errors.Count -gt 0) {
        $errors | Select-Object -First 8 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkRed }
        if ($errors.Count -gt 8) {
            Write-Host "    ... ($($errors.Count - 8) error lainnya)" -ForegroundColor DarkRed
        }
    }
    else {
        $lines | Select-Object -Last 8 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkRed }
    }

    return $false
}

function Restart-App {
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    Stop-App

    if (Invoke-AppBuild) {
        $exe = Get-AppExePath
        if ($exe) {
            Start-Process -FilePath $exe -WorkingDirectory (Split-Path -Parent $exe) | Out-Null
            $script:lastGoodExe = $exe
            Write-Ok "NusaLite dijalankan ulang."
        }
        else {
            Write-Fail "Executable tidak ditemukan setelah build."
        }
    }
    elseif ($script:lastGoodExe -and (Test-Path -LiteralPath $script:lastGoodExe)) {
        Start-Process -FilePath $script:lastGoodExe -WorkingDirectory (Split-Path -Parent $script:lastGoodExe) | Out-Null
        Write-Note "Build gagal; menjalankan build terakhir yang berhasil. Watcher akan mencoba lagi saat ada perubahan."
    }
    else {
        Write-Note "Build gagal; aplikasi berhenti. Perbaiki error, watcher akan mencoba lagi saat ada perubahan."
    }

    Write-Step ("Siklus restart selesai dalam {0:N1} detik." -f $stopwatch.Elapsed.TotalSeconds)
}

function New-AppWatchers {
    foreach ($existing in $script:watchers) {
        try {
            $existing.Dispose()
        }
        catch {
        }
    }
    $script:watchers = @()

    $directories = @($projectDir)
    Get-ChildItem -LiteralPath $projectDir -Directory -ErrorAction SilentlyContinue |
        Where-Object { $ignoredDirectories -notcontains $_.Name } |
        ForEach-Object { $directories += $_.FullName }

    foreach ($directory in $directories) {
        $watcher = New-Object System.IO.FileSystemWatcher
        $watcher.Path = $directory
        $watcher.IncludeSubdirectories = ($directory -ne $projectDir)
        $watcher.Filter = "*.*"
        $watcher.NotifyFilter = [IO.NotifyFilters]::FileName -bor [IO.NotifyFilters]::LastWrite -bor [IO.NotifyFilters]::CreationTime -bor [IO.NotifyFilters]::DirectoryName
        $watcher.InternalBufferSize = 65536
        $script:watchers += $watcher
    }

    Write-Step "Memantau $($script:watchers.Count) folder proyek."
}

function Test-IgnoredChange {
    param([string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $false
    }

    $normalized = $RelativePath.Replace("/", "\")
    foreach ($segment in $ignoredDirectories) {
        if ($normalized -match "(^|\\)$([regex]::Escape($segment))(\\|$)") {
            return $true
        }
    }

    $extension = [IO.Path]::GetExtension($normalized).ToLowerInvariant()
    return -not ($watchedExtensions -contains $extension)
}

function Wait-ForAppChange {
    while ($true) {
        foreach ($watcher in $script:watchers) {
            try {
                $change = $watcher.WaitForChanged([IO.WatcherChangeTypes]::All, 80)
            }
            catch {
                New-AppWatchers
                return "<overflow>"
            }

            if (-not $change.TimedOut -and -not (Test-IgnoredChange $change.Name)) {
                return $change.Name
            }
        }
    }
}

function Clear-PendingChanges {
    foreach ($watcher in $script:watchers) {
        while ($true) {
            try {
                $change = $watcher.WaitForChanged([IO.WatcherChangeTypes]::All, 10)
            }
            catch {
                break
            }

            if ($change.TimedOut) {
                break
            }
        }
    }
}

Write-Host ""
Write-Host " NusaLite dev watcher" -ForegroundColor Magenta
Write-Step "Proyek : $($csproj.FullName)"
Write-Step "Config : $Configuration"
Write-Step "Debounce: ${DebounceMs}ms"
Write-Host ""

New-AppWatchers
Restart-App

try {
    while ($true) {
        $changed = Wait-ForAppChange
        Write-Note "Perubahan terdeteksi: $changed"
        Start-Sleep -Milliseconds $DebounceMs
        Clear-PendingChanges
        Restart-App
    }
}
finally {
    foreach ($watcher in $script:watchers) {
        try {
            $watcher.Dispose()
        }
        catch {
        }
    }
    Write-Step "Watcher berhenti. Aplikasi tetap berjalan."
}

param(
    [string]$ProfileDir = "$env:APPDATA\Thunderstore Mod Manager\DataFolder\REPO\profiles\REPOMentalMods-Test",
    [string]$PlayerLog = "$env:USERPROFILE\AppData\LocalLow\semiwork\Repo\Player.log",
    [string]$ConfigName = 'com.mentalize.repoperformance.cfg',
    [string]$ProcessName = 'REPO',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\logs'),
    [int]$LaunchTimeoutSec = 3600,
    [int]$MaxSessionSec = 21600
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$stamp = Get-Date -Format 'yyyy-MM-ddTHH-mm-ss'
$capture = Join-Path $OutDir "repo-session-$stamp.log"
$stopFile = Join-Path $OutDir 'STOP'
if (Test-Path $stopFile) { Remove-Item $stopFile -Confirm:$false }

$bepLog = Join-Path $ProfileDir 'BepInEx\LogOutput.log'
$cfgPath = Join-Path $ProfileDir "BepInEx\config\$ConfigName"

$flagBep = '^\[(Error|Fatal|Warning)\s*:|Exception|Missing(Method|Field|Member)|TypeLoad|Could not load|Skipping \[|incompatib|REPOPerformance|previous message repeated'
$flagUnity = 'Exception|Crash!!!|^Error|out of memory|REPOPerformance'

$script:t0 = $null
$script:counts = @{ Error = 0; Fatal = 0; Warning = 0; Exception = 0; Kit = 0; Config = 0 }
$writer = New-Object System.IO.StreamWriter($capture, $false, (New-Object System.Text.UTF8Encoding($false)))
$writer.AutoFlush = $true

function Write-Event([string]$src, [string]$line, [bool]$flag) {
    $rel = if ($script:t0) { '{0,9:F3}' -f ((Get-Date) - $script:t0).TotalSeconds } else { '    -.---' }
    $mark = if ($flag) { '!' } else { ' ' }
    $out = "$(Get-Date -Format 'HH:mm:ss.fff') +${rel}s $mark $src | $line"
    $writer.WriteLine($out)
    if ($flag) { [Console]::Out.WriteLine($out) }
}

function New-Tail([string]$path) {
    $len = if (Test-Path $path) { (Get-Item $path).Length } else { 0 }
    @{ Path = $path; Pos = [long]$len; Buf = ''; Stale = $true; Dec = (New-Object System.Text.UTF8Encoding($false)).GetDecoder() }
}

function Read-Tail($tail) {
    if (-not (Test-Path $tail.Path)) { return @() }
    if ($tail.Stale) {
        if ((Get-Item $tail.Path).LastWriteTime -lt $script:t0) { return @() }
        $tail.Stale = $false; $tail.Pos = 0; $tail.Buf = ''
    }
    $fs = $null
    try {
        $share = [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
        $fs = [System.IO.File]::Open($tail.Path, 'Open', 'Read', $share)
    } catch { return @() }
    try {
        if ($fs.Length -lt $tail.Pos) { $tail.Pos = 0; $tail.Buf = '' }
        if ($fs.Length -eq $tail.Pos) { return @() }
        [void]$fs.Seek($tail.Pos, 'Begin')
        $bytes = New-Object byte[] ($fs.Length - $tail.Pos)
        $n = $fs.Read($bytes, 0, $bytes.Length)
        $tail.Pos += $n
        $chars = New-Object char[] ($tail.Dec.GetCharCount($bytes, 0, $n))
        $c = $tail.Dec.GetChars($bytes, 0, $n, $chars, 0)
        $text = $tail.Buf + (New-Object string ($chars, 0, $c))
        $parts = $text -split "`r?`n"
        $tail.Buf = $parts[-1]
        if ($parts.Length -le 1) { return @() }
        return $parts[0..($parts.Length - 2)]
    } finally { $fs.Dispose() }
}

function Read-Config([string]$path) {
    $map = [ordered]@{}
    if (-not (Test-Path $path)) { return $null }
    $section = ''
    # Share ReadWrite+Delete: BepInEx rewrites this file once per Bind() and a read lock makes the plugin's Awake throw.
    try {
        $share = [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
        $fs = [System.IO.File]::Open($path, 'Open', 'Read', $share)
        try { $text = (New-Object System.IO.StreamReader($fs)).ReadToEnd() } finally { $fs.Dispose() }
    } catch { return $null }
    foreach ($l in ($text -split "`r?`n")) {
        if ($l -match '^\[(.+)\]\s*$') { $section = $Matches[1]; continue }
        if ($l -match '^\s*#' -or $l -notmatch '=') { continue }
        $k, $v = $l -split '=', 2
        $map["[$section] $($k.Trim())"] = $v.Trim()
    }
    return $map
}

function Compare-Config($old, $new) {
    if ($null -eq $new) { return }
    if ($null -eq $old) {
        Write-Event 'CFG  ' "config file present with $($new.Count) settings" $true
        foreach ($k in $new.Keys) { Write-Event 'CFG  ' "  $k = $($new[$k])" $false }
        return
    }
    foreach ($k in $new.Keys) {
        if (-not $old.Contains($k)) { Write-Event 'CFG  ' "ADDED   $k = $($new[$k])" $true; $script:counts.Config++ }
        elseif ($old[$k] -ne $new[$k]) { Write-Event 'CFG  ' "CHANGED $k : $($old[$k]) -> $($new[$k])" $true; $script:counts.Config++ }
    }
}

function Step-Bep($tail) {
    foreach ($line in (Read-Tail $tail)) {
        $flag = $line -match $flagBep
        if ($line -match '^\[(Error|Fatal|Warning)\s*:') { $script:counts[$Matches[1]]++ }
        if ($line -match 'Exception') { $script:counts.Exception++ }
        if ($line -match 'REPOPerformance') { $script:counts.Kit++ }
        Write-Event 'BEP  ' $line $flag
    }
}

function Step-Unity($tail) {
    foreach ($line in (Read-Tail $tail)) {
        Write-Event 'UNITY' $line ($line -match $flagUnity)
    }
}

try {
    $bep = New-Tail $bepLog
    $unity = New-Tail $PlayerLog
    $cfg = Read-Config $cfgPath
    Write-Event 'WATCH' "capture file: $capture" $true
    Write-Event 'WATCH' "watching $bepLog" $true
    if ($null -eq $cfg) { Write-Event 'WATCH' "config not created yet: $cfgPath" $true } else { Compare-Config $null $cfg }
    Write-Event 'WATCH' "waiting for $ProcessName.exe ..." $true

    $waitStart = Get-Date
    $alreadyRunning = @(Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    if ($alreadyRunning.Count) { Write-Event 'WATCH' "ignoring already-running pid(s) $($alreadyRunning -join ','); waiting for a fresh launch" $true }
    $proc = $null
    while (-not $proc) {
        $proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Where-Object { $alreadyRunning -notcontains $_.Id } | Select-Object -First 1
        if ($proc) { break }
        if (Test-Path $stopFile) { Write-Event 'WATCH' 'stop file seen before launch' $true; return }
        if (((Get-Date) - $waitStart).TotalSeconds -gt $LaunchTimeoutSec) { Write-Event 'WATCH' 'timed out waiting for launch' $true; return }
        Start-Sleep -Milliseconds 200
    }
    $script:t0 = try { $proc.StartTime } catch { Get-Date }
    Write-Event 'WATCH' "$ProcessName.exe started (pid $($proc.Id))" $true

    $lastCfgWrite = if (Test-Path $cfgPath) { (Get-Item $cfgPath).LastWriteTimeUtc } else { [datetime]::MinValue }
    $running = $true
    while ($running) {
        Step-Bep $bep
        Step-Unity $unity
        if (Test-Path $cfgPath) {
            $w = (Get-Item $cfgPath).LastWriteTimeUtc
            if ($w -ne $lastCfgWrite -and ((Get-Date).ToUniversalTime() - $w).TotalSeconds -ge 2) {
                $lastCfgWrite = $w
                $newCfg = Read-Config $cfgPath
                Compare-Config $cfg $newCfg
                if ($null -ne $newCfg) { $cfg = $newCfg }
            }
        }
        if ($proc.HasExited) { $running = $false; Write-Event 'WATCH' "$ProcessName.exe exited (code $($proc.ExitCode))" $true }
        elseif (Test-Path $stopFile) { $running = $false; Write-Event 'WATCH' 'stop file seen' $true }
        elseif (((Get-Date) - $script:t0).TotalSeconds -gt $MaxSessionSec) { $running = $false; Write-Event 'WATCH' 'max session length reached' $true }
        else { Start-Sleep -Milliseconds 250 }
    }
    Start-Sleep -Seconds 3
    Step-Bep $bep
    Step-Unity $unity
    $c = $script:counts
    Write-Event 'WATCH' "SUMMARY errors=$($c.Error) fatals=$($c.Fatal) warnings=$($c.Warning) exceptionLines=$($c.Exception) kitLines=$($c.Kit) configChanges=$($c.Config)" $true
} finally {
    $writer.Dispose()
}

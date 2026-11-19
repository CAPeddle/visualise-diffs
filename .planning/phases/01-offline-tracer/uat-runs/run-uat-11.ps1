<#
Phase 01 UAT test 11, option (a): truth 40 of 01-VERIFICATION.md, end to end, on Windows.

  "A protected verdict by handle ends the run with reason bad-arguments before vdiff starts;
   any other hold failure skips the log silently."

Run it once under each host, from the repository root:

  pwsh       -NoProfile -ExecutionPolicy Bypass -File .planning/phases/01-offline-tracer/uat-runs/run-uat-11.ps1
  powershell -NoProfile -ExecutionPolicy Bypass -File .planning/phases/01-offline-tracer/uat-runs/run-uat-11.ps1

Checks (each prints PASS or FAIL with its evidence; the last line is the overall verdict):

  C1  The verdict by handle, direct. scripts/trace-run.ps1's functions are defined in this host
      (as the drive_root driver in TraceRunScriptTests does) and Open-HeldDirectory is called on a
      path below a junction into a protected directory. The text of that path is not under the
      protected directory; only the handle's final path is. Expected: it throws
      held-directory-protected and creates nothing inside the protected directory. Control: a
      path outside is held.
  C2  The Invoke-Trace mapping, end to end. A subst drive letter is mapped to the analysed
      repository and -LogPath points through it. subst is an accepted AR-04 residual: the
      script's text checks do not see through it (shown here by calling Test-SameOrUnder), so
      only the handle check can refuse it. Expected: six lines with result fail and reason
      bad-arguments, nothing in -Out (vdiff never started), nothing in the repository.
  C3  Any other hold failure skips the log. A child host defines a type named
      VdiffTrace.HoldNative before it runs the script, so the script's Add-Type of the two
      native declarations fails. Expected: the run passes, the five outputs are written, no log
      and no log directory exist. Control: the same run without the pre-defined type writes the log.

Needs git, a built tests/Vdiff.Tests/bin/Debug/net10.0/vdiff.dll (dotnet build
tests/Vdiff.Tests/Vdiff.Tests.csproj) and the release sidecar under extractors/cpp-ts. It creates
everything in a temporary directory and removes it, and removes its subst mapping, at the end.
#>
param(
    [string]$VdiffDll
)

$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'run-uat-11.ps1 is Windows-only (truth 40 is the Windows branch)' }

$repoRoot = (& git rev-parse --show-toplevel).Trim()
$scriptPath = Join-Path $repoRoot 'scripts\trace-run.ps1'
if ([string]::IsNullOrWhiteSpace($VdiffDll)) { $VdiffDll = Join-Path $repoRoot 'tests\Vdiff.Tests\bin\Debug\net10.0\vdiff.dll' }
if (-not (Test-Path -LiteralPath $VdiffDll -PathType Leaf)) { throw "vdiff.dll not found at $VdiffDll; build tests/Vdiff.Tests first" }
$hostExe = (Get-Process -Id $PID).Path
$hostName = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh ' + $PSVersionTable.PSVersion } else { 'powershell ' + $PSVersionTable.PSVersion }

$results = New-Object System.Collections.Generic.List[string]
$failed = 0
function Report([string]$Check, [bool]$Ok, [string]$Evidence) {
    $verdict = if ($Ok) { 'PASS' } else { 'FAIL' }
    if (-not $Ok) { $script:failed++ }
    $line = "$verdict $Check : $Evidence"
    $script:results.Add($line)
    Write-Output $line
}

function Invoke-Host([string[]]$Arguments) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $hostExe
    $quoted = foreach ($a in $Arguments) { if ($a -match '[\s"]') { '"' + ($a -replace '"', '\"') + '"' } else { $a } }
    $psi.Arguments = ($quoted -join ' ')
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $stdout = $p.StandardOutput.ReadToEnd()
    $stderr = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    return New-Object PSObject -Property @{ ExitCode = $p.ExitCode; Stdout = $stdout; Stderr = $stderr }
}

function Get-Fields([string]$Stdout) {
    $map = @{}
    foreach ($l in ($Stdout -split "`n")) { if ($l -match '^([a-z_]+): (.+)$') { $map[$Matches[1]] = $Matches[2].Trim() } }
    return $map
}

function Format-Fields($Map) { return ('result={0} reason={1} repo_unchanged={2} outputs_present={3}' -f $Map['result'], $Map['reason'], $Map['repo_unchanged'], $Map['outputs_present']) }

function Get-Entries([string]$Dir) {
    if (-not (Test-Path -LiteralPath $Dir)) { return @() }
    return @(Get-ChildItem -LiteralPath $Dir -Force -Recurse | ForEach-Object { $_.FullName.Substring($Dir.Length) })
}

$scratch = Join-Path ([IO.Path]::GetTempPath()) ('vdiff-uat11-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
[void][IO.Directory]::CreateDirectory($scratch)
$substLetter = $null

try {
    # ---- the script's functions in this host (the drive_root driver pattern) ----
    $tokens = $null; $errors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) { throw 'trace-run.ps1 does not parse' }
    foreach ($definition in $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
        Invoke-Expression $definition.Extent.Text
    }
    $script:IsWin = $true
    $script:PathComparison = [StringComparison]::OrdinalIgnoreCase
    $script:HoldTypeReady = $false
    $script:LinkCount = 0
    $script:MaxLinks = 40

    # ---- C1: verdict by handle, direct ----
    $protectedDir = Join-Path $scratch 'protected'
    [void][IO.Directory]::CreateDirectory($protectedDir)
    $junction = Join-Path $scratch 'jn'
    [void](New-Item -ItemType Junction -Path $junction -Target $protectedDir)
    $viaJunction = Join-Path $junction 'sub\deeper'
    $textCatches = Test-LexicallySameOrUnder $viaJunction $protectedDir
    $message = '(no exception)'
    try { $h = Open-HeldDirectory $viaJunction @($protectedDir); Close-HeldDirectory $h }
    catch { $message = $_.Exception.Message }
    $createdInside = Test-Path -LiteralPath (Join-Path $protectedDir 'sub')
    Report 'C1 verdict-by-handle' (($message -eq 'held-directory-protected') -and -not $textCatches -and -not $createdInside) `
        "text-under-protected=$textCatches; Open-HeldDirectory threw '$message'; created-inside-protected=$createdInside"

    $freeDir = Join-Path $scratch 'free\x'
    $heldPath = '(none)'; $controlError = ''
    try { $h = Open-HeldDirectory $freeDir @($protectedDir); $heldPath = $h.Path; Close-HeldDirectory $h }
    catch { $controlError = $_.Exception.Message }
    Report 'C1 control' ((-not $controlError) -and [string]::Equals($heldPath, $freeDir, [StringComparison]::OrdinalIgnoreCase)) `
        "held '$($heldPath.Replace($scratch, '<scratch>'))' $controlError"

    # ---- a small analysed repository (the BuildHistory shape of TraceRunScriptTests) ----
    $repo = Join-Path $scratch 'repo'
    [void][IO.Directory]::CreateDirectory($repo)
    function G { & git -C $repo -c user.name=uat -c user.email=uat@example.invalid -c core.autocrlf=false @args 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { throw "git $args failed" } }
    function W([string]$Rel, [string]$Text) {
        $p = Join-Path $repo $Rel
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($p))
        [IO.File]::WriteAllText($p, $Text.Replace("`r`n", "`n"))
    }
    G init -q -b main
    W '.gitignore' "*.log`n"
    W 'include/a.h' "class Alpha {`npublic:`n    int size() const;`n};`n"
    W 'include/b.h' "struct Beta {`n    int id;`n};`n"
    G add -A; G commit -q -m base
    $c1 = (& git -C $repo rev-parse HEAD).Trim()
    G checkout -q -b feature
    W 'include/a.h' "class Alpha {`npublic:`n    long size() const;`n    void grow(int by);`n};`n"
    W 'other/c.h' "class Gamma {`npublic:`n    int weight;`n};`n"
    G add -A; G commit -q -m head
    $c2 = (& git -C $repo rev-parse HEAD).Trim()
    G checkout -q main

    $core = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass')
    function Get-RunArgs([string]$Out, [string]$Log) { return @('-Repo', $repo, '-Base', $c1, '-Head', $c2, '-Out', $Out, '-LogPath', $Log, '-VdiffDll', $VdiffDll, '-SkipMermaid') }

    # ---- C2: the Invoke-Trace mapping, end to end, through a subst drive ----
    $used = @([IO.DriveInfo]::GetDrives() | ForEach-Object { $_.Name.Substring(0, 1).ToUpperInvariant() })
    $substLetter = @('V', 'W', 'X', 'Y', 'U', 'T', 'S', 'R', 'Q', 'P') | Where-Object { $used -notcontains $_ } | Select-Object -First 1
    if (-not $substLetter) { throw 'no free drive letter for subst' }
    & subst.exe "${substLetter}:" $repo
    if ($LASTEXITCODE -ne 0) { $substLetter = $null; throw 'subst failed' }
    $substLog = "${substLetter}:\logs\vdiff.log"
    $textRefuses = Test-SameOrUnder $substLog $repo
    $out2 = Join-Path $scratch 'out-c2'
    $statusBefore = (& git -C $repo status --porcelain --ignored) -join "`n"
    $r2 = Invoke-Host ($core + @('-File', $scriptPath) + (Get-RunArgs $out2 $substLog))
    $f2 = Get-Fields $r2.Stdout
    $outEntries = Get-Entries $out2
    $repoLogs = Test-Path -LiteralPath (Join-Path $repo 'logs')
    $statusAfter = (& git -C $repo status --porcelain --ignored) -join "`n"
    Report 'C2 protected-verdict-maps-to-bad-arguments' (
        (-not $textRefuses) -and $f2['result'] -eq 'fail' -and $f2['reason'] -eq 'bad-arguments' -and
        $outEntries.Count -eq 0 -and -not $repoLogs -and $statusBefore -eq $statusAfter -and $r2.ExitCode -eq 1) `
        ("text-check-refuses-subst-log=$textRefuses; " + (Format-Fields $f2) + "; exit=$($r2.ExitCode); out-entries=$($outEntries.Count) (vdiff started if > 0); logs-dir-in-repo=$repoLogs; repo-status-unchanged=$($statusBefore -eq $statusAfter)")
    & subst.exe "${substLetter}:" /D | Out-Null
    $substLetter = $null

    # ---- C3: any other hold failure skips the log ----
    $out3a = Join-Path $scratch 'out-c3-control'
    $log3a = Join-Path $scratch 'logs-c3-control\vdiff.log'
    $r3a = Invoke-Host ($core + @('-File', $scriptPath) + (Get-RunArgs $out3a $log3a))
    $f3a = Get-Fields $r3a.Stdout
    Report 'C3 control (log written when the hold works)' (
        $f3a['result'] -eq 'pass' -and (Test-Path -LiteralPath $log3a -PathType Leaf)) `
        ((Format-Fields $f3a) + "; log-written=$(Test-Path -LiteralPath $log3a -PathType Leaf)")

    $wrapper = Join-Path $scratch 'collide-then-run.ps1'
    $wrapperText = @'
param([string]$ScriptPath, [string]$Repo, [string]$Base, [string]$Head, [string]$Out, [string]$LogPath, [string]$VdiffDll)
# A type with the script's name already in this process: the script's Add-Type of HoldNative fails.
Add-Type -TypeDefinition 'namespace VdiffTrace { public static class HoldNative { public static int Marker = 1; } }'
& $ScriptPath -Repo $Repo -Base $Base -Head $Head -Out $Out -LogPath $LogPath -VdiffDll $VdiffDll -SkipMermaid
exit $LASTEXITCODE
'@
    [IO.File]::WriteAllText($wrapper, $wrapperText, (New-Object System.Text.UTF8Encoding($false)))
    $out3b = Join-Path $scratch 'out-c3-failed-hold'
    $logDir3b = Join-Path $scratch 'logs-c3-failed-hold'
    $log3b = Join-Path $logDir3b 'vdiff.log'
    $r3b = Invoke-Host ($core + @('-File', $wrapper, '-ScriptPath', $scriptPath, '-Repo', $repo, '-Base', $c1, '-Head', $c2, '-Out', $out3b, '-LogPath', $log3b, '-VdiffDll', $VdiffDll))
    $f3b = Get-Fields $r3b.Stdout
    $outputs3b = @(Get-ChildItem -LiteralPath $out3b -File -ErrorAction SilentlyContinue).Count
    Report 'C3 failed-hold-skips-log' (
        $f3b['result'] -eq 'pass' -and $f3b['reason'] -eq 'none' -and $outputs3b -ge 5 -and
        -not (Test-Path -LiteralPath $log3b) -and -not (Test-Path -LiteralPath $logDir3b)) `
        ((Format-Fields $f3b) + "; exit=$($r3b.ExitCode); output-files=$outputs3b; log-written=$(Test-Path -LiteralPath $log3b); log-dir-exists=$(Test-Path -LiteralPath $logDir3b)")
    if ($r3b.Stderr.Trim()) { Write-Output ('  C3 stderr: ' + $r3b.Stderr.Trim().Replace($scratch, '<scratch>')) }
}
catch {
    Report 'driver' $false ('driver error: ' + $_.Exception.Message.Replace($scratch, '<scratch>'))
}
finally {
    if ($substLetter) { & subst.exe "${substLetter}:" /D | Out-Null }
    $j = Join-Path $scratch 'jn'
    if (Test-Path -LiteralPath $j) { [IO.Directory]::Delete($j) }
    Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
}

$overall = if ($failed -eq 0) { 'PASS' } else { 'FAIL' }
Write-Output "OVERALL $overall on $hostName at $((& git -C $repoRoot rev-parse --short HEAD).Trim()) ($failed failing check(s))"
if ($failed -eq 0) { exit 0 } else { exit 1 }

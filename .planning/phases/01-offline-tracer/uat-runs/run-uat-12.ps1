<#
Phase 01 UAT test 12, option 1: a real drive-root write by vdiff (CR-01, TRC-02), on a subst drive.

Run from the repository root (either PowerShell host):

  pwsh -NoProfile -ExecutionPolicy Bypass -File .planning/phases/01-offline-tracer/uat-runs/run-uat-12.ps1

What it does:
  1. Makes a small analysed repository and an empty folder "vol" in a temporary directory, and maps
     a free drive letter L: to "vol" with subst. L:\ is then a drive root this account can write to.
  2. Creates L:\sub and starts vdiff with L:\sub as its current directory and --out L:\.
  3. Discriminator: a child process in the same current directory resolves "L:" (the spelling the
     CR-01 bug trimmed L:\ to) to L:\sub. So with the bug the files would land in L:\sub.
Expected: exit 0, five files directly in L:\, nothing in L:\sub, no temp files left, and the success
line names L:\. Caveat (stated in the UAT): L:\ is a subst root backed by a folder on another volume.
The driver removes its subst mapping and the temporary directory at the end.

Needs git and a built tests/Vdiff.Tests/bin/Debug/net10.0/vdiff.dll with the release sidecar.
#>
param([string]$VdiffDll)

$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'run-uat-12.ps1 is Windows-only' }
$repoRoot = (& git rev-parse --show-toplevel).Trim()
if ([string]::IsNullOrWhiteSpace($VdiffDll)) { $VdiffDll = Join-Path $repoRoot 'tests\Vdiff.Tests\bin\Debug\net10.0\vdiff.dll' }
if (-not (Test-Path -LiteralPath $VdiffDll -PathType Leaf)) { throw "vdiff.dll not found at $VdiffDll; build tests/Vdiff.Tests first" }

$failed = 0
function Report([string]$Check, [bool]$Ok, [string]$Evidence) {
    if (-not $Ok) { $script:failed++ }
    Write-Output ('{0} {1} : {2}' -f $(if ($Ok) { 'PASS' } else { 'FAIL' }), $Check, $Evidence)
}
function Start-Child([string]$File, [string[]]$Arguments, [string]$WorkingDirectory) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $File
    $quoted = foreach ($a in $Arguments) { if ($a -match '[\s"]') { '"' + ($a -replace '"', '\"') + '"' } else { $a } }
    $psi.Arguments = ($quoted -join ' ')
    $psi.WorkingDirectory = $WorkingDirectory
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $o = $p.StandardOutput.ReadToEnd(); $e = $p.StandardError.ReadToEnd(); $p.WaitForExit()
    return New-Object PSObject -Property @{ ExitCode = $p.ExitCode; Stdout = $o; Stderr = $e }
}

$scratch = Join-Path ([IO.Path]::GetTempPath()) ('vdiff-uat12-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
[void][IO.Directory]::CreateDirectory($scratch)
$letter = $null
try {
    # ---- analysed repository (outside the subst volume) ----
    $repo = Join-Path $scratch 'repo'
    [void][IO.Directory]::CreateDirectory($repo)
    function G { & git -C $repo -c user.name=uat -c user.email=uat@example.invalid -c core.autocrlf=false @args 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { throw "git $args failed" } }
    function W([string]$Rel, [string]$Text) {
        $p = Join-Path $repo $Rel
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($p))
        [IO.File]::WriteAllText($p, $Text)
    }
    G init -q -b main
    W 'include/a.h' "class Alpha {`npublic:`n    int size() const;`n};`n"
    G add -A; G commit -q -m base
    $c1 = (& git -C $repo rev-parse HEAD).Trim()
    W 'include/a.h' "class Alpha {`npublic:`n    long size() const;`n    void grow(int by);`n};`n"
    W 'other/c.h' "class Gamma {`npublic:`n    int weight;`n};`n"
    G add -A; G commit -q -m head
    $c2 = (& git -C $repo rev-parse HEAD).Trim()

    # ---- the subst volume ----
    $vol = Join-Path $scratch 'vol'
    [void][IO.Directory]::CreateDirectory($vol)
    $used = @([IO.DriveInfo]::GetDrives() | ForEach-Object { $_.Name.Substring(0, 1).ToUpperInvariant() })
    $letter = @('S', 'T', 'U', 'V', 'W', 'X', 'Y', 'R', 'Q', 'P') | Where-Object { $used -notcontains $_ } | Select-Object -First 1
    if (-not $letter) { throw 'no free drive letter for subst' }
    & subst.exe "${letter}:" $vol
    if ($LASTEXITCODE -ne 0) { $letter = $null; throw 'subst failed' }
    $root = "${letter}:\"
    $sub = "${letter}:\sub"
    [void][IO.Directory]::CreateDirectory($sub)

    # ---- discriminator: the CR-01 spelling resolves to the current directory ----
    $hostExe = (Get-Process -Id $PID).Path
    $probe = Start-Child $hostExe @('-NoProfile', '-NonInteractive', '-Command', "[IO.Path]::GetFullPath('${letter}:')") $sub
    $trimmedResolvesTo = $probe.Stdout.Trim()
    Report 'setup discriminates' ([string]::Equals($trimmedResolvesTo, $sub, [StringComparison]::OrdinalIgnoreCase)) `
        "from current directory $sub the trimmed spelling '${letter}:' resolves to '$trimmedResolvesTo' (where the CR-01 bug would write)"

    # ---- the run ----
    $run = Start-Child 'dotnet' @($VdiffDll, '--repo', $repo, '--base', $c1, '--head', $c2, '--out', $root) $sub
    $rootFiles = @(Get-ChildItem -LiteralPath $root -File -Force | Select-Object -ExpandProperty Name)
    $subEntries = @(Get-ChildItem -LiteralPath $sub -Force -Recurse)
    $temps = @(Get-ChildItem -LiteralPath $root -Force -Recurse -Filter '*.tmp-*')
    $stdout = $run.Stdout.Trim().Replace($scratch, '<scratch>')
    $stderr = $run.Stderr.Trim().Replace($scratch, '<scratch>')
    Report 'vdiff exit 0' ($run.ExitCode -eq 0) "exit=$($run.ExitCode)"
    Report 'five files in the root' ($rootFiles.Count -eq 5) ("$($rootFiles.Count) file(s) in ${root}: " + ($rootFiles -join ', '))
    Report 'nothing in the current directory' ($subEntries.Count -eq 0) "$($subEntries.Count) entr(y/ies) in $sub"
    Report 'no temp files' ($temps.Count -eq 0) "$($temps.Count) *.tmp-* file(s)"
    Report 'success line names the typed root' ($stdout -like "*$root*") ("stdout: " + $stdout)
    if ($stderr) { Write-Output ('  stderr: ' + $stderr) }
}
catch {
    Report 'driver' $false ('driver error: ' + $_.Exception.Message.Replace($scratch, '<scratch>'))
}
finally {
    if ($letter) { & subst.exe "${letter}:" /D | Out-Null }
    Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction SilentlyContinue
}
$hostName = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh ' + $PSVersionTable.PSVersion } else { 'powershell ' + $PSVersionTable.PSVersion }
Write-Output ("OVERALL {0} on {1} at {2} ({3} failing check(s))" -f $(if ($failed -eq 0) { 'PASS' } else { 'FAIL' }), $hostName, (& git -C $repoRoot rev-parse --short HEAD).Trim(), $failed)
if ($failed -eq 0) { exit 0 } else { exit 1 }

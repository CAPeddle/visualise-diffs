<#
Trial wrapper for the vdiff tracer (TRC-03, D-01, D-09).

Runs vdiff once against a repository, checks that the repository is unchanged afterwards and
prints exactly six fixed-vocabulary lines. It never prints a path, a commit id, a name, a count
or an exact time: no value that came from a parameter, an exception or vdiff output is echoed.

  result: pass|fail
  reason: none|bad-arguments|out-not-outside|tool-failed|repo-changed|outputs-missing|over-budget|script-error
  wall_clock: under-1-min|1-to-5-min|5-to-20-min|over-20-min
  repo_unchanged: yes|no|unknown
  outputs_present: yes|no
  mermaid_syntax: ok|fail|skipped

Exit code 0 on pass, 1 otherwise. A repo_unchanged value of unknown (the after-run snapshot
could not be taken) is reported with reason script-error.

Usage (from the vdiff repository root):
  pwsh -NoProfile -File scripts/trace-run.ps1 -Repo <dir> -Base <id> -Head <id> -Out <dir>
       [-Blank NAME[,NAME...]] [-Include GLOB] [-VdiffDll <path>] [-LogPath <file>] [-SkipMermaid]

  -Out and -LogPath must lie outside the analysed repository and outside the vdiff repository,
  judged after resolving symbolic links and directory junctions. Bound (T-01-39): the guard
  compares drive-letter paths after resolving symbolic links and directory junctions. It does not
  resolve subst or mapped drive letters or DOS 8.3 short names, and review findings IN-03 (link
  targets written without a drive letter) and IN-04 (case-insensitive file systems other than
  Windows compared case-sensitively) remain open. A path in another Windows namespace
  (extended-length, device, UNC including loopback) and a link whose target is a volume GUID
  path are refused, not resolved (T-01-41). A link that cannot be resolved (a loop, no access)
  ends the run with reason script-error, never with a guess.
  Reason precedence: bad-arguments for blank, unbound, non-hex or missing-repository input, then
  out-not-outside for -Out (namespace, then containment), then bad-arguments for -LogPath.
  -LogPath receives vdiff's own output for private diagnosis; without it that output is discarded.

Runs under Windows PowerShell 5.1 and PowerShell 7. ASCII characters only.
#>
param(
    [string]$Repo,
    [string]$Base,
    [string]$Head,
    [string]$Out,
    [string[]]$Blank,
    [string[]]$Include,
    [string]$VdiffDll,
    [string]$LogPath,
    [switch]$SkipMermaid
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:IsWin = ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT)
$script:PathComparison = if ($script:IsWin) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
$script:VdiffRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$script:BudgetSeconds = 300
$script:VdiffTimeoutMs = 25 * 60 * 1000
$script:SmallTimeoutMs = 10 * 60 * 1000
$script:MermaidTimeoutMs = 180 * 1000

# ---- helpers ----------------------------------------------------------------------------

function ConvertTo-ArgText([string]$Text) {
    if ($Text.Length -eq 0) { return '""' }
    if ($Text -notmatch '[\s"]') { return $Text }
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    $slashes = 0
    foreach ($ch in $Text.ToCharArray()) {
        if ($ch -eq [char]92) {
            $slashes++
        }
        elseif ($ch -eq [char]34) {
            [void]$sb.Append([char]92, ($slashes * 2) + 1)
            [void]$sb.Append([char]34)
            $slashes = 0
        }
        else {
            if ($slashes -gt 0) { [void]$sb.Append([char]92, $slashes); $slashes = 0 }
            [void]$sb.Append($ch)
        }
    }
    if ($slashes -gt 0) { [void]$sb.Append([char]92, $slashes * 2) }
    [void]$sb.Append('"')
    return $sb.ToString()
}

# Runs a native program with stdout and stderr both captured and returned, never echoed.
# Returns an object with ExitCode, Stdout, Stderr and TimedOut. Throws only when the program
# cannot be started.
function Invoke-Native([string]$File, [string[]]$Arguments, [int]$TimeoutMs, [string[]]$DropVariables, [hashtable]$SetVariables) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $File
    $psi.Arguments = (($Arguments | ForEach-Object { ConvertTo-ArgText ([string]$_) }) -join ' ')
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardInput = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [Text.Encoding]::UTF8
    foreach ($name in $DropVariables) { [void]$psi.EnvironmentVariables.Remove($name) }
    if ($SetVariables) {
        foreach ($name in $SetVariables.Keys) { $psi.EnvironmentVariables[$name] = [string]$SetVariables[$name] }
    }
    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi
    [void]$proc.Start()
    try {
        $proc.StandardInput.Close()
        $outTask = $proc.StandardOutput.ReadToEndAsync()
        $errTask = $proc.StandardError.ReadToEndAsync()
        $timedOut = $false
        if (-not $proc.WaitForExit($TimeoutMs)) {
            $timedOut = $true
            try { $proc.Kill() } catch { $null = $_ }
        }
        else {
            $proc.WaitForExit()
        }
        $stdout = ''
        $stderr = ''
        if ($outTask.Wait(10000)) { $stdout = $outTask.Result }
        if ($errTask.Wait(10000)) { $stderr = $errTask.Result }
        $code = -1
        if (-not $timedOut) { $code = $proc.ExitCode }
        return (New-Object PSObject -Property @{ ExitCode = $code; Stdout = $stdout; Stderr = $stderr; TimedOut = $timedOut })
    }
    finally {
        $proc.Dispose()
    }
}

$script:GitDrop = @('GIT_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE', 'GIT_COMMON_DIR', 'GIT_NAMESPACE', 'GIT_EXTERNAL_DIFF')
$script:GitSet = @{ GIT_NO_LAZY_FETCH = '1'; GIT_OPTIONAL_LOCKS = '0'; GIT_TERMINAL_PROMPT = '0' }

# Every call: no fsmonitor hook or daemon, no optional locks, no lazy fetch (C47, D-02).
function Invoke-Git([string]$RepoDir, [string[]]$VerbArguments) {
    $all = @('-c', 'core.fsmonitor=false', '--no-optional-locks', '-C', $RepoDir) + $VerbArguments
    return (Invoke-Native 'git' $all $script:SmallTimeoutMs $script:GitDrop $script:GitSet)
}

function Get-FullPath([string]$Path) {
    $resolved = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    $full = [IO.Path]::GetFullPath($resolved)
    $root = [IO.Path]::GetPathRoot($full)
    if ($full.Length -gt $root.Length) {
        $full = $full.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    }
    return $full
}

function Test-LexicallySameOrUnder([string]$Path, [string]$Dir) {
    if ([string]::IsNullOrEmpty($Dir)) { return $false }
    $p = Get-FullPath $Path
    $d = Get-FullPath $Dir
    if ($p.Equals($d, $script:PathComparison)) { return $true }
    $sep = [string][IO.Path]::DirectorySeparatorChar
    $prefix = $d
    if (-not $prefix.EndsWith($sep)) { $prefix = $prefix + $sep }
    return $p.StartsWith($prefix, $script:PathComparison)
}

# True on Windows when the full path text does not start with an ASCII letter, a colon and a
# backslash (T-01-41, T-01-44). A pattern match on the text, never on GetPathRoot: the root of a
# volume GUID path is empty on Windows PowerShell 5.1 and the root of a device path has no
# trailing separator. The case-sensitive operator keeps non-ASCII letters that fold to ASCII
# ones out. The same rule as FinalPath.Step in the C# tool (same rule, two implementations).
function Test-NonDriveRoot([string]$FullPath) {
    if (-not $script:IsWin) { return $false }
    return ($FullPath -cnotmatch '^[A-Za-z]:\x5C')
}

$script:LinkCount = 0
$script:MaxLinks = 40

# The same rule as FinalPath.Resolve in the C# tool: walk the full path from its root one segment
# at a time; a symbolic link or directory junction replaces everything so far by its target,
# resolved by the same rule; stop at the first segment that does not exist and append the rest;
# at most 40 links per resolution. Any failure throws: the caller never falls back to the lexical
# path, and the outer catch prints reason script-error. The mechanism is Get-Item with LinkType
# and Target because ResolveLinkTarget does not exist in Windows PowerShell 5.1.
# Bound (T-01-39): the guard compares drive-letter paths after resolving symbolic links and
# directory junctions. It does not resolve subst or mapped drive letters or DOS 8.3 short names,
# and review findings IN-03 (link targets written without a drive letter) and IN-04
# (case-insensitive file systems other than Windows compared case-sensitively) remain open.
# A path in another Windows namespace (extended-length, device, UNC including loopback) and a
# link whose target is a volume GUID path are refused, not resolved (T-01-41).
function Resolve-FinalPath([string]$Path) {
    $script:LinkCount = 0
    return (Resolve-FinalPathStep $Path)
}

function Resolve-FinalPathStep([string]$Path) {
    $full = Get-FullPath $Path
    if (Test-NonDriveRoot $full) { throw 'unsupported namespace' }
    $root = [IO.Path]::GetPathRoot($full)
    $separators = [char[]]@([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
    $segments = @($full.Substring($root.Length).Split($separators, [StringSplitOptions]::RemoveEmptyEntries))
    $prefix = $root
    for ($i = 0; $i -lt $segments.Count; $i++) {
        $candidate = [IO.Path]::Combine($prefix, $segments[$i])
        $item = $null
        try { $item = Get-Item -LiteralPath $candidate -Force -ErrorAction Stop }
        catch [System.Management.Automation.ItemNotFoundException] { $item = $null }
        if ($null -eq $item) {
            $result = $prefix
            for ($j = $i; $j -lt $segments.Count; $j++) { $result = [IO.Path]::Combine($result, $segments[$j]) }
            return $result
        }
        if ($item.LinkType -eq 'SymbolicLink' -or $item.LinkType -eq 'Junction') {
            $script:LinkCount++
            if ($script:LinkCount -gt $script:MaxLinks) { throw 'too many links' }
            $target = [string]@($item.Target)[0]
            if ($target.StartsWith('\??\', [StringComparison]::Ordinal)) { $target = $target.Substring(4) }
            if ([string]::IsNullOrEmpty($target)) { throw 'unreadable link' }
            # The runtime strips the NT prefix before it reports a target, so only a drive-letter
            # path is followed; every other reported target is refused (R-1, R-2, T-01-44).
            if (Test-NonDriveRoot $target) { throw 'unsupported link target' }
            if (-not [IO.Path]::IsPathRooted($target)) { $target = [IO.Path]::Combine($prefix, $target) }
            $prefix = Resolve-FinalPathStep (Get-FullPath $target)
        }
        else {
            $prefix = $candidate
        }
    }
    return $prefix
}

# True when Path is Dir or below it, judged lexically first and then after link resolution.
function Test-SameOrUnder([string]$Path, [string]$Dir) {
    if ([string]::IsNullOrEmpty($Dir)) { return $false }
    if (Test-LexicallySameOrUnder $Path $Dir) { return $true }
    return (Test-LexicallySameOrUnder (Resolve-FinalPath $Path) (Resolve-FinalPath $Dir))
}

function Get-FirstLine([string]$Text) {
    $trimmed = $Text.Trim()
    if ($trimmed.Length -eq 0) { return '' }
    return ($trimmed -split "[\r\n]+")[0]
}

function Get-Sha256Hex([byte[]]$Bytes) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($Bytes)) -replace '-', '') }
    finally { $sha.Dispose() }
}

# One SHA-256 string over everything the run must leave alone: the status, HEAD, branch, refs,
# worktrees and stash list (with exit codes) and the bytes of the index file. Never printed.
function Get-RepoSnapshot([string]$RepoDir) {
    $questions = @(
        @('status', '--porcelain=v1', '-z', '--untracked-files=all'),
        @('rev-parse', 'HEAD'),
        @('symbolic-ref', '-q', 'HEAD'),
        @('for-each-ref'),
        @('worktree', 'list', '--porcelain'),
        @('stash', 'list')
    )
    $sb = New-Object System.Text.StringBuilder
    foreach ($q in $questions) {
        $r = Invoke-Git $RepoDir $q
        if ($r.TimedOut) { throw 'snapshot timed out' }
        [void]$sb.Append('$ ' + (@($q) -join ' ') + ' -> ' + [string]$r.ExitCode + "`n")
        [void]$sb.Append($r.Stdout)
        [void]$sb.Append("`n")
    }
    $gd = Invoke-Git $RepoDir @('rev-parse', '--path-format=absolute', '--absolute-git-dir')
    $gitDir = Get-FirstLine $gd.Stdout
    if ($gd.ExitCode -ne 0 -or $gitDir.Length -eq 0) { throw 'no git directory' }
    $indexFile = Join-Path (Get-FullPath $gitDir) 'index'
    if (Test-Path -LiteralPath $indexFile -PathType Leaf) {
        [void]$sb.Append('index ' + (Get-Sha256Hex ([IO.File]::ReadAllBytes($indexFile))) + "`n")
    }
    else {
        [void]$sb.Append("index none`n")
    }
    return (Get-Sha256Hex ([Text.Encoding]::UTF8.GetBytes($sb.ToString())))
}

function Get-WallClock([double]$Seconds) {
    if ($Seconds -lt 60) { return 'under-1-min' }
    if ($Seconds -lt 300) { return '1-to-5-min' }
    if ($Seconds -lt 1200) { return '5-to-20-min' }
    return 'over-20-min'
}

# Writes the diagnostic log without following a link planted at its name (T-01-45, WR-01).
# A directory, symbolic link or junction at the name means no log (a create-new open would
# follow a dangling link and create its target); a plain file or a hard link is removed by name,
# which removes only the directory entry and never the file it points to; the log is then
# created new. Attributes are read, not followed, so a dangling link is seen too.
function Write-LogFile([string]$Path, [string]$Text) {
    $dirName = Split-Path -Parent $Path
    if ($dirName -and -not (Test-Path -LiteralPath $dirName)) { [void](New-Item -ItemType Directory -Path $dirName) }
    $attributes = $null
    try { $attributes = [IO.File]::GetAttributes($Path) }
    catch [System.IO.FileNotFoundException] { $attributes = $null }
    catch [System.IO.DirectoryNotFoundException] { $attributes = $null }
    if ($null -ne $attributes) {
        if (($attributes -band [IO.FileAttributes]::Directory) -ne 0) { return }
        if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return }
        [IO.File]::Delete($Path)
    }
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($Text)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length) }
    finally { $stream.Dispose() }
}

# ---- the run ----------------------------------------------------------------------------

# $F holds the six fields. Early exits from Invoke-Trace leave the before-run defaults.
function Invoke-Trace([hashtable]$F) {
    foreach ($value in @($Repo, $Base, $Head, $Out)) {
        if ([string]::IsNullOrWhiteSpace($value)) { $F.reason = 'bad-arguments'; return }
    }
    if ($script:UnboundArguments -and @($script:UnboundArguments).Count -gt 0) { $F.reason = 'bad-arguments'; return }
    if ($Base -notmatch '^[0-9a-fA-F]{7,64}$' -or $Head -notmatch '^[0-9a-fA-F]{7,64}$') { $F.reason = 'bad-arguments'; return }

    $repoFull = Get-FullPath $Repo
    if (-not (Test-Path -LiteralPath $repoFull -PathType Container)) { $F.reason = 'bad-arguments'; return }
    $probe = Invoke-Git $repoFull @('rev-parse', '--git-dir')
    if ($probe.TimedOut -or $probe.ExitCode -ne 0) { $F.reason = 'bad-arguments'; return }

    $blankNames = @()
    foreach ($item in @($Blank)) {
        if ($null -eq $item) { continue }
        foreach ($piece in ([string]$item -split ',')) {
            $name = $piece.Trim()
            if ($name.Length -gt 0) { $blankNames += $name }
        }
    }
    $includeGlobs = @()
    foreach ($item in @($Include)) {
        if ($null -ne $item -and ([string]$item).Length -gt 0) { $includeGlobs += [string]$item }
    }

    # The directories a write must stay out of: the work tree, the git directory, the common
    # git directory (a linked worktree) and this repository.
    $protected = @($script:VdiffRoot)
    $top = Invoke-Git $repoFull @('rev-parse', '--path-format=absolute', '--show-toplevel')
    if ($top.ExitCode -eq 0 -and (Get-FirstLine $top.Stdout).Length -gt 0) { $protected += (Get-FirstLine $top.Stdout) }
    $gdir = Invoke-Git $repoFull @('rev-parse', '--path-format=absolute', '--absolute-git-dir')
    if ($gdir.ExitCode -eq 0 -and (Get-FirstLine $gdir.Stdout).Length -gt 0) { $protected += (Get-FirstLine $gdir.Stdout) }
    $cdir = Invoke-Git $repoFull @('rev-parse', '--path-format=absolute', '--git-common-dir')
    if ($cdir.ExitCode -eq 0 -and (Get-FirstLine $cdir.Stdout).Length -gt 0) { $protected += (Get-FirstLine $cdir.Stdout) }
    # The raw -Repo spelling protects only when it is a drive-letter path. The git-reported roots
    # above already cover a repository addressed through another namespace (git reports a drive
    # path for the extended and device spellings; a UNC-hosted repository yields a non-drive root
    # that the resolver refuses with script-error, fail closed, as the CLI does).
    if (-not (Test-NonDriveRoot $repoFull)) { $protected += $repoFull }

    $outFull = Get-FullPath $Out
    if (Test-NonDriveRoot $outFull) { $F.reason = 'out-not-outside'; return }
    foreach ($dir in $protected) {
        if (Test-SameOrUnder $outFull $dir) { $F.reason = 'out-not-outside'; return }
    }
    $logFull = $null
    if (-not [string]::IsNullOrWhiteSpace($LogPath)) {
        $logFull = Get-FullPath $LogPath
        if (Test-NonDriveRoot $logFull) { $F.reason = 'bad-arguments'; return }
        foreach ($dir in $protected) {
            if (Test-SameOrUnder $logFull $dir) { $F.reason = 'bad-arguments'; return }
        }
    }

    $dll = $VdiffDll
    if ([string]::IsNullOrWhiteSpace($dll)) {
        $dll = Join-Path $script:VdiffRoot 'src/Vdiff.Cli/bin/Release/net10.0/vdiff.dll'
    }
    $dllFull = Get-FullPath $dll
    if (-not (Test-Path -LiteralPath $dllFull -PathType Leaf)) { $F.reason = 'tool-failed'; return }

    # Before the run: from here on the fields describe a real run.
    $before = Get-RepoSnapshot $repoFull

    $vdiffArgs = @($dllFull, '--repo', $repoFull, '--base', $Base, '--head', $Head, '--out', $outFull)
    foreach ($name in $blankNames) { $vdiffArgs += @('--blank', $name) }
    foreach ($glob in $includeGlobs) { $vdiffArgs += @('--include', $glob) }

    $clock = [Diagnostics.Stopwatch]::StartNew()
    $run = Invoke-Native 'dotnet' $vdiffArgs $script:VdiffTimeoutMs @() $null
    $clock.Stop()
    $seconds = $clock.Elapsed.TotalSeconds
    $F.wall = Get-WallClock $seconds

    # The log is diagnostic: it is checked again here, after the long vdiff run and just before the
    # write (T-01-46), and an unsafe or unresolvable path skips it silently. No reason changes.
    if ($logFull) {
        try {
            $logSafe = -not (Test-NonDriveRoot $logFull)
            if ($logSafe) {
                foreach ($dir in $protected) {
                    if (Test-SameOrUnder $logFull $dir) { $logSafe = $false; break }
                }
            }
            if ($logSafe) { Write-LogFile $logFull ([string]$run.Stdout + "`n" + [string]$run.Stderr) }
        }
        catch { $null = $_ }
    }

    $snapshotFailed = $false
    $after = $null
    try { $after = Get-RepoSnapshot $repoFull } catch { $snapshotFailed = $true }
    if ($snapshotFailed) { $F.unchanged = 'unknown' }
    elseif ($after -eq $before) { $F.unchanged = 'yes' }
    else { $F.unchanged = 'no' }

    $expected = @('base.model.json', 'head.model.json', 'diff.json', 'summary.mmd', 'report.html')
    $present = $true
    foreach ($file in $expected) {
        if (-not (Test-Path -LiteralPath (Join-Path $outFull $file) -PathType Leaf)) { $present = $false }
    }
    if ($present) { $F.outputs = 'yes' } else { $F.outputs = 'no' }

    $vdiffOk = (-not $run.TimedOut) -and ($run.ExitCode -eq 0)

    # Informational only: never changes the result (D-02). The fixed check of plan 01-09
    # raises the text-size limit; mermaid-cli is never called directly from here.
    $checkScript = Join-Path $script:VdiffRoot 'tools/mermaid-check/check.mjs'
    $packages = Join-Path $script:VdiffRoot 'tools/mermaid-check/node_modules'
    $summary = Join-Path $outFull 'summary.mmd'
    if ((-not $SkipMermaid) -and $vdiffOk -and (Test-Path -LiteralPath $summary -PathType Leaf) -and
        (Test-Path -LiteralPath $checkScript -PathType Leaf) -and (Test-Path -LiteralPath $packages -PathType Container)) {
        try {
            $check = Invoke-Native 'node' @($checkScript, $summary) $script:MermaidTimeoutMs @() $null
            if ($check.TimedOut) { $F.mermaid = 'fail' }
            elseif ($check.ExitCode -eq 0) { $F.mermaid = 'ok' }
            elseif ($check.ExitCode -eq 3) { $F.mermaid = 'skipped' }
            else { $F.mermaid = 'fail' }
        }
        catch { $F.mermaid = 'skipped' }
    }

    if (-not $vdiffOk) { $F.reason = 'tool-failed' }
    elseif ($F.unchanged -eq 'unknown') { $F.reason = 'script-error' }
    elseif ($F.unchanged -ne 'yes') { $F.reason = 'repo-changed' }
    elseif ($F.outputs -ne 'yes') { $F.reason = 'outputs-missing' }
    elseif ($seconds -ge $script:BudgetSeconds) { $F.reason = 'over-budget' }
    else { $F.reason = 'none'; $F.result = 'pass' }
}

$script:UnboundArguments = $args
$fields = @{ result = 'fail'; reason = 'script-error'; wall = 'under-1-min'; unchanged = 'unknown'; outputs = 'no'; mermaid = 'skipped' }
try {
    $fields = @{ result = 'fail'; reason = 'none'; wall = 'under-1-min'; unchanged = 'unknown'; outputs = 'no'; mermaid = 'skipped' }
    Invoke-Trace $fields
}
catch {
    $fields = @{ result = 'fail'; reason = 'script-error'; wall = 'under-1-min'; unchanged = 'unknown'; outputs = 'no'; mermaid = 'skipped' }
}

$lines = @(
    ('result: ' + $fields.result),
    ('reason: ' + $fields.reason),
    ('wall_clock: ' + $fields.wall),
    ('repo_unchanged: ' + $fields.unchanged),
    ('outputs_present: ' + $fields.outputs),
    ('mermaid_syntax: ' + $fields.mermaid)
)
[Console]::Out.Write(($lines -join "`n") + "`n")
[Console]::Out.Flush()
if ($fields.result -eq 'pass') { exit 0 } else { exit 1 }

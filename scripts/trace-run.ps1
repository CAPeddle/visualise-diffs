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
  compares drive-letter paths after resolving symbolic links and directory junctions. On Windows
  a link is followed only when the target the runtime reports for it is a drive-letter path, and
  every other reported target is refused, not resolved (T-01-41). That covers a target in an NT
  namespace (GLOBALROOT, bare device paths, UNC and volume GUID paths), a relative target, a
  target written without a drive letter and a drive-relative target. The runtime strips the NT
  prefix before it reports a target, so the test is on the reported text and not on a prefix, and
  raw reparse data is not read. Paths in other Windows namespaces (extended-length, device, UNC
  including loopback) are refused too. Still unresolved: subst and mapped drive letters, DOS 8.3
  short names, and review finding IN-04 (case-insensitive file systems other than Windows compared
  case-sensitively). A link that cannot be resolved (a loop, no access) or whose target is refused
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
# directory junctions. On Windows a link is followed only when the target the runtime reports
# for it is a drive-letter path, and every other reported target is refused, not resolved
# (T-01-41). That covers a target in an NT namespace (GLOBALROOT, bare device paths, UNC and
# volume GUID paths), a relative target, a target written without a drive letter and a
# drive-relative target. The runtime strips the NT prefix before it reports a target, so the
# test is on the reported text and not on a prefix, and raw reparse data is not read. Paths in
# other Windows namespaces (extended-length, device, UNC including loopback) are refused too.
# Still unresolved: subst and mapped drive letters, DOS 8.3 short names, and review finding
# IN-04 (case-insensitive file systems other than Windows compared case-sensitively).
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

# ---- the held log directory (G-01-1; T-01-43, T-01-46) ------------------------------------------

$script:HoldTypeReady = $false

# Compiles the two native declarations once per process. Called only on Windows and only when
# -LogPath is given (L-2). The text is fixed and single-quoted: no argument and no repository
# content reaches it. A host that forbids compiling (Constrained Language Mode) makes this throw,
# the caller then holds nothing, and the log is skipped silently, never written unprotected.
function Initialize-HoldType {
    if ($script:HoldTypeReady) { return }
    $members = @'
[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
public static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode, System.IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, System.IntPtr templateFile);

[System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
public static extern uint GetFinalPathNameByHandleW(Microsoft.Win32.SafeHandles.SafeFileHandle handle, System.Text.StringBuilder filePath, uint filePathLength, uint flags);
'@
    Add-Type -MemberDefinition $members -Name 'HoldNative' -Namespace 'VdiffTrace'
    $script:HoldTypeReady = $true
}

# GENERIC_READ 2147483648 (a hex literal above 0x7FFFFFFF is a negative integer in PowerShell),
# FILE_SHARE_READ + FILE_SHARE_WRITE 3 (no delete sharing), OPEN_EXISTING 3, and
# FILE_FLAG_BACKUP_SEMANTICS 33554432 (0x02000000, needed to open a directory). The flag that
# opens a link itself instead of following it is deliberately not set, so a link at the last
# component is followed and the place the log would really land is what is judged.
function Open-DirectoryHandle([string]$Directory) {
    $handle = [VdiffTrace.HoldNative]::CreateFileW($Directory, [uint32]2147483648, [uint32]3, [IntPtr]::Zero, [uint32]3, [uint32]33554432, [IntPtr]::Zero)
    if ($handle.IsInvalid) {
        $handle.Dispose()
        throw 'cannot open directory'
    }
    return $handle
}

function Get-HandleFinalPath($Handle) {
    $builder = New-Object System.Text.StringBuilder -ArgumentList 1024
    $length = [VdiffTrace.HoldNative]::GetFinalPathNameByHandleW($Handle, $builder, [uint32]$builder.Capacity, [uint32]0)
    if ($length -ge $builder.Capacity) {
        # Too small: the call returned the size it needs, including the terminator.
        $builder = New-Object System.Text.StringBuilder -ArgumentList ([int]$length + 1)
        $length = [VdiffTrace.HoldNative]::GetFinalPathNameByHandleW($Handle, $builder, [uint32]$builder.Capacity, [uint32]0)
    }
    if ($length -eq 0 -or $length -ge $builder.Capacity) { throw 'unresolvable final path' }
    return $builder.ToString()
}

# The final-path forms Windows reports, reduced to a path files can be written to (the same rule
# as HeldDirectory.Normalize in the C# tool). A drive-letter path loses its extended-length prefix
# and is judged. A UNC path becomes \\server\share\... and is accepted without being judged: the
# guard has never judged those spellings and this function makes no claim about them. Every other
# shape throws.
function ConvertFrom-FinalPathForm([string]$FinalPath) {
    $unc = '\\?\UNC\'
    $extended = '\\?\'
    if ($FinalPath.StartsWith($unc, [StringComparison]::OrdinalIgnoreCase) -and $FinalPath.Length -gt $unc.Length) {
        return (New-Object PSObject -Property @{ Path = ('\\' + $FinalPath.Substring($unc.Length)); Judged = $false })
    }
    if ($FinalPath.StartsWith($extended, [StringComparison]::Ordinal)) {
        $rest = $FinalPath.Substring($extended.Length)
        if ($rest -cmatch '^[A-Za-z]:\x5C') {
            return (New-Object PSObject -Property @{ Path = $rest; Judged = $true })
        }
    }
    throw 'unsupported final path'
}

function Test-UnderAny([string]$Path, [string[]]$Dirs) {
    foreach ($dir in $Dirs) {
        if (Test-LexicallySameOrUnder $Path $dir) { return $true }
    }
    return $false
}

function Get-JudgedFinalPath($Handle, [string[]]$ResolvedProtected) {
    $form = ConvertFrom-FinalPathForm (Get-HandleFinalPath $Handle)
    if ($form.Judged -and (Test-UnderAny $form.Path $ResolvedProtected)) { throw 'held-directory-protected' }
    return $form.Path
}

# Opens the directory, creating the missing chain under it, judges its final path against the
# protected directories and returns an object with Path (the only path the log may be written
# through) and Handle. The same rule as HeldDirectory.Create in the C# tool, in the same order:
# the deepest existing ancestor (the anchor) is opened and judged before anything is created, the
# missing directories are created under the anchor's final path, the leaf is opened and judged,
# and only then is the anchor released. A refusal or a failure after a directory was created
# removes the directories this call created, bottom-up and non-recursively. A protected verdict
# throws the fixed text held-directory-protected; any other failure throws something else and
# the caller skips the log. Off Windows no handle is held (Handle is null): the resolved anchor
# is judged before and the resolved leaf after the directories are created.
function Open-HeldDirectory([string]$Path, [string[]]$Protected) {
    $full = Get-FullPath $Path
    $resolvedProtected = @()
    foreach ($dir in $Protected) { $resolvedProtected += (Resolve-FinalPath $dir) }

    $remainder = New-Object 'System.Collections.Generic.List[string]'
    $anchor = $full
    while (-not [IO.Directory]::Exists($anchor)) {
        $parent = [IO.Path]::GetDirectoryName($anchor)
        if ([string]::IsNullOrEmpty($parent)) { throw 'unresolvable path' }
        $remainder.Insert(0, [IO.Path]::GetFileName($anchor))
        $anchor = $parent
    }

    $created = New-Object 'System.Collections.Generic.List[string]'
    $anchorHandle = $null
    $leafHandle = $null
    try {
        if ($script:IsWin) {
            Initialize-HoldType
            $anchorHandle = Open-DirectoryHandle $anchor
            $leafDirectory = Get-JudgedFinalPath $anchorHandle $resolvedProtected
            foreach ($segment in $remainder) {
                $leafDirectory = [IO.Path]::Combine($leafDirectory, $segment)
                if (-not [IO.Directory]::Exists($leafDirectory)) {
                    [void][IO.Directory]::CreateDirectory($leafDirectory)
                    $created.Add($leafDirectory)
                }
            }
            $leafHandle = Open-DirectoryHandle $leafDirectory
            $leafPath = Get-JudgedFinalPath $leafHandle $resolvedProtected
            $anchorHandle.Dispose()
            $anchorHandle = $null
            $held = New-Object PSObject -Property @{ Path = $leafPath; Handle = $leafHandle }
            $leafHandle = $null
            return $held
        }
        if (Test-UnderAny (Resolve-FinalPath $anchor) $resolvedProtected) { throw 'held-directory-protected' }
        $current = $anchor
        foreach ($segment in $remainder) {
            $current = [IO.Path]::Combine($current, $segment)
            [void][IO.Directory]::CreateDirectory($current)
            $created.Add($current)
        }
        $resolvedLeaf = Resolve-FinalPath $full
        if (Test-UnderAny $resolvedLeaf $resolvedProtected) { throw 'held-directory-protected' }
        return (New-Object PSObject -Property @{ Path = $resolvedLeaf; Handle = $null })
    }
    catch {
        $failure = $_
        if ($null -ne $leafHandle) { $leafHandle.Dispose() }
        if ($null -ne $anchorHandle) { $anchorHandle.Dispose() }
        for ($i = $created.Count - 1; $i -ge 0; $i--) {
            try { [IO.Directory]::Delete($created[$i]) } catch { $null = $_ }
        }
        throw $failure
    }
}

function Close-HeldDirectory($Hold) {
    if ($null -eq $Hold) { return }
    if ($null -ne $Hold.Handle) {
        try { $Hold.Handle.Dispose() } catch { $null = $_ }
    }
}

# Writes the diagnostic log through the final path of the held directory, never through the typed
# -LogPath: a link entry in the typed path can be retargeted while the directory is held (T-01-45,
# T-01-46, WR-01). A directory, symbolic link or junction at the final name means no log; a plain
# file or a hard link is removed by name, which removes only the directory entry and never the
# file it points to. The text goes to a temporary name made of the leaf, .tmp- and a new GUID
# (created new, no sharing) and is then moved to the final name with a plain move: the final name
# was just freed, and a link planted there in the meantime makes the move fail instead of being
# followed. Attributes are read, not followed, so a dangling link is seen too. On any failure the
# temporary file is deleted and the error propagates.
function Write-LogFile([string]$HeldPath, [string]$LeafName, [string]$Text) {
    $final = [IO.Path]::Combine($HeldPath, $LeafName)
    $attributes = $null
    try { $attributes = [IO.File]::GetAttributes($final) }
    catch [System.IO.FileNotFoundException] { $attributes = $null }
    catch [System.IO.DirectoryNotFoundException] { $attributes = $null }
    if ($null -ne $attributes) {
        if (($attributes -band [IO.FileAttributes]::Directory) -ne 0) { return }
        if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { return }
        [IO.File]::Delete($final)
    }
    $temporary = [IO.Path]::Combine($HeldPath, $LeafName + '.tmp-' + [Guid]::NewGuid().ToString('N'))
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($Text)
    $moved = $false
    try {
        $stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $stream.Write($bytes, 0, $bytes.Length) }
        finally { $stream.Dispose() }
        [IO.File]::Move($temporary, $final)
        $moved = $true
    }
    finally {
        if (-not $moved) {
            try { [IO.File]::Delete($temporary) } catch { $null = $_ }
        }
    }
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

    # The log's directory is opened and judged by its final path now, before anything runs, and held
    # until the log is written (L-3). A protected verdict ends the run as the first check does; any
    # other failure (the type cannot be compiled, a directory cannot be created) leaves no hold and
    # no log, and the run goes on (L-2).
    $logHold = $null
    if ($logFull) {
        try { $logHold = Open-HeldDirectory ([IO.Path]::GetDirectoryName($logFull)) $protected }
        catch {
            $holdFailure = $_
            if ($holdFailure.Exception.Message -eq 'held-directory-protected') { $F.reason = 'bad-arguments'; return }
            $logHold = $null
        }
    }

    try {
        # Before the run: from here on the fields describe a real run.
        $before = Get-RepoSnapshot $repoFull

        $vdiffArgs = @($dllFull, '--repo', $repoFull, '--base', $Base, '--head', $Head, '--out', $outFull)
        foreach ($name in $blankNames) { $vdiffArgs += @('--blank', $name) }
        foreach ($glob in $includeGlobs) { $vdiffArgs += @('--include', $glob) }

        # The vdiff repository root travels in the environment, never as an argument: vdiff cannot know it
        # from its own arguments (D-03 fixes them), and the variable only adds directories --out must stay
        # out of (the CLI refuses an --out inside any of them at both of its checks and in its held output
        # directory), so an unset or hostile environment cannot weaken that guard (T-01-58).
        $protectedForChild = $script:VdiffRoot

        $clock = [Diagnostics.Stopwatch]::StartNew()
        $run = Invoke-Native 'dotnet' $vdiffArgs $script:VdiffTimeoutMs @() @{ VDIFF_PROTECTED_DIRS = $protectedForChild }
        $clock.Stop()
        $seconds = $clock.Elapsed.TotalSeconds
        $F.wall = Get-WallClock $seconds

        # The log is diagnostic and is written through the final path of the held directory. Where no
        # handle is held (Linux and macOS) its containment is checked again here, after the long vdiff
        # run and just before the write (T-01-46), and an unsafe or unresolvable path skips it silently;
        # with a handle the final path judged before the run is the decisive fact and a by-name re-check
        # after a retarget would suppress a log that is safely written. No reason changes. Only the
        # containment test is repeated: the namespace test is a pure text test on a string that cannot
        # have changed since the first check, while containment reads the file system.
        if ($logHold) {
            try {
                $logSafe = $true
                if ($null -eq $logHold.Handle) {
                    foreach ($dir in $protected) {
                        if (Test-SameOrUnder $logFull $dir) { $logSafe = $false; break }
                    }
                }
                if ($logSafe) { Write-LogFile $logHold.Path ([IO.Path]::GetFileName($logFull)) ([string]$run.Stdout + "`n" + [string]$run.Stderr) }
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
    finally {
        Close-HeldDirectory $logHold
    }
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

using Vdiff.Core.Extraction;
using Vdiff.Core.Process;

namespace Vdiff.Tests.Support;

/// <summary>
/// Builds the Rust sidecar once before the first test that needs it and points the test process
/// at the binary through VDIFF_CPP_TS. The build always runs (a no-op when the binary is
/// current): <c>cargo test</c> never rebuilds the release binary, so a stale sidecar would
/// otherwise hide later changes. A failed build fails the run; it never skips.
///
/// Rule for every test class: one that reaches the real sidecar (any use of
/// <c>CliApp.Run</c>, or of <c>CppSidecar.Extract</c> with the real binary) or that sets
/// process-wide environment variables carries <c>[Collection("sidecar")]</c>, so xunit runs
/// those classes one at a time after this build.
/// </summary>
public sealed class SidecarFixture
{
    const string BuildCommand = "cargo build --release --locked --manifest-path extractors/cpp-ts/Cargo.toml";

    public SidecarCommand Command { get; }

    public SidecarFixture()
    {
        var root = FindRepositoryRoot();
        var limits = new ProcessLimits(TimeSpan.FromMinutes(10), 64L * 1024 * 1024, 64 * 1024);
        ProcessResult result;
        try
        {
            result = BoundedProcess.Run(
                "cargo",
                ["build", "--release", "--locked", "--manifest-path", "extractors/cpp-ts/Cargo.toml"],
                limits,
                workingDirectory: root);
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"`{BuildCommand}` could not run: {e.Message}", e);
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"`{BuildCommand}` failed (exit {result.ExitCode}):\n{result.StderrTail}");
        }

        // Locate() walks up to the folder holding extractors/cpp-ts/Cargo.toml and takes the release build.
        Command = CppSidecar.Locate(AppContext.BaseDirectory, _ => null);
        Environment.SetEnvironmentVariable("VDIFF_CPP_TS", Command.File);
    }

    static string FindRepositoryRoot()
    {
        for (var dir = Path.GetFullPath(AppContext.BaseDirectory); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "extractors", "cpp-ts", "Cargo.toml")))
            {
                return dir;
            }
        }

        throw new InvalidOperationException("could not find extractors/cpp-ts/Cargo.toml above the test binaries");
    }
}

[CollectionDefinition("sidecar")]
public sealed class SidecarCollection : ICollectionFixture<SidecarFixture>
{
}

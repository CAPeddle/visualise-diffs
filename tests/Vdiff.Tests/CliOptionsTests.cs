using Vdiff.Core;
using Vdiff.Core.Cli;
using Vdiff.Tests.Support;

namespace Vdiff.Tests;

/// <summary>The command line surface (D-03): arguments are checked before git or the file system is touched.</summary>
[Collection("sidecar")]
public class CliOptionsTests
{
    const string Hex = "0123456789abcdef0123456789abcdef01234567";

    static string[] Valid(params string[] extra) =>
        ["--repo", "r", "--base", Hex, "--head", "abcdef1", "--out", "o", .. extra];

    [Fact]
    public void Help_prints_usage_to_stdout_and_exits_zero()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = CliApp.Run(["--help"], stdout, stderr);

        Assert.Equal(0, exit);
        Assert.Contains("--repo", stdout.ToString());
        Assert.Equal("", stderr.ToString());
    }

    [Fact]
    public void Valid_arguments_parse_with_repeatable_blank_and_include()
    {
        var options = CliOptions.Parse(Valid("--blank", "EXPORT_API", "--blank", "_X1", "--include", "include/**"));

        Assert.Equal("r", options.Repo);
        Assert.Equal(Hex, options.Base);
        Assert.Equal("abcdef1", options.Head);
        Assert.Equal("o", options.Out);
        Assert.Equal(["EXPORT_API", "_X1"], options.Blank);
        Assert.Equal(["include/**"], options.Include);
        Assert.False(options.Help);
    }

    [Theory]
    [InlineData("--repo")]
    [InlineData("--base")]
    [InlineData("--head")]
    [InlineData("--out")]
    public void A_missing_required_flag_is_an_error(string flag)
    {
        var args = Valid().ToList();
        var at = args.IndexOf(flag);
        args.RemoveRange(at, 2);

        var error = Assert.Throws<VdiffException>(() => CliOptions.Parse(args));
        Assert.Contains(flag, error.Message);
    }

    [Theory]
    [InlineData("--base", "main")]
    [InlineData("--base", "abc12")]
    [InlineData("--head", "--upload-pack=x")]
    [InlineData("--head", "abcdefg")]
    public void A_commit_id_must_be_seven_to_sixty_four_hexadecimal_characters(string flag, string value)
    {
        var args = Valid().ToList();
        args[args.IndexOf(flag) + 1] = value;

        Assert.Throws<VdiffException>(() => CliOptions.Parse(args));
    }

    [Fact]
    public void Unknown_duplicate_and_valueless_arguments_are_errors()
    {
        Assert.Throws<VdiffException>(() => CliOptions.Parse(Valid("--verbose")));
        Assert.Throws<VdiffException>(() => CliOptions.Parse(Valid("--out", "again")));
        Assert.Throws<VdiffException>(() => CliOptions.Parse(["--repo"]));
        Assert.Throws<VdiffException>(() => CliOptions.Parse(["--repo", "--base", Hex]));
    }

    [Fact]
    public void Blank_names_must_be_ascii_identifiers_and_limits_hold()
    {
        Assert.Throws<VdiffException>(() => CliOptions.Parse(Valid("--blank", "1ABC")));
        Assert.Throws<VdiffException>(() => CliOptions.Parse(Valid("--blank", "A-B")));
        Assert.Throws<VdiffException>(() => CliOptions.Parse(Valid("--blank", "ÄX")));
        Assert.Throws<VdiffException>(() =>
            CliOptions.Parse(Valid([.. Enumerable.Range(0, 257).SelectMany(i => new[] { "--blank", "M" + i })])));
        Assert.Throws<VdiffException>(() =>
            CliOptions.Parse(Valid([.. Enumerable.Range(0, 65).SelectMany(i => new[] { "--include", "g" + i })])));
        Assert.Throws<VdiffException>(() => CliOptions.Parse(Valid("--include", new string('a', 513))));
        Assert.Throws<VdiffException>(() => CliOptions.Parse(Valid("--include", "a\0b")));
        Assert.Throws<VdiffException>(() => CliOptions.Parse(Valid("--include", "")));
    }

    [Fact]
    public void A_failure_exits_one_with_a_vdiff_message_on_stderr_and_nothing_on_stdout()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = CliApp.Run(["--repo", "r"], stdout, stderr);

        Assert.Equal(1, exit);
        Assert.StartsWith("vdiff: ", stderr.ToString());
        Assert.Equal("", stdout.ToString());
    }

    [Fact]
    public void Containment_is_checked_on_directory_boundaries()
    {
        var root = Path.Combine(Path.GetTempPath(), "vdiff-contain", "repo");

        Assert.True(CliApp.IsSameOrUnder(root, root));
        Assert.True(CliApp.IsSameOrUnder(Path.Combine(root, "out"), root));
        Assert.True(CliApp.IsSameOrUnder(Path.Combine(root, "a", "..", "b"), root));
        Assert.False(CliApp.IsSameOrUnder(root + "-out", root));
        Assert.False(CliApp.IsSameOrUnder(Path.Combine(root, "..", "out"), root));
        Assert.Equal(OperatingSystem.IsWindows(), CliApp.IsSameOrUnder(root.ToUpperInvariant() + Path.DirectorySeparatorChar + "x", root));
    }
}

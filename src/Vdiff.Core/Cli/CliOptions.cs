using System.Text.RegularExpressions;
using Vdiff.Core.Git;

namespace Vdiff.Core.Cli;

/// <summary>
/// The command line of <c>vdiff</c> (D-03): <c>--repo --base --head --out</c> once each,
/// <c>--blank</c> and <c>--include</c> repeatable, and <c>--help</c>. Every other argument is an
/// error, and every value is checked here before anything touches git or the file system (T-01-01).
/// </summary>
public sealed partial record CliOptions(
    string Repo,
    string Base,
    string Head,
    string Out,
    IReadOnlyList<string> Blank,
    IReadOnlyList<string> Include,
    bool Help)
{
    public const int MaxBlankNames = 256;
    public const int MaxBlankNameLength = 128;
    public const int MaxIncludeGlobs = 64;
    public const int MaxGlobLength = 512;

    public const string Usage =
        "usage: vdiff --repo <path> --base <commit> --head <commit> --out <dir> [--blank <MACRO>]... [--include <glob>]...\n" +
        "\n" +
        "  --repo <path>      the git repository to analyse (read only; nothing is checked out)\n" +
        "  --base <commit>    the base commit, 7 to 64 hexadecimal characters\n" +
        "  --head <commit>    the head commit, 7 to 64 hexadecimal characters\n" +
        "  --out <dir>        where the six output files are written; must lie outside the repository\n" +
        "  --blank <MACRO>    blank this export macro name before parsing (repeatable, at most 256)\n" +
        "  --include <glob>   only analyse changed C++ files matching this glob (repeatable, at most 64)\n" +
        "  --help             print this text\n" +
        "\n" +
        "Writes base.model.json, head.model.json, diff.json, summary.mmd, report.html and run.meta.json under --out.\n" +
        "Exit code 0 on success, 1 on failure.\n";

    public static CliOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Any(a => a == "--help"))
        {
            return new CliOptions("", "", "", "", [], [], Help: true);
        }

        var single = new Dictionary<string, string>(StringComparer.Ordinal);
        var blank = new List<string>();
        var include = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (name is not ("--repo" or "--base" or "--head" or "--out" or "--blank" or "--include"))
            {
                throw new VdiffException($"unknown argument '{Printable(name)}'; try --help");
            }

            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new VdiffException($"{name} needs a value");
            }

            var value = args[++i];
            switch (name)
            {
                case "--blank":
                    blank.Add(value);
                    break;
                case "--include":
                    include.Add(value);
                    break;
                default:
                    if (!single.TryAdd(name, value))
                    {
                        throw new VdiffException($"{name} may be given only once");
                    }

                    break;
            }
        }

        foreach (var required in new[] { "--repo", "--base", "--head", "--out" })
        {
            if (!single.ContainsKey(required))
            {
                throw new VdiffException($"{required} is required; try --help");
            }
        }

        foreach (var (name, value) in single)
        {
            if (value.Length == 0 || value.Contains('\0'))
            {
                throw new VdiffException($"{name} needs a non-empty value without NUL characters");
            }
        }

        GitObjectReader.ValidateCommitInput(single["--base"]);
        GitObjectReader.ValidateCommitInput(single["--head"]);

        if (blank.Count > MaxBlankNames)
        {
            throw new VdiffException($"at most {MaxBlankNames} --blank names are allowed");
        }

        foreach (var macro in blank)
        {
            if (macro.Length > MaxBlankNameLength || !IdentifierPattern().IsMatch(macro))
            {
                throw new VdiffException($"a --blank name must be an ASCII identifier of at most {MaxBlankNameLength} characters (letters, digits and underscore, not starting with a digit)");
            }
        }

        if (include.Count > MaxIncludeGlobs)
        {
            throw new VdiffException($"at most {MaxIncludeGlobs} --include globs are allowed");
        }

        foreach (var glob in include)
        {
            if (glob.Length == 0 || glob.Length > MaxGlobLength || glob.Contains('\0'))
            {
                throw new VdiffException($"an --include glob must be 1 to {MaxGlobLength} characters without NUL characters");
            }
        }

        return new CliOptions(single["--repo"], single["--base"], single["--head"], single["--out"], blank, include, Help: false);
    }

    // Echoes an unknown argument without letting control characters through, and not at full length.
    static string Printable(string text)
    {
        var shown = text.Length > 40 ? text[..40] + "..." : text;
        return new string(shown.Select(c => char.IsControl(c) ? '?' : c).ToArray());
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
}

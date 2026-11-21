using System.Reflection;

namespace Vdiff.Core.Model;

/// <summary>One tool an extractor ran on, with the version it reported.</summary>
public sealed record ToolVersion(string Tool, string Version);

/// <summary>One extractor that contributed to the run and the tool versions it reported.</summary>
public sealed record ExtractorRun(string Name, IReadOnlyList<ToolVersion> Tools);

/// <summary>
/// Facts that vary between runs over identical trees (commit ids, tool versions). Never part of the
/// canonical files; written to run.meta.json.
/// </summary>
public sealed record RunMeta(
    string Schema,
    string VdiffVersion,
    string BaseCommit,
    string HeadCommit,
    IReadOnlyList<ExtractorRun> Extractors)
{
    public const string SchemaName = "vdiff-run-meta/v1";

    /// <summary>
    /// The informational version of this assembly without any build metadata (the text after the
    /// first '+'), or 0.0.0 when the assembly carries none.
    /// </summary>
    public static string CurrentVdiffVersion
    {
        get
        {
            var informational = typeof(RunMeta).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(informational))
            {
                return "0.0.0";
            }

            var plus = informational.IndexOf('+');
            var version = plus < 0 ? informational : informational[..plus];
            return version.Length == 0 ? "0.0.0" : version;
        }
    }
}

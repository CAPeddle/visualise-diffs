using System.Text.RegularExpressions;
using Vdiff.Tests.Support;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Vdiff.Tests.Ci;

/// <summary>
/// Static guard over <c>azure-pipelines.yml</c> (D-09, D15). The pipeline cannot run until an
/// organisation is connected, so its shape is checked here: Microsoft-hosted images only, no
/// target identifiers, no token or persisted credentials, locked cargo and script-free npm.
/// </summary>
[Trait("Category", "Contract")]
public sealed class PipelineYamlTests
{
    static readonly string[] AllowedImages =
    [
        "windows-2025",
        "windows-2025-vs2026",
        "windows-latest",
        "ubuntu-24.04",
        "ubuntu-26.04",
        "ubuntu-latest",
    ];

    static readonly string[] ForbiddenKeys =
    [
        "organization",
        "project",
        "serviceConnection",
        "azureSubscription",
        "connectedServiceName",
        "group",
    ];

    static readonly string[] ScriptKeys = ["script", "bash", "pwsh", "powershell"];

    const string ExactNpm = "npm --prefix tools/mermaid-check ci --ignore-scripts";
    const string TemplateImage = "$(imageName)";
    const string ReportVariable = "VDIFF_FIXTURE_REPORT";
    const string MermaidFilter = "Category!=MermaidCli";

    static readonly Regex StatementSplit = new(@"\r?\n|&&|\|\||;", RegexOptions.Compiled);
    static readonly Regex CargoCommand =
        new(@"\bcargo\s+(build|test|check|run|install|bench|clippy|doc)\b", RegexOptions.Compiled);
    static readonly Regex NpmCommand = new(@"(^|\s)npm(\s|$)", RegexOptions.Compiled);

    /// <summary>
    /// Returns one message per rule violation; each starts with a rule tag and a colon.
    /// An empty list means the pipeline text passes every rule.
    /// </summary>
    public static IReadOnlyList<string> PipelineProblems(string yamlText)
    {
        var problems = new List<string>();

        if (yamlText.Contains("System.AccessToken", StringComparison.OrdinalIgnoreCase)
            || yamlText.Contains("SYSTEM_ACCESSTOKEN", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add("token: the pipeline text mentions the System access token");
        }

        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yamlText));
        }
        catch (YamlException ex)
        {
            problems.Add($"parse: the YAML does not parse: {ex.Message}");
            return problems;
        }

        if (stream.Documents.Count != 1)
        {
            problems.Add($"parse: expected exactly one YAML document, found {stream.Documents.Count}");
            return problems;
        }

        var matrixImages = new List<string>();
        var reportStepSeen = false;
        Walk(stream.Documents[0].RootNode, problems, matrixImages, ref reportStepSeen);

        var windows = matrixImages.Count(i => i.StartsWith("windows-", StringComparison.Ordinal));
        var ubuntu = matrixImages.Count(i => i.StartsWith("ubuntu-", StringComparison.Ordinal));
        if (windows != 1 || ubuntu != 1)
        {
            problems.Add($"matrix: expected exactly one windows- and one ubuntu- leg, found {windows} and {ubuntu}");
        }

        if (!reportStepSeen)
        {
            problems.Add($"fixture-report: no step sets {ReportVariable}");
        }

        return problems;
    }

    static void Walk(YamlNode node, List<string> problems, List<string> matrixImages, ref bool reportStepSeen)
    {
        switch (node)
        {
            case YamlMappingNode map:
                CheckMapping(map, problems, matrixImages, ref reportStepSeen);
                foreach (var pair in map.Children)
                {
                    Walk(pair.Value, problems, matrixImages, ref reportStepSeen);
                }

                break;
            case YamlSequenceNode seq:
                foreach (var child in seq.Children)
                {
                    Walk(child, problems, matrixImages, ref reportStepSeen);
                }

                break;
        }
    }

    static void CheckMapping(
        YamlMappingNode map, List<string> problems, List<string> matrixImages, ref bool reportStepSeen)
    {
        foreach (var pair in map.Children)
        {
            if (pair.Key is not YamlScalarNode keyNode || keyNode.Value is null)
            {
                problems.Add("parse: a mapping key is not a plain scalar");
                continue;
            }

            var key = keyNode.Value;

            if (ForbiddenKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add($"identifier: key '{key}' names an organisation, project, connection or variable group");
            }

            switch (key)
            {
                case "vmImage" or "imageName":
                    CheckImage(key, pair.Value, problems);
                    break;
                case "pool":
                    CheckPool(pair.Value, problems);
                    break;
                case "matrix":
                    CollectMatrixImages(pair.Value, matrixImages);
                    break;
                case "checkout":
                    CheckCheckout(map, problems);
                    break;
            }

            if (ScriptKeys.Contains(key) && pair.Value is YamlScalarNode script)
            {
                CheckScript(script.Value ?? string.Empty, problems);
            }
        }

        if (map.Children.TryGetValue(new YamlScalarNode("env"), out var env)
            && env is YamlMappingNode envMap
            && envMap.Children.ContainsKey(new YamlScalarNode(ReportVariable)))
        {
            reportStepSeen = true;
            var text = ScriptKeys
                .Select(k => map.Children.TryGetValue(new YamlScalarNode(k), out var v) ? (v as YamlScalarNode)?.Value : null)
                .FirstOrDefault(v => v is not null) ?? string.Empty;
            if (!text.Contains(MermaidFilter, StringComparison.Ordinal))
            {
                problems.Add($"fixture-report: the step that sets {ReportVariable} lacks the {MermaidFilter} filter");
            }
        }
    }

    static void CheckImage(string key, YamlNode value, List<string> problems)
    {
        if (value is not YamlScalarNode { Value: { } image })
        {
            problems.Add($"image: '{key}' is not a plain scalar");
            return;
        }

        var allowed = image == TemplateImage || AllowedImages.Contains(image, StringComparer.Ordinal);
        if (!allowed)
        {
            problems.Add($"image: '{image}' is not a Microsoft-hosted image label on the allow-list");
        }
    }

    static void CheckPool(YamlNode value, List<string> problems)
    {
        if (value is YamlScalarNode)
        {
            problems.Add("pool: the pool is given as a name");
        }
        else if (value is YamlMappingNode poolMap
                 && poolMap.Children.Keys.OfType<YamlScalarNode>()
                     .Any(k => string.Equals(k.Value, "name", StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add("pool: the pool mapping has a 'name' key");
        }
    }

    static void CollectMatrixImages(YamlNode matrix, List<string> images)
    {
        if (matrix is not YamlMappingNode legs)
        {
            return;
        }

        foreach (var leg in legs.Children.Values.OfType<YamlMappingNode>())
        {
            foreach (var pair in leg.Children)
            {
                if (pair.Key is YamlScalarNode { Value: "imageName" } && pair.Value is YamlScalarNode { Value: { } image })
                {
                    images.Add(image);
                }
            }
        }
    }

    static void CheckCheckout(YamlMappingNode step, List<string> problems)
    {
        var persists = step.Children.TryGetValue(new YamlScalarNode("persistCredentials"), out var value)
                       && value is YamlScalarNode { Value: "false" };
        if (!persists)
        {
            problems.Add("checkout: a checkout step does not set persistCredentials: false");
        }
    }

    static void CheckScript(string script, List<string> problems)
    {
        foreach (var raw in StatementSplit.Split(script))
        {
            var statement = raw.Trim();
            if (statement.Length == 0 || statement.StartsWith('#'))
            {
                continue;
            }

            if (CargoCommand.IsMatch(statement) && !statement.Contains("--locked", StringComparison.Ordinal))
            {
                problems.Add($"cargo: '{statement}' runs without --locked");
            }

            if (NpmCommand.IsMatch(statement) && statement != ExactNpm)
            {
                problems.Add($"npm: '{statement}' is not exactly '{ExactNpm}'");
            }
        }
    }

    [Fact]
    public void Committed_pipeline_has_no_problems()
    {
        var path = Path.Combine(RepoRoot.Find(), "azure-pipelines.yml");
        Assert.True(File.Exists(path), "azure-pipelines.yml is missing from the repository root");

        var problems = PipelineProblems(File.ReadAllText(path));

        Assert.True(problems.Count == 0, "azure-pipelines.yml problems:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Committed_pipeline_is_lf_only_and_ends_with_a_line_feed()
    {
        var path = Path.Combine(RepoRoot.Find(), "azure-pipelines.yml");
        Assert.True(File.Exists(path), "azure-pipelines.yml is missing from the repository root");

        var bytes = File.ReadAllBytes(path);

        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.NotEmpty(bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
    }

    [Fact]
    public void Baseline_pipeline_passes_every_rule()
    {
        Assert.Empty(PipelineProblems(Baseline));
    }

    public static TheoryData<string, string, string, string> NegativeCases => new()
    {
        // name, text to replace in the baseline, replacement, expected rule tag
        { "self-hosted image label", "imageName: ubuntu-24.04", "imageName: self-hosted-agent", "image" },
        { "pool with a name key", "vmImage: $(imageName)", "name: BuildPool", "pool" },
        { "pool given as a bare name", "pool:\n    vmImage: $(imageName)", "pool: BuildPool", "pool" },
        { "variable group", "jobs:", "variables:\n- group: shared-secrets\njobs:", "identifier" },
        { "organization key", "jobs:", "organization: contoso\njobs:", "identifier" },
        { "service connection key", "jobs:", "serviceConnection: conn\njobs:", "identifier" },
        {
            "System.AccessToken",
            "VDIFF_FIXTURE_REPORT: $(Build.ArtifactStagingDirectory)/fixture-report.md",
            "VDIFF_FIXTURE_REPORT: $(Build.ArtifactStagingDirectory)/fixture-report.md\n      GIT_TOKEN: $(System.AccessToken)",
            "token"
        },
        { "persisted checkout credentials", "persistCredentials: false", "persistCredentials: true", "checkout" },
        { "checkout without persistCredentials", "\n    persistCredentials: false", "", "checkout" },
        { "cargo build without --locked", "cargo build --release --locked", "cargo build --release", "cargo" },
        { "cargo test without --locked", "cargo test --locked", "cargo test", "cargo" },
        { "npm install", ExactNpm, "npm install", "npm" },
        { "npm ci without --ignore-scripts", ExactNpm, "npm --prefix tools/mermaid-check ci", "npm" },
        { "test step without the MermaidCli filter", MermaidFilter, "Category=Unit", "fixture-report" },
        { "two Windows legs and no Linux leg", "imageName: ubuntu-24.04", "imageName: windows-2025-vs2026", "matrix" },
    };

    [Theory]
    [MemberData(nameof(NegativeCases))]
    public void Each_rule_fires_on_a_single_defect(string name, string find, string replace, string rule)
    {
        var mutated = Baseline.Replace(find, replace, StringComparison.Ordinal);
        Assert.NotEqual(Baseline, mutated);

        var problems = PipelineProblems(mutated);

        Assert.True(
            problems.Any(p => p.StartsWith(rule + ":", StringComparison.Ordinal)),
            $"case '{name}' did not raise a '{rule}' problem; got: [{string.Join(" | ", problems)}]");
    }

    static readonly string Baseline = """
        jobs:
        - job: build_and_test
          strategy:
            matrix:
              windows:
                imageName: windows-2025
              linux:
                imageName: ubuntu-24.04
          pool:
            vmImage: $(imageName)
          steps:
          - checkout: self
            persistCredentials: false
          - script: cargo build --release --locked --manifest-path extractors/cpp-ts/Cargo.toml
          - script: cargo test --locked --manifest-path extractors/cpp-ts/Cargo.toml
          - script: >
              dotnet test vdiff.sln -c Release --no-build
              --filter "Category!=MermaidCli"
            env:
              VDIFF_FIXTURE_REPORT: $(Build.ArtifactStagingDirectory)/fixture-report.md
          - script: npm --prefix tools/mermaid-check ci --ignore-scripts
        """.ReplaceLineEndings("\n") + "\n";
}

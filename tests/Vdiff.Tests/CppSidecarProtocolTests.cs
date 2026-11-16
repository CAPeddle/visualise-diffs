using System.Text;
using Vdiff.Core;
using Vdiff.Core.Extraction;

namespace Vdiff.Tests;

/// <summary>
/// The sidecar client, tested on canned bytes and on stand-in child processes (git with a
/// shell alias), never on the real sidecar binary: that is built in parallel by plan 01-01 and
/// exercised end to end by plan 01-03.
/// </summary>
public class CppSidecarProtocolTests
{
    const string Canned = """
        {"schema":"vdiff-cpp-ts/v0","tree_sitter":"0.27.0","tree_sitter_cpp":"0.23.4","macros_blanked":["EXPORT_API"],
         "files":[
          {"path":"a.h","status":"ok","error_nodes":1,"future_file_field":true,"types":[
            {"kind":"class","name":"Widget","scope":"shop::billing","bases":["Base"],"start_byte":0,"end_byte":40,"start_line":1,"end_line":4,
             "future_type_field":"x",
             "members":[{"name":"size","kind":"method","access":"public","key":"size() const","signature":"int size() const",
                         "start_byte":10,"end_byte":25,"start_line":2,"end_line":2}]}]},
          {"path":"b.h","status":"timeout","error_nodes":0,"types":[]},
          {"path":"c.h","status":"error","error_nodes":3,"types":[]}],
         "future_top_field":[1,2]}
        """;

    static string BinaryName => "vdiff-cpp-ts" + (OperatingSystem.IsWindows() ? ".exe" : "");

    static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vdiff-sc-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public string Touch(params string[] parts)
        {
            var full = System.IO.Path.Combine([Path, .. parts]);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, []);
            return full;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    // ---- BuildRequest ----

    [Fact]
    public void BuildRequest_is_one_header_line_then_the_raw_file_bytes_back_to_back()
    {
        var first = new byte[] { 1, 2, 3 };
        var second = Utf8("ünï\n");
        var files = new[] { new SidecarInput("src/a.h", first), new SidecarInput("dir/é.cpp", second) };

        var request = CppSidecar.BuildRequest(files, ["EXPORT_API", "DECLARE_X"], TimeSpan.FromSeconds(10));

        var newline = Array.IndexOf(request, (byte)'\n');
        var header = Encoding.UTF8.GetString(request, 0, newline);
        Assert.Equal(
            "{\"schema\":\"vdiff-cpp-ts-request/v0\",\"blank\":[\"EXPORT_API\",\"DECLARE_X\"],\"parse_budget_ms\":10000," +
            "\"files\":[{\"path\":\"src/a.h\",\"len\":3},{\"path\":\"dir/é.cpp\",\"len\":" + second.Length + "}]}",
            header);
        Assert.NotEqual("dir/é.cpp".Length, second.Length); // the length is of the content, not of the path
        Assert.Equal(first.Concat(second).ToArray(), request[(newline + 1)..]);
    }

    [Fact]
    public void BuildRequest_with_no_files_is_just_the_header_line()
    {
        var request = CppSidecar.BuildRequest([], [], TimeSpan.FromMilliseconds(250));

        Assert.Equal(
            "{\"schema\":\"vdiff-cpp-ts-request/v0\",\"blank\":[],\"parse_budget_ms\":250,\"files\":[]}\n",
            Encoding.UTF8.GetString(request));
    }

    // ---- ParseResponse ----

    [Fact]
    public void ParseResponse_reads_types_members_status_and_versions_and_ignores_unknown_fields()
    {
        var response = CppSidecar.ParseResponse(Utf8(Canned), 1024 * 1024);

        Assert.Equal("vdiff-cpp-ts/v0", response.Schema);
        Assert.Equal("0.27.0", response.TreeSitter);
        Assert.Equal("0.23.4", response.TreeSitterCpp);
        Assert.Equal(["EXPORT_API"], response.MacrosBlanked);
        Assert.Equal(["a.h", "b.h", "c.h"], response.Files!.Select(f => f.Path));
        Assert.Equal(["ok", "timeout", "error"], response.Files!.Select(f => f.Status));
        Assert.Equal([1, 0, 3], response.Files!.Select(f => f.ErrorNodes));
        var type = Assert.Single(response.Files![0].Types);
        Assert.Equal(("class", "Widget", "shop::billing"), (type.Kind, type.Name, type.Scope));
        Assert.Equal(["Base"], type.Bases);
        Assert.Equal((0, 40, 1, 4), (type.StartByte, type.EndByte, type.StartLine, type.EndLine));
        var member = Assert.Single(type.Members);
        Assert.Equal(("size", "method", "public", "size() const", "int size() const"),
            (member.Name, member.Kind, member.Access, member.Key, member.Signature));
        Assert.Equal((10, 25, 2, 2), (member.StartByte, member.EndByte, member.StartLine, member.EndLine));
    }

    [Fact]
    public void ParseResponse_returns_a_timed_out_file_with_its_status_instead_of_dropping_it()
    {
        var response = CppSidecar.ParseResponse(Utf8(Canned), 1024 * 1024);

        var timedOut = Assert.Single(response.Files!, f => f.Path == "b.h");
        Assert.Equal("timeout", timedOut.Status);
        Assert.Empty(timedOut.Types);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{\"schema\":")]
    [InlineData("[]")]
    public void ParseResponse_rejects_empty_or_malformed_json(string text)
    {
        Assert.Throws<VdiffException>(() => CppSidecar.ParseResponse(Utf8(text), 1024));
    }

    [Fact]
    public void ParseResponse_rejects_a_wrong_schema()
    {
        var wrong = Canned.Replace("vdiff-cpp-ts/v0", "vdiff-cpp-ts/v1");

        var e = Assert.Throws<VdiffException>(() => CppSidecar.ParseResponse(Utf8(wrong), 1024 * 1024));

        Assert.Contains("schema", e.Message);
    }

    [Fact]
    public void ParseResponse_rejects_a_response_without_files()
    {
        var noFiles = "{\"schema\":\"vdiff-cpp-ts/v0\",\"tree_sitter\":\"0.27.0\",\"tree_sitter_cpp\":\"0.23.4\",\"macros_blanked\":[]}";

        Assert.Throws<VdiffException>(() => CppSidecar.ParseResponse(Utf8(noFiles), 1024));
    }

    [Fact]
    public void ParseResponse_rejects_a_file_status_that_is_not_ok_timeout_or_error()
    {
        var unknown = Canned.Replace("\"status\":\"timeout\"", "\"status\":\"parse-timeout\"");

        var e = Assert.Throws<VdiffException>(() => CppSidecar.ParseResponse(Utf8(unknown), 1024 * 1024));

        Assert.Contains("status", e.Message);
    }

    [Fact]
    public void ParseResponse_rejects_a_file_with_no_status()
    {
        var noStatus = Canned.Replace("\"status\":\"error\",", "");

        Assert.Throws<VdiffException>(() => CppSidecar.ParseResponse(Utf8(noStatus), 1024 * 1024));
    }

    [Fact]
    public void ParseResponse_rejects_a_response_larger_than_the_limit()
    {
        var bytes = Utf8(Canned);

        Assert.Throws<VdiffException>(() => CppSidecar.ParseResponse(bytes, bytes.Length - 1));
        CppSidecar.ParseResponse(bytes, bytes.Length);
    }

    // ---- Locate ----

    [Fact]
    public void Locate_prefers_the_path_in_VDIFF_CPP_TS()
    {
        using var dir = new TempDir();
        var custom = dir.Touch("custom", "my-sidecar");
        dir.Touch("app", BinaryName); // would also be found, but the variable wins

        var command = CppSidecar.Locate(Path.Combine(dir.Path, "app"), name => name == "VDIFF_CPP_TS" ? custom : null);

        Assert.Equal(custom, command.File);
        Assert.Empty(command.LeadingArgs);
    }

    [Fact]
    public void Locate_fails_naming_the_variable_when_VDIFF_CPP_TS_points_at_nothing()
    {
        using var dir = new TempDir();

        var e = Assert.Throws<VdiffException>(() =>
            CppSidecar.Locate(dir.Path, name => name == "VDIFF_CPP_TS" ? Path.Combine(dir.Path, "missing") : null));

        Assert.Contains("VDIFF_CPP_TS", e.Message);
    }

    [Fact]
    public void Locate_finds_the_binary_beside_the_app()
    {
        using var dir = new TempDir();
        var beside = dir.Touch("app", BinaryName);
        dir.Touch("app", "extractors", "cpp-ts", BinaryName);

        var command = CppSidecar.Locate(Path.Combine(dir.Path, "app"), _ => null);

        Assert.Equal(beside, command.File);
    }

    [Fact]
    public void Locate_finds_extractors_cpp_ts_beside_the_app()
    {
        using var dir = new TempDir();
        var nested = dir.Touch("app", "extractors", "cpp-ts", BinaryName);

        var command = CppSidecar.Locate(Path.Combine(dir.Path, "app"), _ => null);

        Assert.Equal(nested, command.File);
    }

    [Fact]
    public void Locate_walks_up_to_the_cargo_release_build_of_the_repository()
    {
        using var dir = new TempDir();
        dir.Touch("extractors", "cpp-ts", "Cargo.toml");
        var built = dir.Touch("extractors", "cpp-ts", "target", "release", BinaryName);
        var deep = Path.Combine(dir.Path, "src", "Vdiff.Cli", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(deep);

        var command = CppSidecar.Locate(deep, _ => null);

        Assert.Equal(built, command.File);
    }

    [Fact]
    public void Locate_fails_with_the_cargo_command_when_nothing_is_found()
    {
        using var dir = new TempDir();
        dir.Touch("extractors", "cpp-ts", "Cargo.toml"); // source present, never built

        var e = Assert.Throws<VdiffException>(() => CppSidecar.Locate(dir.Path, _ => null));

        Assert.Contains("cargo build --release --locked --manifest-path extractors/cpp-ts/Cargo.toml", e.Message);
        Assert.Contains("VDIFF_CPP_TS", e.Message);
    }

    // ---- Extract (stand-in child processes) ----

    static SidecarCommand Shell(string script) => new("git", ["-c", "alias.fake=!" + script, "fake"]);

    static readonly SidecarLimits Quick = new(TimeSpan.FromSeconds(30), 1024 * 1024, 64 * 1024, TimeSpan.FromSeconds(10));

    [Fact]
    public void Extract_treats_a_non_zero_exit_as_a_failure_carrying_the_stderr_but_no_file_content()
    {
        var secret = new SidecarInput("a.h", Utf8("TOP-SECRET-FILE-CONTENT"));

        var e = Assert.Throws<VdiffException>(() =>
            CppSidecar.Extract([secret], [], Shell("cat > /dev/null; echo 'sidecar refused the request' >&2; exit 2"), Quick));

        Assert.Contains("exit", e.Message);
        Assert.Contains("sidecar refused the request", e.Message);
        Assert.DoesNotContain("TOP-SECRET", e.Message);
    }

    [Fact]
    public void Extract_treats_empty_output_as_a_failure()
    {
        var e = Assert.Throws<VdiffException>(() =>
            CppSidecar.Extract([], [], Shell("cat > /dev/null; exit 0"), Quick));

        Assert.Contains("no output", e.Message);
    }

    [Fact]
    public void Extract_treats_a_timeout_as_a_failure()
    {
        var limits = Quick with { Timeout = TimeSpan.FromSeconds(1) };

        Assert.Throws<VdiffException>(() => CppSidecar.Extract([], [], Shell("sleep 60"), limits));
    }

    [Fact]
    public void Extract_runs_the_sidecar_even_with_zero_files_so_version_metadata_is_present()
    {
        using var dir = new TempDir();
        var stdinCopy = Path.Combine(dir.Path, "stdin.bin").Replace('\\', '/');
        var empty = "{\"schema\":\"vdiff-cpp-ts/v0\",\"tree_sitter\":\"0.27.0\",\"tree_sitter_cpp\":\"0.23.4\",\"macros_blanked\":[],\"files\":[]}";

        var response = CppSidecar.Extract([], [], Shell($"cat > \"{stdinCopy}\"; echo '{empty}'"), Quick);

        Assert.Equal("0.27.0", response.TreeSitter);
        Assert.Empty(response.Files!);
        Assert.True(File.Exists(stdinCopy));
    }

    [Fact]
    public void Extract_sends_the_exact_manifest_on_stdin_and_parses_the_answer()
    {
        using var dir = new TempDir();
        var stdinCopy = Path.Combine(dir.Path, "stdin.bin").Replace('\\', '/');
        var files = new[] { new SidecarInput("a.h", Utf8("class A {};\n")), new SidecarInput("b.cpp", Utf8("int x;\n")) };
        var answer = "{\"schema\":\"vdiff-cpp-ts/v0\",\"tree_sitter\":\"0.27.0\",\"tree_sitter_cpp\":\"0.23.4\",\"macros_blanked\":[\"M\"]," +
                     "\"files\":[{\"path\":\"a.h\",\"status\":\"ok\",\"error_nodes\":0,\"types\":[{\"kind\":\"class\",\"name\":\"A\"}]}," +
                     "{\"path\":\"b.cpp\",\"status\":\"timeout\",\"error_nodes\":0,\"types\":[]}]}";

        var response = CppSidecar.Extract(files, ["M"], Shell($"cat > \"{stdinCopy}\"; echo '{answer}'"), Quick);

        Assert.Equal(CppSidecar.BuildRequest(files, ["M"], Quick.ParseBudget), File.ReadAllBytes(stdinCopy));
        Assert.Equal(["ok", "timeout"], response.Files!.Select(f => f.Status));
        Assert.Equal("A", response.Files![0].Types[0].Name);
    }
}

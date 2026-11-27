using Vdiff.Core;
using Vdiff.Core.Scope;

namespace Vdiff.Tests.Scope;

/// <summary>The scope filter alone (D-10): extensions and --include globs, no repository.</summary>
public class ScopeFilterTests
{
    [Theory]
    [InlineData("a.h", true)]
    [InlineData("a.hpp", true)]
    [InlineData("a.hh", true)]
    [InlineData("a.hxx", true)]
    [InlineData("a.cpp", true)]
    [InlineData("a.cc", true)]
    [InlineData("a.cxx", true)]
    [InlineData("src/deep/A.CPP", true)]
    [InlineData("src/A.Hpp", true)]
    [InlineData("a.c", false)]
    [InlineData("a.cs", false)]
    [InlineData("a.hpp.bak", false)]
    [InlineData("a.txt", false)]
    [InlineData("Makefile", false)]
    [InlineData("dir.h/readme", false)]
    [InlineData("", false)]
    public void Only_the_cpp_extensions_are_in_scope_case_insensitively(string path, bool expected)
    {
        Assert.Equal(expected, new ScopeFilter().IsInScope(path));
    }

    [Fact]
    public void With_no_globs_every_in_scope_extension_qualifies_without_the_matcher()
    {
        // A Matcher with no include pattern matches nothing (C37), so null, empty and absent must all bypass it.
        Assert.True(new ScopeFilter().IsInScope("anywhere/x.h"));
        Assert.True(new ScopeFilter(null).IsInScope("anywhere/x.h"));
        Assert.True(new ScopeFilter([]).IsInScope("anywhere/x.h"));
    }

    [Theory]
    [InlineData("src/**", "src/a.cpp", true)]
    [InlineData("src/**", "src/deep/er/a.cpp", true)]
    [InlineData("src/**", "include/a.h", false)]
    [InlineData("**/*.h", "a.h", true)]
    [InlineData("**/*.h", "include/shop/a.h", true)]
    [InlineData("**/*.h", "src/a.cpp", false)]
    [InlineData("include/shop/*.h", "include/shop/a.h", true)]
    [InlineData("include/shop/*.h", "include/shop/deep/a.h", false)]
    public void A_glob_matches_the_way_file_system_globbing_documents(string glob, string path, bool expected)
    {
        Assert.Equal(expected, new ScopeFilter([glob]).IsInScope(path));
    }

    [Fact]
    public void A_star_pattern_is_not_recursive()
    {
        var filter = new ScopeFilter(["*.h"]);

        Assert.True(filter.IsInScope("a.h"));
        Assert.False(filter.IsInScope("sub/a.h"));
    }

    [Fact]
    public void Two_globs_are_an_or()
    {
        var filter = new ScopeFilter(["include/a/**", "src/*.cpp"]);

        Assert.True(filter.IsInScope("include/a/x.h"));
        Assert.True(filter.IsInScope("src/y.cpp"));
        Assert.False(filter.IsInScope("include/b/x.h"));
        Assert.False(filter.IsInScope("src/deep/y.cpp"));
    }

    [Fact]
    public void Glob_matching_is_ordinal_and_case_sensitive()
    {
        var filter = new ScopeFilter(["include/a/**"]);

        Assert.True(filter.IsInScope("include/a/x.h"));
        Assert.False(filter.IsInScope("Include/A/x.h"));
        Assert.False(filter.IsInScope("include/A/x.h"));
    }

    [Theory]
    [InlineData("./src/**", "src/a.cpp", true)]
    [InlineData("./src/**", "other/a.cpp", false)]
    [InlineData("././src/**", "src/a.cpp", true)]
    [InlineData("./*.h", "a.h", true)]
    [InlineData("./*.h", "sub/a.h", false)]
    public void A_leading_dot_slash_is_accepted(string glob, string path, bool expected)
    {
        Assert.Equal(expected, new ScopeFilter([glob]).IsInScope(path));
    }

    [Fact]
    public void The_extension_check_still_applies_when_a_glob_matches()
    {
        var filter = new ScopeFilter(["docs/**"]);

        Assert.False(filter.IsInScope("docs/readme.md"));
        Assert.True(filter.IsInScope("docs/api.h"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("./")]
    [InlineData(".//")]
    public void An_empty_pattern_is_rejected(string glob)
    {
        Assert.Throws<VdiffException>(() => new ScopeFilter(["src/**", glob]));
    }

    [Fact]
    public void The_order_of_the_globs_never_changes_a_decision()
    {
        string[] globs = ["src/**", "include/a/**", "**/*.hpp"];
        string[] paths = ["src/a.cpp", "include/a/x.h", "include/b/x.h", "lib/deep/y.hpp", "lib/y.cpp"];

        var forward = new ScopeFilter(globs);
        var backward = new ScopeFilter(globs.Reverse());

        Assert.Equal(paths.Select(forward.IsInScope), paths.Select(backward.IsInScope));
    }
}

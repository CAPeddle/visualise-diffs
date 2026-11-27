using System.Collections;
using System.Reflection;
using Vdiff.Core.Diff;
using Vdiff.Core.Extraction;
using Vdiff.Core.Model;
using Vdiff.Core.Output;

namespace Vdiff.Tests.Contract;

/// <summary>
/// Success criterion 4 (R-23): the same model, diff or run meta serialises to the same bytes whatever the order
/// of its collections. A seeded Fisher-Yates shuffle, driven by reflection over the v1 records, permutes every
/// list of the full samples; a guard fact proves each reachable list really was permuted, so the byte
/// comparison cannot pass because nothing moved. No property-testing package: the seeds are fixed.
/// </summary>
[Trait("Category", "Contract")]
public class CanonicalSerialisationTests
{
    const string ModelNamespace = "Vdiff.Core.Model";

    public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 25).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void A_seeded_shuffle_of_every_list_serialises_to_the_same_bytes(int seed)
    {
        // This theory's own throwaway set: nothing here is shared with the guard fact below.
        var permuted = new HashSet<string>(StringComparer.Ordinal);
        var random = new Random(seed);

        Assert.Equal(
            OutputWriter.Serialize(ContractSamples.FullModel()),
            OutputWriter.Serialize(Shuffle(ContractSamples.FullModel(), random, permuted)));
        Assert.Equal(
            OutputWriter.Serialize(ContractSamples.FullDiff()),
            OutputWriter.Serialize(Shuffle(ContractSamples.FullDiff(), random, permuted)));
        Assert.Equal(
            OutputWriter.Serialize(ContractSamples.FullMeta()),
            OutputWriter.Serialize(Shuffle(ContractSamples.FullMeta(), random, permuted)));
    }

    [Fact]
    public void The_25_seeds_permute_every_reachable_list()
    {
        // The fact runs the 25 seeds itself into a set local to it, so its verdict is the same run alone or in
        // any order (T-02-16).
        var permuted = new HashSet<string>(StringComparer.Ordinal);
        for (var seed = 1; seed <= 25; seed++)
        {
            var random = new Random(seed);
            Shuffle(ContractSamples.FullModel(), random, permuted);
            Shuffle(ContractSamples.FullDiff(), random, permuted);
            Shuffle(ContractSamples.FullMeta(), random, permuted);
        }

        var reachable = ListProperties();
        Assert.NotEmpty(reachable);
        foreach (var name in reachable)
        {
            Assert.True(
                permuted.Contains(name),
                $"{name} was never permuted by seeds 1 to 25: give ContractSamples at least two distinct elements here.");
        }

        // The walk must know about the lists the plan names, so an empty or short walk cannot pass the loop above.
        Assert.Contains("TypeModel.Files", reachable);
        Assert.Contains("MemberDiff.ModifiersBefore", reachable);
        Assert.Contains("MemberDiff.ModifiersAfter", reachable);
        Assert.Contains("RunMeta.Extractors", reachable);
        Assert.Contains("DiffFile.Edges", reachable);
    }

    static SidecarMember MemberOf(string key, int start) =>
        new() { Name = key, Kind = "method", Access = "public", Key = key, Signature = "int " + key, StartByte = start, EndByte = start + 4 };

    static SidecarType TypeOf(string name, int start, params SidecarMember[] members) =>
        new() { Kind = "class", Name = name, Scope = "shop", StartByte = start, EndByte = start + 50, Members = members };

    [Fact]
    public void The_builder_does_not_depend_on_the_order_the_sidecar_reports_types_and_members()
    {
        SidecarType[] forward =
        [
            TypeOf("A", 0, MemberOf("a1()", 5), MemberOf("a2()", 15)),
            TypeOf("B", 100, MemberOf("b1()", 105), MemberOf("b2()", 115)),
            TypeOf("C", 200, MemberOf("c1()", 205), MemberOf("c2()", 215)),
        ];
        SidecarType[] reversed =
        [
            .. forward.Reverse().Select(t => new SidecarType
            {
                Kind = t.Kind, Name = t.Name, Scope = t.Scope, StartByte = t.StartByte, EndByte = t.EndByte,
                Members = [.. t.Members.Reverse()],
            }),
        ];

        Assert.Equal(
            OutputWriter.Serialize(Build(forward)),
            OutputWriter.Serialize(Build(reversed)));
    }

    static ModelFile Build(SidecarType[] types) =>
        ModelBuilder.Build(
            new SidecarResponse
            {
                Schema = "vdiff-cpp-ts/v0",
                TreeSitter = "0.27.0",
                TreeSitterCpp = "0.23.4",
                Files = [new SidecarFile { Path = "src/a.h", Status = "ok", Types = types }],
            },
            [],
            []);

    [Fact]
    public void The_differ_does_not_depend_on_the_order_of_either_models_collections()
    {
        var b = ContractSamples.FullModel();
        var beta = b.Types.Single(t => t.Name == "Beta");
        var changedBeta = beta with
        {
            Members = [.. beta.Members.Select(m => m.Name == "Submit" ? m with { Signature = "public virtual async Task<int> Submit(Order order)" } : m)],
        };

        // Drop the Gamma type, change one member of Beta.
        var h = b with { Types = [changedBeta, .. b.Types.Where(t => t.Name == "Alpha")] };
        var expected = OutputWriter.Serialize(ModelDiffer.Diff(b, h));

        Assert.NotEqual(OutputWriter.Serialize(ModelDiffer.Diff(b, b)), expected);
        for (var seed = 1; seed <= 25; seed++)
        {
            var random = new Random(seed);
            var shuffled = ModelDiffer.Diff(
                Shuffle(b, random, new HashSet<string>(StringComparer.Ordinal)),
                Shuffle(h, random, new HashSet<string>(StringComparer.Ordinal)));
            Assert.Equal(expected, OutputWriter.Serialize(shuffled));
        }
    }

    [Fact]
    public void Elements_equal_on_every_canonical_key_serialise_the_same_in_either_input_order()
    {
        // R-10 ordering edge: every comparer ends with an ordinal compare of the element's JSON.
        var span = new SourceSpan(10, 70, 2, 5);
        TypeModel TypeNamed(string name) =>
            new(Languages.Cpp, "class t::X", "t::X", "class", name, "t", null, false, "a.h", null, [], [], span, []);
        var typeX = TypeNamed("X");
        var typeY = TypeNamed("Y");
        var emptyModel = ContractSamples.EmptyModel();
        Assert.Equal(
            OutputWriter.Serialize(emptyModel with { Types = [typeX, typeY] }),
            OutputWriter.Serialize(emptyModel with { Types = [typeY, typeX] }));

        var memberSpan = new SourceSpan(20, 30, 3, 3);
        MemberModel MemberSigned(string signature) =>
            new("f()", "f", "method", "public", [], signature, memberSpan);
        var memberX = MemberSigned("int f()");
        var memberY = MemberSigned("long f()");
        Assert.Equal(
            OutputWriter.Serialize(emptyModel with { Types = [typeX with { Members = [memberX, memberY] }] }),
            OutputWriter.Serialize(emptyModel with { Types = [typeX with { Members = [memberY, memberX] }] }));

        TypeDiff DiffNamed(string fqn) =>
            new(Languages.Cpp, "class t::X", fqn, "class", States.Added, null, "a.h", null, null, new BaseChanges([], []), []);
        var diffX = DiffNamed("t::X");
        var diffY = DiffNamed("u::X");
        var emptyDiff = ContractSamples.EmptyDiff();
        Assert.Equal(
            OutputWriter.Serialize(emptyDiff with { Types = [diffX, diffY] }),
            OutputWriter.Serialize(emptyDiff with { Types = [diffY, diffX] }));
    }

    // ---- reflection helpers ------------------------------------------------------------------------------

    static bool IsModelRecord(Type type) =>
        type.Namespace == ModelNamespace && type.GetMethod("<Clone>$") is not null;

    static IEnumerable<PropertyInfo> RecordProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name != "EqualityContract" && p.GetIndexParameters().Length == 0);

    static bool IsList(Type type, out Type element)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            element = type.GetGenericArguments()[0];
            return true;
        }

        element = typeof(object);
        return false;
    }

    /// <summary>Every <c>Record.Property</c> that is an <c>IReadOnlyList</c> on a record reachable from the three roots.</summary>
    static IReadOnlySet<string> ListProperties()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<Type>();
        Walk(typeof(ModelFile));
        Walk(typeof(DiffFile));
        Walk(typeof(RunMeta));
        return names;

        void Walk(Type record)
        {
            if (!IsModelRecord(record) || !visited.Add(record))
            {
                return;
            }

            foreach (var property in RecordProperties(record))
            {
                if (IsList(property.PropertyType, out var element))
                {
                    names.Add($"{record.Name}.{property.Name}");
                    Walk(element);
                }
                else
                {
                    Walk(property.PropertyType);
                }
            }
        }
    }

    /// <summary>
    /// Returns a copy of <paramref name="value"/> in which every list, at every depth, is permuted by a
    /// Fisher-Yates shuffle driven by <paramref name="random"/>; elements are shuffled first. A list whose
    /// order changed is recorded in <paramref name="permuted"/> as <c>Record.Property</c>.
    /// </summary>
    static T Shuffle<T>(T value, Random random, ISet<string> permuted) =>
        (T)ShuffleValue(value, random, permuted)!;

    static object? ShuffleValue(object? value, Random random, ISet<string> permuted)
    {
        if (value is null)
        {
            return null;
        }

        var type = value.GetType();
        if (!IsModelRecord(type))
        {
            return value;
        }

        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single(c => c.GetParameters().All(p => RecordProperties(type).Any(r => string.Equals(r.Name, p.Name, StringComparison.OrdinalIgnoreCase))));
        var arguments = constructor.GetParameters()
            .Select(parameter =>
            {
                var property = RecordProperties(type).Single(r => string.Equals(r.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                var current = property.GetValue(value);
                if (current is not null && IsList(property.PropertyType, out var element))
                {
                    return ShuffleList((IEnumerable)current, element, random, permuted, $"{type.Name}.{property.Name}");
                }

                return ShuffleValue(current, random, permuted);
            })
            .ToArray();
        return constructor.Invoke(arguments);
    }

    static object ShuffleList(IEnumerable items, Type element, Random random, ISet<string> permuted, string name)
    {
        var before = items.Cast<object?>().Select(item => ShuffleValue(item, random, permuted)).ToList();
        var after = new List<object?>(before);
        for (var i = after.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (after[i], after[j]) = (after[j], after[i]);
        }

        if (after.Where((item, index) => !Same(item, before[index])).Any())
        {
            permuted.Add(name);
        }

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
        foreach (var item in after)
        {
            list.Add(item);
        }

        return list;
    }

    static bool Same(object? x, object? y) =>
        ReferenceEquals(x, y) || (x is string && x.Equals(y));
}

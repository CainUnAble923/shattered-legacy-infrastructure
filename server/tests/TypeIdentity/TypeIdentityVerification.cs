// Type identity: every type resolves to itself by the names a save, a spawner and [add use.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// THE DEFECT THIS EXISTS FOR (D34, found in passing by the reachability audit):
//
//   TypeCache files every [TypeAlias] string under its full and short name in the SAME maps as
//   real type names (pinned Server/AssemblyHandler.cs:251-263), and every lookup returns the
//   FIRST type in the bucket (:188-228). Pinned's Glassblower carries
//   [TypeAlias("Server.Mobiles.GargoyleAlchemist")] (Mobiles/Vendors/NPC/Glassblower.cs:6), and
//   ours declares Server.Mobiles.GargoyleAlchemist (customizations/ClusterFGargoyleVendors.cs).
//   Which one is first is the order Assembly.GetTypes() returns them in, which is compilation
//   order, which no source file states. So it is measured here, on every build.
//
// WHERE EACH LOOKUP MATTERS, all pinned 7c9215d97:
//
//   save    GenericEntityPersistence writes idx.Write(e.GetType()), which is flag 2 plus
//           GetTypeHash(type.FullName) (Serialization/MemoryMapFileWriter.cs:246), and World.Load
//           resolves it with AssemblyHandler.FindTypeByHash (GenericEntityPersistence.cs:309):
//           the case-SENSITIVE full-name map. Type references inside entity data go through the
//           same pair (BufferWriter.cs:287, BufferReader.cs:150).
//   spawn   BaseSpawner resolves every entry with FindTypeByName(entry.SpawnedName)
//           (Engines/Spawners/BaseSpawner.cs:1126): the SHORT-name map, case-INsensitive by
//           default. [GenerateSpawners, [add and the spawner gump use the same call.
//   other   FindTypeByFullName(name) with its default ignoreCase=true: quest serialization, BOD
//           entries, talismans (the case-insensitive full-name map).
//
//   Signature, read before writing the probe: FindTypeByFullName(string name, bool ignoreCase =
//   true). The brief's second argument `false` means case-SENSITIVE, which is the save path's
//   map. So GargoyleStoneCrafter, which collides only case-insensitively, cannot show up there.
//
// ORDER IN THIS HOST VERSUS PRODUCTION. The test fixture loads ["Server.dll", "UOContent.dll"]
// (UOContent.Tests/Fixtures/UOContentFixture.cs:24); production loads ["UOContent.dll"] and keeps
// Server.dll as Core.Assembly (Distribution/Data/assemblies.json, Server/Main.cs:432), so the two
// search the assemblies in different orders. Neither D34 nor any of our types can be affected:
// both sides of every bucket that holds one of our types are in UOContent.dll, whose cache is
// the same object either way. The sweep checks each assembly's own cache for that reason, and
// separately reports any full name present in both assemblies.
//
// WHAT IS "OURS". Read from UOContent.pdb, not from a grep: a type is ours when every source
// document the compiler recorded for it is a file apply-patches.sh installed from
// server/customizations, which is present at /customizations in the builder image this runs in.
// Partial extensions of a pinned type (PlayerMobile, Corpse, BaseGuildmaster) and the full-file
// replacements are pinned's types and are not counted. See notes/type-identity.md.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TypeIdentityVerification
{
    private readonly ITestOutputHelper _out;

    public TypeIdentityVerification(ITestOutputHelper output) => _out = output;

    // ---- D34: the known case -----------------------------------------------------------------

    [Fact]
    public void D34_GargoyleAlchemistResolvesToItself()
    {
        const string name = "Server.Mobiles.GargoyleAlchemist";

        var sensitive = AssemblyHandler.FindTypeByFullName(name, false);
        var insensitive = AssemblyHandler.FindTypeByFullName(name);
        var byHash = AssemblyHandler.FindTypeByHash(AssemblyHandler.GetTypeHash(typeof(GargoyleAlchemist)));
        var byShortName = AssemblyHandler.FindTypeByName("GargoyleAlchemist");

        _out.WriteLine($"full, case-sensitive:   {sensitive}");
        _out.WriteLine($"full, case-insensitive: {insensitive}");
        _out.WriteLine($"by hash (save path):    {byHash}");
        _out.WriteLine($"short name (spawners):  {byShortName}");
        _out.WriteLine($"bucket: {Bucket(typeof(GargoyleAlchemist))}");

        Assert.Equal(typeof(GargoyleAlchemist), sensitive);
        Assert.Equal(typeof(GargoyleAlchemist), insensitive);
        Assert.Equal(typeof(GargoyleAlchemist), byHash);
        Assert.Equal(typeof(GargoyleAlchemist), byShortName);
    }

    [Fact]
    public void D34_GargoyleAlchemistSurvivesASaveRoundTrip() =>
        AssertSurvivesSaveRoundTrip(new GargoyleAlchemist());

    [Fact]
    public void D34_GargoyleStoneCrafterResolvesToItself()
    {
        // Pinned's StoneCrafter carries [TypeAlias("Server.Mobiles.GargoyleStonecrafter")], lower-case
        // c. Different string, so the case-sensitive map (saves) cannot collide; the insensitive
        // full-name map and the short-name map (spawners, [add) can.
        const string name = "Server.Mobiles.GargoyleStoneCrafter";

        var sensitive = AssemblyHandler.FindTypeByFullName(name, false);
        var insensitive = AssemblyHandler.FindTypeByFullName(name);
        var byHash = AssemblyHandler.FindTypeByHash(AssemblyHandler.GetTypeHash(typeof(GargoyleStoneCrafter)));
        var byShortName = AssemblyHandler.FindTypeByName("GargoyleStoneCrafter");

        _out.WriteLine($"full, case-sensitive:   {sensitive}");
        _out.WriteLine($"full, case-insensitive: {insensitive}");
        _out.WriteLine($"by hash (save path):    {byHash}");
        _out.WriteLine($"short name (spawners):  {byShortName}");

        Assert.Equal(typeof(GargoyleStoneCrafter), sensitive);
        Assert.Equal(typeof(GargoyleStoneCrafter), insensitive);
        Assert.Equal(typeof(GargoyleStoneCrafter), byHash);
        Assert.Equal(typeof(GargoyleStoneCrafter), byShortName);
    }

    [Fact]
    public void D34_GargoyleStoneCrafterSurvivesASaveRoundTrip() =>
        AssertSurvivesSaveRoundTrip(new GargoyleStoneCrafter());

    // World.Save cannot be driven in this host (it posts to a loop nothing pumps), so this takes
    // the two steps of a real save that involve the type, with the real writer, reader and lookup:
    // the index entry's type (flag 2 + hash, as MemoryMapFileWriter.Write(Type) writes it) and
    // the entity's own bytes. Then it does what GenericEntityPersistence does on load: resolve
    // the hash with FindTypeByHash (:309), take the (Serial) constructor (GetConstructorFor), and
    // Deserialize, checking the length consumed the way InternalDeserialize does.
    private void AssertSurvivesSaveRoundTrip(Mobile original)
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        writer.Write(original.GetType());
        var entityStart = writer.Position;
        original.Serialize(writer);
        var entityLength = writer.Position - entityStart;
        writer.Flush();

        var reader = new BufferReader(buffer);
        var resolved = reader.ReadType();

        _out.WriteLine($"saved {original.GetType().FullName}, loaded as {resolved?.FullName ?? "null"}");
        Assert.Equal(original.GetType(), resolved);

        var ctor = resolved.GetConstructor([typeof(Serial)]);
        Assert.NotNull(ctor);

        var copy = (Mobile)ctor.Invoke([original.Serial]);
        copy.Deserialize(reader);
        var consumed = reader.Position - entityStart;

        _out.WriteLine($"entity bytes written {entityLength}, read {consumed}");
        Assert.Equal(entityLength, consumed);
        Assert.IsType(original.GetType(), copy);

        original.Delete();
        copy.Delete();
    }

    // ---- D35: the transposed twins ------------------------------------------------------------

    // Pinned spells both classes wrong (GuantletsOfAnger, ShroudOfDeciet); ours carry ServUO's
    // correct spellings. The question is whether either pair shadows the other the way D34 could.
    // No [TypeAlias] names either spelling in any tree, so each should be its own bucket on every
    // map. When D35 is decided and one of each pair goes, delete this fact with it.
    [Theory]
    [InlineData("Server.Items.GauntletsOfAnger", "Server.Items.GuantletsOfAnger")]
    [InlineData("Server.Items.ShroudOfDeceit", "Server.Items.ShroudOfDeciet")]
    public void D35_BothSpellingsAreDistinctTypesAndNeitherShadowsTheOther(string oursName, string pinnedName)
    {
        var ours = OurTypes.Value;
        var oursType = AssemblyHandler.FindTypeByFullName(oursName, false);
        var pinnedType = AssemblyHandler.FindTypeByFullName(pinnedName, false);

        _out.WriteLine($"{oursName} -> {Describe(oursType, ours)}; short name -> {Describe(AssemblyHandler.FindTypeByName(oursType?.Name), ours)}");
        _out.WriteLine($"{pinnedName} -> {Describe(pinnedType, ours)}; short name -> {Describe(AssemblyHandler.FindTypeByName(pinnedType?.Name), ours)}");

        Assert.NotNull(oursType);
        Assert.NotNull(pinnedType);
        Assert.NotEqual(oursType, pinnedType);
        Assert.Contains(oursType, ours);
        Assert.DoesNotContain(pinnedType, ours);
        Assert.Equal(oursType, AssemblyHandler.FindTypeByFullName(oursName));
        Assert.Equal(pinnedType, AssemblyHandler.FindTypeByFullName(pinnedName));
        Assert.Equal(oursType, AssemblyHandler.FindTypeByName(oursType.Name));
        Assert.Equal(pinnedType, AssemblyHandler.FindTypeByName(pinnedType.Name));

        var a = (Item)Activator.CreateInstance(oursType);
        var b = (Item)Activator.CreateInstance(pinnedType);
        _out.WriteLine($"LabelNumber ours {a.LabelNumber}, pinned {b.LabelNumber}");
        Assert.Equal(b.LabelNumber, a.LabelNumber);
        a.Delete();
        b.Delete();
    }

    // ---- The sweep ---------------------------------------------------------------------------

    // Every type in every loaded assembly, looked up by its own full name through each map a
    // full-name lookup can use. A save-path miss (case-sensitive) fails whoever owns it. A
    // case-insensitive miss fails only when one of the two types is ours: pinned-only cases are
    // upstream's and are printed, not gated.
    [Fact]
    public void EveryTypeResolvesToItselfByFullName()
    {
        var ours = OurTypes.Value;
        var failures = new List<string>();
        var upstream = new List<string>();
        var checkedTotal = 0;
        var checkedOurs = 0;
        var skippedGenerated = 0;

        foreach (var asm in LoadedAssemblies())
        {
            var cache = AssemblyHandler.GetTypeCache(asm);
            foreach (var t in cache.Types)
            {
                if (IsCompilerGenerated(t))
                {
                    skippedGenerated++;
                    continue;
                }

                checkedTotal++;
                var isOurs = ours.Contains(t);
                if (isOurs)
                {
                    checkedOurs++;
                }

                var hash = AssemblyHandler.GetTypeHash(t);
                var bySensitive = First(cache.GetTypesByHash(hash, true, false));
                var byInsensitive = First(cache.GetTypesByName(t.FullName, true, true));

                if (bySensitive != t)
                {
                    failures.Add($"SAVE  {Owner(t, ours)} {t.FullName} -> {Describe(bySensitive, ours)}");
                }

                if (byInsensitive != t)
                {
                    var line = $"CI    {Owner(t, ours)} {t.FullName} -> {Describe(byInsensitive, ours)}";
                    if (isOurs || byInsensitive != null && ours.Contains(byInsensitive))
                    {
                        failures.Add(line);
                    }
                    else
                    {
                        upstream.Add(line);
                    }
                }
            }
        }

        // The public entry points, over our types, exactly as the brief states the D34 probe.
        foreach (var t in ours)
        {
            var viaApi = AssemblyHandler.FindTypeByFullName(t.FullName, false);
            var viaHash = AssemblyHandler.FindTypeByHash(AssemblyHandler.GetTypeHash(t));
            if (viaApi != t || viaHash != t)
            {
                failures.Add($"API   ours {t.FullName} -> byName {viaApi?.FullName ?? "null"}, byHash {viaHash?.FullName ?? "null"}");
            }
        }

        var crossAssembly = CrossAssemblyFullNames();

        _out.WriteLine($"assemblies: {string.Join(", ", LoadedAssemblies().Select(a => a.GetName().Name))}");
        _out.WriteLine($"types checked: {checkedTotal} (ours {checkedOurs}); compiler-generated skipped: {skippedGenerated}");
        _out.WriteLine($"full names declared in more than one assembly: {crossAssembly.Count}");
        foreach (var n in crossAssembly)
        {
            _out.WriteLine($"  {n}");
        }

        _out.WriteLine($"upstream-only case-insensitive shadows (not gated): {upstream.Count}");
        foreach (var u in upstream)
        {
            _out.WriteLine($"  {u}");
        }

        _out.WriteLine($"failures: {failures.Count}");
        foreach (var f in failures)
        {
            _out.WriteLine($"  {f}");
        }

        Assert.True(checkedOurs > 0, "classified none of our types; the sweep would cover nothing of ours");
        Assert.True(failures.Count == 0, $"{failures.Count} type(s) do not resolve to themselves:\n{string.Join("\n", failures)}");
    }

    // Every type a spawner or [add can create, looked up by its short name the way BaseSpawner
    // does (FindTypeByName, ignoreCase defaulted to true). A miss fails when either side is ours,
    // including a pinned creature whose spawn entry one of our types or aliases would capture.
    [Fact]
    public void EverySpawnableTypeResolvesToItselfByShortName()
    {
        var ours = OurTypes.Value;
        var failures = new List<string>();
        var upstream = new List<string>();
        var checkedTotal = 0;
        var checkedOurs = 0;

        foreach (var asm in LoadedAssemblies())
        {
            foreach (var t in AssemblyHandler.GetTypeCache(asm).Types)
            {
                if (!IsSpawnable(t))
                {
                    continue;
                }

                checkedTotal++;
                var isOurs = ours.Contains(t);
                if (isOurs)
                {
                    checkedOurs++;
                }

                var resolved = AssemblyHandler.FindTypeByName(t.Name);
                if (resolved == t)
                {
                    continue;
                }

                var line = $"{Owner(t, ours)} {t.FullName} -> {Describe(resolved, ours)}   bucket {ShortBucket(t)}";
                if (isOurs || resolved != null && ours.Contains(resolved))
                {
                    failures.Add(line);
                }
                else
                {
                    upstream.Add(line);
                }
            }
        }

        _out.WriteLine($"spawnable types checked: {checkedTotal} (ours {checkedOurs})");
        _out.WriteLine($"upstream-only short-name shadows (not gated): {upstream.Count}");
        foreach (var u in upstream)
        {
            _out.WriteLine($"  {u}");
        }

        _out.WriteLine($"failures: {failures.Count}");
        foreach (var f in failures)
        {
            _out.WriteLine($"  {f}");
        }

        Assert.True(checkedOurs > 0, "classified none of our spawnable types");
        Assert.True(failures.Count == 0, $"{failures.Count} spawnable type(s) do not resolve to themselves by short name:\n{string.Join("\n", failures)}");
    }

    // The classifier is what gives the sweep's count its meaning, so it gets its own check with
    // known answers in each direction rather than a floor on a number.
    [Fact]
    public void TheOursClassifierKnowsOursFromPinned()
    {
        var ours = OurTypes.Value;

        var documents = DocumentsByTypeToken(typeof(GargoyleAlchemist).Assembly);
        foreach (var probe in new[] { typeof(GargoyleAlchemist), typeof(GreyWolf), typeof(PlayerMobile) })
        {
            documents.TryGetValue(probe.MetadataToken, out var docs);
            _out.WriteLine($"{probe.Name} documents: {string.Join(" | ", docs ?? [])}");
        }

        _out.WriteLine($"our types (from UOContent.pdb and /customizations): {ours.Count}");
        foreach (var byNs in ours.GroupBy(t => t.Namespace ?? "(global)").OrderByDescending(g => g.Count()))
        {
            _out.WriteLine($"  {byNs.Count(),5}  {byNs.Key}");
        }

        // The list beside the count. G = compiler-generated (skipped by the full-name sweep),
        // S = spawnable (covered by the short-name sweep).
        foreach (var t in ours.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            _out.WriteLine($"  OURS {(IsCompilerGenerated(t) ? "G" : "-")}{(IsSpawnable(t) ? "S" : "-")} {t.FullName}");
        }

        Assert.Contains(typeof(GargoyleAlchemist), ours);   // top-level customizations file -> Misc/
        Assert.Contains(typeof(Saurosaurus), ours);         // mirrored subdirectory file
        Assert.DoesNotContain(typeof(GreyWolf), ours);      // pinned
        Assert.DoesNotContain(typeof(Glassblower), ours);   // pinned, the other side of D34
        Assert.DoesNotContain(typeof(PlayerMobile), ours);  // pinned, extended by a partial of ours
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static Assembly[] LoadedAssemblies() =>
        AssemblyHandler.Assemblies.Append(Core.Assembly).Where(a => a != null).Distinct().ToArray();

    private static bool IsCompilerGenerated(Type t) =>
        t.Name.Contains('<') || t.IsDefined(typeof(CompilerGeneratedAttribute), false);

    private static bool IsSpawnable(Type t) =>
        !IsCompilerGenerated(t) && t.IsPublic && t is { IsAbstract: false, IsGenericTypeDefinition: false }
        && (typeof(Item).IsAssignableFrom(t) || typeof(Mobile).IsAssignableFrom(t))
        && t.GetConstructors().Any(c => c.GetParameters().Length == 0 || c.GetParameters().All(p => p.IsOptional));

    private static Type First(TypeCache.TypeEnumerator e)
    {
        foreach (var t in e)
        {
            return t;
        }

        return null;
    }

    private static string Owner(Type t, HashSet<Type> ours) => ours.Contains(t) ? "ours  " : "pinned";

    private static string Describe(Type t, HashSet<Type> ours) =>
        t == null ? "null" : $"{t.FullName} ({(ours.Contains(t) ? "ours" : t.Assembly.GetName().Name)})";

    private static string Bucket(Type t)
    {
        var e = AssemblyHandler.GetTypeCache(t.Assembly).GetTypesByHash(AssemblyHandler.GetTypeHash(t), true, false);
        var names = new List<string>();
        foreach (var x in e)
        {
            names.Add(x.FullName);
        }

        return $"[{string.Join(", ", names)}]";
    }

    private static string ShortBucket(Type t)
    {
        var names = new List<string>();
        foreach (var asm in LoadedAssemblies())
        {
            foreach (var x in AssemblyHandler.GetTypeCache(asm).GetTypesByName(t.Name, false, true))
            {
                names.Add(x.FullName);
            }
        }

        return $"[{string.Join(", ", names)}]";
    }

    private static List<string> CrossAssemblyFullNames()
    {
        var seen = new Dictionary<string, Assembly>();
        var result = new List<string>();
        foreach (var asm in LoadedAssemblies())
        {
            foreach (var t in AssemblyHandler.GetTypeCache(asm).Types)
            {
                if (IsCompilerGenerated(t) || t.FullName == null)
                {
                    continue;
                }

                if (seen.TryGetValue(t.FullName, out var other) && other != asm)
                {
                    result.Add($"{t.FullName} ({other.GetName().Name} and {asm.GetName().Name})");
                }
                else
                {
                    seen[t.FullName] = asm;
                }
            }
        }

        return result;
    }

    // ---- which types are ours ----------------------------------------------------------------

    private const string CustomizationsRoot = "/customizations";

    // Top-level server/customizations files apply-patches.sh routes as full-file replacements of
    // pinned files rather than into Misc/ (docker/uo/apply-patches.sh, the find ... ! -name list
    // and the replace_file calls). Their types are pinned's.
    private static readonly HashSet<string> Replacements =
    [
        "CharacterCreation.cs", "CraftContext.cs", "CraftItem.cs", "HammerOfHephaestus.cs",
        "Meditation.cs", "RegenRates.cs", "ResourceInfo.cs"
    ];

    private static readonly Lazy<HashSet<Type>> OurTypes = new(ClassifyOurTypes);

    private static HashSet<Type> ClassifyOurTypes()
    {
        if (!Directory.Exists(CustomizationsRoot))
        {
            throw new InvalidOperationException(
                $"{CustomizationsRoot} is missing. This fact runs in the builder image, where the Dockerfile " +
                "stages server/customizations there; without it no type can be classified as ours."
            );
        }

        // Paths relative to Projects/UOContent/, as apply-patches.sh installs them.
        var ourFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(CustomizationsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(CustomizationsRoot, file).Replace('\\', '/');
            if (rel.Contains('/'))
            {
                ourFiles.Add(rel);
            }
            else if (!Replacements.Contains(rel))
            {
                ourFiles.Add($"Misc/{rel}");
            }
        }

        var content = typeof(GargoyleAlchemist).Assembly;
        var documents = DocumentsByTypeToken(content);
        var result = new HashSet<Type>();

        foreach (var t in AssemblyHandler.GetTypeCache(content).Types)
        {
            if (!documents.TryGetValue(t.MetadataToken, out var docs))
            {
                continue;
            }

            var anyOurs = false;
            var anyPinned = false;
            foreach (var doc in docs)
            {
                var i = doc.IndexOf("/Projects/UOContent/", StringComparison.Ordinal);
                if (i < 0)
                {
                    continue; // generated source (serialization generator), neither side's
                }

                var rel = doc[(i + "/Projects/UOContent/".Length)..];
                if (rel.StartsWith("obj/", StringComparison.Ordinal))
                {
                    continue; // generator output written under obj/, neither side's
                }

                if (ourFiles.Contains(rel))
                {
                    anyOurs = true;
                }
                else
                {
                    anyPinned = true;
                }
            }

            if (anyOurs && !anyPinned)
            {
                result.Add(t);
            }
        }

        return result;
    }

    // Kind GUID of the portable-PDB "type definition documents" record, which Roslyn writes for
    // types that have no method body with sequence points (enums, interfaces, field-only types).
    private static readonly Guid TypeDefinitionDocuments = new("932E74BC-DBA9-4478-8D46-0F32A7BAB3D3");

    // Every source document recorded for each type, including its nested types' documents under
    // their own tokens, from the assembly's portable PDB.
    private static Dictionary<int, HashSet<string>> DocumentsByTypeToken(Assembly asm)
    {
        var pdbPath = Path.ChangeExtension(asm.Location, ".pdb");
        if (!File.Exists(pdbPath))
        {
            throw new InvalidOperationException($"{pdbPath} is missing; cannot tell our types from pinned ones.");
        }

        using var peStream = File.OpenRead(asm.Location);
        using var pe = new PEReader(peStream);
        var md = pe.GetMetadataReader();

        using var pdbStream = File.OpenRead(pdbPath);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var pdb = provider.GetMetadataReader();

        var result = new Dictionary<int, HashSet<string>>();

        HashSet<string> For(TypeDefinitionHandle h)
        {
            var token = MetadataTokens.GetToken(h);
            if (!result.TryGetValue(token, out var set))
            {
                result[token] = set = new HashSet<string>(StringComparer.Ordinal);
            }

            return set;
        }

        foreach (var th in md.TypeDefinitions)
        {
            var set = For(th);
            foreach (var mh in md.GetTypeDefinition(th).GetMethods())
            {
                var info = pdb.GetMethodDebugInformation(mh);
                if (!info.Document.IsNil)
                {
                    set.Add(pdb.GetString(pdb.GetDocument(info.Document).Name));
                    continue;
                }

                if (info.SequencePointsBlob.IsNil)
                {
                    continue;
                }

                foreach (var sp in info.GetSequencePoints())
                {
                    if (!sp.Document.IsNil)
                    {
                        set.Add(pdb.GetString(pdb.GetDocument(sp.Document).Name));
                    }
                }
            }
        }

        foreach (var ch in pdb.CustomDebugInformation)
        {
            var cdi = pdb.GetCustomDebugInformation(ch);
            if (cdi.Parent.Kind != HandleKind.TypeDefinition || pdb.GetGuid(cdi.Kind) != TypeDefinitionDocuments)
            {
                continue;
            }

            var set = For((TypeDefinitionHandle)cdi.Parent);
            var blob = pdb.GetBlobReader(cdi.Value);
            while (blob.RemainingBytes > 0)
            {
                var doc = MetadataTokens.DocumentHandle(blob.ReadCompressedInteger());
                set.Add(pdb.GetString(pdb.GetDocument(doc).Name));
            }
        }

        return result;
    }
}

// CommandDeclarationVerification.cs
//
// cc-P21: every command we register declares what kind of command it is, beside its [Usage].
// Notes in shard-migration notes/cc-P21-console-world-commands.md.
//
// WHY THIS IS A GATE. The console's World setup tab (scripts/Shard-Console.ps1) reads
// [ShardCommand(...)] from server/customizations to decide which commands change a world, which
// have a dry run and what it is, and whether running one twice duplicates anything. It never
// decides those itself. That only stays true if every command declares one: if only some did, an
// undeclared world-changer and an author who forgot would look the same. So a command registered
// with no declaration fails the build here, the way apply-patches.sh refuses a patch it was not
// told about.
//
// WHAT IS "A COMMAND WE REGISTER". Read from the compiled code, not from a grep: every call to
// CommandSystem.Register(string, AccessLevel, CommandEventHandler) in UOContent.dll whose calling
// method's source (from UOContent.pdb) is a file apply-patches.sh installed from
// server/customizations, staged at /customizations in the builder image this runs in. The name,
// access level and handler come from the IL at the call site (ldstr, ldc.i4, ldftn), so a
// registration that a grep of the source would miss is still found. A grep of the source is then
// used the other way round, as an independent count the IL must agree with.
//
// Facts:
//   1. The IL finds exactly the registrations a plain count of the source finds.
//   2. Every one of them has [ShardCommand] on its handler (THE GATE).
//   3. A world-changing command declares whether re-running duplicates, which shard it is for, and
//      a dry run or that it has none.
//   4. A declared dry run is a word of that command's own [Usage] and a string the command's own
//      class compares against, so the console cannot render a flag the command ignores.
//   5. Player-access commands are exactly the ones declared Player, so a staff tool can never reach
//      the player wiki's list and a player command can never be missing from it.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Server;
using Server.Commands;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CommandDeclarationVerification
{
    private readonly ITestOutputHelper _out;

    public CommandDeclarationVerification(ITestOutputHelper output) => _out = output;

    private record Registration(string Name, AccessLevel Access, MethodInfo Handler, MethodBase Site)
    {
        public ShardCommandAttribute Declaration => Handler?.GetCustomAttribute<ShardCommandAttribute>();
        public string Usage => Handler?.GetCustomAttribute<UsageAttribute>()?.Usage;
        public string Where => $"{Site.DeclaringType?.FullName}.{Site.Name}";
    }

    // ---- 1 ----------------------------------------------------------------------------------------

    [Fact]
    public void TheCompiledRegistrationsAreTheOnesInTheSource()
    {
        var il = Registrations.Value;
        var source = SourceRegistrationLines();

        _out.WriteLine($"registrations found in the IL: {il.Count}");
        foreach (var r in il.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            _out.WriteLine($"  {r.Name,-30} {r.Access,-13} {r.Declaration?.Category.ToString() ?? "(none)",-16} {r.Where}");
        }

        _out.WriteLine($"CommandSystem.Register( lines in /customizations, comments skipped: {source.Count}");
        foreach (var s in source)
        {
            _out.WriteLine($"  {s}");
        }

        Assert.All(il, r => Assert.NotNull(r.Handler));
        Assert.Equal(source.Count, il.Count);
    }

    // ---- 2: the gate ------------------------------------------------------------------------------

    [Fact]
    public void EveryCommandWeRegisterDeclaresACategory()
    {
        var missing = Registrations.Value.Where(r => r.Declaration == null).ToList();
        foreach (var r in missing)
        {
            _out.WriteLine($"NO [ShardCommand]: {r.Name} (handler {r.Handler?.DeclaringType?.FullName}.{r.Handler?.Name})");
        }

        Assert.True(
            missing.Count == 0,
            $"{missing.Count} registered command(s) have no [ShardCommand] on their handler: " +
            string.Join(", ", missing.Select(r => r.Name)) +
            ". Add one beside [Usage]; server/customizations/ShardCommandAttribute.cs says what each field means."
        );
    }

    // ---- 3 ----------------------------------------------------------------------------------------

    [Fact]
    public void AWorldCommandDeclaresRerunShardAndDryRun()
    {
        var bad = new List<string>();
        foreach (var r in Registrations.Value)
        {
            var d = r.Declaration;
            if (d == null || !d.ChangesTheWorld)
            {
                continue;
            }

            if (d.Rerun == CommandRerun.Undeclared)
            {
                bad.Add($"{r.Name}: Rerun");
            }

            if (d.Shard == CommandShard.Undeclared)
            {
                bad.Add($"{r.Name}: Shard");
            }

            if (string.IsNullOrEmpty(d.DryRun) == !d.NoDryRun)
            {
                bad.Add($"{r.Name}: exactly one of DryRun or NoDryRun");
            }

            if (string.IsNullOrWhiteSpace(d.Summary))
            {
                bad.Add($"{r.Name}: Summary");
            }
        }

        Assert.True(bad.Count == 0, "world commands missing a fact: " + string.Join("; ", bad));
    }

    // ---- 4 ----------------------------------------------------------------------------------------

    [Fact]
    public void ADeclaredDryRunIsAWordOfItsUsageAndAStringItsClassReads()
    {
        var bad = new List<string>();
        foreach (var r in Registrations.Value)
        {
            var token = r.Declaration?.DryRun;
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            var words = Regex.Split(r.Usage ?? "", "[^A-Za-z0-9_]+");
            if (!words.Contains(token, StringComparer.Ordinal))
            {
                bad.Add($"{r.Name}: DryRun \"{token}\" is not a word of [Usage(\"{r.Usage}\")]");
            }

            var owner = r.Handler.DeclaringType;
            while (owner?.DeclaringType != null)
            {
                owner = owner.DeclaringType;
            }

            if (!StringLiteralsOf(owner).Contains(token, StringComparer.OrdinalIgnoreCase))
            {
                bad.Add($"{r.Name}: no method of {owner?.FullName} loads the string \"{token}\"");
            }

            _out.WriteLine($"{r.Name}: dry run [{r.Name} {token}   usage: {r.Usage}");
        }

        Assert.True(bad.Count == 0, string.Join("; ", bad));
    }

    // ---- 5 ----------------------------------------------------------------------------------------

    [Fact]
    public void PlayerAccessAndThePlayerCategoryAreTheSameCommands()
    {
        var bad = Registrations.Value
            .Where(r => r.Declaration != null)
            .Where(r => (r.Access == AccessLevel.Player) != (r.Declaration.Category == CommandCategory.Player))
            .Select(r => $"{r.Name}: access {r.Access}, category {r.Declaration.Category}")
            .ToList();

        Assert.True(bad.Count == 0, string.Join("; ", bad));
    }

    // ---- the IL scan ------------------------------------------------------------------------------

    private static readonly MethodInfo RegisterMethod = typeof(CommandSystem).GetMethod(
        nameof(CommandSystem.Register),
        [typeof(string), typeof(AccessLevel), typeof(CommandEventHandler)]
    );

    private static readonly Lazy<List<Registration>> Registrations = new(FindRegistrations);

    private static List<Registration> FindRegistrations()
    {
        var content = typeof(PlayerMobile).Assembly;
        var result = new List<Registration>();

        foreach (var site in OurMethods(content))
        {
            string name = null;
            int? access = null;
            MethodInfo handler = null;

            foreach (var (op, operand) in Instructions(site))
            {
                if (op == OpCodes.Ldstr)
                {
                    name = site.Module.ResolveString(operand);
                    access = null;
                    handler = null;
                }
                else if (TryConstant(op, operand, out var value))
                {
                    access ??= value;
                }
                else if (op == OpCodes.Ldftn)
                {
                    handler = Resolve(site, operand) as MethodInfo;
                }
                else if ((op == OpCodes.Call || op == OpCodes.Callvirt) && Resolve(site, operand) == RegisterMethod)
                {
                    result.Add(new Registration(name, (AccessLevel)(access ?? -1), handler, site));
                    name = null;
                    access = null;
                    handler = null;
                }
            }
        }

        return result;
    }

    private static bool TryConstant(OpCode op, int operand, out int value)
    {
        value = op.Value switch
        {
            0x15 => -1, // ldc.i4.m1
            >= 0x16 and <= 0x1E => op.Value - 0x16, // ldc.i4.0 .. ldc.i4.8
            _ => int.MinValue
        };

        if (op == OpCodes.Ldc_I4_S || op == OpCodes.Ldc_I4)
        {
            value = operand;
        }

        return value != int.MinValue;
    }

    private static MemberInfo Resolve(MethodBase site, int token)
    {
        try
        {
            var typeArgs = site.DeclaringType?.IsGenericType == true ? site.DeclaringType.GetGenericArguments() : null;
            var methodArgs = site.IsGenericMethod ? site.GetGenericArguments() : null;
            return site.Module.ResolveMember(token, typeArgs, methodArgs);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static readonly Dictionary<short, OpCode> OpCodeByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null))
        .ToDictionary(o => o.Value);

    // Every instruction of a method body with its operand as an int (0 when it has none or when it is
    // wider than four bytes, which no instruction this scan reads is).
    private static IEnumerable<(OpCode, int)> Instructions(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null)
        {
            yield break;
        }

        var i = 0;
        while (i < il.Length)
        {
            short value = il[i++];
            if (value == 0xFE)
            {
                value = unchecked((short)(0xFE00 | il[i++]));
            }

            var op = OpCodeByValue[value];
            var operand = 0;
            switch (op.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineVar:
                    i += 1;
                    break;
                case OperandType.ShortInlineI:
                    operand = (sbyte)il[i];
                    i += 1;
                    break;
                case OperandType.InlineVar:
                    i += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    i += 8;
                    break;
                case OperandType.InlineSwitch:
                    var count = BitConverter.ToInt32(il, i);
                    i += 4 + 4 * count;
                    break;
                default: // every remaining operand type is four bytes
                    operand = BitConverter.ToInt32(il, i);
                    i += 4;
                    break;
            }

            yield return (op, operand);
        }
    }

    private static HashSet<string> StringLiteralsOf(Type type)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in new[] { type }.Concat(AllNested(type)))
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                     BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (var m in t.GetMethods(all).Cast<MethodBase>().Concat(t.GetConstructors(all)))
            {
                foreach (var (op, operand) in Instructions(m))
                {
                    if (op == OpCodes.Ldstr)
                    {
                        result.Add(m.Module.ResolveString(operand));
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<Type> AllNested(Type type) =>
        type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(n => new[] { n }.Concat(AllNested(n)));

    // ---- which methods are ours -------------------------------------------------------------------
    // Same rule as TypeIdentityVerification, per method rather than per type: a method is ours when
    // the source document the PDB records for it is a file apply-patches.sh installed from
    // server/customizations. Per method, so a partial class extending a pinned type still counts.

    private const string CustomizationsRoot = "/customizations";

    // Top-level files apply-patches.sh routes as full-file replacements of pinned files, not into Misc/.
    private static readonly HashSet<string> Replacements =
    [
        "CharacterCreation.cs", "CraftContext.cs", "CraftItem.cs", "HammerOfHephaestus.cs",
        "Meditation.cs", "RegenRates.cs", "ResourceInfo.cs"
    ];

    private static IEnumerable<string> OurSourceFiles() =>
        Directory.Exists(CustomizationsRoot)
            ? Directory.EnumerateFiles(CustomizationsRoot, "*.cs", SearchOption.AllDirectories)
            : throw new InvalidOperationException(
                $"{CustomizationsRoot} is missing. This fact runs in the builder image, where the Dockerfile " +
                "stages server/customizations there; without it no registration can be classified as ours."
            );

    private static HashSet<string> OurInstalledPaths()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in OurSourceFiles())
        {
            var rel = Path.GetRelativePath(CustomizationsRoot, file).Replace('\\', '/');
            if (rel.Contains('/'))
            {
                result.Add(rel);
            }
            else if (!Replacements.Contains(rel))
            {
                result.Add($"Misc/{rel}");
            }
        }

        return result;
    }

    private static List<MethodBase> OurMethods(Assembly asm)
    {
        var ours = OurInstalledPaths();
        var pdbPath = Path.ChangeExtension(asm.Location, ".pdb");
        if (!File.Exists(pdbPath))
        {
            throw new InvalidOperationException($"{pdbPath} is missing; cannot tell our registrations from pinned ones.");
        }

        using var pdbStream = File.OpenRead(pdbPath);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var pdb = provider.GetMetadataReader();

        using var peStream = File.OpenRead(asm.Location);
        using var pe = new PEReader(peStream);
        var md = pe.GetMetadataReader();

        var result = new List<MethodBase>();
        foreach (var mh in md.MethodDefinitions)
        {
            var info = pdb.GetMethodDebugInformation(mh);
            string doc = null;
            if (!info.Document.IsNil)
            {
                doc = pdb.GetString(pdb.GetDocument(info.Document).Name);
            }
            else if (!info.SequencePointsBlob.IsNil)
            {
                var sp = info.GetSequencePoints().FirstOrDefault(p => !p.Document.IsNil);
                if (!sp.Document.IsNil)
                {
                    doc = pdb.GetString(pdb.GetDocument(sp.Document).Name);
                }
            }

            var at = doc?.IndexOf("/Projects/UOContent/", StringComparison.Ordinal) ?? -1;
            if (at < 0 || !ours.Contains(doc[(at + "/Projects/UOContent/".Length)..]))
            {
                continue;
            }

            try
            {
                var m = asm.ManifestModule.ResolveMethod(MetadataTokens.GetToken(mh));
                if (m != null)
                {
                    result.Add(m);
                }
            }
            catch (ArgumentException)
            {
                // an open generic method needing a context; no registration lives in one
            }
        }

        return result;
    }

    // The independent count: lines of our source that call CommandSystem.Register(, with comment
    // lines skipped. TestCenterKitEntries.cs names the call in a comment, which is why.
    private static List<string> SourceRegistrationLines()
    {
        var result = new List<string>();
        foreach (var file in OurSourceFiles())
        {
            var n = 0;
            foreach (var line in File.ReadLines(file))
            {
                n++;
                var t = line.TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal) || t.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                if (t.Contains("CommandSystem.Register(", StringComparison.Ordinal))
                {
                    result.Add($"{Path.GetRelativePath(CustomizationsRoot, file)}:{n}");
                }
            }
        }

        return result;
    }
}

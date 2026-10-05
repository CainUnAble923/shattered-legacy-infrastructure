// SmithGuildNameVerification.cs
//
// cc-P52 Part G (bug-list D68, Chase 2026-10-04: "Society of Smiths" everywhere). The smith guild showed as "Smiths'
// Fellowship" in the guild directory, the hall page, the join messages and the starter path, as "Smiths' Brotherhood"
// in the contract ledger header, and as "Society of Smiths" in the Seal catalog and the BOD turn-in message. Display
// text only changed; the guild key stays "smithing".
//
// Facts:
//   G1. The guild definition's name is "Society of Smiths" and its key is still "smithing".
//   G2. The guild directory and the smith hall page show "Society of Smiths"; nothing there says "Fellowship".
//   G3. The Seal catalog header, the contract ledger header and the starter path's own rule say "Society of Smiths".
//   G4. No source file installed from server/customizations contains "Fellowship" or "Smiths' Brotherhood".

using System;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.MLQuests.Definitions;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SmithGuildNameVerification
{
    private const string Name = "Society of Smiths";

    // apply-patches.sh copies server/customizations here in the builder image (as CommandDeclarationVerification).
    private const string CustomizationsRoot = "/customizations";

    private readonly ITestOutputHelper _out;

    public SmithGuildNameVerification(ITestOutputHelper output)
    {
        _out = output;
        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }

        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        ClusterFGuildSystem.EnsureRegistered();
    }

    private static bool _startupHooksRun;

    private static string[] Texts(Gump g) =>
        g.Entries.OfType<GumpLabel>().Select(l => l.Text)
            .Concat(g.Entries.OfType<GumpHtml>().Select(h => h.Text))
            .ToArray();

    [Fact]
    public void TheGuildIsTheSocietyOfSmithsEverywhere()
    {
        // G1
        var def = ClusterFGuildSystem.GetDef("smithing");
        Assert.NotNull(def);
        Assert.Equal("smithing", def.Key);
        Assert.Equal(Name, def.Name);

        var account = new Account($"p52g{Guid.NewGuid():N}"[..16], "p52-test-only");
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(1550, 1530, 0), Map.Trammel);

        try
        {
            // G2
            var directory = Texts(new GuildProgressGump(pm, null, null));
            var hall = Texts(new GuildTaskDetailGump(pm, def, account));
            _out.WriteLine($"directory: {string.Join(" | ", directory.Where(t => t.Contains("Smith")))}");
            _out.WriteLine($"hall page: {string.Join(" | ", hall.Where(t => t.Contains("Smith")))}");
            Assert.Contains(Name, directory);
            Assert.Contains(hall, t => t.Contains(Name));
            Assert.DoesNotContain(directory, t => t.Contains("Fellowship"));
            Assert.DoesNotContain(hall, t => t.Contains("Fellowship"));

            // G3
            var catalog = Texts(new SmithSealCatalogGump(pm));
            var ledger = Texts(new GuildContractLedgerGump(pm, "smithing"));
            var starter = GuildStarterItems.OwnRuleFor(typeof(ItsHammerTime));
            _out.WriteLine($"catalog: {catalog.First(t => t.Contains("Smith"))}; " +
                           $"ledger: {ledger.FirstOrDefault(t => t.Contains("Smith"))}; starter: {starter}");
            Assert.Contains(catalog, t => t.Contains(Name));
            Assert.Contains(Name, ledger);
            Assert.Contains(Name, starter);
        }
        finally
        {
            ClusterFAccountPersistence.Get(account)?.ClearGuildData();
            pm.Delete();
            Accounts.Remove(account);
        }
    }

    [Fact]
    public void NoCustomizationSourceNamesTheOldGuild()
    {
        Assert.True(Directory.Exists(CustomizationsRoot), $"{CustomizationsRoot} is not in this image");

        var files = Directory.EnumerateFiles(CustomizationsRoot, "*.cs", SearchOption.AllDirectories).ToList();
        var hits = files
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => x.line.Contains("Fellowship", StringComparison.OrdinalIgnoreCase)
                        || x.line.Contains("Smiths' Brotherhood", StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{Path.GetRelativePath(CustomizationsRoot, x.f)}:{x.i + 1}: {x.line.Trim()}")
            .ToList();

        _out.WriteLine($"{files.Count} files scanned; {hits.Count} hits");
        foreach (var hit in hits)
        {
            _out.WriteLine(hit);
        }

        Assert.True(files.Count > 100, "the scan found the customizations");
        Assert.Empty(hits);
    }
}

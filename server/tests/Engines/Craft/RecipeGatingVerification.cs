// RecipeGatingVerification.cs
//
// cc-P78 (F-43): recipe-gated crafting. Notes in shard-migration notes/cc-P78-recipe-gating.md.
//
// Facts (the brief's tests):
//   1. A free item is visible with no recipes; a Band II item is hidden until its book is learned, then visible. The
//      real craft menu (the patched CreateItemList, read off the wire) lists the free item's buttons, not the hidden one's.
//   2. Learning is account-wide: a second character on the same account sees it.
//   3. A loop reset (skills to zero) keeps learned recipes and guild rank.
//   4. Using a known book refuses and keeps the item; an unknown one teaches and is consumed.
//   5. Hint lines: the count and the guild's rank name for a band; one wild line with no count; none below the floor.
//   6. Promotion: blocked without the skill; rank 5 blocked without the trial once the waiver is cleared; the book is
//      granted on promotion; no duplicate grant when the account already knows it.
//   7. Shop: only the band one ahead; guild currency only; a Band V scroll needs the next-tier material.
//   8. Repair, Smelt and Enhance still start with no recipes learned.
//   9. crafting.recipeGating.enabled = false shows the full stock menus.
//  10. A mule schematic learned once lets the module be crafted again and again (it is never consumed).
//  11. Every craft skill with a menu has exactly one owning guild, and that guild teaches the skill.
// And: the data file round-trips and a bad one is refused whole; the account record v17 and guild record v3 round-trip;
// a guild record from before v3 starts tracked at its standing rank. The band counts per skill are printed for the notes.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RecipeGatingVerification
{
    private readonly ITestOutputHelper _out;

    public RecipeGatingVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();
        Systems();
        ClusterFGuildSystem.EnsureRegistered();
        ClusterFRecipeGate.Enabled = true;
        ClusterFGuildPromotion.TrialsWaivedUntilBuilt = true;
        RecipeGatingTable.Current = RecipeGatingTable.Defaults;
        ClusterFRecipeGate.ClearCache();
    }

    // ---------------------------------------------------------------- setup

    private static bool _startupHooksRun;

    private static void EnsureStartupHooks()
    {
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

    // As CraftCategoryPagingVerification: Def*.Initialize and ServerStarted do not run in the host.
    private static void Systems()
    {
        if (DefAlchemy.CraftSystem == null) DefAlchemy.Initialize();
        if (DefBlacksmithy.CraftSystem == null) DefBlacksmithy.Initialize();
        if (DefBowFletching.CraftSystem == null) DefBowFletching.Initialize();
        if (DefCarpentry.CraftSystem == null) DefCarpentry.Initialize();
        if (DefCartography.CraftSystem == null) DefCartography.Initialize();
        if (DefCooking.CraftSystem == null) DefCooking.Initialize();
        if (DefGlassblowing.CraftSystem == null) DefGlassblowing.Initialize();
        if (DefInscription.CraftSystem == null) DefInscription.Initialize();
        if (DefMasonry.CraftSystem == null) DefMasonry.Initialize();
        if (DefTailoring.CraftSystem == null) DefTailoring.Initialize();
        if (DefTinkering.CraftSystem == null) DefTinkering.Initialize();

        BlacksmithyCraftRegistrations.Register();
        TailoringCraftRegistrations.Register();
        TinkeringCraftRegistrations.Register();
        CarpentryCraftRegistrations.Register();
        MuleModuleCrafts.Register();

        if (DefBlacksmithy.CraftSystem.CraftSubRes.SearchFor(typeof(PlatinumIngot)) == null)
        {
            typeof(ClusterFMiningExtension).GetMethod("OnServerStarted", Private)!.Invoke(null, null);
        }
    }

    private static CraftSystem Smith => DefBlacksmithy.CraftSystem;

    // FindItemsByType is a pooled enumerator (no LINQ on it): copied into a list.
    private static List<T> InPack<T>(PlayerMobile pm) where T : Item
    {
        var list = new List<T>();
        foreach (var item in pm.Backpack.FindItemsByType<T>())
        {
            list.Add(item);
        }

        return list;
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account;
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Account account = null, int slot = 0)
        {
            Account = account ?? new Account($"p78{Guid.NewGuid():N}"[..16], "p78-test-only");
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Account[slot] = Pm;
            Pm.MoveToWorld(new Point3D(1300, 1300 + slot, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreateGuild(Pm);
        public ClusterFAccountData Data => ClusterFAccountPersistence.GetOrCreate(Account);

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            if (Accounts.GetAccount(Account.Username) != null)
            {
                for (var i = 0; i < Account.Length; i++)
                {
                    Account[i]?.Delete();
                }

                Accounts.Remove(Account);
            }
        }
    }

    // A blacksmithy group holding a free item and a Band II book item (no own recipe).
    private static (int GroupIndex, int FreeIndex, int BookIndex, RecipeClass Book) FreeAndBandTwo()
    {
        for (var g = 0; g < Smith.CraftGroups.Count; g++)
        {
            var items = Smith.CraftGroups[g].CraftItems;
            var free = items.FindIndex(i => ClusterFRecipeGate.Classify(Smith, i).Kind == RecipeKind.Free);
            var book = items.FindIndex(i =>
                ClusterFRecipeGate.Classify(Smith, i) is { Kind: RecipeKind.Book } c &&
                RecipeGatingTable.Current.BandOf(c.MinSkill) == 2);
            if (free >= 0 && book >= 0)
            {
                return (g, free, book, ClusterFRecipeGate.Classify(Smith, items[book]));
            }
        }

        throw new InvalidOperationException("no blacksmithy group with a free and a Band II item");
    }

    // ---------------------------------------------------------------- the menu packet

    private static (string Layout, List<string> Lines) SendMenu(Player p, CraftSystem system, int group)
    {
        system.GetContext(p.Pm).LastGroupIndex = group;
        var gump = new CraftGump(p.Pm, system, new SmithHammer(), null);
        var from = p.Ns.SendBuffer.GetReadSpan().Length;
        p.Pm.SendGump(gump);

        var wire = p.Ns.SendBuffer.GetReadSpan()[from..];
        var start = -1;
        for (var i = 0; i + 7 <= wire.Length; i++)
        {
            if (wire[i] == 0xDD && BinaryPrimitives.ReadUInt32BigEndian(wire[(i + 3)..]) == (uint)gump.Serial)
            {
                start = i;
            }
        }

        Assert.True(start >= 0, "no gump on the wire");
        wire = wire[start..];
        var pos = 19;
        var layout = Encoding.ASCII.GetString(Inflate(wire, ref pos));
        var lines = new List<string>();
        var count = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]);
        pos += 4;
        if (count > 0)
        {
            var text = Inflate(wire, ref pos);
            var t = 0;
            for (var i = 0; i < count; i++)
            {
                var len = BinaryPrimitives.ReadUInt16BigEndian(text.AsSpan(t));
                t += 2;
                lines.Add(Encoding.BigEndianUnicode.GetString(text, t, len * 2));
                t += len * 2;
            }
        }

        return (layout, lines);
    }

    private static byte[] Inflate(ReadOnlySpan<byte> wire, ref int pos)
    {
        var packed = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[pos..]) - 4;
        var length = (int)BinaryPrimitives.ReadUInt32BigEndian(wire[(pos + 4)..]);
        using var input = new MemoryStream(wire.Slice(pos + 8, packed).ToArray());
        pos += 8 + packed;
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);
        Assert.Equal(length, (int)output.Length);
        return output.ToArray();
    }

    // CraftGump.GetButtonID(1, i): the "make" button of the item at index i in its group.
    private static string MakeButton(int i) => $" 1 0 {1 + 1 + i * 7} }}";

    // ---------------------------------------------------------------- 1, 2, 9

    [Fact]
    public void AFreeItemIsVisibleABandTwoItemIsHiddenUntilItsBookAndTheWholeAccountSeesIt()
    {
        var (group, free, book, c) = FreeAndBandTwo();
        var items = Smith.CraftGroups[group].CraftItems;
        using var p = new Player();

        Assert.True(ClusterFRecipeGate.IsKnown(p.Pm, Smith, items[free]));
        Assert.False(ClusterFRecipeGate.IsKnown(p.Pm, Smith, items[book]));

        var (layout, _) = SendMenu(p, Smith, group);
        Assert.Contains(MakeButton(free), layout);
        Assert.DoesNotContain(MakeButton(book), layout);

        Assert.True(ClusterFRecipeStore.Learn(p.Pm, c.Key));
        _out.WriteLine($"learned {c.Key}: {ClusterFRecipeStore.Describe(c.Key)}");
        Assert.True(ClusterFRecipeGate.IsKnown(p.Pm, Smith, items[book]));
        (layout, _) = SendMenu(p, Smith, group);
        Assert.Contains(MakeButton(book), layout);

        // 2: a second character on the same account.
        using var second = new Player(p.Account, 1);
        Assert.True(ClusterFRecipeGate.IsKnown(second.Pm, Smith, items[book]));

        // 9: gating off shows every item of the group and no hint line.
        using var stranger = new Player();
        ClusterFRecipeGate.Enabled = false;
        try
        {
            var rows = ClusterFRecipeGate.ItemRows(stranger.Pm, Smith, Smith.CraftGroups[group]);
            Assert.Equal(items.Count, rows.Count);
            Assert.All(rows, r => Assert.True(r.Index >= 0));
            (layout, _) = SendMenu(stranger, Smith, group);
            Assert.Contains(MakeButton(book), layout);
        }
        finally
        {
            ClusterFRecipeGate.Enabled = true;
        }
    }

    // ---------------------------------------------------------------- 3, 4

    [Fact]
    public void ALoopResetKeepsRecipesAndRankAndAKnownBookIsRefusedAndKept()
    {
        var (group, _, book, c) = FreeAndBandTwo();
        var item = Smith.CraftGroups[group].CraftItems[book];
        using var p = new Player();

        var unknown = new PatternBook(SkillName.Blacksmith, c.Band);
        p.Pm.Backpack.DropItem(unknown);
        unknown.OnDoubleClick(p.Pm);
        Assert.True(unknown.Deleted);
        Assert.True(ClusterFRecipeGate.IsKnown(p.Pm, Smith, item));

        var known = new PatternBook(SkillName.Blacksmith, c.Band);
        p.Pm.Backpack.DropItem(known);
        known.OnDoubleClick(p.Pm);
        Assert.False(known.Deleted);
        Assert.Same(p.Pm.Backpack, known.Parent);

        // A rank reached with the skill.
        ClusterFGuildSystem.Join(p.Pm, ClusterFGuildSystem.GetDef("smithing")!);
        p.Guild.AddReputation("smithing", 15_000);
        p.Pm.Skills[SkillName.Blacksmith].Base = 75.0;
        ClusterFGuildPromotion.Update(p.Pm, "smithing");
        Assert.Equal(ClusterFGuildSystem.RankMaster, ClusterFGuildSystem.GuildRankIndex("smithing", p.Guild));

        // The loop: every skill back to zero.
        foreach (var skill in Enum.GetValues<SkillName>())
        {
            p.Pm.Skills[skill].Base = 0;
        }

        ClusterFGuildPromotion.Update(p.Pm, "smithing");
        Assert.Equal(ClusterFGuildSystem.RankMaster, ClusterFGuildSystem.GuildRankIndex("smithing", p.Guild));
        Assert.True(ClusterFRecipeGate.IsKnown(p.Pm, Smith, item));
        Assert.Contains(c.Key, p.Data.LearnedRecipes);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void HintLinesCountTheBandAndNameTheRankTheWildLineHasNoCountAndNoneShowBelowTheFloor()
    {
        var (group, _, _, c) = FreeAndBandTwo();
        var craftGroup = Smith.CraftGroups[group];
        using var p = new Player();

        p.Pm.Skills[SkillName.Blacksmith].Base = 0;
        var rows = ClusterFRecipeGate.ItemRows(p.Pm, Smith, craftGroup);
        Assert.DoesNotContain(rows, r => r.Hint != null);

        p.Pm.Skills[SkillName.Blacksmith].Base = 200;
        rows = ClusterFRecipeGate.ItemRows(p.Pm, Smith, craftGroup);
        foreach (var r in rows.Where(r => r.Hint != null))
        {
            _out.WriteLine(r.Hint);
        }

        var hidden = craftGroup.CraftItems.Count(i => ClusterFRecipeGate.Classify(Smith, i) is { Kind: RecipeKind.Book } k && k.Band == c.Band);
        var expected = $"{hidden} more pattern{(hidden == 1 ? "" : "s")}: Society of Smiths, rank " +
                       $"{ClusterFGuildPromotion.RankNameForIndex("smithing", c.Band - 1)}.";
        Assert.Contains(rows, r => r.Hint == expected);

        // The wild line: in some blacksmithy group with a pinned recipe or a scroll item, at most once, no digits.
        var wildGroup = Smith.CraftGroups.First(g => g.CraftItems.Any(i =>
            ClusterFRecipeGate.Classify(Smith, i).Kind is RecipeKind.OwnRecipe or RecipeKind.Scroll));
        var wild = ClusterFRecipeGate.ItemRows(p.Pm, Smith, wildGroup).Where(r => r.Hint == ClusterFRecipeGate.WildHint).ToList();
        Assert.Single(wild);
        Assert.DoesNotContain(wild[0].Hint, char.IsDigit);

        // The help page for a book hint names the guild's hall.
        var lines = RecipeHintGump.Lines(SkillName.Blacksmith, c.Band);
        Assert.Contains(lines, l => l.Contains("Society of Smiths") && l.Contains("The Haven Blacksmith")); // cc-P678
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void PromotionNeedsTheSkillRankFiveNeedsTheTrialAndBooksAreGrantedOnce()
    {
        using var p = new Player();
        ClusterFGuildSystem.Join(p.Pm, ClusterFGuildSystem.GetDef("smithing")!);
        p.Guild.AddReputation("smithing", 200_000);

        // Without the skill: standing alone stops at what 40 skill allows (rank 1).
        p.Pm.Skills[SkillName.Blacksmith].Base = 40;
        ClusterFGuildPromotion.Update(p.Pm, "smithing");
        Assert.Equal(1, ClusterFGuildSystem.GuildRankIndex("smithing", p.Guild));

        var bandOneBook = ClusterFRecipeStore.BookKey(SkillName.Blacksmith, ClusterFRecipeGate.MergedBand(Smith, 1));
        Assert.Contains(InPack<PatternBook>(p.Pm), b => b.Key == bandOneBook);

        // Rank 5 without the trial, once the waiver is cleared: held at 4.
        ClusterFGuildPromotion.TrialsWaivedUntilBuilt = false;
        try
        {
            p.Pm.Skills[SkillName.Blacksmith].Base = 100;
            ClusterFGuildPromotion.Update(p.Pm, "smithing");
            Assert.Equal(ClusterFGuildSystem.RankGrandmaster, ClusterFGuildSystem.GuildRankIndex("smithing", p.Guild));

            p.Guild.TrialsPassed.Add("smithing");
            ClusterFGuildPromotion.Update(p.Pm, "smithing");
            Assert.Equal(ClusterFGuildSystem.RankTop, ClusterFGuildSystem.GuildRankIndex("smithing", p.Guild));
        }
        finally
        {
            ClusterFGuildPromotion.TrialsWaivedUntilBuilt = true;
        }

        // No duplicate grant: a second character on the account, promoted the same way, gets no book its account knows.
        foreach (var b in InPack<PatternBook>(p.Pm))
        {
            b.OnDoubleClick(p.Pm);
        }

        using var alt = new Player(p.Account, 1);
        ClusterFGuildSystem.Join(alt.Pm, ClusterFGuildSystem.GetDef("smithing")!);
        alt.Guild.AddReputation("smithing", 50_000);
        alt.Pm.Skills[SkillName.Blacksmith].Base = 95;
        var said = ClusterFGuildPromotion.Update(alt.Pm, "smithing");
        foreach (var line in said)
        {
            _out.WriteLine(line);
        }

        Assert.Empty(InPack<PatternBook>(alt.Pm));
        Assert.Contains(said, l => l.Contains("already knows"));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheShopListsOnlyTheBandAheadChargesGuildCurrencyAndBandFiveNeedsTheMaterial()
    {
        using var p = new Player();
        ClusterFGuildSystem.Join(p.Pm, ClusterFGuildSystem.GetDef("smithing")!);
        var bandOne = ClusterFRecipeGate.MergedBand(Smith, 1);

        var rows = GuildRecipeShop.Rows(p.Pm, "smithing");
        foreach (var r in rows)
        {
            _out.WriteLine($"rank 0: {r.Key} {r.Price}");
        }

        Assert.All(rows, r => Assert.True(r.Book && r.Band == bandOne));
        Assert.Single(rows);

        // Pays guild currency, never gold.
        p.Guild.AddCurrency("smithing", 1_000);
        p.Pm.Backpack.DropItem(new Gold(5_000));
        Assert.True(GuildRecipeShop.Buy(p.Pm, "smithing", rows[0].Key, out var message), message);
        Assert.Equal(1_000 - rows[0].Price, p.Guild.GetCurrency("smithing"));
        Assert.Equal(5_000, p.Pm.Backpack.GetAmount(typeof(Gold)));
        Assert.Contains(InPack<PatternBook>(p.Pm), b => b.Key == rows[0].Key);

        // Band V: under a table whose Band V starts at 95 (so stock blacksmithy has scroll items), at rank 4.
        var table = new RecipeGatingTable
        {
            Bands =
            [
                new(1, "I", 30, 50, true), new(2, "II", 50, 70, true), new(3, "III", 70, 90, true),
                new(4, "IV", 90, 95, true), new(5, "V", 95, 120, false), new(6, "VI", 120, 200, false)
            ]
        };
        RecipeGatingTable.Current = table;
        ClusterFRecipeGate.ClearCache();
        try
        {
            p.Guild.AddReputation("smithing", 60_000);
            p.Pm.Skills[SkillName.Blacksmith].Base = 95;
            ClusterFGuildPromotion.Update(p.Pm, "smithing");
            Assert.Equal(ClusterFGuildSystem.RankGrandmaster, ClusterFGuildSystem.GuildRankIndex("smithing", p.Guild));

            var scroll = GuildRecipeShop.Rows(p.Pm, "smithing").First(r => !r.Book);
            Assert.Equal(typeof(PlatinumIngot), scroll.Material);
            p.Guild.AddCurrency("smithing", scroll.Price);

            Assert.False(GuildRecipeShop.Buy(p.Pm, "smithing", scroll.Key, out message));
            _out.WriteLine($"without the ingots: {message}");

            p.Pm.Backpack.DropItem(new PlatinumIngot(scroll.MaterialAmount));
            Assert.True(GuildRecipeShop.Buy(p.Pm, "smithing", scroll.Key, out message), message);
            Assert.Equal(0, p.Pm.Backpack.GetAmount(typeof(PlatinumIngot)));
            Assert.Contains(InPack<AccountRecipeScroll>(p.Pm), s => s.RecipeKey == scroll.Key);
        }
        finally
        {
            RecipeGatingTable.Current = RecipeGatingTable.Defaults;
            ClusterFRecipeGate.ClearCache();
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void RepairSmeltAndEnhanceStillStartWithNoRecipes()
    {
        using var p = new Player();
        var hammer = new SmithHammer();
        p.Pm.Backpack.DropItem(hammer);
        var anvil = new Anvil();
        var forge = new Forge();
        anvil.MoveToWorld(new Point3D(p.Pm.X + 1, p.Pm.Y, p.Pm.Z), Map.Trammel);
        forge.MoveToWorld(new Point3D(p.Pm.X - 1, p.Pm.Y, p.Pm.Z), Map.Trammel);
        Assert.Empty(p.Data.LearnedRecipes);
        Smith.GetContext(p.Pm).LastResourceIndex = 0; // Enhance needs a material picked (iron)

        try
        {
            // CraftGump.OnResponse Misc buttons: GetButtonID(6, index) = 1 + 6 + index * 7. Smelt 1, Repair 5, Enhance 8.
            foreach (var (name, index) in new[] { ("Smelt", 1), ("Repair", 5), ("Enhance", 8) })
            {
                p.Pm.Target = null;
                var gump = new CraftGump(p.Pm, Smith, hammer, null);
                gump.OnResponse(p.Ns, new RelayInfo(1 + 6 + index * 7, default, default, default, default));
                _out.WriteLine($"{name}: target {p.Pm.Target?.GetType().Name ?? "none"}");
                Assert.NotNull(p.Pm.Target);
            }
        }
        finally
        {
            p.Pm.Target = null;
            anvil.Delete();
            forge.Delete();
        }
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void AMuleSchematicLearnedOnceLetsTheModuleBeCraftedAgainAndAgain()
    {
        var (craft, (id, tier)) = MuleModuleCrafts.Map.First(kv => kv.Value.Tier == 1);
        var system = MuleModuleCrafts.Row(id)!.System();
        using var p = new Player();

        Assert.False(ClusterFRecipeGate.IsKnown(p.Pm, system, craft));
        Assert.True(ClusterFRecipeStore.Learn(p.Pm, ClusterFRecipeStore.RecipeKey(MuleModuleCrafts.RecipeId(id, tier))));

        // Known before and after each craft: nothing is consumed by knowing it.
        for (var i = 0; i < 3; i++)
        {
            Assert.True(ClusterFRecipeGate.IsKnown(p.Pm, system, craft));
        }

        using var alt = new Player(p.Account, 1);
        Assert.True(ClusterFRecipeGate.IsKnown(alt.Pm, system, craft));
        Assert.True(alt.Pm.HasRecipe(MuleModuleCrafts.RecipeId(id, tier))); // written onto the character for pinned's checks
    }

    // ---------------------------------------------------------------- 11

    [Fact]
    public void EveryCraftSkillWithAMenuHasExactlyOneOwningGuildThatTeachesIt()
    {
        CraftSystem[] all =
        [
            DefAlchemy.CraftSystem, DefBlacksmithy.CraftSystem, DefBowFletching.CraftSystem, DefCarpentry.CraftSystem,
            DefCartography.CraftSystem, DefCooking.CraftSystem, DefGlassblowing.CraftSystem, DefInscription.CraftSystem,
            DefMasonry.CraftSystem, DefTailoring.CraftSystem, DefTinkering.CraftSystem
        ];

        foreach (var system in all)
        {
            var owners = RecipeOwners.BySkill.Where(kv => kv.Key == system.MainSkill).Select(kv => kv.Value).ToList();
            var owner = Assert.Single(owners);
            Assert.Contains(system.MainSkill, GuildSkillTable.For(owner));
            _out.WriteLine($"{system.GetType().Name,-16} {system.MainSkill,-12} {owner}");
        }

        // And every owner is a registered guild.
        Assert.All(RecipeOwners.BySkill.Values, k => Assert.NotNull(ClusterFGuildSystem.GetDef(k)));
    }

    // ---------------------------------------------------------------- data and saves

    [Fact]
    public void TheDataFileRoundTripsAndABadOneIsRefusedWhole()
    {
        var json = RecipeGatingTable.ToJson(RecipeGatingTable.Defaults);
        var errors = new List<string>();
        var parsed = RecipeGatingTable.Parse(json, "test", errors);
        Assert.Empty(errors);
        Assert.NotNull(parsed);
        Assert.Equal(RecipeGatingTable.Defaults.Bands, parsed.Bands);
        Assert.Equal(RecipeGatingTable.Defaults.BookPrice, parsed.BookPrice);

        var bad = json.Replace("\"Floor\": 50", "\"Floor\": 55");
        Assert.NotEqual(json, bad);
        var lines = RecipeGatingTable.Apply(bad, "bad");
        foreach (var l in lines)
        {
            _out.WriteLine(l);
        }

        Assert.Contains("REFUSED", lines[0]);
        Assert.Same(RecipeGatingTable.Defaults, RecipeGatingTable.Current);
    }

    [Fact]
    public void TheAccountsRecipesAndTheGuildsPromotionRecordsSurviveASave()
    {
        using var p = new Player();
        p.Data.LearnedRecipes.Add("book:Blacksmith:2");
        p.Data.LearnedRecipes.Add("recipe:12");
        var g = p.Guild;
        g.JoinedGuilds.Add("smithing");
        g.RankCap["smithing"] = 3;
        g.PromotedRank["smithing"] = 2;
        g.TrialsPassed.Add("smithing");

        var w = new BufferWriter(true);
        p.Data.Serialize(w);
        var r = new BufferReader(w.Buffer);
        var copy = new ClusterFAccountData(r);

        Assert.Equal(p.Data.LearnedRecipes.OrderBy(k => k), copy.LearnedRecipes.OrderBy(k => k));
        var cg = copy.GetOrCreateGuildData(p.Pm.Serial);
        Assert.Equal(3, cg.RankCap["smithing"]);
        Assert.Equal(2, cg.PromotedRank["smithing"]);
        Assert.Contains("smithing", cg.TrialsPassed);
    }

    [Fact]
    public void AGuildRecordFromBeforePromotionStartsTrackedAtItsStandingRank()
    {
        // A v2 record: joined the Society and the Warriors, Journeyman standing in both.
        var old = new CharacterGuildData();
        old.JoinedGuilds.Add("smithing");
        old.JoinedGuilds.Add("warriors");
        old.GuildReputation["smithing"] = 5_000;
        old.GuildReputation["warriors"] = 5_000;

        var w = new BufferWriter(true);
        old.Serialize(w);
        // Rewrite the version to 2 and drop the v3 tail (three empty counts, 12 bytes).
        var buffer = w.Buffer;
        var length = (int)w.Position - 12;
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 2);

        var r = new BufferReader(buffer.AsSpan(0, length).ToArray());
        var read = new CharacterGuildData(r);

        Assert.Equal(ClusterFGuildSystem.RankJourneyman, read.RankCap["smithing"]);
        Assert.Equal(ClusterFGuildSystem.RankJourneyman, read.PromotedRank["smithing"]);
        Assert.Equal(ClusterFGuildSystem.RankJourneyman, ClusterFGuildSystem.GuildRankIndex("smithing", read));
        Assert.False(read.RankCap.ContainsKey("warriors")); // not a craft guild: standing alone, as before
        Assert.Equal(ClusterFGuildSystem.RankJourneyman, ClusterFGuildSystem.GuildRankIndex("warriors", read));
    }

    [Fact]
    public void BandCountsPerCraftSkill()
    {
        foreach (var line in ClusterFRecipeStore.Counts())
        {
            _out.WriteLine(line);
            Console.WriteLine($"[p78-counts] {line}");
        }

        Assert.Equal(8, ClusterFRecipeGate.GatedSystems().Count);
    }
}

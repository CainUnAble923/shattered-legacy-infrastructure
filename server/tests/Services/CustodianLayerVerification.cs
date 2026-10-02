// CustodianLayerVerification.cs
//
// cc-P33 (F-3), stage 4: the Custodians as our layer on Clean Up Britannia (customizations ClusterFCustodianSystem.cs,
// TrashBag.cs, SanitationWarden*.cs, ClusterFCraftRejects.cs). Notes in shard-migration
// notes/cc-P33-clean-up-britannia.md.
//
// Facts:
//   1. The trash bag is free and one per character: asking again deletes the old bag wherever it is (here, the bank)
//      and leaves another character's bag alone. The bag records its owner.
//   2. The bag's owner survives a save (version 1), and a version 0 bag (written as the pre-cc-P33 build wrote it)
//      loads with no owner.
//   3. Dumping the bag pays each item's Clean Up value, and only the owner can dump it.
//   4. Custodian rank follows lifetime Clean Up points, from any source (a trash barrel too), and does not drop when
//      points are spent. The guild's own rank reader agrees.
//   5. A corpse pays CorpseBasePoints plus its contents at their Clean Up values; a player's corpse is not litter.
//   6. Civic Tokens are gone from every path: no award (cleanup adds no "custodians" scrip), no spend (the Civic
//      Contracts and the Apprentice task pay no scrip and no standing), no display (no gump says "token"), and a
//      balance left in a save is cleared at load with the standing re-synced, idempotently.
//   7. Craft X's rejects go into the crafter's own trash bag (the F-3 hook P18 left), and never into someone else's.

using System;
using System.Collections.Concurrent;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.Craft;
using Server.Engines.Points;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CustodianLayerVerification
{
    private static readonly Point3D Spot = new(1430, 1770, 0);

    private readonly ITestOutputHelper _out;

    public CustodianLayerVerification(ITestOutputHelper output)
    {
        _out = output;
        CleanUpCraftHost.EnsureCraftSystems();
        CleanUpCraftHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        ClusterFWorkOrderSystem.Configure();
    }

    private sealed class Member : IDisposable
    {
        public readonly Account Account = new($"p33{Guid.NewGuid():N}"[..16], "p33-test-only");
        public readonly PlayerMobile Pm;

        public Member(bool join = true, int slot = 0, Account account = null)
        {
            if (account != null)
            {
                Account = account;
            }

            Pm = new PlayerMobile { Player = true };
            Pm.AddItem(new Backpack());
            Pm.RawStr = 100;
            Account[slot] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);

            if (join)
            {
                ClusterFGuildSystem.Join(Pm, ClusterFGuildSystem.GetDef("custodians"));
                SanitationWarden.OnCustodiansJoined(Pm);
            }
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreateGuild(Pm);

        public void Dispose()
        {
            foreach (var bag in TrashBag.OwnedBy(Pm))
            {
                bag.Delete();
            }

            CleanUpBritanniaData.Instance.RemoveEntry(Pm);
            Pm.Delete();
        }
    }

    // -- 1 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTrashBagIsFreeAndOnePerCharacterWhereverTheOldOneIs()
    {
        using var a = new Member();
        using var b = new Member();

        var first = TrashBag.OwnedBy(a.Pm).Single(); // given on joining
        Assert.Same(a.Pm, first.Owner);
        Assert.True(first.IsChildOf(a.Pm.Backpack));

        var bOld = TrashBag.OwnedBy(b.Pm).Single();

        // Leave it in the bank, then ask again.
        a.Pm.BankBox.DropItem(first);
        var points = CleanUpBritanniaData.Instance.GetPoints(a.Pm);
        var second = TrashBag.IssueTo(a.Pm);

        Assert.True(first.Deleted);
        Assert.False(second.Deleted);
        Assert.True(second.IsChildOf(a.Pm.Backpack));
        Assert.Same(a.Pm, second.Owner);
        Assert.Single(TrashBag.OwnedBy(a.Pm));
        Assert.Equal(points, CleanUpBritanniaData.Instance.GetPoints(a.Pm), 9); // free

        // On the ground too.
        second.MoveToWorld(new Point3D(Spot.X + 5, Spot.Y, Spot.Z), Map.Trammel);
        var third = TrashBag.IssueTo(a.Pm);
        Assert.True(second.Deleted);
        Assert.Single(TrashBag.OwnedBy(a.Pm));
        Assert.Same(third, TrashBag.OwnedBy(a.Pm)[0]);

        // Another character's bag is not touched.
        Assert.False(bOld.Deleted);
        Assert.Single(TrashBag.OwnedBy(b.Pm));

        // Joining again does not hand out a second bag.
        SanitationWarden.OnCustodiansJoined(a.Pm);
        Assert.Single(TrashBag.OwnedBy(a.Pm));
    }

    // -- 2 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheBagsOwnerSurvivesASaveAndAVersion0BagHasNone()
    {
        using var a = new Member(join: false);
        var bag = new TrashBag { Owner = a.Pm };

        try
        {
            var buffer = new byte[16384];
            var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
            bag.Serialize(writer);
            writer.Flush();
            var length = (int)writer.Position;

            var copy = new TrashBag(bag.Serial);
            var reader = new BufferReader(buffer);
            copy.Deserialize(reader);
            Assert.Equal(length, (int)reader.Position);
            Assert.Same(a.Pm, copy.Owner);

            // Version 1 ends with the version (int 1) and the owner's serial: drop both and write version 0, as the
            // pre-cc-P33 build wrote it (SerializationGenerator(0, false), no fields).
            Assert.Equal(1, BitConverter.ToInt32(buffer, length - 8));
            Assert.Equal((uint)a.Pm.Serial, BitConverter.ToUInt32(buffer, length - 4));
            var v0 = buffer.Take(length - 8).Concat(BitConverter.GetBytes(0)).ToArray();

            var old = new TrashBag(bag.Serial);
            var v0Reader = new BufferReader(v0);
            old.Deserialize(v0Reader);
            Assert.Equal(v0.Length, (int)v0Reader.Position);
            Assert.Null(old.Owner);

            _out.WriteLine($"version 1: {length} bytes; version 0: {v0.Length} bytes, owner {old.Owner?.ToString() ?? "none"}");
        }
        finally
        {
            bag.Delete();
        }
    }

    // -- 3 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void DumpingPaysCleanUpValuesAndOnlyTheOwnerCanDump()
    {
        using var a = new Member();
        using var b = new Member();
        var bag = TrashBag.OwnedBy(a.Pm).Single();

        bag.DropItem(new VeriteIngot(2));     // 17
        bag.DropItem(new Longsword());        // 12 x .1 = 1
        bag.DropItem(new Candle());           // 0, still litter: deleted
        var blessed = new Candle { LootType = LootType.Blessed };
        bag.DropItem(blessed);                // not eligible: stays

        // Not the owner: nothing happens.
        b.Pm.Backpack.DropItem(bag);
        Assert.Equal(0.0, TrashBagGump.DumpBag(b.Pm, bag), 9);
        Assert.Equal(4, bag.Items.Count);
        a.Pm.Backpack.DropItem(bag);

        var before = CleanUpBritanniaData.Instance.GetPoints(a.Pm);
        var paid = TrashBagGump.DumpBag(a.Pm, bag);

        Assert.Equal(18.0, paid, 9);
        Assert.Equal(18.0, CleanUpBritanniaData.Instance.GetPoints(a.Pm) - before, 9);
        Assert.Single(bag.Items);
        Assert.Same(blessed, bag.Items[0]);
    }

    // -- 4 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void RankFollowsLifetimePointsAndDoesNotDropWhenTheyAreSpent()
    {
        using var a = new Member();
        var cub = CleanUpBritanniaData.Instance;

        Assert.Equal("Volunteer", ClusterFCustodianSystem.GetCustodianRank(a.Guild.GetReputation("custodians")));

        // Points from a plain trash barrel count too: every Clean Up award moves the rank.
        var barrel = new TrashBarrel();
        barrel.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);

        try
        {
            var ingots = new ValoriteIngot(13); // 130
            a.Pm.Backpack.DropItem(ingots);
            Assert.True(barrel.OnDragDrop(a.Pm, ingots));
            barrel.Empty(501479);
        }
        finally
        {
            barrel.Delete();
        }

        Assert.Equal(130.0, cub.GetLifetimePoints(a.Pm), 9);
        Assert.Equal(130, a.Guild.GetReputation("custodians"));
        Assert.Equal("Custodian", ClusterFCustodianSystem.GetCustodianRank(a.Guild.GetReputation("custodians")));
        Assert.Equal("Custodian", ClusterFGuildSystem.GetRankName("custodians", 130));

        Assert.True(cub.DeductPoints(a.Pm, 100));
        Assert.Equal(30.0, cub.GetPoints(a.Pm), 9);
        Assert.Equal(130, a.Guild.GetReputation("custodians"));
        Assert.Equal("Custodian", ClusterFCustodianSystem.GetCustodianRank(a.Guild.GetReputation("custodians")));

        // A non-member's points are kept but write no standing; joining brings the rank in at once.
        using var later = new Member(join: false);
        cub.AwardPoints(later.Pm, 40, message: false);
        Assert.False(ClusterFAccountPersistence.GetGuild(later.Pm)?.GuildReputation.ContainsKey("custodians") ?? false);
        ClusterFGuildSystem.Join(later.Pm, ClusterFGuildSystem.GetDef("custodians"));
        SanitationWarden.OnCustodiansJoined(later.Pm);
        Assert.Equal(40, later.Guild.GetReputation("custodians"));
        Assert.Equal("Junior Custodian", ClusterFCustodianSystem.GetCustodianRank(later.Guild.GetReputation("custodians")));

        // The ladder.
        Assert.Equal("Junior Custodian", ClusterFCustodianSystem.GetCustodianRank(25));
        Assert.Equal("Senior Custodian", ClusterFCustodianSystem.GetCustodianRank(500));
        Assert.Equal("Chief Custodian", ClusterFCustodianSystem.GetCustodianRank(1250));
        Assert.Equal((500, "Senior Custodian"), ClusterFCustodianSystem.GetNextRank(130));
    }

    // -- 5 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void ACorpsePaysItsBasePlusContentsAndAPlayersCorpseIsNotLitter()
    {
        using var a = new Member();
        var rat = new Rat();
        var corpse = new Corpse(rat, []);

        try
        {
            corpse.DropItem(new Gold(300));        // 3
            corpse.DropItem(new ShadowIronIngot(4)); // 3
            corpse.DropItem(new Candle());         // 0
            corpse.MoveToWorld(Spot, Map.Trammel);

            Assert.True(ClusterFCustodianSystem.IsEligible(corpse));
            Assert.Equal(ClusterFCustodianSystem.CorpseBasePoints + 6.0, ClusterFCustodianSystem.ComputePoints(corpse), 9);

            var empty = new Corpse(rat, []);
            Assert.Equal(ClusterFCustodianSystem.CorpseBasePoints, ClusterFCustodianSystem.ComputePoints(empty), 9);
            empty.Delete();

            var playerCorpse = new Corpse(a.Pm, []);
            playerCorpse.MoveToWorld(Spot, Map.Trammel);
            Assert.False(ClusterFCustodianSystem.IsEligible(playerCorpse));
            playerCorpse.Delete();
        }
        finally
        {
            corpse.Delete();
            rat.Delete();
        }
    }

    // -- 6 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void CivicTokensAreGoneFromEveryPath()
    {
        using var a = new Member();
        var bag = TrashBag.OwnedBy(a.Pm).Single();

        // No award: cleaning up adds Clean Up points and no "custodians" scrip.
        bag.DropItem(new ValoriteIngot(1));
        TrashBagGump.DumpBag(a.Pm, bag);
        Assert.Equal(0, a.Guild.GetCurrency("custodians"));
        Assert.False(a.Guild.GuildCurrency.ContainsKey("custodians"));

        // No source: the Apprentice task and the Civic Contracts pay no scrip and no standing.
        var def = ClusterFGuildSystem.GetDef("custodians");
        Assert.Equal(0, def.JoinScrip);
        Assert.Equal(0, def.JoinReputation);

        var orders = ClusterFWorkOrderSystem.GetForGuild("custodians").ToList();
        Assert.Equal(5, orders.Count);
        Assert.All(orders, o => Assert.Equal(0, o.VoucherReward));
        Assert.All(orders, o => Assert.Equal(0, o.StandingReward));
        Assert.Equal([0, 25, 125, 500, 1250], orders.Select(o => o.MinStanding).OrderBy(m => m).ToArray());

        // No display: no gump the Custodians show says "token".
        using var outsider = new Member(join: false);
        Gump[] gumps =
        [
            new SanitationWardenGump(a.Pm, def, a.Account),
            new SanitationWardenGump(outsider.Pm, def, outsider.Account),
            new TrashBagGump(a.Pm, bag)
        ];

        foreach (var gump in gumps)
        {
            var text = string.Join(" | ", gump.Entries.Select(e => e switch
            {
                GumpLabel l => l.Text,
                GumpHtml h  => h.Text,
                _           => null
            }).Where(t => t != null));

            _out.WriteLine($"{gump.GetType().Name}: {text[..Math.Min(160, text.Length)]}...");
            Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
        }

        // A balance and a token-earned standing left in a save are cleared at load.
        a.Guild.GuildCurrency["custodians"] = 1234;
        a.Guild.GuildReputation["custodians"] = 5000;

        var (balances, standings) = ClusterFCustodianSystem.RetireCivicTokens();
        Assert.True(balances >= 1 && standings >= 1, $"cleared {balances}, re-synced {standings}");
        Assert.False(a.Guild.GuildCurrency.ContainsKey("custodians"));
        Assert.Equal((int)Math.Floor(CleanUpBritanniaData.Instance.GetLifetimePoints(a.Pm)), a.Guild.GetReputation("custodians"));
        Assert.Equal("Volunteer", ClusterFCustodianSystem.GetCustodianRank(a.Guild.GetReputation("custodians")));

        // Idempotent.
        Assert.Equal((0, 0), ClusterFCustodianSystem.RetireCivicTokens());
    }

    // -- 7 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void CraftXRejectsGoIntoTheCraftersOwnTrashBagOnly()
    {
        using var a = new Member();
        using var b = new Member();
        var own = TrashBag.OwnedBy(a.Pm).Single();
        var theirs = TrashBag.OwnedBy(b.Pm).Single();

        var run = new MakeXRun { From = a.Pm, System = DefCarpentry.CraftSystem };

        // Someone else's bag in the pack is not used.
        own.Internalize();
        a.Pm.Backpack.DropItem(theirs);
        var first = new QuarterStaff();
        a.Pm.Backpack.DropItem(first);
        ClusterFCraftRejects.Handle(run, first);
        Assert.Equal(0, run.Trashed);
        Assert.False(first.IsChildOf(theirs));
        b.Pm.Backpack.DropItem(theirs);

        // The crafter's own bag is.
        a.Pm.Backpack.DropItem(own);
        var reject = new QuarterStaff();
        a.Pm.Backpack.DropItem(reject);
        ClusterFCraftRejects.Handle(run, reject);

        Assert.Equal(1, run.Trashed);
        Assert.True(reject.IsChildOf(own));
        Assert.Contains("1 to the trash bag", ClusterFCraftRejects.Describe(run));
    }
}

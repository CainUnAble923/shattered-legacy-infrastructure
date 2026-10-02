// AccountDataSerializerVerification.cs
//
// D40: the live shard stopped loading its world on 2026-09-29 at 14:16 with
//   Bad deserialize of Server.ClusterFAccountPersistence (0x40128E03)
//   ArgumentOutOfRangeException: Ticks must be between ... at ReadDateTime from WoodDiscoveryEntry..ctor
//
// The save had been written by the 2026-06-29 build (image sl-modernuo:rollback-live-20260927),
// whose WoodDiscoveryEntry was version 1: a per-grove location list in place of FirstFound. That
// version never reached server/customizations. The canonical reader read the version number and
// ignored it, so it read the location list's first string as ticks. Full measurement in
// shard-migration notes/cc-P11-account-data-serializer.md.
//
// cc-P13 made that version 1 canonical: grove tracking is wanted and the old live world is not being
// kept, so nothing needs version 0 written any more. Each wood type keeps a list of groves
// (facet, region, Point3D, DateTime, amount). Notes in shard-migration notes/cc-P13-grove-tracking.md.
//
// Facts:
//   1. Every ClusterFAccountData field survives a write and a read with every collection non-empty,
//      and the read consumes exactly the bytes the write produced (the check ModernUO's loader makes).
//      Wood entries carry several groves, and every grove field is compared.
//   2. The exact 276 bytes of WoodDiscoveries from the live save read back, and the reader stops on
//      the last byte. Written again they are version 1, keep their groves, and match the save byte for byte.
//   3. An unknown WoodDiscoveryEntry version fails loudly instead of being read as another version.
//   4. One wood type with several groves round-trips as version 1, every field intact.
//   5. A version-0 entry reads forward as one Unknown grove carrying the old FirstFound, and is
//      written back as version 1.
//   6. Groves merge only on the same facet and region; region labels never call a house wilderness.
//
// cc-P15 added version 14, a guild starter record per character (notes/cc-P15-guild-starter-path.md):
//   7. Fact 1 carries two characters' records, every field compared.
//   8. Version 13 bytes, written by a frozen copy of the pre-cc-P15 writer, load whole and come back
//      with no records; written again they are version 15.
//   9. A newer account-data version, or an unknown starter-record version, fails loudly.
//
// cc-P18 (F-7) added version 15: guild membership, Apprentice marks, reputation, scrip, work orders,
// smith commissions and the Artificer order moved from the account to CharacterGuildData, one per
// character serial (notes/cc-P18-reset-stone-young-craftx.md):
//  10. Fact 1 carries two characters' guild data and exploration, every field compared.
//  11. A version 14 save with account-level guild data (bytes from a frozen copy of the pre-cc-P18
//      writer) loads whole: the guild data and the Apprentice flags are cleared, not given to a
//      character, and everything else is kept. Written again it is version 15. A version 14 save with
//      no guild data is not reported as having dropped any.
//  12. An unknown CharacterGuildData version fails loudly.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

public class AccountDataSerializerVerification
{
    private readonly ITestOutputHelper _out;

    public AccountDataSerializerVerification(ITestOutputHelper output) => _out = output;

    // WoodDiscoveries of account CainUnAble as the live save holds them: Saves/Items/Items.bin,
    // object 0x40128E03, bytes 265404..265679 of the object. Count 4, then four version-1 entries
    // (RegularWood 244, OakWood 101, AshWood 20, YewWood 10), each with one ("Unknown", "Unknown",
    // 0,0,0) location that the 2026-06-29 build synthesised when it migrated them from version 0.
    private const string LiveWoodDiscoveriesHex =
        "0400000001000000010B526567756C6172576F6F64F400000001000000010000000107556E6B6E6F776E" +
        "0107556E6B6E6F776E000000000000000000000000FC10F673E4BBDE08F40000000100000001074F616B" +
        "576F6F646500000001000000010000000107556E6B6E6F776E0107556E6B6E6F776E0000000000000000" +
        "0000000009A7312CECBBDE0865000000010000000107417368576F6F641400000001000000010000000107" +
        "556E6B6E6F776E0107556E6B6E6F776E0000000000000000000000000B984E27B4BCDE08140000000100" +
        "00000107596577576F6F640A00000001000000010000000107556E6B6E6F776E0107556E6B6E6F776E00" +
        "0000000000000000000000C6384EC7B4BCDE080A000000";

    private static readonly Serial First  = (Serial)0x1234u;
    private static readonly Serial Second = (Serial)0x5678u;

    private static (byte[] Buffer, long Length) Write(Action<IGenericWriter> serialize)
    {
        var writer = new BufferWriter(true);
        serialize(writer);
        return (writer.Buffer, writer.Position);
    }

    private static Dictionary<uint, BitArray?[]> Chunks(ClusterFAccountData d) =>
        (Dictionary<uint, BitArray?[]>)typeof(ClusterFAccountData)
            .GetField("_exploredChunks", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(d)!;

    private static Dictionary<uint, CharacterGuildData> GuildData(ClusterFAccountData d) =>
        (Dictionary<uint, CharacterGuildData>)typeof(ClusterFAccountData)
            .GetField("_guildData", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(d)!;

    // One character's guild data with every collection non-empty and every field set. Before v15 the
    // account held exactly this, once; the frozen writers below write it at the account level.
    private static void FillGuild(CharacterGuildData g)
    {
        g.GuildReputation["mining"] = 300;
        g.GuildReputation["smithing"] = -5;
        g.GuildCurrency["mining"] = 42;
        g.JoinedGuilds.Add("mining");
        g.JoinedGuilds.Add("foresters");
        g.ApprenticeGuilds.Add("mining");

        var active = new WorkOrderEntry("mining.dullcopper.50", "mining");
        active.TamingProgress["Horse"] = 2;
        g.ActiveWorkOrders.Add(active);
        g.CompletedWorkOrders.Add(new WorkOrderEntry("smithing.plate.10", "smithing")
        {
            CompletedAt = new DateTime(2026, 9, 2, 8, 30, 0, DateTimeKind.Utc)
        });

        g.SmithCommissions.Add(new SmithCommissionEntry(
            "c1", "platechest", CraftResource.Valorite, true, "Sir Test", "Mind the rivets", 12, 34));
        var large = new SmithLargeCommissionEntry(
            "L1", "ringmail", CraftResource.Agapite, false, "Dame Test", "", 56, 78);
        large.FulfilledPieces.Add("ringmailchest");
        g.SmithLargeCommissions.Add(large);

        g.AcceptArtificerOrder("artificer.slayer.silver", 0x40001234u);
    }

    private static ClusterFAccountData FullyPopulated()
    {
        var d = new ClusterFAccountData
        {
            Renown             = 1234,
            AchievementPoints  = 567,
            LastSeenBulletinId = 89
        };

        var restored = new RestorationEntry("legacy.jacobs_pickaxe", "quest")
        {
            RestorationCount = 3,
            LastRestoredAt   = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc),
            HasActiveCopy    = true
        };
        d.RestorationRegistry[restored.Key] = restored;
        var neverRestored = new RestorationEntry("legacy.hammer_of_hephaestus", "admin");
        d.RestorationRegistry[neverRestored.Key] = neverRestored;

        d.SetFlag("league.joined");
        d.SetFlagValue("league.referrer", "Aetherion");

        var ore = new OreDiscoveryEntry("Valorite") { TotalMined = 77, State = DiscoveryState.Reported };
        ore.AddLocation("Felucca", "Minoc", new Point3D(2560, 480, 0)).AmountMined = 50;
        ore.AddLocation("Trammel", "Wilderness", new Point3D(1, 2, -3)).Reported = true;
        d.OreDiscoveries[ore.OreKey] = ore;

        d.ImbuingDiscoveries["Hit Chance Increase"] = 4;

        d.EncounteredCreatures.Add("Dragon");
        d.EncounteredCreatures.Add("Ridgeback");

        // Two characters' exploration. The first: two facets visited, the other four never, bits set at
        // both ends and in the middle. The second: one facet.
        var fel = d.GetOrCreateExploration(First, 0, 40, 30);
        fel[0] = true;
        fel[599] = true;
        fel[1199] = true;
        d.GetOrCreateExploration(First, 4, 13, 7)[90] = true;
        d.GetOrCreateExploration(Second, 1, 40, 30)[7] = true;

        d.WoodDiscoveries["Ironwood"] = SeveralGroves();
        var starwood = new WoodDiscoveryEntry("Starwood") { TotalChopped = 1 };
        starwood.Locations.Add(new WoodLocationRecord(
            "Ilshenar", "Wilderness", new Point3D(1100, 600, -80), new DateTime(2026, 9, 3, 1, 2, 3, DateTimeKind.Utc), 1));
        d.WoodDiscoveries["Starwood"] = starwood;

        // v14 (cc-P15): two characters' guild starter records, one of them everything, one nearly empty.
        var full = d.GetOrCreateGuildStarter(First);
        full.ToolsTaken.Add("mining");
        full.ToolsTaken.Add("warriors");
        full.ItemsTaken.Add("TheDeluciansLostMine");
        full.ItemsTaken.Add("EnGuarde");
        full.WelcomeShown = true;
        d.GetOrCreateGuildStarter(Second).ToolsTaken.Add("keepers");

        // v15 (cc-P18): two characters' guild data, one of them everything, one a single guild with scrip.
        FillGuild(d.GetOrCreateGuildData(First));
        var second = d.GetOrCreateGuildData(Second);
        second.JoinedGuilds.Add("keepers");
        second.GuildCurrency["keepers"] = 7;
        return d;
    }

    private static Dictionary<uint, GuildStarterRecord> StarterRecords(ClusterFAccountData d) =>
        (Dictionary<uint, GuildStarterRecord>)typeof(ClusterFAccountData)
            .GetField("_guildStarter", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(d)!;

    // Version 13 as the build before cc-P15 wrote it: a frozen copy of ClusterFAccountData.Serialize at
    // d0a1782 (server/customizations/ClusterFAccountData.cs:276-350 there), so these bytes do not come
    // from the writer under test. The guild fields were the account's then; `legacy` supplies them.
    private static void SerializeVersion13(ClusterFAccountData d, CharacterGuildData legacy, IGenericWriter w,
        int version = 13)
    {
        w.Write(version);

        w.Write(d.Renown);
        w.Write(d.AchievementPoints);
        w.Write(d.LastSeenBulletinId);

        w.Write(legacy.GuildReputation.Count);
        foreach (var (k, v) in legacy.GuildReputation) { w.Write(k); w.Write(v); }

        w.Write(legacy.GuildCurrency.Count);
        foreach (var (k, v) in legacy.GuildCurrency) { w.Write(k); w.Write(v); }

        w.Write(d.RestorationRegistry.Count);
        foreach (var entry in d.RestorationRegistry.Values) entry.Serialize(w);

        w.Write(legacy.JoinedGuilds.Count);
        foreach (var key in legacy.JoinedGuilds) w.Write(key);

        // Before v15 the Apprentice marks were account flags "guild.apprentice.<key>" (cc-P15).
        var flags = new List<string>(d.Flags);
        foreach (var key in legacy.ApprenticeGuilds) flags.Add("guild.apprentice." + key);
        w.Write(flags.Count);
        foreach (var f in flags) w.Write(f);

        w.Write(d.FlagValues.Count);
        foreach (var (k, v) in d.FlagValues) { w.Write(k); w.Write(v); }

        w.Write(legacy.ActiveWorkOrders.Count);
        foreach (var e in legacy.ActiveWorkOrders) e.Serialize(w);

        w.Write(legacy.CompletedWorkOrders.Count);
        foreach (var e in legacy.CompletedWorkOrders) e.Serialize(w);

        w.Write(d.OreDiscoveries.Count);
        foreach (var entry in d.OreDiscoveries.Values) entry.Serialize(w);

        w.Write(legacy.SmithCommissions.Count);
        foreach (var e in legacy.SmithCommissions) e.Serialize(w);

        w.Write(legacy.SmithLargeCommissions.Count);
        foreach (var e in legacy.SmithLargeCommissions) e.Serialize(w);

        var chunks = Chunks(d);
        w.Write(chunks.Count);
        foreach (var (serial, facets) in chunks)
        {
            w.Write(serial);
            for (var f = 0; f < 6; f++)
            {
                var bits = facets[f];
                if (bits == null)
                    w.Write(false);
                else
                {
                    w.Write(true);
                    w.Write(bits);
                }
            }
        }

        w.Write(d.EncounteredCreatures.Count);
        foreach (var name in d.EncounteredCreatures) w.Write(name);

        w.Write(d.WoodDiscoveries.Count);
        foreach (var entry in d.WoodDiscoveries.Values) entry.Serialize(w);

        w.Write(d.ImbuingDiscoveries.Count);
        foreach (var (k, v) in d.ImbuingDiscoveries) { w.Write(k); w.Write(v); }

        w.Write(legacy.ActiveArtificerOrderKey ?? "");
        w.Write(legacy.ActiveArtificerItemSerial);
    }

    // Version 14 as the build before cc-P18 wrote it (ClusterFAccountData.cs:316-398 at bb51fad): the
    // version 13 layout, then the per-character guild starter records.
    private static void SerializeVersion14(ClusterFAccountData d, CharacterGuildData legacy, IGenericWriter w)
    {
        SerializeVersion13(d, legacy, w, 14);

        var records = StarterRecords(d);
        w.Write(records.Count);
        foreach (var (serial, record) in records)
        {
            w.Write(serial);
            record.Serialize(w);
        }
    }

    // Three groves on three facets, in the order found, with distinct values in every field: a named
    // region, the default region, an unnamed non-default one, a sub-second timestamp and a negative Z.
    private static WoodDiscoveryEntry SeveralGroves()
    {
        var e = new WoodDiscoveryEntry("Ironwood") { TotalChopped = 19, State = DiscoveryState.Reported };
        e.Locations.Add(new WoodLocationRecord(
            "Trammel", "Yew", new Point3D(560, 990, 0), new DateTime(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc).AddTicks(1234567), 11));
        e.Locations.Add(new WoodLocationRecord(
            "Felucca", "Wilderness", new Point3D(4400, 1100, 3), new DateTime(2026, 9, 2, 7, 30, 0, DateTimeKind.Utc), 5));
        e.Locations.Add(new WoodLocationRecord(
            "Malas", "HouseRegion", new Point3D(1000, 1500, -20), new DateTime(2026, 9, 4, 22, 15, 9, DateTimeKind.Utc), 3));
        return e;
    }

    private static void AssertSameGroves(WoodDiscoveryEntry o, WoodDiscoveryEntry c)
    {
        Assert.Equal(o.WoodKey, c.WoodKey);
        Assert.Equal(o.TotalChopped, c.TotalChopped);
        Assert.Equal(o.State, c.State);
        Assert.Equal(o.FirstFound.Ticks, c.FirstFound.Ticks);
        Assert.Equal(o.Locations.Count, c.Locations.Count);
        for (var i = 0; i < o.Locations.Count; i++)
        {
            var (ol, cl) = (o.Locations[i], c.Locations[i]);
            Assert.Equal(
                (ol.FacetName, ol.RegionName, ol.Location, ol.DiscoveredAt.Ticks, ol.AmountChopped),
                (cl.FacetName, cl.RegionName, cl.Location, cl.DiscoveredAt.Ticks, cl.AmountChopped));
        }
    }

    // The first int of a serialized WoodDiscoveryEntry is its version.
    private static int VersionOf(byte[] buffer, int offset = 0) => BitConverter.ToInt32(buffer, offset);

    private static void AssertSameGuild(CharacterGuildData o, CharacterGuildData c)
    {
        Assert.Equal(o.JoinedGuilds.OrderBy(k => k), c.JoinedGuilds.OrderBy(k => k));
        Assert.Equal(o.ApprenticeGuilds.OrderBy(k => k), c.ApprenticeGuilds.OrderBy(k => k));
        Assert.Equal(o.GuildReputation, c.GuildReputation);
        Assert.Equal(o.GuildCurrency, c.GuildCurrency);
        Assert.Equal(o.ActiveArtificerOrderKey, c.ActiveArtificerOrderKey);
        Assert.Equal(o.ActiveArtificerItemSerial, c.ActiveArtificerItemSerial);

        Assert.Equal(o.ActiveWorkOrders.Count, c.ActiveWorkOrders.Count);
        Assert.Equal(o.CompletedWorkOrders.Count, c.CompletedWorkOrders.Count);
        var orders = new List<(WorkOrderEntry, WorkOrderEntry)>();
        for (var i = 0; i < o.ActiveWorkOrders.Count; i++)
            orders.Add((o.ActiveWorkOrders[i], c.ActiveWorkOrders[i]));
        for (var i = 0; i < o.CompletedWorkOrders.Count; i++)
            orders.Add((o.CompletedWorkOrders[i], c.CompletedWorkOrders[i]));
        foreach (var (oe, ce) in orders)
        {
            Assert.Equal(oe.DefKey, ce.DefKey);
            Assert.Equal(oe.GuildKey, ce.GuildKey);
            Assert.Equal(oe.AcceptedAt, ce.AcceptedAt);
            Assert.Equal(oe.CompletedAt, ce.CompletedAt);
            Assert.Equal(oe.TamingProgress, ce.TamingProgress);
        }

        Assert.Equal(o.SmithCommissions.Count, c.SmithCommissions.Count);
        for (var i = 0; i < o.SmithCommissions.Count; i++)
        {
            var (oc, cc) = (o.SmithCommissions[i], c.SmithCommissions[i]);
            Assert.Equal(
                (oc.Id, oc.ItemKey, oc.Material, oc.RequireExceptional, oc.RequesterName, oc.RequesterNote, oc.SealReward, oc.StandingReward, oc.IssuedAt),
                (cc.Id, cc.ItemKey, cc.Material, cc.RequireExceptional, cc.RequesterName, cc.RequesterNote, cc.SealReward, cc.StandingReward, cc.IssuedAt));
        }

        Assert.Equal(o.SmithLargeCommissions.Count, c.SmithLargeCommissions.Count);
        for (var i = 0; i < o.SmithLargeCommissions.Count; i++)
        {
            var (oc, cc) = (o.SmithLargeCommissions[i], c.SmithLargeCommissions[i]);
            Assert.Equal(
                (oc.Id, oc.SetKey, oc.Material, oc.RequireExceptional, oc.RequesterName, oc.RequesterNote, oc.SealReward, oc.StandingReward, oc.IssuedAt),
                (cc.Id, cc.SetKey, cc.Material, cc.RequireExceptional, cc.RequesterName, cc.RequesterNote, cc.SealReward, cc.StandingReward, cc.IssuedAt));
            Assert.Equal(oc.FulfilledPieces, cc.FulfilledPieces);
        }
    }

    [Fact]
    public void AnAccountWithEveryCollectionNonEmptySurvivesAWriteAndARead()
    {
        var original = FullyPopulated();
        var (buffer, length) = Write(original.Serialize);

        var reader = new BufferReader(buffer);
        var copy = new ClusterFAccountData(reader);

        _out.WriteLine($"wrote {length} bytes, read {reader.Position}");
        Assert.Equal(length, reader.Position);
        Assert.False(copy.DroppedAccountGuildData);

        Assert.Equal(original.Renown, copy.Renown);
        Assert.Equal(original.AchievementPoints, copy.AchievementPoints);
        Assert.Equal(original.LastSeenBulletinId, copy.LastSeenBulletinId);
        Assert.Equal(original.Flags, copy.Flags);
        Assert.Equal(original.FlagValues, copy.FlagValues);
        Assert.Equal(original.ImbuingDiscoveries, copy.ImbuingDiscoveries);
        Assert.Equal(original.EncounteredCreatures, copy.EncounteredCreatures);

        Assert.Equal(original.RestorationRegistry.Count, copy.RestorationRegistry.Count);
        foreach (var (key, o) in original.RestorationRegistry)
        {
            var c = copy.RestorationRegistry[key];
            Assert.Equal(o.Key, c.Key);
            Assert.Equal(o.Source, c.Source);
            Assert.Equal(o.UnlockedAt, c.UnlockedAt);
            Assert.Equal(o.RestorationCount, c.RestorationCount);
            Assert.Equal(o.LastRestoredAt, c.LastRestoredAt);
            Assert.Equal(o.HasActiveCopy, c.HasActiveCopy);
        }

        Assert.Equal(original.OreDiscoveries.Count, copy.OreDiscoveries.Count);
        foreach (var (key, o) in original.OreDiscoveries)
        {
            var c = copy.OreDiscoveries[key];
            Assert.Equal(o.OreKey, c.OreKey);
            Assert.Equal(o.TotalMined, c.TotalMined);
            Assert.Equal(o.State, c.State);
            Assert.Equal(o.Locations.Count, c.Locations.Count);
            for (var i = 0; i < o.Locations.Count; i++)
            {
                Assert.Equal(o.Locations[i].FacetName, c.Locations[i].FacetName);
                Assert.Equal(o.Locations[i].RegionName, c.Locations[i].RegionName);
                Assert.Equal(o.Locations[i].Location, c.Locations[i].Location);
                Assert.Equal(o.Locations[i].DiscoveredAt, c.Locations[i].DiscoveredAt);
                Assert.Equal(o.Locations[i].AmountMined, c.Locations[i].AmountMined);
                Assert.Equal(o.Locations[i].Reported, c.Locations[i].Reported);
            }
        }

        // Exploration, both characters, every bit.
        var oChunks = Chunks(original);
        var cChunks = Chunks(copy);
        Assert.Equal(2, cChunks.Count);
        Assert.Equal(oChunks.Keys.OrderBy(k => k), cChunks.Keys.OrderBy(k => k));
        foreach (var (serial, oFacets) in oChunks)
        {
            var cFacets = cChunks[serial];
            for (var f = 0; f < 6; f++)
            {
                if (oFacets[f] == null)
                {
                    Assert.Null(cFacets[f]);
                    continue;
                }
                Assert.NotNull(cFacets[f]);
                Assert.Equal(oFacets[f]!.Length, cFacets[f]!.Length);
                for (var b = 0; b < oFacets[f]!.Length; b++)
                    Assert.True(oFacets[f]![b] == cFacets[f]![b], $"character {serial:X} facet {f} bit {b}");
            }
        }

        // FirstFound is the earliest grove. Ticks, not a formatted string, so nothing is rounded.
        Assert.Equal(original.WoodDiscoveries.Count, copy.WoodDiscoveries.Count);
        foreach (var (key, o) in original.WoodDiscoveries)
        {
            var c = copy.WoodDiscoveries[key];
            _out.WriteLine($"wood {key}: {o.Locations.Count} groves -> {c.Locations.Count}, FirstFound {o.FirstFound.Ticks} -> {c.FirstFound.Ticks}");
            AssertSameGroves(o, c);
            Assert.NotEqual(0, c.FirstFound.Ticks);
        }
        Assert.Equal(3, copy.WoodDiscoveries["Ironwood"].Locations.Count);

        // v14: every character's guild starter record, every field.
        AssertSameStarterRecords(original, copy);
        Assert.Equal(2, copy.GuildStarterRecordCount);

        // v15: every character's guild data, every field.
        Assert.Equal(2, copy.GuildDataCount);
        Assert.Equal(GuildData(original).Keys.OrderBy(k => k), GuildData(copy).Keys.OrderBy(k => k));
        foreach (var (serial, o) in GuildData(original))
            AssertSameGuild(o, GuildData(copy)[serial]);
        Assert.Contains("mining", copy.GetGuildData(First)!.JoinedGuilds);
        Assert.DoesNotContain("mining", copy.GetGuildData(Second)!.JoinedGuilds);
    }

    private static void AssertSameStarterRecords(ClusterFAccountData original, ClusterFAccountData copy)
    {
        var o = StarterRecords(original);
        var c = StarterRecords(copy);
        Assert.Equal(o.Keys.OrderBy(k => k), c.Keys.OrderBy(k => k));
        foreach (var (serial, record) in o)
        {
            Assert.Equal(record.ToolsTaken.OrderBy(k => k), c[serial].ToolsTaken.OrderBy(k => k));
            Assert.Equal(record.ItemsTaken.OrderBy(k => k), c[serial].ItemsTaken.OrderBy(k => k));
            Assert.Equal(record.WelcomeShown, c[serial].WelcomeShown);
        }
    }

    // The account-level guild data an old save carries: everything set, as the account held it.
    private static CharacterGuildData LegacyAccountGuild()
    {
        var legacy = new CharacterGuildData();
        FillGuild(legacy);
        return legacy;
    }

    // What an old save carries apart from guild data: FullyPopulated without its per-character guild
    // data, which no build before v15 could write.
    private static ClusterFAccountData WithoutGuildData()
    {
        var d = FullyPopulated();
        d.ClearGuildData();
        return d;
    }

    [Fact]
    public void AVersion13SaveFromTheBuildBeforeGuildStarterRecordsLoads()
    {
        var original = WithoutGuildData();
        var (v13, v13Length) = Write(w => SerializeVersion13(original, LegacyAccountGuild(), w));
        Assert.Equal(13, VersionOf(v13));

        var reader = new BufferReader(v13);
        var copy = new ClusterFAccountData(reader);
        _out.WriteLine($"v13: {v13Length} bytes, read {reader.Position}");
        Assert.Equal(v13Length, reader.Position);

        // Everything version 13 carried that stays on the account is back; the starter records are
        // empty, as for any character before them; the account's guild data is cleared (cc-P18).
        Assert.Equal(original.Flags, copy.Flags);
        Assert.Equal(Chunks(original).Keys.OrderBy(k => k), Chunks(copy).Keys.OrderBy(k => k));
        Assert.Equal(original.EncounteredCreatures, copy.EncounteredCreatures);
        Assert.Equal(original.WoodDiscoveries.Count, copy.WoodDiscoveries.Count);
        Assert.Equal(original.ImbuingDiscoveries, copy.ImbuingDiscoveries);
        Assert.Equal(0, copy.GuildStarterRecordCount);
        Assert.Equal(0, copy.GuildDataCount);
        Assert.True(copy.DroppedAccountGuildData);

        // Written again it is version 15, and it reads back whole.
        var (v15, v15Length) = Write(copy.Serialize);
        Assert.Equal(ClusterFAccountData.CurrentVersion, VersionOf(v15));
        Assert.Equal(15, VersionOf(v15));
        var again = new BufferReader(v15);
        new ClusterFAccountData(again);
        Assert.Equal(v15Length, again.Position);
    }

    [Fact]
    public void AVersion14SaveWithAccountLevelGuildDataLoadsAndTheGuildDataIsCleared()
    {
        var original = WithoutGuildData();
        var (v14, v14Length) = Write(w => SerializeVersion14(original, LegacyAccountGuild(), w));
        Assert.Equal(14, VersionOf(v14));

        var reader = new BufferReader(v14);
        var copy = new ClusterFAccountData(reader);
        _out.WriteLine($"v14: {v14Length} bytes, read {reader.Position}");
        Assert.Equal(v14Length, reader.Position);

        // The guild data is gone: no character has it, and no Apprentice flag is left on the account.
        Assert.True(copy.DroppedAccountGuildData);
        Assert.Equal(0, copy.GuildDataCount);
        Assert.Null(copy.GetGuildData(First));
        Assert.Null(copy.GetGuildData(Second));
        Assert.DoesNotContain(copy.Flags, f => f.StartsWith("guild.apprentice.", StringComparison.OrdinalIgnoreCase));

        // Everything else is kept, the starter records included.
        Assert.Equal(original.Renown, copy.Renown);
        Assert.Equal(original.AchievementPoints, copy.AchievementPoints);
        Assert.Equal(original.LastSeenBulletinId, copy.LastSeenBulletinId);
        Assert.Equal(original.Flags, copy.Flags);
        Assert.Equal(original.FlagValues, copy.FlagValues);
        Assert.Equal(original.RestorationRegistry.Keys, copy.RestorationRegistry.Keys);
        Assert.Equal(original.OreDiscoveries.Keys, copy.OreDiscoveries.Keys);
        Assert.Equal(original.WoodDiscoveries.Keys, copy.WoodDiscoveries.Keys);
        Assert.Equal(original.ImbuingDiscoveries, copy.ImbuingDiscoveries);
        Assert.Equal(original.EncounteredCreatures, copy.EncounteredCreatures);
        Assert.Equal(Chunks(original).Keys.OrderBy(k => k), Chunks(copy).Keys.OrderBy(k => k));
        AssertSameStarterRecords(original, copy);

        // Written again it is version 15, and it reads back whole with nothing more dropped.
        var (v15, v15Length) = Write(copy.Serialize);
        Assert.Equal(15, VersionOf(v15));
        var again = new BufferReader(v15);
        var reread = new ClusterFAccountData(again);
        Assert.Equal(v15Length, again.Position);
        Assert.False(reread.DroppedAccountGuildData);

        // A version 14 save with no guild data at all is not reported as having dropped any.
        var (empty, emptyLength) = Write(w => SerializeVersion14(original, new CharacterGuildData(), w));
        var emptyReader = new BufferReader(empty);
        var clean = new ClusterFAccountData(emptyReader);
        Assert.Equal(emptyLength, emptyReader.Position);
        Assert.False(clean.DroppedAccountGuildData);
    }

    [Fact]
    public void AnAccountRecordOrStarterRecordOfAnUnknownVersionFailsLoudly()
    {
        var (newer, _) = Write(w => w.Write(ClusterFAccountData.CurrentVersion + 1));
        var ex = Assert.Throws<InvalidDataException>(() => new ClusterFAccountData(new BufferReader(newer)));
        _out.WriteLine(ex.Message);
        Assert.Contains($"version {ClusterFAccountData.CurrentVersion + 1}", ex.Message);

        var (record, _) = Write(w =>
        {
            w.Write(1);
            w.Write(0);
            w.Write(0);
            w.Write(false);
        });
        ex = Assert.Throws<InvalidDataException>(() => new GuildStarterRecord(new BufferReader(record)));
        _out.WriteLine(ex.Message);
        Assert.Contains("version 1", ex.Message);

        // cc-P18: the per-character guild data carries its own version too.
        var (guild, _) = Write(w => w.Write(CharacterGuildData.CurrentVersion + 1));
        ex = Assert.Throws<InvalidDataException>(() => new CharacterGuildData(new BufferReader(guild)));
        _out.WriteLine(ex.Message);
        Assert.Contains($"CharacterGuildData version {CharacterGuildData.CurrentVersion + 1}", ex.Message);
    }

    [Fact]
    public void TheWoodDiscoveriesThatStoppedTheLiveShardReadBackToTheLastByte()
    {
        var bytes = Convert.FromHexString(LiveWoodDiscoveriesHex);
        var reader = new BufferReader(bytes);

        var count = reader.ReadInt();
        var entries = new List<WoodDiscoveryEntry>();
        for (var i = 0; i < count; i++)
            entries.Add(new WoodDiscoveryEntry(reader));

        _out.WriteLine($"{bytes.Length} bytes, read {reader.Position}");
        Assert.Equal(bytes.Length, reader.Position);

        Assert.Equal(new[] { "RegularWood", "OakWood", "AshWood", "YewWood" }, entries.ConvertAll(e => e.WoodKey));
        Assert.Equal(new[] { 244, 101, 20, 10 }, entries.ConvertAll(e => e.TotalChopped));
        Assert.All(entries, e => Assert.Equal(DiscoveryState.Reported, e.State));

        // The one location of each entry carries the original FirstFound (2026-05-27 11:38:20Z for
        // RegularWood); that is what the version-0 FirstFound has to come back as.
        Assert.Equal(639154787005632764L, entries[0].FirstFound.Ticks);
        Assert.Equal(639155681757116614L, entries[3].FirstFound.Ticks);

        // The synthesised grove is kept, not collapsed.
        Assert.All(entries, e =>
        {
            var loc = Assert.Single(e.Locations);
            Assert.False(loc.HasKnownLocation);
            Assert.Equal((WoodLocationRecord.Unknown, WoodLocationRecord.Unknown, Point3D.Zero, e.TotalChopped),
                (loc.FacetName, loc.RegionName, loc.Location, loc.AmountChopped));
        });

        // Written again, they are version 1, byte for byte what the live save held, and read back the same.
        var (buffer, length) = Write(w =>
        {
            w.Write(entries.Count);
            foreach (var e in entries) e.Serialize(w);
        });
        Assert.Equal(1, VersionOf(buffer, 4));
        Assert.Equal(LiveWoodDiscoveriesHex, Convert.ToHexString(buffer, 0, (int)length));

        var again = new BufferReader(buffer);
        again.ReadInt();
        foreach (var e in entries)
            AssertSameGroves(e, new WoodDiscoveryEntry(again));
        Assert.Equal(length, again.Position);
    }

    [Fact]
    public void AWoodDiscoveryEntryOfAnUnknownVersionFailsLoudly()
    {
        var (buffer, _) = Write(w =>
        {
            w.Write(2);
            w.Write("Ironwood");
            w.Write(1);
            w.Write(0);
            w.Write(DateTime.UtcNow);
        });

        var ex = Assert.Throws<InvalidDataException>(() => new WoodDiscoveryEntry(new BufferReader(buffer)));
        _out.WriteLine(ex.Message);
        Assert.Contains("version 2", ex.Message);
    }

    [Fact]
    public void AWoodTypeFoundInSeveralGrovesRoundTripsAsVersion1()
    {
        var original = SeveralGroves();
        var (buffer, length) = Write(original.Serialize);

        _out.WriteLine($"{original.Locations.Count} groves, {length} bytes, version {VersionOf(buffer)}");
        Assert.Equal(1, VersionOf(buffer));

        var reader = new BufferReader(buffer);
        var copy = new WoodDiscoveryEntry(reader);
        Assert.Equal(length, reader.Position);
        AssertSameGroves(original, copy);
        Assert.Equal(original.Locations[0].DiscoveredAt, copy.FirstFound);
    }

    [Fact]
    public void AVersion0SaveReadsForwardAsOneUnknownGrove()
    {
        var firstFound = new DateTime(2026, 5, 27, 11, 38, 20, DateTimeKind.Utc).AddTicks(5632764);
        var (v0, v0Length) = Write(w =>
        {
            w.Write(0);
            w.Write("Ghostwood");
            w.Write(42);
            w.Write((int)DiscoveryState.Discovered);
            w.Write(firstFound);
        });

        var reader = new BufferReader(v0);
        var entry = new WoodDiscoveryEntry(reader);
        Assert.Equal(v0Length, reader.Position);

        Assert.Equal(("Ghostwood", 42, DiscoveryState.Discovered), (entry.WoodKey, entry.TotalChopped, entry.State));
        var loc = Assert.Single(entry.Locations);
        Assert.Equal(("Unknown", "Unknown", Point3D.Zero, firstFound.Ticks, 42),
            (loc.FacetName, loc.RegionName, loc.Location, loc.DiscoveredAt.Ticks, loc.AmountChopped));
        Assert.Equal(firstFound.Ticks, entry.FirstFound.Ticks);

        var (v1, v1Length) = Write(entry.Serialize);
        _out.WriteLine($"v0 {v0Length} bytes -> v{VersionOf(v1)} {v1Length} bytes");
        Assert.Equal(1, VersionOf(v1));
        AssertSameGroves(entry, new WoodDiscoveryEntry(new BufferReader(v1)));
    }

    [Fact]
    public void GrovesMergeOnlyOnTheSameFacetAndRegion()
    {
        var e = new WoodDiscoveryEntry("YewWood");
        e.AddLocation("Trammel", "Wilderness", new Point3D(1000, 1000, 0)).AmountChopped = 10;

        Assert.NotNull(e.FindNearbyLocation("Trammel", "Wilderness", new Point3D(1010, 1011, 0), 15));
        Assert.Null(e.FindNearbyLocation("Trammel", "Wilderness", new Point3D(1011, 1011, 0), 15));
        Assert.Null(e.FindNearbyLocation("Felucca", "Wilderness", new Point3D(1000, 1000, 0), 15));
        Assert.Null(e.FindNearbyLocation("Trammel", "HouseRegion", new Point3D(1000, 1000, 0), 15));

        // A version-0 grove sits at 0,0 on no facet; a chop near the origin must not land in it.
        e.Locations.Add(new WoodLocationRecord("Unknown", "Unknown", Point3D.Zero, DateTime.UnixEpoch, 5));
        Assert.Null(e.FindNearbyLocation("Trammel", "Wilderness", new Point3D(3, 3, 0), 15));
    }

    [Fact]
    public void RegionLabelsNeverCallAnUnnamedRegionWilderness()
    {
        Assert.Equal("Britain", WoodLocationRecord.RegionLabel("Britain", false, "TownRegion"));
        Assert.Equal("Wilderness", WoodLocationRecord.RegionLabel(null, true, "Region"));
        Assert.Equal("Wilderness", WoodLocationRecord.RegionLabel("", true, "Region"));
        Assert.Equal("HouseRegion", WoodLocationRecord.RegionLabel(null, false, "HouseRegion"));
        Assert.Equal("HouseRegion", WoodLocationRecord.RegionLabel("  ", false, "HouseRegion"));
    }
}

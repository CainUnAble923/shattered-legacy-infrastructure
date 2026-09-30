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
//      with no records; written again they are version 14.
//   9. A newer account-data version, or an unknown starter-record version, fails loudly.

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

    private static (byte[] Buffer, long Length) Write(Action<IGenericWriter> serialize)
    {
        var writer = new BufferWriter(true, new ConcurrentQueue<Type>());
        serialize(writer);
        return (writer.Buffer, writer.Position);
    }

    private static Dictionary<uint, BitArray?[]> Chunks(ClusterFAccountData d) =>
        (Dictionary<uint, BitArray?[]>)typeof(ClusterFAccountData)
            .GetField("_exploredChunks", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(d)!;

    private static ClusterFAccountData FullyPopulated()
    {
        var d = new ClusterFAccountData
        {
            Renown             = 1234,
            AchievementPoints  = 567,
            LastSeenBulletinId = 89
        };

        d.GuildReputation["mining"] = 300;
        d.GuildReputation["smithing"] = -5;
        d.GuildCurrency["mining"] = 42;
        d.JoinedGuilds.Add("mining");
        d.JoinedGuilds.Add("foresters");

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

        var active = new WorkOrderEntry("mining.dullcopper.50", "mining");
        active.TamingProgress["Horse"] = 2;
        d.ActiveWorkOrders.Add(active);
        d.CompletedWorkOrders.Add(new WorkOrderEntry("smithing.plate.10", "smithing")
        {
            CompletedAt = new DateTime(2026, 9, 2, 8, 30, 0, DateTimeKind.Utc)
        });

        var ore = new OreDiscoveryEntry("Valorite") { TotalMined = 77, State = DiscoveryState.Reported };
        ore.AddLocation("Felucca", "Minoc", new Point3D(2560, 480, 0)).AmountMined = 50;
        ore.AddLocation("Trammel", "Wilderness", new Point3D(1, 2, -3)).Reported = true;
        d.OreDiscoveries[ore.OreKey] = ore;

        d.ImbuingDiscoveries["Hit Chance Increase"] = 4;

        d.SmithCommissions.Add(new SmithCommissionEntry(
            "c1", "platechest", CraftResource.Valorite, true, "Sir Test", "Mind the rivets", 12, 34));
        var large = new SmithLargeCommissionEntry(
            "L1", "ringmail", CraftResource.Agapite, false, "Dame Test", "", 56, 78);
        large.FulfilledPieces.Add("ringmailchest");
        d.SmithLargeCommissions.Add(large);

        d.EncounteredCreatures.Add("Dragon");
        d.EncounteredCreatures.Add("Ridgeback");

        // Two facets visited, the other four never; bits set at both ends and in the middle.
        var fel = d.GetOrCreateExploration((Serial)0x1234u, 0, 40, 30);
        fel[0] = true;
        fel[599] = true;
        fel[1199] = true;
        d.GetOrCreateExploration((Serial)0x1234u, 4, 13, 7)[90] = true;

        d.WoodDiscoveries["Ironwood"] = SeveralGroves();
        var starwood = new WoodDiscoveryEntry("Starwood") { TotalChopped = 1 };
        starwood.Locations.Add(new WoodLocationRecord(
            "Ilshenar", "Wilderness", new Point3D(1100, 600, -80), new DateTime(2026, 9, 3, 1, 2, 3, DateTimeKind.Utc), 1));
        d.WoodDiscoveries["Starwood"] = starwood;

        d.AcceptArtificerOrder("artificer.slayer.silver", 0x40001234u);

        // v14 (cc-P15): two characters' guild starter records, one of them everything, one nearly empty.
        var full = d.GetOrCreateGuildStarter((Serial)0x1234u);
        full.ToolsTaken.Add("mining");
        full.ToolsTaken.Add("warriors");
        full.ItemsTaken.Add("TheDeluciansLostMine");
        full.ItemsTaken.Add("EnGuarde");
        full.WelcomeShown = true;
        d.GetOrCreateGuildStarter((Serial)0x5678u).ToolsTaken.Add("keepers");
        return d;
    }

    private static Dictionary<uint, GuildStarterRecord> StarterRecords(ClusterFAccountData d) =>
        (Dictionary<uint, GuildStarterRecord>)typeof(ClusterFAccountData)
            .GetField("_guildStarter", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(d)!;

    // Version 13 as the build before cc-P15 wrote it: a frozen copy of ClusterFAccountData.Serialize at
    // d0a1782 (server/customizations/ClusterFAccountData.cs:276-350 there), so these bytes do not come
    // from the writer under test. The entries' own writers are unchanged by cc-P15.
    private static void SerializeVersion13(ClusterFAccountData d, IGenericWriter w)
    {
        w.Write(13);

        w.Write(d.Renown);
        w.Write(d.AchievementPoints);
        w.Write(d.LastSeenBulletinId);

        w.Write(d.GuildReputation.Count);
        foreach (var (k, v) in d.GuildReputation) { w.Write(k); w.Write(v); }

        w.Write(d.GuildCurrency.Count);
        foreach (var (k, v) in d.GuildCurrency) { w.Write(k); w.Write(v); }

        w.Write(d.RestorationRegistry.Count);
        foreach (var entry in d.RestorationRegistry.Values) entry.Serialize(w);

        w.Write(d.JoinedGuilds.Count);
        foreach (var key in d.JoinedGuilds) w.Write(key);

        w.Write(d.Flags.Count);
        foreach (var f in d.Flags) w.Write(f);

        w.Write(d.FlagValues.Count);
        foreach (var (k, v) in d.FlagValues) { w.Write(k); w.Write(v); }

        w.Write(d.ActiveWorkOrders.Count);
        foreach (var e in d.ActiveWorkOrders) e.Serialize(w);

        w.Write(d.CompletedWorkOrders.Count);
        foreach (var e in d.CompletedWorkOrders) e.Serialize(w);

        w.Write(d.OreDiscoveries.Count);
        foreach (var entry in d.OreDiscoveries.Values) entry.Serialize(w);

        w.Write(d.SmithCommissions.Count);
        foreach (var e in d.SmithCommissions) e.Serialize(w);

        w.Write(d.SmithLargeCommissions.Count);
        foreach (var e in d.SmithLargeCommissions) e.Serialize(w);

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

        w.Write(d.ActiveArtificerOrderKey ?? "");
        w.Write(d.ActiveArtificerItemSerial);
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

    [Fact]
    public void AnAccountWithEveryCollectionNonEmptySurvivesAWriteAndARead()
    {
        var original = FullyPopulated();
        var (buffer, length) = Write(original.Serialize);

        var reader = new BufferReader(buffer);
        var copy = new ClusterFAccountData(reader);

        _out.WriteLine($"wrote {length} bytes, read {reader.Position}");
        Assert.Equal(length, reader.Position);

        Assert.Equal(original.Renown, copy.Renown);
        Assert.Equal(original.AchievementPoints, copy.AchievementPoints);
        Assert.Equal(original.LastSeenBulletinId, copy.LastSeenBulletinId);
        Assert.Equal(original.GuildReputation, copy.GuildReputation);
        Assert.Equal(original.GuildCurrency, copy.GuildCurrency);
        Assert.Equal(original.JoinedGuilds, copy.JoinedGuilds);
        Assert.Equal(original.Flags, copy.Flags);
        Assert.Equal(original.FlagValues, copy.FlagValues);
        Assert.Equal(original.ImbuingDiscoveries, copy.ImbuingDiscoveries);
        Assert.Equal(original.EncounteredCreatures, copy.EncounteredCreatures);
        Assert.Equal(original.ActiveArtificerOrderKey, copy.ActiveArtificerOrderKey);
        Assert.Equal(original.ActiveArtificerItemSerial, copy.ActiveArtificerItemSerial);

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

        Assert.Equal(original.ActiveWorkOrders.Count, copy.ActiveWorkOrders.Count);
        Assert.Equal(original.CompletedWorkOrders.Count, copy.CompletedWorkOrders.Count);
        var orders = new List<(WorkOrderEntry, WorkOrderEntry)>();
        for (var i = 0; i < original.ActiveWorkOrders.Count; i++)
            orders.Add((original.ActiveWorkOrders[i], copy.ActiveWorkOrders[i]));
        for (var i = 0; i < original.CompletedWorkOrders.Count; i++)
            orders.Add((original.CompletedWorkOrders[i], copy.CompletedWorkOrders[i]));
        foreach (var (o, c) in orders)
        {
            Assert.Equal(o.DefKey, c.DefKey);
            Assert.Equal(o.GuildKey, c.GuildKey);
            Assert.Equal(o.AcceptedAt, c.AcceptedAt);
            Assert.Equal(o.CompletedAt, c.CompletedAt);
            Assert.Equal(o.TamingProgress, c.TamingProgress);
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

        Assert.Equal(original.SmithCommissions.Count, copy.SmithCommissions.Count);
        for (var i = 0; i < original.SmithCommissions.Count; i++)
        {
            var (o, c) = (original.SmithCommissions[i], copy.SmithCommissions[i]);
            Assert.Equal(
                (o.Id, o.ItemKey, o.Material, o.RequireExceptional, o.RequesterName, o.RequesterNote, o.SealReward, o.StandingReward, o.IssuedAt),
                (c.Id, c.ItemKey, c.Material, c.RequireExceptional, c.RequesterName, c.RequesterNote, c.SealReward, c.StandingReward, c.IssuedAt));
        }

        Assert.Equal(original.SmithLargeCommissions.Count, copy.SmithLargeCommissions.Count);
        for (var i = 0; i < original.SmithLargeCommissions.Count; i++)
        {
            var (o, c) = (original.SmithLargeCommissions[i], copy.SmithLargeCommissions[i]);
            Assert.Equal(
                (o.Id, o.SetKey, o.Material, o.RequireExceptional, o.RequesterName, o.RequesterNote, o.SealReward, o.StandingReward, o.IssuedAt),
                (c.Id, c.SetKey, c.Material, c.RequireExceptional, c.RequesterName, c.RequesterNote, c.SealReward, c.StandingReward, c.IssuedAt));
            Assert.Equal(o.FulfilledPieces, c.FulfilledPieces);
        }

        var oChunks = Chunks(original);
        var cChunks = Chunks(copy);
        Assert.Equal(oChunks.Keys, cChunks.Keys);
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
                    Assert.True(oFacets[f]![b] == cFacets[f]![b], $"facet {f} bit {b}");
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

    [Fact]
    public void AVersion13SaveFromTheBuildBeforeGuildStarterRecordsLoads()
    {
        var original = FullyPopulated();
        var (v13, v13Length) = Write(w => SerializeVersion13(original, w));
        Assert.Equal(13, VersionOf(v13));

        var reader = new BufferReader(v13);
        var copy = new ClusterFAccountData(reader);
        _out.WriteLine($"v13: {v13Length} bytes, read {reader.Position}");
        Assert.Equal(v13Length, reader.Position);

        // Everything version 13 carried is back; the new record is empty, as for any character before it.
        Assert.Equal(original.JoinedGuilds, copy.JoinedGuilds);
        Assert.Equal(original.GuildCurrency, copy.GuildCurrency);
        Assert.Equal(original.Flags, copy.Flags);
        Assert.Equal(Chunks(original).Keys, Chunks(copy).Keys);
        Assert.Equal(original.EncounteredCreatures, copy.EncounteredCreatures);
        Assert.Equal(original.WoodDiscoveries.Count, copy.WoodDiscoveries.Count);
        Assert.Equal(original.ImbuingDiscoveries, copy.ImbuingDiscoveries);
        Assert.Equal(original.ActiveArtificerOrderKey, copy.ActiveArtificerOrderKey);
        Assert.Equal(original.ActiveArtificerItemSerial, copy.ActiveArtificerItemSerial);
        Assert.Equal(0, copy.GuildStarterRecordCount);

        // Written again it is version 14, and it reads back whole.
        var (v14, v14Length) = Write(copy.Serialize);
        Assert.Equal(ClusterFAccountData.CurrentVersion, VersionOf(v14));
        Assert.Equal(14, VersionOf(v14));
        var again = new BufferReader(v14);
        new ClusterFAccountData(again);
        Assert.Equal(v14Length, again.Position);
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

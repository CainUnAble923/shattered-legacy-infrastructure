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
// Three facts:
//   1. Every ClusterFAccountData field survives a write and a read with every collection non-empty,
//      and the read consumes exactly the bytes the write produced (the check ModernUO's loader makes).
//   2. The exact 276 bytes of WoodDiscoveries from the live save read back, and the reader stops on
//      the last byte.
//   3. An unknown WoodDiscoveryEntry version fails loudly instead of being read as another version.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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

        d.WoodDiscoveries["Ironwood"] = new WoodDiscoveryEntry("Ironwood")
        {
            TotalChopped = 19,
            State        = DiscoveryState.Reported
        };
        d.WoodDiscoveries["Starwood"] = new WoodDiscoveryEntry("Starwood") { TotalChopped = 1 };

        d.AcceptArtificerOrder("artificer.slayer.silver", 0x40001234u);
        return d;
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

        // FirstFound is get-only and set from UtcNow by the new-entry constructor; the read
        // constructor sets it from the stream. Ticks, not a formatted string, so nothing is rounded.
        Assert.Equal(original.WoodDiscoveries.Count, copy.WoodDiscoveries.Count);
        foreach (var (key, o) in original.WoodDiscoveries)
        {
            var c = copy.WoodDiscoveries[key];
            _out.WriteLine($"wood {key}: FirstFound {o.FirstFound.Ticks} -> {c.FirstFound.Ticks}");
            Assert.Equal(o.WoodKey, c.WoodKey);
            Assert.Equal(o.TotalChopped, c.TotalChopped);
            Assert.Equal(o.State, c.State);
            Assert.Equal(o.FirstFound.Ticks, c.FirstFound.Ticks);
            Assert.NotEqual(0, c.FirstFound.Ticks);
        }
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

        // Written again, they are version 0 and read back the same.
        var (buffer, length) = Write(w => { foreach (var e in entries) e.Serialize(w); });
        var again = new BufferReader(buffer);
        foreach (var e in entries)
        {
            var c = new WoodDiscoveryEntry(again);
            Assert.Equal((e.WoodKey, e.TotalChopped, e.State, e.FirstFound.Ticks), (c.WoodKey, c.TotalChopped, c.State, c.FirstFound.Ticks));
        }
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
}

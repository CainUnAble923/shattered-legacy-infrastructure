// CompanionBaseVerification.cs
//
// cc-P264, companions B1: the companion base and its record. Notes in shard-migration
// notes/cc-P264-companions-b1-base-and-record.md.
//
// Facts:
//   1. The record round-trips: lineage id, path, branch, personality, loop, rank, level, XP, epithet, parents,
//      generation, breed time, per-skill peaks, sex; the caps come back from the loop.
//   2. Lineage ids are never reused: not after a delete, not after a restart (a fresh counter moves past every loaded
//      companion's id), and the counter's own save keeps its place.
//   3. Logging out with the owner: away on the internal map, still controlled and counted, never auto-stabled, marked
//      stabled against the dead-pet sweep; a restart while away keeps that; login brings it back following.
//   4. The follower math: slot cost by rank re-counts the owner, the party limit by the owner's loop, a new player keeps
//      room for a pet or mount, the FollowersMax curve never lowers a value.
//   5. The skill cap is a player's: each skill's cap is the max level, Skills.Cap is 58 times that (never the creature
//      700.0), and it follows the loop.
//   6. Player parity: hits, stamina and mana by the player's formulas, no SetDamage, the weapon's damage-type split on
//      equip, and the skill gain boost takes a controlled companion.

using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Companions;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CompanionBaseVerification
{
    private readonly ITestOutputHelper _out;

    public CompanionBaseVerification(ITestOutputHelper output)
    {
        _out = output;
        ClusterFSkillCaps.ReloadForTests();
    }

    private static readonly Point3D Spot = new(1500, 1600, 0);

    private static PlayerMobile NewOwner()
    {
        // Player before MoveToWorld, or the sector never lists it.
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Spot, Map.Felucca);
        return pm;
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(typeof(T), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    [Fact]
    public void TheRecordSavesAndLoads()
    {
        var c = new BaseCompanion(CompanionPath.Arcane, Personality.Gloomy, true);

        try
        {
            c.SetLoop(3);
            c.SetRank(2);
            c.SetLevel(77);
            c.SetBranch(CompanionBranch.Necromancer);
            c.SetLineage(11, 12, 2);
            c.Epithet = "the Reluctant";
            c.NextBreedTime = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
            c.Xp = 4321;
            c.Skills[SkillName.Magery].Base = 80.0;
            c.Skills[SkillName.Magery].Base = 60.0; // the peak stays 80

            var copy = RoundTrip(c);

            Assert.Equal(c.LineageId, copy.LineageId);
            Assert.Equal(CompanionPath.Arcane, copy.Path);
            Assert.Equal(CompanionBranch.Necromancer, copy.Branch);
            Assert.Equal(Personality.Gloomy, copy.Personality);
            Assert.True(copy.Female);
            Assert.Equal(3, copy.Loop);
            Assert.Equal(110, copy.MaxLevel);
            Assert.Equal(2, copy.Rank);
            Assert.Equal(77, copy.Level);
            Assert.Equal(4321, copy.Xp);
            Assert.Equal("the Reluctant", copy.Epithet);
            Assert.Equal(11, copy.Parent1Id);
            Assert.Equal(12, copy.Parent2Id);
            Assert.Equal(2, copy.Generation);
            Assert.Equal(c.NextBreedTime, copy.NextBreedTime);
            Assert.Equal(80.0, copy.SkillPeak(SkillName.Magery));
            Assert.Equal(60.0, copy.Skills[SkillName.Magery].Base);
            Assert.Equal(2, copy.ControlSlots);
            Assert.Equal(110.0, copy.Skills[SkillName.Magery].Cap);
            Assert.False(copy.AwayWithOwner);
        }
        finally
        {
            c.Delete();
        }
    }

    [Fact]
    public void LineageIdsAreNeverReused()
    {
        var a = new BaseCompanion(CompanionPath.Blade, Personality.Noble, false);
        var b = new BaseCompanion(CompanionPath.Bow, Personality.Commoner, true);
        var aId = a.LineageId;
        Assert.NotEqual(aId, b.LineageId);
        Assert.True(aId > 0, "an id is never 0 (0 means no parent)");

        // Not after a delete.
        a.Delete();
        var c = new BaseCompanion(CompanionPath.Rogue, Personality.Sellsword, false);
        Assert.True(c.LineageId > b.LineageId, $"after a delete: {c.LineageId} after {b.LineageId}");

        // The counter's save keeps its place, and a load never moves it back.
        var buffer = new byte[256];
        var writer = new BufferWriter(buffer, true);
        CompanionLineage.Instance.Serialize(writer);
        writer.Flush();
        var saved = CompanionLineage.PeekNext;

        CompanionLineage.ResetForTests();
        CompanionLineage.Instance.Deserialize(new BufferReader(buffer));
        Assert.Equal(saved, CompanionLineage.PeekNext);

        // A restart with a lost counter file: the loaded companions move a fresh counter past their ids.
        CompanionLineage.ResetForTests();
        var reloaded = RoundTrip(c);
        Assert.Equal(c.LineageId, reloaded.LineageId);
        Assert.True(CompanionLineage.PeekNext > c.LineageId, $"counter {CompanionLineage.PeekNext} past loaded id {c.LineageId}");

        var d = new BaseCompanion(CompanionPath.Support, Personality.Devout, true);
        Assert.NotEqual(aId, d.LineageId);
        Assert.NotEqual(b.LineageId, d.LineageId);
        Assert.NotEqual(c.LineageId, d.LineageId);

        CompanionLineage.ResetForTests(Math.Max(saved, d.LineageId + 1));
        b.Delete();
        c.Delete();
        d.Delete();
    }

    [Fact]
    public void ACompanionLogsOutAndInWithItsOwner()
    {
        var owner = NewOwner();
        var c = new BaseCompanion(CompanionPath.Blade, Personality.Braggart, false);

        try
        {
            Assert.True(CompanionParty.Join(owner, c, out var reason), reason);
            Assert.Equal(1, owner.Followers);
            Assert.False(c.CanAutoStable);

            CompanionParty.OnOwnerLogout(owner);

            Assert.Equal(Map.Internal, c.Map);
            Assert.True(c.Controlled);
            Assert.Same(owner, c.ControlMaster);
            Assert.True(c.AwayWithOwner);
            Assert.True(c.IsStabled, "stabled while away, so the dead-pet sweep cannot take a ghost companion");
            Assert.Equal(1, owner.Followers);
            Assert.True(owner.AutoStabled == null || !owner.AutoStabled.Contains(c), "never auto-stabled");

            // A restart while away: IsStabled is not saved, the companion's own flag brings it back.
            var stray = new BaseCompanion(CompanionPath.Bow, Personality.Ditzy, true);
            stray.SetAway(true);
            var loaded = RoundTrip(stray);
            Assert.True(loaded.AwayWithOwner);
            Assert.True(loaded.IsStabled);
            stray.Delete();

            CompanionParty.OnOwnerLogin(owner);

            Assert.Equal(owner.Map, c.Map);
            Assert.Equal(owner.Location, c.Location);
            Assert.Equal(OrderType.Follow, c.ControlOrder);
            Assert.Same(owner, c.ControlTarget);
            Assert.False(c.AwayWithOwner);
            Assert.False(c.IsStabled);
            Assert.Equal(1, owner.Followers);
        }
        finally
        {
            c.Delete();
            owner.Delete();
        }
    }

    [Fact]
    public void TheFollowerMathFollowsTheSettings()
    {
        // The defaults (settings; numbers for Chase in the notes).
        Assert.Equal(new[] { 1, 1, 2, 2, 3, 3, 3 }, new[] { 0, 1, 2, 3, 4, 5, 9 }.Select(CompanionSettings.SlotCostForRank));
        Assert.Equal(new[] { 50, 100, 105, 150, 200, 200 }, new[] { 0, 1, 2, 11, 21, 30 }.Select(CompanionSettings.MaxLevelAtLoop));
        Assert.Equal(21, CompanionSettings.TopLoop);
        Assert.Equal(new[] { 3, 4, 6, 6 }, new[] { 0, 1, 5, 40 }.Select(CompanionSettings.PartySizeAt));
        Assert.Equal(new[] { 5, 18, 19, 19 }, new[] { 0, 11, 21, 50 }.Select(CompanionSettings.FollowersMaxAt));

        var owner = NewOwner();
        var a = new BaseCompanion(CompanionPath.Blade, Personality.Noble, false);
        var b = new BaseCompanion(CompanionPath.Support, Personality.Devout, true);
        var third = new BaseCompanion(CompanionPath.Bow, Personality.Commoner, false);
        var horse = new Horse();

        try
        {
            // A new player (loop 0, stock 5 slots): two squires, then the party is full...
            Assert.Equal(5, owner.FollowersMax);
            Assert.True(CompanionParty.Join(owner, a, out var reason), reason);
            Assert.True(CompanionParty.Join(owner, b, out reason), reason);
            Assert.Equal(2, owner.Followers);
            Assert.False(CompanionParty.Join(owner, third, out reason));
            _out.WriteLine(reason);
            Assert.Contains("party is full", reason);

            // ...and a pet or mount still fits beside them.
            horse.MoveToWorld(owner.Location, owner.Map);
            Assert.True(horse.SetControlMaster(owner));
            Assert.Equal(3, owner.Followers);

            // A rank change moves the owner's count with the slot cost.
            a.SetRank(2);
            Assert.Equal(2, a.ControlSlots);
            Assert.Equal(4, owner.Followers);
            a.SetRank(4);
            Assert.Equal(3, a.ControlSlots);
            Assert.Equal(5, owner.Followers);
            a.SetRank(0);
            Assert.Equal(3, owner.Followers);

            // Slots bind too: at 5 of 5, a rank-4 squire (3 slots) does not fit even in a bigger party.
            CompanionSettings.OwnerLoopProvider = m => m == owner ? 5 : 0;
            Assert.Equal(5, CompanionParty.CompanionLimit(owner));
            third.SetRank(4);
            owner.Followers = 3;
            Assert.False(CompanionParty.CanTake(owner, third.ControlSlots, out reason));
            Assert.Contains("too many followers", reason);

            // The curve raises FollowersMax at the owner's loop and never lowers it.
            CompanionParty.ApplyFollowersMax(owner);
            Assert.Equal(11, owner.FollowersMax);
            Assert.True(CompanionParty.Join(owner, third, out reason), reason);
            owner.FollowersMax = 30;
            CompanionParty.ApplyFollowersMax(owner);
            Assert.Equal(30, owner.FollowersMax);
        }
        finally
        {
            CompanionSettings.OwnerLoopProvider = null;
            a.Delete();
            b.Delete();
            third.Delete();
            horse.Delete();
            owner.Delete();
        }
    }

    [Fact]
    public void TheSkillCapIsAPlayers()
    {
        var c = new BaseCompanion(CompanionPath.Blade, Personality.Sellsword, false);

        try
        {
            var n = SkillInfo.Table.Length;

            // Loop 0: each skill 50.0, the total 58 x 50.0, not the creature default 700.0.
            Assert.Equal(50.0, c.Skills[SkillName.Swords].Cap);
            Assert.Equal(n * 500, c.Skills.Cap);

            c.SetLoop(21);
            Assert.Equal(200.0, c.Skills[SkillName.Swords].Cap);
            Assert.Equal(n * 2000, c.Skills.Cap);

            // Past 700.0 total, gains still have room (the creature cap would floor them).
            for (var i = 0; i < 10; i++)
            {
                c.Skills[i].Base = 100.0;
            }

            Assert.True(c.Skills.Total < c.Skills.Cap, $"total {c.Skills.Total} under cap {c.Skills.Cap}");

            // And it follows the loop down as well.
            c.SetLoop(1);
            Assert.Equal(100.0, c.Skills[SkillName.Swords].Cap);
            Assert.Equal(n * 1000, c.Skills.Cap);
        }
        finally
        {
            c.Delete();
        }
    }

    [Fact]
    public void ItFightsAndGainsAsAPlayer()
    {
        var owner = NewOwner();
        var c = new BaseCompanion(CompanionPath.Blade, Personality.Noble, false);
        var wasEnabled = ClusterFSkillGain.Enabled;

        try
        {
            // Pools: Str 50 -> 50 / 2 + 50 = 75 hits; stamina is Dex, mana is Int.
            Assert.Equal(75, c.HitsMax);
            Assert.Equal(40, c.StamMax);
            Assert.Equal(30, c.ManaMax);

            // Never SetDamage: the weapon's dice decide.
            Assert.Equal(-1, c.DamageMin);
            Assert.Equal(-1, c.DamageMax);

            // The weapon's damage-type split arrives on equip and goes on unequip.
            var blade = new Katana();
            blade.AosElementDamages.Fire = 40;
            blade.AosElementDamages.Cold = 10;
            c.AddItem(blade);
            Assert.Equal(50, c.PhysicalDamage);
            Assert.Equal(40, c.FireDamage);
            Assert.Equal(10, c.ColdDamage);

            c.RemoveItem(blade);
            Assert.Equal(100, c.PhysicalDamage);
            Assert.Equal(0, c.FireDamage);
            blade.Delete();

            // The gain boost takes a controlled, living companion, not a wild one.
            var shouldBoost = typeof(ClusterFSkillGain).GetMethod("ShouldBoost", BindingFlags.NonPublic | BindingFlags.Static)!;
            ClusterFSkillGain.Enabled = true;
            var skill = c.Skills[SkillName.Swords];
            Assert.False((bool)shouldBoost.Invoke(null, new object[] { c, skill })!, "a wild companion is not boosted");
            Assert.True(CompanionParty.Join(owner, c, out var reason), reason);
            Assert.True((bool)shouldBoost.Invoke(null, new object[] { c, skill })!, "a controlled companion is boosted");
        }
        finally
        {
            ClusterFSkillGain.Enabled = wasEnabled;
            c.Delete();
            owner.Delete();
        }
    }
}

// LevelGearProcVerification.cs
//
// cc-P633: weapon procs bought at the levelable gear milestone, stage 1 (customizations LevelGear/LevelGearProcs.cs, the
// patch BaseWeapon-level-gear-proc-hit.patch; notes: shard-migration notes/cc-P633-build-procs-milestone.md; design
// notes/cc-P569-design-procs-levelable-milestone.md).
//
// Facts:
//   1. Buying: off by default; level 99 refused and 100 accepted; 99 points refused and exactly 100 accepted; a refusal
//      changes nothing; a second purchase (a replayed confirmation) charges nothing; armour and found weapons refused;
//      a full set of slots refused; the milestone takes a slot; a purchase at level 150 still saves required level 100.
//   2. A real weapon hit (BaseWeapon.OnHit through the patch): a roll of 499 of 10,000 binds Stormstrike's cooldown on
//      the player and the weapon, 500 does not; a blocked strike draws no roll.
//   3. The effect: 12 energy (Stormstrike) or 12 fire (Emberstrike) through the normal damage path, from the attacker.
//   4. The cooldown: shared by the owner across weapons and kept on the weapon across owners; 9.999 seconds refuses and
//      exactly 10 allows.
//   5. PvE only: a player, a tamed pet, a creature not being fought, and a strike with no damage never roll.
//   6. Companions: a controlled companion's strike uses its online player owner's cooldown; an offline owner refuses.
//   7. Save and load: a version 0 row loads with no milestone (even at level 150); a purchase round-trips; unexpired
//      cooldowns round-trip as absolute times and expired ones are dropped.
//   8. Settings: an invalid chance turns the feature off.
//   9. P647 (review fixes): off the test shard the milestone is off and [LevelGearProcs on refuses; the bound effect
//      shows on the tooltip; the confirmation charges only the price and unlock it showed; a shut chest mimic draws no
//      roll; a weapon cooldown survives a restart and a hand-over; the bound page shows unavailable and the saved
//      level; expired and deleted cooldown entries are pruned.

using System;
using System.Collections.Generic;
using Server;
using Server.Commands;
using Server.Companions;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LevelGearProcVerification : IDisposable
{
    private static readonly Point3D Spot = new(1480, 1750, 0);
    private static readonly DateTime T0 = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<IEntity> _things = [];
    private readonly List<int> _rolls = [];
    private DateTime _now = T0;
    private int _rollValue = 10000;

    public LevelGearProcVerification()
    {
        LevelGearStore.Rows.Clear();
        LevelGearProcCooldowns.Owners.Clear();
        LevelGearProcCooldowns.Weapons.Clear();
        LevelGearProcSettings.EnabledOverride = true;
        LevelGearProcSettings.IsTestShard = () => true; // stage 1 is test-shard only (P647)
        ClusterFLevelGearProcs.NowOverride = () => _now;
        ClusterFLevelGearProcs.Roll = max =>
        {
            _rolls.Add(max);
            return _rollValue;
        };
    }

    public void Dispose()
    {
        foreach (var e in _things)
        {
            e.Delete();
        }

        LevelGearStore.Rows.Clear();
        LevelGearProcCooldowns.Owners.Clear();
        LevelGearProcCooldowns.Weapons.Clear();
        LevelGearProcSettings.EnabledOverride = null;
        LevelGearProcSettings.IsTestShard = () => ClusterFStaffHub.IsTestShard;
        ClusterFLevelGearProcs.NowOverride = null;
        ClusterFLevelGearProcs.Roll = Utility.Random;
    }

    private T Track<T>(T e) where T : IEntity
    {
        _things.Add(e);
        return e;
    }

    private PlayerMobile Player(int dx = 0)
    {
        var pm = Track(new PlayerMobile { Player = true, Race = Race.Human, Name = "P633 tester" });
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y, Spot.Z), Map.Trammel);
        return pm;
    }

    private T Levelable<T>(T item, int level, int points) where T : Item
    {
        item.PlayerConstructed = true;
        if (item is BaseWeapon)
        {
            item.Layer = Layer.OneHanded; // the test host has no tiledata, where a weapon's layer comes from
        }

        Track(item);
        Assert.Null(ClusterFLevelGear.MakeLevelable(item, null));
        var s = LevelGearStore.Of(item)!;
        s.Level = level;
        s.Xp = ClusterFLevelGear.XpForLevel(level);
        s.Points = points;
        return item;
    }

    // A wild creature next to the attacker, being fought, with no resists and plenty of hits.
    private Ettin Foe(Mobile attacker)
    {
        var foe = Track(new Ettin());
        foe.SetHits(5000);
        foreach (ResistanceType r in Enum.GetValues(typeof(ResistanceType)))
        {
            foe.SetResistance(r, 0);
        }

        foe.MoveToWorld(new Point3D(attacker.X + 1, attacker.Y, attacker.Z), Map.Trammel);
        attacker.Combatant = foe;
        return foe;
    }

    // A player wielding a levelable katana with Stormstrike bound.
    private (PlayerMobile pm, Katana katana) Armed(string key = "stormstrike")
    {
        var pm = Player();
        var katana = Levelable(new Katana(), 100, 100);
        pm.AddItem(katana);
        Assert.Null(ClusterFLevelGearProcs.Buy(pm, katana, LevelGearProc.ByKey(key)));
        return (pm, katana);
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheMilestoneIsBoughtOnceAtLevel100For100Points()
    {
        var pm = Player();
        var storm = LevelGearProc.Stormstrike;

        LevelGearProcSettings.EnabledOverride = null;
        var off = Levelable(new Katana(), 100, 100);
        pm.Backpack.DropItem(off);
        Assert.Contains("not available", ClusterFLevelGearProcs.WhyNotBuy(pm, off, storm));
        LevelGearProcSettings.EnabledOverride = true;

        var young = Levelable(new Katana(), 99, 500);
        pm.Backpack.DropItem(young);
        Assert.Equal("Reach item level 100.", ClusterFLevelGearProcs.Buy(pm, young, storm));

        var poor = Levelable(new Katana(), 100, 99);
        pm.Backpack.DropItem(poor);
        Assert.Contains("costs 100 points", ClusterFLevelGearProcs.Buy(pm, poor, storm));
        Assert.Equal(99, LevelGearStore.Of(poor)!.Points);
        Assert.Null(LevelGearStore.Of(poor)!.Proc);

        var armour = Levelable(new PlateChest(), 100, 100);
        pm.Backpack.DropItem(armour);
        Assert.Contains("levelable weapon", ClusterFLevelGearProcs.Buy(pm, armour, storm));

        var elsewhere = Levelable(new Katana(), 100, 100);
        Assert.Contains("carry or wear", ClusterFLevelGearProcs.Buy(pm, elsewhere, storm));

        var exact = Levelable(new Katana(), 100, 100);
        pm.Backpack.DropItem(exact);
        var before = ClusterFLevelGear.SlotsUsed(exact);
        Assert.Null(ClusterFLevelGearProcs.Buy(pm, exact, storm));
        var s = LevelGearStore.Of(exact)!;
        Assert.Equal(0, s.Points);
        Assert.Equal("stormstrike", s.Proc!.Key);
        Assert.Equal(100, s.Proc.RequiredLevel);
        Assert.Equal(100, s.Proc.PaidCost);
        Assert.Equal(before + 1, ClusterFLevelGear.SlotsUsed(exact));

        // A replayed confirmation: refused, and nothing more is paid.
        s.Points = 300;
        Assert.Contains("already", ClusterFLevelGearProcs.Buy(pm, exact, LevelGearProc.Emberstrike));
        Assert.Equal(300, s.Points);
        Assert.Equal("stormstrike", s.Proc.Key);

        // Bought late, the milestone still asks level 100 (the purchase level is history).
        var late = Levelable(new Katana(), 150, 100);
        pm.Backpack.DropItem(late);
        Assert.Null(ClusterFLevelGearProcs.Buy(pm, late, LevelGearProc.Emberstrike));
        Assert.Equal(100, LevelGearStore.Of(late)!.Proc!.RequiredLevel);
        Assert.Equal(150, LevelGearStore.Of(late)!.Proc!.PurchaseLevel);

        // Five properties already: no room.
        var full = Levelable(new Katana(), 100, 100);
        pm.Backpack.DropItem(full);
        full.Attributes.Luck = 10;
        full.Attributes.WeaponDamage = 10;
        full.Attributes.AttackChance = 5;
        full.Attributes.DefendChance = 5;
        full.Attributes.WeaponSpeed = 5;
        Assert.Equal(5, ClusterFLevelGear.SlotsUsed(full));
        Assert.Contains("free one", ClusterFLevelGearProcs.Buy(pm, full, storm));
        Assert.Equal(100, LevelGearStore.Of(full)!.Points);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ARealWeaponHitRollsTheMilestone()
    {
        var (pm, katana) = Armed();
        var foe = Foe(pm);

        _rollValue = 500; // one past the 5% chance
        katana.OnHit(pm, foe);
        Assert.Equal(new[] { 10000 }, _rolls);
        Assert.Empty(LevelGearProcCooldowns.Owners);

        _rollValue = 499;
        katana.OnHit(pm, foe);
        Assert.Equal(2, _rolls.Count);
        Assert.Equal(T0.AddSeconds(10), LevelGearProcCooldowns.Owners[pm.Serial]);
        Assert.Equal(T0.AddSeconds(10), LevelGearProcCooldowns.Weapons[katana.Serial]);

        // Blocked by the cooldown: no roll at all.
        katana.OnHit(pm, foe);
        Assert.Equal(2, _rolls.Count);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void StormstrikeIsTwelveEnergyAndEmberstrikeTwelveFire()
    {
        var (pm, katana) = Armed();
        var foe = Foe(pm);
        _rollValue = 0;

        var hits = foe.Hits;
        Assert.Equal(12, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, foe, 20));
        Assert.Equal(hits - 12, foe.Hits);

        // With 50 energy resist and none to fire, the same strike halves.
        foe.SetResistance(ResistanceType.Energy, 50);
        _now = T0.AddSeconds(10);
        Assert.Equal(6, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, foe, 20));

        var (pm2, ember) = Armed("emberstrike");
        var foe2 = Foe(pm2);
        foe2.SetResistance(ResistanceType.Energy, 100);
        Assert.Equal(12, ClusterFLevelGearProcs.OnWeaponHit(ember, pm2, foe2, 20));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheCooldownIsSharedByTheOwnerAndKeptOnTheWeapon()
    {
        var (pm, katana) = Armed();
        var foe = Foe(pm);
        _rollValue = 0;
        Assert.Equal(12, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, foe, 20));

        // Another milestone weapon of the same player: the owner's cooldown holds it.
        katana.Delete();
        var second = Levelable(new Katana(), 100, 100);
        pm.AddItem(second);
        Assert.Null(ClusterFLevelGearProcs.Buy(pm, second, LevelGearProc.Stormstrike));
        _now = T0.AddSeconds(9.999);
        Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(second, pm, foe, 20));
        Assert.Single(_rolls);

        _now = T0.AddSeconds(10);
        Assert.Equal(12, ClusterFLevelGearProcs.OnWeaponHit(second, pm, foe, 20));

        // Handed to another player during its cooldown: the weapon's own cooldown holds it.
        second.MoveToWorld(Spot, Map.Trammel);
        var other = Player(2);
        other.AddItem(second);
        var foe2 = Foe(other);
        _now = T0.AddSeconds(15);
        Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(second, other, foe2, 20));
        Assert.Equal(2, _rolls.Count);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void OnlyAWildCreatureBeingFoughtCanBeStruck()
    {
        var (pm, katana) = Armed();
        _rollValue = 0;

        var victim = Player(1);
        pm.Combatant = victim;
        Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, victim, 20));

        var pet = Foe(pm);
        pet.Controlled = true;
        pet.ControlMaster = victim;
        Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, pet, 20));

        var foe = Foe(pm);
        pm.Combatant = null;
        Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, foe, 20));

        pm.Combatant = foe;
        Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, foe, 0));

        // A weapon in the backpack is not the one striking.
        pm.Backpack.DropItem(katana);
        Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, foe, 20));

        Assert.Empty(_rolls);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void ACompanionUsesItsOnlineOwnersCooldown()
    {
        var owner = Player();
        var companion = Track(new BaseCompanion(CompanionPath.Blade, Personality.Noble, false));
        companion.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        companion.Controlled = true;
        companion.ControlMaster = owner;

        var katana = Levelable(new Katana(), 100, 100);
        owner.Backpack.DropItem(katana);
        Assert.Null(ClusterFLevelGearProcs.Buy(owner, katana, LevelGearProc.Stormstrike));
        companion.EquipItem(katana);
        if (katana.Parent != companion)
        {
            companion.AddItem(katana);
        }

        var foe = Foe(companion);
        _rollValue = 0;

        // Offline owner: nothing.
        Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(katana, companion, foe, 20));
        Assert.Empty(_rolls);

        var ns = PacketTestUtilities.CreateTestNetState();
        try
        {
            owner.NetState = ns;
            Assert.Equal(12, ClusterFLevelGearProcs.OnWeaponHit(katana, companion, foe, 20));
            Assert.Equal(T0.AddSeconds(10), LevelGearProcCooldowns.Owners[owner.Serial]);
            Assert.False(LevelGearProcCooldowns.Owners.ContainsKey(companion.Serial));
        }
        finally
        {
            owner.NetState = null;
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void PurchasesAndCooldownsSaveAndLoad()
    {
        // A version 0 store (cc-P261's shape) by hand: one row at level 150, no milestone field.
        var old = Levelable(new Katana(), 150, 440);
        var buffer = new byte[1 << 16];
        var w = new BufferWriter(buffer, true);
        w.WriteEncodedInt(0);
        w.WriteEncodedInt(1);
        w.Write(old.Serial);
        w.WriteEncodedInt(150);
        w.Write(ClusterFLevelGear.XpForLevel(150));
        w.WriteEncodedInt(440);
        w.WriteEncodedInt(200);
        w.Write(T0);
        w.Write(Serial.MinusOne);
        w.WriteEncodedInt(0);
        w.Flush();
        LevelGearStore.Rows.Clear();
        LevelGearStore.Instance.Deserialize(new BufferReader(buffer));
        Assert.Equal(150, LevelGearStore.Of(old)!.Level);
        Assert.Null(LevelGearStore.Of(old)!.Proc);

        var (pm, katana) = Armed("emberstrike");
        LevelGearProcCooldowns.Owners[pm.Serial] = T0.AddSeconds(7);
        LevelGearProcCooldowns.Weapons[katana.Serial] = T0.AddSeconds(-1); // expired

        buffer = new byte[1 << 16];
        w = new BufferWriter(buffer, true);
        LevelGearStore.Instance.Serialize(w);
        w.Flush();
        var cd = new byte[1 << 12];
        var wc = new BufferWriter(cd, true);
        LevelGearProcCooldowns.Instance.Serialize(wc);
        wc.Flush();

        LevelGearStore.Rows.Clear();
        LevelGearProcCooldowns.Owners.Clear();
        LevelGearProcCooldowns.Weapons.Clear();
        LevelGearStore.Instance.Deserialize(new BufferReader(buffer));
        LevelGearProcCooldowns.Instance.Deserialize(new BufferReader(cd));

        var p = LevelGearStore.Of(katana)!.Proc!;
        Assert.Equal("emberstrike", p.Key);
        Assert.Equal(100, p.RequiredLevel);
        Assert.Equal(100, p.PurchaseLevel);
        Assert.Equal(100, p.PaidCost);
        Assert.Equal(T0, p.PurchasedAt);
        Assert.Null(LevelGearStore.Of(old)!.Proc);
        Assert.Equal(T0.AddSeconds(7), LevelGearProcCooldowns.Owners[pm.Serial]);
        Assert.Empty(LevelGearProcCooldowns.Weapons);
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void AnInvalidChanceTurnsTheMilestoneOff()
    {
        var (pm, katana) = Armed();
        var foe = Foe(pm);
        _rollValue = 0;
        const string key = "clusterf.levelGear.procs.chanceBasisPoints";
        try
        {
            ServerConfiguration.SetSetting(key, 20000);
            Assert.NotNull(LevelGearProcSettings.Problem());
            Assert.False(LevelGearProcSettings.Enabled);
            Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, foe, 20));
        }
        finally
        {
            ServerConfiguration.SetSetting(key, 500);
        }

        Assert.True(LevelGearProcSettings.Enabled);
        Assert.Equal(12, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, foe, 20));
    }

    // ---------------------------------------------------------------- 9 (P647: review fixes)

    private static readonly System.Reflection.MethodInfo GumpRemove = typeof(GumpSystem).GetMethod("Remove",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
        [typeof(NetState), typeof(BaseGump)], null);

    // A button press as the gump system delivers it: the gump leaves the client's list, then its OnResponse runs.
    private static void Press(BaseGump gump, NetState ns, int buttonId)
    {
        GumpRemove?.Invoke(null, [ns, gump]);
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    private static NetState Online(PlayerMobile pm)
    {
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        return ns;
    }

    // P633-a: off the test shard neither the switch nor the setting turns the milestone on, and the command refuses.
    [Fact]
    public void TheMilestoneStaysOffOffTheTestShard()
    {
        var (pm, katana) = Armed();
        var foe = Foe(pm);
        _rollValue = 0;
        var gm = Track(new PlayerMobile { Player = true, AccessLevel = AccessLevel.Owner, Name = "P647 gm" });
        if (!CommandSystem.Entries.ContainsKey("LevelGearProcs"))
        {
            ClusterFLevelGearProcs.Configure();
        }

        LevelGearProcSettings.IsTestShard = () => false;
        Assert.False(LevelGearProcSettings.Enabled); // the override is on
        Assert.Contains("not available", ClusterFLevelGearProcs.WhyNotBuy(pm, Levelable(new Katana(), 100, 100), LevelGearProc.Stormstrike));
        Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, foe, 20));
        Assert.Empty(_rolls);

        LevelGearProcSettings.EnabledOverride = null;
        CommandSystem.Handle(gm, $"{CommandSystem.Prefix}LevelGearProcs on");
        Assert.Null(LevelGearProcSettings.EnabledOverride);

        LevelGearProcSettings.IsTestShard = () => true;
        CommandSystem.Handle(gm, $"{CommandSystem.Prefix}LevelGearProcs on");
        Assert.True(LevelGearProcSettings.EnabledOverride);
        Assert.Equal(12, ClusterFLevelGearProcs.OnWeaponHit(katana, pm, foe, 20));
    }

    // P633-b: the bought effect shows on the weapon's tooltip, through the item's own property list.
    [Fact]
    public void TheBoughtEffectShowsOnTheWeaponTooltip()
    {
        var (_, katana) = Armed();

        // The test host does not run LevelGearStore.Configure, which subscribes the level gear tooltip; do as it does.
        Action<Item, IPropertyList> hook = ClusterFLevelGear.AddTooltip;
        var subscribed = Item.ExtraProperties != null && Array.IndexOf(Item.ExtraProperties.GetInvocationList(), hook) >= 0;
        if (!subscribed)
        {
            Item.ExtraProperties += hook;
        }

        try
        {
            var lines = PropertyListReader.Read(katana);
            Assert.Contains(lines, l => l.Number == 1060847 && l.Argument.StartsWith("Level 100\t")); // the hook runs here
            Assert.True(lines.Has(1060847, "Milestone: Stormstrike\t(PvE)"));
        }
        finally
        {
            if (!subscribed)
            {
                Item.ExtraProperties -= hook;
            }
        }
    }

    // P645-1: the confirmation keeps the price and unlock it showed; if either changed, nothing is paid and the new terms
    // are shown to confirm again.
    [Fact]
    public void TheConfirmationChargesOnlyThePriceItShowed()
    {
        const string costKey = "clusterf.levelGear.procs.pointCost";
        const string unlockKey = "clusterf.levelGear.procs.unlockLevel";
        var pm = Player();
        var ns = Online(pm);
        try
        {
            var katana = Levelable(new Katana(), 100, 440);
            pm.Backpack.DropItem(katana);
            var s = LevelGearStore.Of(katana)!;

            var shown = new LevelGearMilestoneConfirmGump(pm, katana, "stormstrike");
            pm.SendGump(shown);
            ServerConfiguration.SetSetting(costKey, 200);
            Press(shown, ns, 1);
            Assert.Equal(440, s.Points);
            Assert.Null(s.Proc);
            var refreshed = pm.FindGump<LevelGearMilestoneConfirmGump>();
            Assert.NotNull(refreshed);
            Assert.NotSame(shown, refreshed);
            Assert.Contains(PetTrainingVerification.OnTheWire(ns, refreshed).Strings, x => x.Contains("for 200 item points"));

            Press(refreshed, ns, 1); // the new terms, confirmed
            Assert.Equal(240, s.Points);
            Assert.Equal(200, s.Proc!.PaidCost);

            // The unlock level is part of the terms too.
            var second = Levelable(new Katana(), 100, 100);
            pm.Backpack.DropItem(second);
            ServerConfiguration.SetSetting(costKey, 100);
            var before = new LevelGearMilestoneConfirmGump(pm, second, "emberstrike");
            pm.SendGump(before);
            ServerConfiguration.SetSetting(unlockKey, 90);
            Press(before, ns, 1);
            Assert.Null(LevelGearStore.Of(second)!.Proc);
            Assert.Equal(100, LevelGearStore.Of(second)!.Points);
            var again = pm.FindGump<LevelGearMilestoneConfirmGump>();
            Assert.NotNull(again);
            Press(again, ns, 1);
            Assert.Equal(90, LevelGearStore.Of(second)!.Proc!.RequiredLevel);
        }
        finally
        {
            ServerConfiguration.SetSetting(costKey, 100);
            ServerConfiguration.SetSetting(unlockKey, 100);
            pm.NetState = null;
            ns.Dispose();
        }
    }

    // P645-2: a creature that refuses damage now (a chest mimic with its lid shut) draws no roll and starts no cooldown.
    [Fact]
    public void AShutChestMimicDrawsNoRollAndNoCooldown()
    {
        var (pm, katana) = Armed();
        var mimic = Track(new ChestMimic(2));
        foreach (ResistanceType r in Enum.GetValues(typeof(ResistanceType)))
        {
            mimic.SetResistance(r, 0);
        }

        mimic.MoveToWorld(new Point3D(pm.X + 1, pm.Y, pm.Z), Map.Trammel);
        pm.Combatant = mimic;
        _rollValue = 0;

        mimic.ShutLid();
        Assert.False(mimic.CanBeDamaged());
        var hits = mimic.Hits;
        katana.OnHit(pm, mimic);
        Assert.Equal(hits, mimic.Hits);
        Assert.Empty(_rolls);
        Assert.Empty(LevelGearProcCooldowns.Owners);
        Assert.Empty(LevelGearProcCooldowns.Weapons);

        mimic.OpenLid();
        Assert.True(mimic.CanBeDamaged());
        katana.OnHit(pm, mimic);
        Assert.Single(_rolls);
        Assert.Equal(T0.AddSeconds(10), LevelGearProcCooldowns.Weapons[katana.Serial]);
    }

    // P645-3: a weapon cooldown survives a restart and a change of owner: no roll before its saved deadline, one at it.
    [Fact]
    public void AWeaponCooldownSurvivesARestartAndAHandOver()
    {
        var (pm, katana) = Armed();
        LevelGearProcCooldowns.Owners[pm.Serial] = T0.AddSeconds(7);
        LevelGearProcCooldowns.Weapons[katana.Serial] = T0.AddSeconds(10);

        var cd = new byte[1 << 12];
        var wc = new BufferWriter(cd, true);
        LevelGearProcCooldowns.Instance.Serialize(wc);
        wc.Flush();
        LevelGearProcCooldowns.Owners.Clear();
        LevelGearProcCooldowns.Weapons.Clear();
        _now = T0.AddSeconds(3); // the restart takes time
        LevelGearProcCooldowns.Instance.Deserialize(new BufferReader(cd));
        Assert.Equal(T0.AddSeconds(7), LevelGearProcCooldowns.Owners[pm.Serial]);

        var other = Player(2);
        other.AddItem(katana);
        Assert.Same(other, katana.Parent);
        var foe = Foe(other);
        _rollValue = 0;

        _now = T0.AddSeconds(9.999);
        Assert.Equal(0, ClusterFLevelGearProcs.OnWeaponHit(katana, other, foe, 20));
        Assert.Empty(_rolls);

        _now = T0.AddSeconds(10);
        Assert.Equal(12, ClusterFLevelGearProcs.OnWeaponHit(katana, other, foe, 20));
        Assert.Single(_rolls);

        // A save of a version this build does not know is refused, not misread.
        var bad = new byte[16];
        var wb = new BufferWriter(bad, true);
        wb.WriteEncodedInt(LevelGearProcCooldowns.CurrentVersion + 1);
        wb.Flush();
        Assert.Throws<System.IO.InvalidDataException>(() => LevelGearProcCooldowns.Instance.Deserialize(new BufferReader(bad)));
    }

    // P645-4: the bound effect's page says when it cannot fire, and names the level it was bought at.
    [Fact]
    public void TheBoundEffectPageShowsWhenItIsUnavailable()
    {
        const string unlockKey = "clusterf.levelGear.procs.unlockLevel";
        const string choiceKey = "clusterf.levelGear.procs.stormstrike.enabled";
        var (pm, katana) = Armed();
        var ns = Online(pm);
        try
        {
            List<string> Page()
            {
                var g = new LevelGearMilestoneGump(pm, katana);
                pm.SendGump(g);
                return PetTrainingVerification.OnTheWire(ns, g).Strings;
            }

            Assert.Contains(Page(), x => x == "Your cooldown: ready");

            ServerConfiguration.SetSetting(unlockKey, 90);
            var tuned = Page();
            Assert.Contains(tuned, x => x.Contains("level 100"));
            Assert.DoesNotContain(tuned, x => x.Contains("level 90"));
            ServerConfiguration.SetSetting(unlockKey, 100);

            LevelGearProcSettings.EnabledOverride = false;
            var off = Page();
            Assert.DoesNotContain(off, x => x.Contains("ready"));
            Assert.Contains(off, x => x.Contains("unavailable"));
            LevelGearProcSettings.EnabledOverride = true;

            ServerConfiguration.SetSetting(choiceKey, false);
            var choiceOff = Page();
            Assert.DoesNotContain(choiceOff, x => x.Contains("ready"));
            Assert.Contains(choiceOff, x => x.Contains("unavailable"));
        }
        finally
        {
            ServerConfiguration.SetSetting(unlockKey, 100);
            ServerConfiguration.SetSetting(choiceKey, true);
            pm.NetState = null;
            ns.Dispose();
        }
    }

    // P645-5: expired and deleted entries leave the cooldown maps; a future deadline stays.
    [Fact]
    public void ExpiredAndDeletedCooldownsArePruned()
    {
        var (pm, katana) = Armed();
        var gone = Player(3);
        var goneWeapon = Track(new Katana());
        LevelGearProcCooldowns.Owners[pm.Serial] = T0.AddSeconds(5);   // live
        LevelGearProcCooldowns.Owners[gone.Serial] = T0.AddSeconds(-1); // expired
        LevelGearProcCooldowns.Weapons[katana.Serial] = T0.AddSeconds(5);
        LevelGearProcCooldowns.Weapons[goneWeapon.Serial] = T0.AddSeconds(5);
        goneWeapon.Delete(); // deleted, though not yet expired

        LevelGearProcCooldowns.Prune(T0);
        Assert.Equal(new[] { pm.Serial }, LevelGearProcCooldowns.Owners.Keys);
        Assert.Equal(new[] { katana.Serial }, LevelGearProcCooldowns.Weapons.Keys);

        // Read after its deadline, an entry is dropped there and then.
        _now = T0.AddSeconds(5);
        Assert.Equal(TimeSpan.Zero, ClusterFLevelGearProcs.OwnerCooldown(pm));
        Assert.Empty(LevelGearProcCooldowns.Owners);
    }
}

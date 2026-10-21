// cc-P275: deployable traps, phase 1 (P201's design, approved by Chase 2026-10-21 as recommended, D1 to D7). Notes:
// shard-migration notes/cc-P275-deployable-traps-build.md; the design: notes/cc-P201-deployable-traps-design.md.
//
// A tinker crafts a kit (Tinkering, TinkeringCraftRegistrations), Alchemy's potions and Poisoning load the gas and
// blast payloads (drop the potion on the kit), and a player sets it with Remove Trap: the setter's Remove Trap decides
// arming time, trigger size, damage and hold, concealment and how many traps they may keep out. Remove Trap also
// disarms a set trap (the skill's target, through the IRemoveTrapTrainingKit hook cc-P199 added), and gains on both.
// The Thieves' Den owns the trade: Journeyman sets one more trap, Master's traps work on bosses at half.
//
// The rules every trap obeys (D4, D5):
//   - Monsters only, on every facet. Players, pets, summons and vendors never set one off. clusterf.traps.pvp (off)
//     lets a trap in Felucca hit anyone its owner may harm, and the owner is flagged as a thrown potion flags them.
//   - Owner present: a trap fires only while its owner is on the same map, within clusterf.traps.presenceRange (12)
//     tiles, alive and logged in. Otherwise it lies dormant and the creature walks over it. The alarm ignores the range
//     (it farms nothing) but still needs the owner on the map and logged in.
//   - Every point of damage and every poison comes from the owner, so loot rights, the loot boxes and Clean Up credit
//     the trapper through pinned's own GetLootingRights.
//   - Charges, not timers: a trap fires its charges, then is spent until the owner re-sets it by hand (double-click,
//     or a fresh payload dropped on a gas, blast or dart trap). The bear trap opens again between its snaps.
//   - Litter control is the world's item decay: a set trap decays clusterf.traps.decayMinutes (30) after its last
//     set, a spent one after clusterf.traps.spentDecayMinutes (2).
//
// Every number in the design's section 3 is a setting, clusterf.traps.<trap>.<name> (the defaults below).

#nullable enable

using System;
using System.Collections.Generic;
using Server.Engines.CreatureStealing;
using Server.Engines.PartySystem;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;

namespace Server.Items;

public enum DeployableTrapKind
{
    SnareNet,
    Caltrops,
    Gas,
    Blast,
    Dart,
    BearTrap,
    Alarm,
    Shackle
}

public static class DeployableTraps
{
    public static readonly DeployableTrapKind[] AllKinds =
    [
        DeployableTrapKind.SnareNet, DeployableTrapKind.Caltrops, DeployableTrapKind.Gas, DeployableTrapKind.Blast,
        DeployableTrapKind.Dart, DeployableTrapKind.BearTrap, DeployableTrapKind.Alarm, DeployableTrapKind.Shackle
    ];

    // -- Settings -------------------------------------------------------------------------------------------------

    public static bool PvpEnabled => ServerConfiguration.GetOrUpdateSetting("clusterf.traps.pvp", false);
    public static int PresenceRange => ServerConfiguration.GetOrUpdateSetting("clusterf.traps.presenceRange", 12);
    public static int DecayMinutes => ServerConfiguration.GetOrUpdateSetting("clusterf.traps.decayMinutes", 30);
    public static int SpentDecayMinutes => ServerConfiguration.GetOrUpdateSetting("clusterf.traps.spentDecayMinutes", 2);
    public static int AreaCap => ServerConfiguration.GetOrUpdateSetting("clusterf.traps.areaCap", 6);
    public static int AreaCapRange => ServerConfiguration.GetOrUpdateSetting("clusterf.traps.areaCapRange", 8);
    public static int SetRange => ServerConfiguration.GetOrUpdateSetting("clusterf.traps.setRange", 2);
    public static int PracticeHoldSeconds => ServerConfiguration.GetOrUpdateSetting("clusterf.traps.practiceHoldSeconds", 4);

    public static string KeyOf(DeployableTrapKind kind) => kind switch
    {
        DeployableTrapKind.SnareNet => "snare",
        DeployableTrapKind.Caltrops => "caltrops",
        DeployableTrapKind.Gas      => "gas",
        DeployableTrapKind.Blast    => "blast",
        DeployableTrapKind.Dart     => "dart",
        DeployableTrapKind.BearTrap => "bear",
        DeployableTrapKind.Alarm    => "alarm",
        _                           => "shackle"
    };

    /// <summary>The design's section 3 numbers, by trap and name: the defaults of clusterf.traps.&lt;trap&gt;.&lt;name&gt;.</summary>
    public static readonly Dictionary<(DeployableTrapKind, string), int> Defaults = new()
    {
        [(DeployableTrapKind.SnareNet, "charges")] = 1,
        [(DeployableTrapKind.SnareNet, "holdSeconds")] = 4,
        [(DeployableTrapKind.SnareNet, "tinkering")] = 30,
        [(DeployableTrapKind.SnareNet, "removeTrap")] = 0,

        [(DeployableTrapKind.Caltrops, "charges")] = 5,
        [(DeployableTrapKind.Caltrops, "radius")] = 1,
        [(DeployableTrapKind.Caltrops, "damageMin")] = 5,
        [(DeployableTrapKind.Caltrops, "damageMax")] = 10,
        [(DeployableTrapKind.Caltrops, "slowSeconds")] = 8,
        [(DeployableTrapKind.Caltrops, "tinkering")] = 40,
        [(DeployableTrapKind.Caltrops, "removeTrap")] = 20,

        [(DeployableTrapKind.Gas, "charges")] = 1,
        [(DeployableTrapKind.Gas, "radius")] = 2,
        [(DeployableTrapKind.Gas, "tinkering")] = 60,
        [(DeployableTrapKind.Gas, "removeTrap")] = 40,

        [(DeployableTrapKind.Blast, "charges")] = 1,
        [(DeployableTrapKind.Blast, "radius")] = 2,
        [(DeployableTrapKind.Blast, "lesserMin")] = 20,
        [(DeployableTrapKind.Blast, "lesserMax")] = 40,
        [(DeployableTrapKind.Blast, "damageMin")] = 30,
        [(DeployableTrapKind.Blast, "damageMax")] = 60,
        [(DeployableTrapKind.Blast, "greaterMin")] = 40,
        [(DeployableTrapKind.Blast, "greaterMax")] = 80,
        [(DeployableTrapKind.Blast, "tinkering")] = 90,
        [(DeployableTrapKind.Blast, "removeTrap")] = 60,

        [(DeployableTrapKind.Dart, "charges")] = 5, // the most darts it holds; each dart is a charge
        [(DeployableTrapKind.Dart, "damageMin")] = 25,
        [(DeployableTrapKind.Dart, "damageMax")] = 50,
        [(DeployableTrapKind.Dart, "tinkering")] = 50,
        [(DeployableTrapKind.Dart, "removeTrap")] = 30,

        [(DeployableTrapKind.BearTrap, "charges")] = 3,
        [(DeployableTrapKind.BearTrap, "holdSeconds")] = 6,
        [(DeployableTrapKind.BearTrap, "damageMin")] = 15,
        [(DeployableTrapKind.BearTrap, "damageMax")] = 30,
        [(DeployableTrapKind.BearTrap, "tinkering")] = 80,
        [(DeployableTrapKind.BearTrap, "removeTrap")] = 50,

        [(DeployableTrapKind.Alarm, "charges")] = 10,
        [(DeployableTrapKind.Alarm, "radius")] = 2,
        [(DeployableTrapKind.Alarm, "tinkering")] = 70,
        [(DeployableTrapKind.Alarm, "removeTrap")] = 30,

        [(DeployableTrapKind.Shackle, "charges")] = 1,
        [(DeployableTrapKind.Shackle, "radius")] = 1,
        [(DeployableTrapKind.Shackle, "holdSeconds")] = 3,
        [(DeployableTrapKind.Shackle, "tinkering")] = 110,
        [(DeployableTrapKind.Shackle, "removeTrap")] = 80,
    };

    public static int Setting(DeployableTrapKind kind, string name) =>
        ServerConfiguration.GetOrUpdateSetting($"clusterf.traps.{KeyOf(kind)}.{name}", Defaults[(kind, name)]);

    // -- What each trap is ----------------------------------------------------------------------------------------

    public static string NameOf(DeployableTrapKind kind) => kind switch
    {
        DeployableTrapKind.SnareNet => "snare net",
        DeployableTrapKind.Caltrops => "caltrops",
        DeployableTrapKind.Gas      => "gas trap",
        DeployableTrapKind.Blast    => "blast trap",
        DeployableTrapKind.Dart     => "dart trap",
        DeployableTrapKind.BearTrap => "bear trap",
        DeployableTrapKind.Alarm    => "alarm trap",
        _                           => "shackle trap"
    };

    // Stock art for six (D7, the design's section 7.1). The snare's sprung art, the gas canister, the floor dart
    // launcher and the shackle's rune ring are art requests; until they land these are placeholders (a hued net, a green
    // potion, the fukiya darts, the Magic Trap scroll). Every id here is walkable in tiledata except the keg 0x1940
    // (Impassable): the blast trap fires on a creature entering its radius, so nothing needs to step on it.
    public static int SetArt(DeployableTrapKind kind) => kind switch
    {
        DeployableTrapKind.SnareNet => 0x0DC8, // fishing net (stock)
        DeployableTrapKind.Caltrops => 0x119A, // floor spikes (stock)
        DeployableTrapKind.Gas      => 0x0F0A, // placeholder: a green potion (request trap-gas-canister)
        DeployableTrapKind.Blast    => 0x1940, // keg (stock)
        DeployableTrapKind.Dart     => 0x2806, // placeholder: fukiya darts (request trap-dart-launcher)
        DeployableTrapKind.BearTrap => 0xB2C5, // "Bear Trap On" (stock; the client check is a play test)
        DeployableTrapKind.Alarm    => 0x4C5A, // cowbell (stock)
        _                           => 0x1F39  // placeholder: the Magic Trap scroll (request trap-shackle-ring)
    };

    public static int SpentArt(DeployableTrapKind kind) => kind switch
    {
        DeployableTrapKind.Caltrops => 0x11A0, // another facing of the floor spikes
        DeployableTrapKind.BearTrap => 0xB2C3, // "Bear Trap Off"
        DeployableTrapKind.Alarm    => 0x4C5B,
        _                           => SetArt(kind) // placeholder: the set art, hued SpentHue (request trap-snare-net)
    };

    public const int SpentHue = 0x3E9;  // a dull grey: spent, until each trap's sprung art lands
    public const int PracticeHue = 0x59; // the practice net's blue

    /// <summary>Area traps fire on a creature entering their radius and (gas, blast, shackle) hit everything in it.</summary>
    public static bool IsArea(DeployableTrapKind kind) =>
        kind is DeployableTrapKind.Caltrops or DeployableTrapKind.Gas or DeployableTrapKind.Blast or DeployableTrapKind.Alarm
            or DeployableTrapKind.Shackle;

    /// <summary>Ground traps never fire on a flyer (the design's section 3.1).</summary>
    public static bool IsGround(DeployableTrapKind kind) =>
        kind is DeployableTrapKind.SnareNet or DeployableTrapKind.Caltrops or DeployableTrapKind.BearTrap;

    /// <summary>Nets, caltrops, jaws and darts pass through the incorporeal.</summary>
    public static bool IsPhysical(DeployableTrapKind kind) => IsGround(kind) || kind == DeployableTrapKind.Dart;

    /// <summary>pinned has no incorporeal flag (the design's section 3.1): the list.</summary>
    public static HashSet<Type> IncorporealTypes { get; } =
    [
        typeof(Shade), typeof(Spectre), typeof(Wraith), typeof(Bogle), typeof(Wisp), typeof(EnergyVortex),
        typeof(BladeSpirits), typeof(AirElemental), typeof(PoisonElemental)
    ];

    // -- Remove Trap scales (the design's section 1.1) -------------------------------------------------------------

    public static double RemoveTrapMin(DeployableTrapKind kind) => Setting(kind, "removeTrap");
    public static double TinkeringMin(DeployableTrapKind kind) => Setting(kind, "tinkering");

    /// <summary>Traps out at once: 1 + RT/50, and one more at Den Journeyman.</summary>
    public static int MaxOut(Mobile owner) =>
        1 + (int)(owner.Skills.RemoveTrap.Value / 50) + (ThievesDenStealRules.DenRank(owner) >= ClusterFGuildSystem.RankJourneyman ? 1 : 0);

    public static double ArmingSeconds(double removeTrap, bool hidden) => hidden ? 1.0 : Math.Max(0.0, 4.0 - removeTrap / 100.0);

    /// <summary>How near a creature must come: an area trap's radius; the bear trap's own tile; else 0, 1 from 100, 2 at 200.</summary>
    public static int TriggerRadius(DeployableTrapKind kind, double removeTrap)
    {
        if (IsArea(kind))
        {
            return Setting(kind, "radius");
        }

        if (kind == DeployableTrapKind.BearTrap)
        {
            return 0;
        }

        return removeTrap >= 200 ? 2 : removeTrap >= 100 ? 1 : 0;
    }

    public static double DamageScale(double removeTrap) => 0.5 + removeTrap / 200.0;

    public static double HoldSeconds(DeployableTrapKind kind, double removeTrap) => Setting(kind, "holdSeconds") + removeTrap / 50.0;

    public static int Concealment(double removeTrap, bool hidden) => (int)removeTrap + (hidden ? 20 : 0);

    /// <summary>The set's gain window: the kit's Remove Trap minimum to 50 above it.</summary>
    public static (double Min, double Max) SetGainWindow(DeployableTrapKind kind)
    {
        var min = RemoveTrapMin(kind);
        return (min, min + 50);
    }

    /// <summary>A hit's gain window: 40 above the set's to 70 above its top (the shackle's reaches 200).</summary>
    public static (double Min, double Max) HitGainWindow(DeployableTrapKind kind)
    {
        var (min, max) = SetGainWindow(kind);
        return (min + 40, max + 70);
    }

    public static int Charges(DeployableTrapKind kind, bool exceptional) =>
        Setting(kind, "charges") + (exceptional && kind is not (DeployableTrapKind.Caltrops or DeployableTrapKind.Blast or DeployableTrapKind.Dart or DeployableTrapKind.Alarm) ? 1 : 0);

    // -- Who a trap may touch (D4, D5, the Den's boss rule) --------------------------------------------------------

    /// <summary>The owner is on the trap's map, alive and logged in, and (but for the alarm) within the presence range.</summary>
    public static bool OwnerPresent(DeployedTrap trap)
    {
        var owner = trap.Owner;

        if (owner == null || owner.Deleted || !owner.Alive || owner.NetState == null || owner.Map != trap.Map || trap.Map == null ||
            trap.Map == Map.Internal)
        {
            return false;
        }

        return trap.Kind == DeployableTrapKind.Alarm || owner.InRange(trap.GetWorldLocation(), PresenceRange);
    }

    public static bool IsBoss(Mobile m) => m is BaseCreature bc && ThievesDenStealRules.IsBoss(bc);

    public static bool OwnerMayTrapBosses(Mobile owner) => ThievesDenStealRules.DenRank(owner) >= ClusterFGuildSystem.RankMaster;

    public static bool IsFlying(Mobile m) => m.Flying || m is BaseCreature { CanFly: true };

    /// <summary>
    /// Whether the trap may touch <paramref name="m"/> at all (D4): a wild, living, unsummoned monster (a tutorial
    /// creature only for an owner it engages), or with clusterf.traps.pvp on in Felucca anyone the owner may harm. Then
    /// pinned's harm rule from the owner (Young protection, the Burrows' limit).
    /// </summary>
    public static bool IsTarget(DeployedTrap trap, Mobile m)
    {
        var owner = trap.Owner;

        if (owner == null || m == null || m.Deleted || !m.Alive || m == owner || m.AccessLevel > AccessLevel.Player || m.Blessed)
        {
            return false;
        }

        var monster = m is BaseCreature { IsMonster: true, Controlled: false, Summoned: false, IsDeadBondedPet: false } bc &&
                      bc is not BaseVendor && !bc.IsInvulnerable && (bc is not TutorialCreature || TutorialCreature.Engages(owner));

        if (!monster && !(PvpEnabled && trap.Map == Map.Felucca))
        {
            return false;
        }

        return owner.CanBeHarmful(m, false);
    }

    /// <summary>Why a target that is otherwise fair shrugs this trap off (null: it does not). A boss below Master says so.</summary>
    public static string? Shrugs(DeployedTrap trap, Mobile m)
    {
        var kind = trap.Kind;

        if (kind == DeployableTrapKind.Alarm)
        {
            return null; // a bell notices everything
        }

        if (IsGround(kind) && IsFlying(m))
        {
            return "flying";
        }

        if (IsPhysical(kind) && IncorporealTypes.Contains(m.GetType()))
        {
            return "incorporeal";
        }

        if (IsBoss(m) && !OwnerMayTrapBosses(trap.Owner!))
        {
            return "boss";
        }

        return null;
    }

    public static string BossNotTrustedLine(Mobile boss, DeployableTrapKind kind) =>
        $"The {boss.Name} does not notice your {NameOf(kind)}. The Den would not trust you with that one yet.";

    /// <summary>The owner and the owner's party see an armed trap; staff see everything.</summary>
    public static bool CanSee(DeployedTrap trap, Mobile from)
    {
        var owner = trap.Owner;
        return owner != null && (from == owner || Party.Get(owner) is { } party && party.Contains(from));
    }

    // -- Where a trap may go (the design's section 5) ---------------------------------------------------------------

    /// <summary>Every armed or spent trap in the world, by owner (rebuilt on load).</summary>
    public static readonly HashSet<DeployedTrap> Placed = [];

    public static int CountOut(Mobile owner)
    {
        var n = 0;
        foreach (var t in Placed)
        {
            if (t.Owner == owner && !t.Deleted)
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Whether a trap of this kind may be set at <paramref name="p"/>; when not, the line to say.</summary>
    public static bool CanSetAt(Mobile from, DeployableTrapKit kit, Map map, Point3D p, out string? refusal)
    {
        refusal = null;

        if (map == null || map == Map.Internal || from.Map != map)
        {
            refusal = "You cannot set a trap there.";
        }
        else if (!from.InRange(p, SetRange))
        {
            refusal = "That is too far away.";
        }
        else if (from.Mounted || from.Flying)
        {
            refusal = "You cannot set the trap while riding or flying.";
        }
        else if (!kit.Practice && from.Skills.RemoveTrap.Value < RemoveTrapMin(kit.Kind))
        {
            refusal = "You do not know enough about traps to set that.";
        }
        else if (kit.Practice && Region.Find(p, map)?.IsPartOf<OldHavenBurrowsRegion>() != true)
        {
            refusal = "The practice net is for the Burrows.";
        }
        else if (!kit.IsLoaded)
        {
            refusal = kit.Kind switch
            {
                DeployableTrapKind.Gas   => "The gas trap is empty. Drop a poison potion on it first.",
                DeployableTrapKind.Blast => "The blast trap is empty. Drop an explosion potion on it first.",
                _                        => "The dart trap is empty. Drop fukiya darts on it first."
            };
        }
        else if (Region.Find(p, map) is { } region &&
                 (region.GetRegion<GuardedRegion>() is { } guarded && !guarded.IsDisabled() || region.IsPartOf<HouseRegion>() ||
                  BaseHouse.FindHouseAt(p, map, 16) != null))
        {
            refusal = "You cannot set a trap in a town or a house.";
        }
        else if (!TileIsClear(map, p))
        {
            refusal = "You cannot set a trap there.";
        }
        else if (CountOut(from) >= MaxOut(from))
        {
            refusal = "You already have as many traps out as you can watch.";
        }
        else if (ArmedNear(map, p) >= AreaCap)
        {
            refusal = "There are enough traps here already.";
        }

        return refusal == null;
    }

    /// <summary>No mobile on the tile, no other trap, door, teleporter, gate or crawlway on it, and no door beside it.</summary>
    public static bool TileIsClear(Map map, Point3D p)
    {
        foreach (var m in map.GetMobilesInRange(p, 0))
        {
            if (m.Z + 16 > p.Z && p.Z + 16 > m.Z)
            {
                return false;
            }
        }

        foreach (var item in map.GetItemsInRange(p, 1))
        {
            var onTile = item.X == p.X && item.Y == p.Y;

            if (item is BaseDoor || onTile && item is DeployedTrap or Teleporter or Moongate or RootdeepCrawlway)
            {
                return false;
            }
        }

        return true;
    }

    public static int ArmedNear(Map map, Point3D p)
    {
        var n = 0;
        foreach (var t in Placed)
        {
            if (!t.Deleted && t.Map == map && t.Armed && Utility.InRange(t.Location, p, AreaCapRange))
            {
                n++;
            }
        }

        return n;
    }
}

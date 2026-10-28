// ServUO: Services/Revamped Dungeons/BlackthornDungeon/Region.cs (cc-P196).
//
// Two regions ServUO declares in Data/Regions.xml:
//
//   <region type="BlackthornDungeon" priority="50" name="BlackthornDungeon">   Trammel and Felucca
//     <rect x="6151" y="2301" width="413" height="539" />  go 6430,2678,0  music Dungeon9
//   <region type="BlackthornCastle" priority="51" name="Blackthorn Castle">    Trammel only
//     <rect x="1503" y="1377" width="79" height="120" /> <rect x="1520" y="1482" width="6" height="23" />
//     <rect x="1466" y="1407" width="40" height="88" />  go 1529,1477,0  music LBCastle
//
// Pinned ModernUO reads its regions from Data/regions.json, a data file apply-patches.sh does not carry, and that
// file has neither (checked: no region touches the dungeon rectangle on either facet; Britain's TownRegion
// (1416,1498 to 1740,1777) meets the castle's southern strip, which is why ServUO's castle is priority 51), so both
// are registered here at Initialize (Main.cs invokes Initialize after RegionJsonSerializer.LoadRegions), the
// WrongRegions shape (cc-P194).
//
// BlackthornDungeon: every five seconds a hidden player is revealed, and a player in the dungeon proper (Y > 2575,
// south of the invasion rooms) who has not moved for two minutes is thrown to one of eight spots in a noxious gas
// cloud (lethal poison, 500855) that draws every idle creature within twelve tiles onto them. Recall, gate and mark
// are refused (CheckTravel allows TeleportFrom and TeleportTo only); staff pass. A creature's death inside hands
// every damager with looting rights to BlackthornData (the Minax artifact tally; ServUO reaches it through
// EventSink.OnKilledBy, see Services/PointsSystems/BlackthornData.cs). ServUO's OnDeath also tells the invasion
// controller; that is the next prompt's (notes, Left for the next prompt), and the hook is marked.
// ServUO skips the tick while its 2019 Town Cryer arc's brotherhood event is on (the points system named for that
// brotherhood, Enabled); that event is not on this shard, so the tick always runs (D, notes).
//
// BlackthornCastle: a guarded region where no spell or ability can be cast (Paladin 1062075, Ninja and Samurai
// 1062938, everything else 502629), mounts are refused on entry (dismounted, 30 seconds before remounting, 1153052)
// and pets are sent to the stables (1153050) or, when they cannot be (human-bodied, dead, a loaded pack animal, or
// the stables are full), outside the castle at one of four spots (1153053). ServUO's A Visit To Castle Blackthorn
// quest hook (Town Cryer) is dropped (D, notes).
//
// Conversion notes: GetEnumeratedMobiles is GetMobiles; LocalOverheadMessage is (type, hue, number) here;
// a player's stabled pets are PlayerMobile.Stabled (a HashSet, AddStabled); AIObject.MoveTo is (mobile, range); CheckTravel is
// BaseRegion's with the out message; the repeating tick is a DelayCallTimer the registry stops on Unregister.

using System;
using System.Collections.Generic;
using Server.Engines.Points;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Spells;
using Server.Spells.Bushido;
using Server.Spells.Chivalry;
using Server.Spells.Ninjitsu;

namespace Server.Engines.Blackthorn;

public static class BlackthornRegions
{
    public const string DungeonName = "BlackthornDungeon";
    public const string CastleName = "Blackthorn Castle";

    public static readonly Rectangle2D DungeonBounds = new(6151, 2301, 413, 539);

    public static readonly Rectangle2D[] CastleBounds =
    {
        new(1503, 1377, 79, 120),
        new(1520, 1482, 6, 23),
        new(1466, 1407, 40, 88)
    };

    private static readonly Dictionary<Map, BlackthornDungeon> _dungeons = new();
    private static BlackthornCastle _castle;

    /// <summary>
    ///     cc-P677 (SoS swap S8): off. On the Siege of Sosaria land the dungeon's area (DungeonBounds) holds the author's
    ///     own dungeons and the castle's is his town, so neither region registers at start and [GenBlackthorn refuses.
    ///     The code stays whole for P691, which moves Blackthorn into the Fold's zone 5 and turns this on there.
    /// </summary>
    public static bool OnThisLand { get; set; }

    public static void Initialize()
    {
        if (!OnThisLand)
        {
            return;
        }

        Register(Map.Trammel);
        Register(Map.Felucca);
    }

    public static BlackthornDungeon GetDungeon(Map map) => _dungeons.GetValueOrDefault(map);

    public static BlackthornCastle Castle => _castle;

    /// <summary>
    ///     Registers the dungeon region on a facet (and the castle on Trammel). Safe to call twice: the second call
    ///     returns the first registration.
    /// </summary>
    public static BlackthornDungeon Register(Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return null;
        }

        if (_dungeons.TryGetValue(map, out var existing) && existing?.Registered == true)
        {
            return existing;
        }

        var dungeon = new BlackthornDungeon(map);
        dungeon.Register();
        _dungeons[map] = dungeon;

        if (map == Map.Trammel && _castle?.Registered != true)
        {
            _castle = new BlackthornCastle(map);
            _castle.Register();
        }

        return dungeon;
    }

    /// <summary>Test support: unregisters the facet's dungeon region (and the castle with Trammel's).</summary>
    public static void Unregister(Map map)
    {
        if (_dungeons.Remove(map, out var dungeon) && dungeon != null)
        {
            dungeon.StopTick();
            dungeon.Unregister();
        }

        if (map == Map.Trammel && _castle != null)
        {
            _castle.Unregister();
            _castle = null;
        }
    }
}

public class BlackthornDungeon : DungeonRegion
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);
    public const long IdleMilliseconds = 120000;
    public const int InvasionRoomsSouthEdge = 2575;

    public static readonly Point3D[] RandomLocations =
    {
        new(6459, 2781, 0),
        new(6451, 2781, 0),
        new(6443, 2781, 0),
        new(6409, 2792, 0),
        new(6356, 2781, 0),
        new(6272, 2702, 0),
        new(6272, 2656, 0),
        new(6456, 2623, 0)
    };

    private Timer _tick;

    public BlackthornDungeon(Map map)
        : base(BlackthornRegions.DungeonName, map, DefaultPriority, ConvertTo3D(BlackthornRegions.DungeonBounds))
    {
        GoLocation = new Point3D(6430, 2678, 0);
        _tick = Timer.DelayCall(TickInterval, TickInterval, OnTick);
    }

    public void StopTick()
    {
        _tick?.Stop();
        _tick = null;
    }

    public void OnTick()
    {
        if (!Registered)
        {
            return;
        }

        foreach (var m in GetMobiles())
        {
            if (m is not PlayerMobile || m.AccessLevel != AccessLevel.Player)
            {
                continue;
            }

            if (m.Hidden)
            {
                m.RevealingAction();
            }

            if (m.Y > InvasionRoomsSouthEdge && m.LastMoveTime + IdleMilliseconds < Core.TickCount)
            {
                MoveLocation(m);
            }
        }
    }

    public void MoveLocation(Mobile m)
    {
        var p = RandomLocations.RandomElement();

        m.MoveToWorld(p, Map);

        for (var x = m.X - 1; x <= m.X + 1; x++)
        {
            for (var y = m.Y - 1; y <= m.Y + 1; y++)
            {
                Effects.SendLocationEffect(new Point3D(x, y, m.Z), m.Map, Utility.RandomList(14120, 4518, 14133), 16, 1, 1166, 0);
            }
        }

        Effects.PlaySound(m.Location, m.Map, 0x231);
        m.LocalOverheadMessage(MessageType.Regular, 0x22, 500855); // You are enveloped by a noxious gas cloud!
        m.ApplyPoison(m, Poison.Lethal);

        foreach (var mob in Map.GetMobilesInRange(m.Location, 12))
        {
            if (mob.Combatant == null && mob is BaseCreature bc && bc.GetMaster() == null && mob.CanBeHarmful(m))
            {
                if (mob.InLOS(m))
                {
                    Timer.DelayCall(() => mob.Combatant = m);
                }
                else
                {
                    bc.AIObject?.MoveTo(m, 1);
                }
            }
        }

        m.LastMoveTime = Core.TickCount;
    }

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;

        if (m.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        return travelType > TravelCheckType.Mark;
    }

    public override void OnDeath(Mobile m)
    {
        if (m is BaseCreature bc)
        {
            // ServUO: InvasionController.TramInstance / FelInstance.OnDeath(bc) here as well (the next prompt).
            BlackthornData.Instance.OnCreatureDeath(bc);
        }

        base.OnDeath(m);
    }
}

public class BlackthornCastle : GuardedRegion
{
    public static readonly Point3D[] StableLocs =
    {
        new(1510, 1543, 25),
        new(1516, 1542, 25),
        new(1520, 1542, 25),
        new(1525, 1542, 25)
    };

    public static readonly TimeSpan RemountDelay = TimeSpan.FromSeconds(30);

    public BlackthornCastle(Map map)
        : base(BlackthornRegions.CastleName, map, DefaultPriority + 1, BlackthornRegions.CastleBounds) =>
        GoLocation = new Point3D(1529, 1477, 0);

    public override bool OnBeginSpellCast(Mobile m, ISpell s)
    {
        if (m.AccessLevel > AccessLevel.Player)
        {
            return base.OnBeginSpellCast(m, s);
        }

        int loc;

        if (s is PaladinSpell)
        {
            loc = 1062075; // You cannot use a Paladin ability here.
        }
        else if (s is NinjaMove or NinjaSpell or SamuraiSpell or SamuraiMove)
        {
            loc = 1062938; // That ability does not seem to work here.
        }
        else
        {
            loc = 502629; // You cannot cast that spell here.
        }

        m.SendLocalizedMessage(loc);
        return false;
    }

    public override void OnLocationChanged(Mobile m, Point3D oldLocation)
    {
        base.OnLocationChanged(m, oldLocation);

        // ServUO: Quests.AVisitToCastleBlackthornQuest.CheckLocation for a player stepping north of y 1485 between
        // x 1520 and 1525 (a Town Cryer quest, not on this shard).

        if (m.AccessLevel > AccessLevel.Player)
        {
            return;
        }

        if (m.Mount != null)
        {
            var mount = m.Mount;

            if (m is PlayerMobile pm)
            {
                pm.SetMountBlock(BlockMountType.DismountRecovery, RemountDelay, true);
            }
            else
            {
                mount.Rider = null;
            }

            m.SendLocalizedMessage(1153052); // Mounts and flying are not permitted in this area.

            if (mount is BaseCreature { Controlled: true } ridden)
            {
                TryAutoStable(ridden);
            }
        }

        if (m is BaseCreature { Controlled: true } pet)
        {
            TryAutoStable(pet);
        }
    }

    public void TryAutoStable(BaseCreature pet)
    {
        if (pet == null)
        {
            return;
        }

        var owner = pet.GetMaster();

        if (!pet.Controlled || owner == null)
        {
            return;
        }

        if (pet.Body.IsHuman || pet.IsDeadPet)
        {
            SendToStables(pet, owner);
        }
        else if (owner is not PlayerMobile pm || (pm.Stabled?.Count ?? 0) >= AnimalTrainer.GetMaxStabled(owner))
        {
            SendToStables(pet, owner);
        }
        else if (pet is PackLlama or PackHorse or Beetle && pet.Backpack?.Items.Count > 0)
        {
            SendToStables(pet, owner);
        }
        else
        {
            pet.ControlTarget = null;
            pet.ControlOrder = OrderType.Stay;
            pet.Internalize();

            pet.SetControlMaster(null);
            pet.SummonMaster = null;

            pet.IsStabled = true;
            pet.StabledBy = owner;

            if (Core.SE)
            {
                pet.Loyalty = BaseCreature.MaxLoyalty; // Wonderfully happy
            }

            pm.AddStabled(pet);
            // Pets are not permitted in this location. Your pet named ~1_NAME~ has been sent to the stables.
            owner.SendLocalizedMessage(1153050, pet.Name);
        }
    }

    public void SendToStables(BaseCreature bc, Mobile m = null)
    {
        bc.MoveToWorld(StableLocs.RandomElement(), Map);

        // Pets are not permitted in this area. Your pet named ~1_NAME~ could not be sent to the stables, so has been
        // teleported outside the event area.
        m?.SendLocalizedMessage(1153053, bc.Name);
    }

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;

        if (m.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        return travelType > TravelCheckType.Mark;
    }
}

// ServUO: Services/Revamped Dungeons/DespiseRevamped/Region.cs (CC4 Despise).
//
// Four DYNAMIC regions, created and Register()ed by DespiseController.BeginTimer at runtime (not from
// regions.json): "Despise Good", "Despise Evil", "Despise Lower" and "Despise Start". They sit inside the
// static "Despise" DungeonRegion Trammel entry at the same priority (50); both emulators sort dynamic
// regions ahead of static ones at equal priority (ModernUO Region.cs:235-253, ServUO Server/Region.cs:694),
// so Region.Find returns these inside their rectangles. They have no parent, so nothing chains to the
// static dungeon region — light level and no-housing are re-stated here exactly as ServUO does.
//
// What the region does: awards power and karma (and a PutridHeart to the master) when a possessed
// creature lands the most damage on a wild one; kicks players who die in the lower level, and pets or
// summons that are not Despise creatures, back to the entrance; refuses to open a DespiseCreature's corpse;
// blocks recall/gate/mark into the dungeon while allowing recall out; forces dungeon light; tells the
// controller when the overlord dies.
//
// Conversion notes:
//   CheckTravel is BaseRegion.CheckTravel(m, loc, type, out TextDefinition) here; SpellHelper.CheckTravel
//     consults both the destination and the current region with it (SpellHelper.cs:669-679), as ServUO does.
//   EventSink.OnEnterRegion has no ModernUO counterpart. ServUO uses it to dissolve the orb when its owner
//     leaves the dungeon; OnExit below does the same from the region's side (see DespiseController.OnLeaveDespise).
//   WhisperingWithWispsQuest.OnBossSlain is a Town Cryer quest hook, not on this shard (D-30).
//   GetEnumeratedMobiles/Items are GetMobiles/GetItems; TimerStateCallback is a lambda.

using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;

namespace Server.Engines.Despise;

public class DespiseRegion : BaseRegion
{
    private readonly bool _lowerLevel;

    private readonly Rectangle2D _kickBounds = new(5576, 626, 6, 10);

    public DespiseRegion(string name, Rectangle2D[] bounds) : this(name, bounds, false)
    {
    }

    // ServUO passes Region.DefaultPriority (50), the same as the static "Despise" DungeonRegion these sit
    // inside, and wins the tie because RunUO's Sector sorts dynamic regions ahead of static ones. Pinned
    // ModernUO constructs EVERY region, regions.json ones included, through the chain that sets
    // Dynamic = true (Region.cs:159-165, the [JsonConstructor] at :144), so its "dynamic first" arm never
    // separates the two and an equal-priority tie falls to list order: the 2026-09-19 headless run saw
    // Region.Find return the static dungeon everywhere and the controller find 0 of its 30 army spawners.
    // Priority 51 is the same outcome ServUO gets from its sort. Recorded in notes/cc4-despise.md.
    public const int DespisePriority = DefaultPriority + 1;

    public DespiseRegion(string name, Rectangle2D[] bounds, bool lowerLevel)
        : base(name, Map.Trammel, DespisePriority, bounds)
    {
        _lowerLevel = lowerLevel;
        Register();
    }

    public bool IsInGoodRegion(Point3D loc)
    {
        foreach (var rec in DespiseController.GoodBounds)
        {
            if (rec.Contains(loc))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsInEvilRegion(Point3D loc)
    {
        foreach (var rec in DespiseController.EvilBounds)
        {
            if (rec.Contains(loc))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsInLowerRegion(Point3D loc)
    {
        foreach (var rec in DespiseController.LowerLevelBounds)
        {
            if (rec.Contains(loc))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsInStartRegion(Point3D loc) => !IsInLowerRegion(loc) && !IsInEvilRegion(loc) && !IsInGoodRegion(loc);

    public override void OnDeath(Mobile m)
    {
        base.OnDeath(m);

        if (m is DespiseBoss boss)
        {
            var controller = DespiseController.Instance;

            if (controller != null && controller.Boss == boss)
            {
                // ServUO: Server.Engines.Quests.WhisperingWithWispsQuest.OnBossSlain(boss); Town Cryer, D-30.

                controller.OnBossSlain();
            }
        }
        else if (m is PlayerMobile && _lowerLevel)
        {
            KickFromRegion(m, false);
        }
    }

    public override bool OnBeforeDeath(Mobile m)
    {
        if (m is DespiseCreature dc && m.Region?.IsPartOf<DespiseRegion>() == true)
        {
            if (!dc.Controlled && dc.Orb == null)
            {
                var creatures = new Dictionary<DespiseCreature, int>();

                foreach (var de in m.DamageEntries)
                {
                    if (de.Damager is DespiseCreature creat)
                    {
                        if (!creat.Controlled || creat.Orb == null)
                        {
                            continue;
                        }

                        if (creatures.ContainsKey(creat))
                        {
                            creatures[creat] += de.DamageGiven;
                        }
                        else
                        {
                            creatures[creat] = de.DamageGiven;
                        }
                    }
                }

                if (creatures.Count > 0)
                {
                    DespiseCreature topdam = null;
                    var highest = 0;

                    foreach (var (creature, damage) in creatures)
                    {
                        if (topdam == null || damage > highest)
                        {
                            topdam = creature;
                            highest = damage;
                        }
                    }

                    if (topdam != null && highest > 0)
                    {
                        var mobKarma = Math.Abs(dc.Karma);
                        var karma = (int)(mobKarma / 10.0 * highest / dc.HitsMax);

                        if (karma < 1)
                        {
                            karma = 1;
                        }

                        if (dc.Karma > 0)
                        {
                            karma *= -1;
                        }

                        var master = topdam.GetMaster();
                        var oldAlign = topdam.Alignment;
                        var power = topdam.Power;
                        topdam.Karma += karma;
                        var newAlign = topdam.Alignment;

                        if (master != null && karma > 0)
                        {
                            master.SendLocalizedMessage(1153281); // Your possessed creature has gained karma!
                        }
                        else if (master != null && karma < 0)
                        {
                            master.SendLocalizedMessage(1153282); // Your possessed creature has lost karma!
                        }

                        if (power < topdam.MaxPower)
                        {
                            topdam.Progress += dc.Power;

                            if (topdam.Power > power && master != null)
                            {
                                master.SendLocalizedMessage(1153294, topdam.Name); // ~1_NAME~ has achieved a new threshold in power!
                            }
                        }
                        else
                        {
                            master?.SendLocalizedMessage(1153309); // Your controlled creature cannot gain further power.
                        }

                        if (oldAlign != newAlign && newAlign != Alignment.Neutral && topdam.MaxPower < 15)
                        {
                            topdam.MaxPower = 15;

                            master?.SendLocalizedMessage(1153293, topdam.Name); // ~1_NAME~ is growing in strength.

                            topdam.Delta(MobileDelta.Noto);

                            topdam.FixedEffect(0x373A, 10, 30);
                            topdam.PlaySound(0x209);
                        }

                        if (master?.Map != null && master.Map != Map.Internal && master.Backpack != null)
                        {
                            var heart = new PutridHeart(Utility.RandomMinMax(dc.Power * 8, dc.Power * 10));

                            if (!master.Backpack.TryDropItem(master, heart, false))
                            {
                                heart.MoveToWorld(master.Location, master.Map);
                            }
                        }
                    }
                }
            }
        }

        return base.OnBeforeDeath(m);
    }

    public override bool OnDoubleClick(Mobile m, object o)
    {
        if (o is BallOfSummoning || o is BraceletOfBinding)
        {
            return false;
        }

        if (o is Corpse c && m.AccessLevel == AccessLevel.Player)
        {
            if (c.Owner == null || c.Owner is DespiseCreature)
            {
                m.SendLocalizedMessage(1152684); // There is no loot on the corpse.
                return false;
            }
        }

        return base.OnDoubleClick(m, o);
    }

    public static void GetArmyPower(ref int good, ref int evil)
    {
        foreach (var orb in WispOrb.Orbs)
        {
            if (orb.Alignment == Alignment.Good)
            {
                good += orb.GetArmyPower();
            }
            else if (orb.Alignment == Alignment.Evil)
            {
                evil += orb.GetArmyPower();
            }
        }
    }

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;

        if (m.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        return travelType switch
        {
            TravelCheckType.RecallFrom   => true,
            TravelCheckType.RecallTo     => false,
            TravelCheckType.GateFrom     => false,
            TravelCheckType.GateTo       => false,
            TravelCheckType.Mark         => false,
            TravelCheckType.TeleportFrom => true,
            TravelCheckType.TeleportTo   => true,
            _                            => false
        };
    }

    public override void OnEnter(Mobile m)
    {
        if (m.AccessLevel > AccessLevel.Player)
        {
            return;
        }

        if (!IsInStartRegion(m.Location) && m is BaseCreature bc && m is not DespiseCreature && m is not CorruptedWisp &&
            m is not EnsorcledWisp && (bc.Controlled || bc.Summoned))
        {
            KickPet(bc);
        }

        if (m is PlayerMobile && IsInLowerRegion(m.Location))
        {
            var orb = DespiseController.GetWispOrb(m);

            if (orb == null)
            {
                Timer.DelayCall(TimeSpan.FromSeconds(1), () => Kick_Callback(m));
            }
        }
    }

    public override void OnExit(Mobile m)
    {
        base.OnExit(m);

        DespiseController.OnLeaveDespise(m);
    }

    public override void OnLocationChanged(Mobile m, Point3D oldLocation)
    {
        Timer.DelayCall(
            TimeSpan.FromSeconds(1.5),
            () =>
            {
                if (!IsInStartRegion(m.Location) && m is BaseCreature bc && m is not DespiseCreature && m is not CorruptedWisp &&
                    m is not EnsorcledWisp && (bc.Controlled || bc.Summoned))
                {
                    if (bc.Summoned)
                    {
                        m.Delete();
                    }
                    else
                    {
                        KickFromRegion(m, false);
                    }
                }
            }
        );

        base.OnLocationChanged(m, oldLocation);
    }

    private void KickPet(BaseCreature bc)
    {
        Timer.DelayCall(
            TimeSpan.FromSeconds(0.5),
            () =>
            {
                if (bc.Summoned)
                {
                    bc.Delete();
                }
                else
                {
                    KickFromRegion(bc, false);
                }

                bc.GetMaster()?.SendLocalizedMessage(bc.Summoned ? 1153193 : 1153192); // Your pet has been teleported outside the Despise dungeon entrance.
            }
        );
    }

    public void Kick_Callback(Mobile m)
    {
        if (m != null)
        {
            KickFromRegion(m, true);
            m.SendLocalizedMessage(1153347); // Without the presence of a Wisp Orb, strong magical forces send you back to whence you came...
        }
    }

    private void KickFromRegion(Mobile m, bool telepet)
    {
        while (true)
        {
            var x = Utility.RandomMinMax(_kickBounds.X, _kickBounds.X + _kickBounds.Width);
            var y = Utility.RandomMinMax(_kickBounds.Y, _kickBounds.Y + _kickBounds.Height);
            var z = Map.Trammel.GetAverageZ(x, y);
            var p = new Point3D(x, y, z);

            if (Map.CanSpawnMobile(p))
            {
                m.Corpse?.MoveToWorld(p, Map.Trammel);

                m.MoveToWorld(p, Map.Trammel);

                if (telepet)
                {
                    WispOrb.TeleportPet(m);
                }
                else
                {
                    var orb = DespiseController.GetWispOrb(m);

                    if (orb?.Pet != null)
                    {
                        orb.Pet.Kill();
                    }
                }

                break;
            }
        }
    }

    public override bool AllowHousing(Mobile from, Point3D p) => false;

    public override void AlterLightLevel(Mobile m, ref int global, ref int personal)
    {
        global = LightCycle.DungeonLevel;
    }
}

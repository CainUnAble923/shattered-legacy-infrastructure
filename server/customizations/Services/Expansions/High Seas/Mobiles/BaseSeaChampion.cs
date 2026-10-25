// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/BaseSeaChampion.cs -> ours, the High Seas fishing
// port. The base of Charybdis and Osiredon: a champion with no skull, no statues and empty artifact lists of its own,
// that tracks who damaged it (through pets to their masters) and, at death, gives one artifact of its lists to a damager
// chosen in proportion to damage done (ServUO's AwardArtifact), and that may push a boat it is fighting.
//
// Changes from ServUO (shard-migration notes/cc-P193-port-high-seas-fishing.md):
// - Boats: pinned boats have no hull points, so BaseBoat.OnTakenDamage does not exist and a sea champion cannot damage
//   a boat; the push (StartMove, one step away, drifting) is kept. Reverses when High Seas ship combat is ported.
// - OnHitByCannon(IShipCannon, int) is left out: there are no cannons.
// - BritannianShip (a galleon) is not here, so GetValidPoint's boat size is always the classic boat's 3.
// - ServUO's constructor caller passes the fisher; that field is not saved there either, so after a restart the champion
//   turns to its combatant, as in ServUO.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.Engines.CannedEvil;
using Server.Items;
using Server.Multis;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public abstract partial class BaseSeaChampion : BaseChampion
{
    private DateTime _nextBoatDamage;
    private bool _inDamageMode;
    private Mobile _fisher;
    private Dictionary<Mobile, int> _damageEntries;

    public BaseSeaChampion(Mobile fisher, AIType ai, FightMode fm) : base(ai, fm)
    {
        _nextBoatDamage = Core.Now;
        _inDamageMode = false;
        _fisher = fisher;
        _damageEntries = new Dictionary<Mobile, int>();
    }

    public override Type[] UniqueList => Array.Empty<Type>();
    public override Type[] SharedList => Array.Empty<Type>();
    public override Type[] DecorativeList => Array.Empty<Type>();
    public override MonsterStatuetteType[] StatueTypes => Array.Empty<MonsterStatuetteType>();
    public override ChampionSkullType SkullType => ChampionSkullType.None;

    public virtual bool CanDamageBoats => false;
    public virtual TimeSpan BoatDamageCooldown => TimeSpan.MaxValue;
    public virtual DateTime NextBoatDamage => _nextBoatDamage;
    public virtual int MinBoatDamage => 0;
    public virtual int MaxBoatDamage => 0;
    public virtual int DamageRange => 15;

    public override double BonusPetDamageScalar => 1.75;

    public Mobile Fisher => _fisher;

    public override void OnThink()
    {
        base.OnThink();

        if (_inDamageMode)
        {
            TryDamageBoat();
        }
        else if (CanDamageBoats && Core.Now >= NextBoatDamage)
        {
            _inDamageMode = true;
        }
    }

    public override bool OnBeforeDeath()
    {
        RegisterDamageTo(this);
        AwardArtifact(RollArtifact());

        return base.OnBeforeDeath();
    }

    /// <summary>
    /// cc-P269: the artifact roll at death. Pinned's BaseChampion.GetArtifact is not virtual, so a champion with its own
    /// odds (ServUO's GetArtifact override: Corgul) overrides this; the others keep pinned's 5% / 10% / 15%.
    /// </summary>
    public virtual Item RollArtifact() => GetArtifact();

    public virtual void RegisterDamageTo(Mobile m)
    {
        if (m == null)
        {
            return;
        }

        foreach (var de in m.DamageEntries)
        {
            var damager = de.Damager;

            if (damager == null)
            {
                continue;
            }

            var master = damager.GetDamageMaster(m);

            if (master != null)
            {
                damager = master;
            }

            RegisterDamage(damager, de.DamageGiven);
        }
    }

    public void RegisterDamage(Mobile from, int amount)
    {
        if (from?.Player != true)
        {
            return;
        }

        _damageEntries ??= new Dictionary<Mobile, int>();

        if (_damageEntries.TryGetValue(from, out var have))
        {
            _damageEntries[from] = have + amount;
        }
        else
        {
            _damageEntries.Add(from, amount);
        }
    }

    /// <summary>
    /// ServUO's shape, kept: the roll is made over the eligible damagers' total, then walked over every damager's
    /// damage, eligible or not, so an ineligible damager's share can land the artifact and it is then given to them (and
    /// deleted when their pack refuses it). Faithful, not tidy.
    /// </summary>
    public void AwardArtifact(Item artifact)
    {
        if (artifact == null)
        {
            return;
        }

        _damageEntries ??= new Dictionary<Mobile, int>();

        var totalDamage = 0;

        foreach (var kvp in _damageEntries)
        {
            if (IsEligible(kvp.Key, artifact))
            {
                totalDamage += kvp.Value;
            }
        }

        var randomDamage = Utility.RandomMinMax(1, totalDamage);

        totalDamage = 0;

        foreach (var kvp in _damageEntries)
        {
            totalDamage += kvp.Value;

            if (totalDamage > randomDamage)
            {
                GiveArtifact(kvp.Key, artifact);
                return;
            }
        }

        artifact.Delete();
    }

    public void GiveArtifact(Mobile to, Item artifact)
    {
        if (to == null || artifact == null)
        {
            return;
        }

        var pack = to.Backpack;

        if (pack == null || !pack.TryDropItem(to, artifact, false))
        {
            artifact.Delete();
        }
        else
        {
            to.SendLocalizedMessage(1062317); // For your valor in combating the fallen beast, a special artifact has been bestowed on you.
        }
    }

    public bool IsEligible(Mobile m, Item artifact) =>
        m.Player && m.Alive && m.InRange(Location, 32) && m.Backpack != null && m.Backpack.CheckHold(m, artifact, false);

    public void TryDamageBoat()
    {
        var focusMob = _fisher;

        if (focusMob?.Alive != true)
        {
            focusMob = Combatant;
        }

        if (focusMob == null || focusMob.AccessLevel > AccessLevel.Player || !InRange(focusMob.Location, DamageRange))
        {
            return;
        }

        var boat = BaseBoat.FindBoatAt(focusMob.Location, focusMob.Map);

        if (boat == null)
        {
            return;
        }

        var range = DamageRange;

        for (var x = X - range; x <= X + range; x++)
        {
            for (var y = Y - range; y <= Y + range; y++)
            {
                if (BaseBoat.FindBoatAt(new Point3D(x, y, Z), Map) == boat)
                {
                    DoDamageBoat(boat);
                    _nextBoatDamage = Core.Now + BoatDamageCooldown;
                    _inDamageMode = false;
                    return;
                }
            }
        }
    }

    /// <summary>ServUO: boat.OnTakenDamage(this, damage) then a one-step push away from the champion. Only the push here.</summary>
    public virtual void DoDamageBoat(BaseBoat boat)
    {
        for (var x = X - 2; x <= X + 2; x++)
        {
            for (var y = Y - 2; y <= Y + 2; y++)
            {
                var b = BaseBoat.FindBoatAt(new Point3D(x, y, Z), Map);

                if (b != null && boat == b)
                {
                    var toPush = Direction.North;

                    if (X < x && x - X > 1)
                    {
                        toPush = Direction.West;
                    }
                    else if (X > x && X - x > 1)
                    {
                        toPush = Direction.East;
                    }
                    else if (Y < y)
                    {
                        toPush = Direction.South;
                    }
                    else if (Y > y)
                    {
                        toPush = Direction.North;
                    }

                    PushBoat(boat, toPush);
                }
            }
        }
    }

    /// <summary>One drifting step (ServUO: boat.StartMove(dir, 1, 0x2, boat.SlowDriftInterval, true, false)).</summary>
    public static void PushBoat(BaseBoat boat, Direction dir) =>
        boat.StartMove(dir, 1, 0x2, TimeSpan.FromSeconds(0.5), true, false);

    public Point3D GetValidPoint(BaseBoat boat, Map map, int distance)
    {
        if (boat == null || map == null || map == Map.Internal)
        {
            return new Point3D(X + Utility.RandomMinMax(-1, 1), Y + Utility.RandomMinMax(-1, 1), Z);
        }

        distance = Math.Clamp(distance, 5, 15);

        var x = boat.X;
        var y = boat.Y;
        int z;
        const int size = 3;

        var range = Math.Max(1, distance - size);

        switch (boat.Facing)
        {
            default:
            case Direction.South:
            case Direction.North:
                x = Utility.RandomBool()
                    ? Utility.RandomMinMax(x - distance, x - (distance - range))
                    : Utility.RandomMinMax(x + (distance - range), x + distance);
                y = Utility.RandomMinMax(y - 8, y + 8);
                z = map.GetAverageZ(x, y);
                break;
            case Direction.East:
            case Direction.West:
                x = Utility.RandomMinMax(x - 8, x + 8);
                y = Utility.RandomBool()
                    ? Utility.RandomMinMax(y - distance, y - (distance - range))
                    : Utility.RandomMinMax(y + (distance - range), y + distance);
                z = map.GetAverageZ(x, y);
                break;
        }

        return new Point3D(x, y, z);
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        _nextBoatDamage = Core.Now;
        _damageEntries = new Dictionary<Mobile, int>();
    }
}

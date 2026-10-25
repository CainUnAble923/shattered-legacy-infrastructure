// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/Charybdis.cs -> ours, the High Seas fishing port.
//
// Charybdis (ServUO's type is spelt Charydbis, kept), the sea champion the Oracle of the Sea finds: 30,000 hits, a mage,
// raises up to 25 giant tentacles around its targets, teleports every minute (hiding, striking everyone on the target's
// boat with lightning, reappearing near them), and every 45 to 80 seconds sends a wave along a line toward its target's
// boat that pushes the boat. Drops the Fisherman's set (unique), the Helm of Vengeance, Ring of the Soulbinder, Rune
// Engraved Peg Leg and Culling Blade (shared), enchanted blade and vortex deeds (decorative), fish steaks, a message in a
// bottle, four special fishing nets and a fishing pole.
//
// Who spawns it: ServUO's CharydbisSpawner, through the Oracle of the Sea and a charybdis-baited pole (cc-P269:
// Spawners/CharybdisSpawner.cs, HighSeasFishing.TrySummonCharybdis); staff may also place one. OnBeforeDeath tells the
// spawner when its own Charybdis dies.
//
// Changes from ServUO (shard-migration notes/cc-P193-port-high-seas-fishing.md):
// - The wave damages no boat (pinned boats have no hull points, BaseSeaChampion); it still pushes it.
// - The wave's water spouts (EffectSpawn) read the land tile at the path offset (x, y) in ServUO, not at the world point,
//   so they never appeared; here the world point is read. Fixed, not copied: a bug that made OSI's visible attack silent.
// - SpawnTentacle rolled x from (loc.X - 10, loc.Y + 10) and y the same way in ServUO; here each axis uses its own
//   bounds, as the surrounding code plainly intends.
// - The corpse's fishing pole is a plain one: pinned's pole carries no magic attributes (ServUO gives it two to five).
//   (cc-P269: the two to five fish pies are in the corpse now.)
// - The tentacle list is not saved on the champion; each tentacle saves its master and re-registers itself on load, which
//   gives the same list without a serialized collection.
// - EffectSpawn is a top-level type here (CharydbisEffectSpawn), not a nested class, for the serialization generator.
// - IsSoulbound (ServUO's Treasures event flag) and SkillMasteryPrimer (Time of Legends masteries) are not in pinned.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Multis;
using Server.Spells;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Charydbis : BaseSeaChampion
{
    public static readonly TimeSpan SpawnRate = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan TeleportRate = TimeSpan.FromSeconds(60);
    public static readonly int SpawnMax = 25;

    private readonly List<Mobile> _tentacles = new();
    private DateTime _nextSpawn;
    private DateTime _nextTeleport;
    private Point3D _lastLocation;
    private Map _lastMap;
    private bool _hasPushed;

    [Constructible]
    public Charydbis() : this(null)
    {
    }

    public Charydbis(Mobile fisher) : base(fisher, AIType.AI_Mage, FightMode.Closest)
    {
        RangeFight = 8;

        Body = 1244;
        BaseSoundID = 353;

        _nextSpawn = Core.Now + SpawnRate;
        _nextTeleport = Core.Now + TeleportRate;

        CanSwim = true;
        CantWalk = true;

        SetStr(533, 586);
        SetDex(113, 131);
        SetInt(110, 155);

        SetHits(30000);
        SetMana(8000);
        SetDamage(24, 33);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Energy, 50);

        SetResistance(ResistanceType.Physical, 70, 80);
        SetResistance(ResistanceType.Fire, 70, 80);
        SetResistance(ResistanceType.Cold, 45, 55);
        SetResistance(ResistanceType.Poison, 80, 90);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.Wrestling, 120.1, 121.2);
        SetSkill(SkillName.Tactics, 120.15, 123.1);
        SetSkill(SkillName.MagicResist, 165.2, 178.7);
        SetSkill(SkillName.Anatomy, 111.0, 111.7);
        SetSkill(SkillName.Magery, 134.6, 140.5);
        SetSkill(SkillName.EvalInt, 200.8, 243.6);
        SetSkill(SkillName.Meditation, 565);

        Fame = 32000;
        Karma = -32000;
    }

    public override string DefaultName => "charydbis";

    public override bool CanDamageBoats => true;
    public override TimeSpan BoatDamageCooldown => TimeSpan.FromSeconds(Utility.RandomMinMax(45, 80));
    public override int MinBoatDamage => 5;
    public override int MaxBoatDamage => 15;
    public override int DamageRange => 10;

    public override int Meat => 5;
    // ServUO also sets TreasureMapChance .50; pinned BaseCreature has no such member (its map drop is pinned's own rule), as
    // CC4 recorded for Shame's creatures. The level is kept.
    public override int TreasureMapLevel => 7;

    public override Type[] UniqueList =>
        new[] { typeof(FishermansHat), typeof(FishermansVest), typeof(FishermansEelskinGloves), typeof(FishermansTrousers) };

    public override Type[] SharedList =>
        new[] { typeof(HelmOfVengence), typeof(RingOfTheSoulbinder), typeof(RuneEngravedPegLeg), typeof(CullingBlade) };

    public override Type[] DecorativeList => new[] { typeof(EnchantedBladeDeed), typeof(EnchantedVortexDeed) };

    [CommandProperty(AccessLevel.GameMaster)]
    public int Tentacles => _tentacles.Count;

    public void AddTentacle(Mobile tent)
    {
        if (!_tentacles.Contains(tent))
        {
            _tentacles.Add(tent);
        }
    }

    public void RemoveTentacle(Mobile tent) => _tentacles.Remove(tent);

    public override void OnThink()
    {
        base.OnThink();

        if (_nextSpawn < Core.Now && _tentacles.Count < SpawnMax)
        {
            SpawnTentacle();
        }

        if (_nextTeleport < Core.Now)
        {
            DoTeleport();
        }
    }

    public void DoTeleport()
    {
        var combatant = Combatant;

        if (combatant == null)
        {
            _nextTeleport = Core.Now + TeleportRate;
            return;
        }

        _lastLocation = Location;
        _lastMap = Map;
        DoTeleportEffects(_lastLocation, _lastMap);

        Hidden = true;
        Internalize();

        DoAreaLightningAttack(combatant);

        Timer.DelayCall(TimeSpan.FromSeconds(3), () => FinishTeleport(combatant));
        _nextTeleport = Core.Now + TeleportRate;
    }

    public void FinishTeleport(Mobile combatant)
    {
        if (Deleted)
        {
            return;
        }

        var focusLoc = combatant?.Map == null ? _lastLocation : combatant.Location;

        var map = _lastMap;

        if (map == null || map == Map.Internal)
        {
            return;
        }

        var newLoc = Point3D.Zero;
        var boat = BaseBoat.FindBoatAt(focusLoc, map);

        for (var i = 0; i < 25; i++)
        {
            if (boat != null)
            {
                newLoc = GetValidPoint(boat, map, 10);
            }
            else
            {
                var x = focusLoc.X + Utility.RandomMinMax(-12, 12);
                var y = focusLoc.Y + Utility.RandomMinMax(-12, 12);
                var z = map.GetAverageZ(x, y);

                newLoc = new Point3D(x, y, z);
            }

            var t = map.Tiles.GetLandTile(newLoc.X, newLoc.Y);

            if (!SpellHelper.CheckMulti(newLoc, map) && IsSeaTile(t))
            {
                break;
            }
        }

        if (newLoc == Point3D.Zero || _lastLocation.GetDistanceToSqrt(newLoc) > 15)
        {
            newLoc = _lastLocation;
        }

        DoTeleportEffects(newLoc, map);
        Hidden = false;

        var pnt = newLoc;
        Timer.DelayCall(TimeSpan.FromSeconds(.5), () => TimedMoveToWorld(pnt, map, combatant));
    }

    public void TimedMoveToWorld(Point3D pnt, Map map, Mobile focus)
    {
        if (Deleted)
        {
            return;
        }

        MoveToWorld(pnt, map);
        Combatant = focus;

        DoAreaLightningAttack(focus);
    }

    /// <summary>Everyone on the target's boat: a bolt and 50 to 100 energy damage, scaled by this champion's remaining hits (at least 40).</summary>
    public void DoAreaLightningAttack(Mobile focus)
    {
        if (focus?.Map == null)
        {
            return;
        }

        var boat = BaseBoat.FindBoatAt(focus.Location, focus.Map);

        if (boat == null)
        {
            return;
        }

        var aboard = new List<Mobile>();

        foreach (var ent in boat.GetMovingEntities())
        {
            if (ent is Mobile m && m.Alive && CanBeHarmful(m, false))
            {
                aboard.Add(m);
            }
        }

        foreach (var mob in aboard)
        {
            var damage = Math.Max(40, Utility.RandomMinMax(50, 100) * ((double)Hits / HitsMax));

            mob.BoltEffect(0);
            AOS.Damage(mob, this, (int)damage, false, 0, 0, 0, 0, 0, 0, 100);
            mob.FixedParticles(0x36BD, 20, 10, 5044, EffectLayer.Head);
        }
    }

    public void DoTeleportEffects(Point3D p, Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return;
        }

        for (var x = -2; x <= 2; x++)
        {
            for (var y = -2; y <= 2; y++)
            {
                if (Math.Abs(x) == 2 && Math.Abs(y) == 2)
                {
                    continue;
                }

                var pnt = new Point3D(p.X + x, p.Y + y, map.GetAverageZ(p.X + x, p.Y + y));
                Effects.SendLocationEffect(pnt, map, 0x3728, 16, 4);
            }
        }

        Effects.PlaySound(p, map, 0x025);
        Effects.PlaySound(p, map, 0x026);
        Effects.PlaySound(p, map, 0x027);
    }

    /// <summary>The wave: a line of DamageRange points from this champion toward the boat (or the combatant aboard it), walked by EffectsTimer.</summary>
    public override void DoDamageBoat(BaseBoat boat)
    {
        if (boat == null)
        {
            return;
        }

        _hasPushed = false;

        var tx = boat.X;
        var ty = boat.Y;

        var combatant = Combatant;

        if (combatant != null && boat.Contains(combatant.X, combatant.Y))
        {
            tx = combatant.X;
            ty = combatant.Y;
        }

        var dir = Utility.GetDirection(X, Y, tx, ty);
        var path = new Point3DList();

        for (var i = 0; i < DamageRange; i++)
        {
            int x = 0, y = 0;

            switch (dir & Direction.Mask)
            {
                case Direction.North: y -= i; break;
                case Direction.Right: y -= i; x += i; break;
                case Direction.East: x += i; break;
                case Direction.Down: x += i; y += i; break;
                case Direction.South: y += i; break;
                case Direction.Left: y += i; x -= i; break;
                case Direction.West: x -= i; break;
                case Direction.Up: x -= i; y -= i; break;
            }

            path.Add(X + x, Y + y, Z);
        }

        new EffectsTimer(this, path, dir & Direction.Mask, DamageRange).Start();
    }

    public void OnTick(Point3DList path, Direction dir, int i)
    {
        if (Deleted || Map == null || Map == Map.Internal || path.Count <= i)
        {
            return;
        }

        var point = path[i];
        var o = i - 1;

        Effects.PlaySound(point, Map, 278);
        Effects.PlaySound(point, Map, 279);

        for (var rn = 0; rn < o * 2 + 1; rn++)
        {
            int y = 0, x = 0, y2 = 0, x2 = 0;
            var diag = false;

            switch (dir)
            {
                case Direction.North: x = x - o + rn; break;
                case Direction.Right: x = x - o + rn; y = y - o + rn; break;
                case Direction.East: y = y - o + rn; break;
                case Direction.Down: y = y - o + rn; x = x + o - rn; break;
                case Direction.South: x = x + o - rn; break;
                case Direction.Left: x = x + o - rn; y = y + o - rn; break;
                case Direction.West: y = y + o - rn; break;
                case Direction.Up: y = y + o - rn; x = x - o + rn; break;
            }

            switch (dir)
            {
                case Direction.Right: y2++; diag = true; break;
                case Direction.Down: x2--; diag = true; break;
                case Direction.Left: y2--; diag = true; break;
                case Direction.Up: x2++; diag = true; break;
            }

            var ep = new Point3D(point.X + x, point.Y + y, point.Z);
            var ep2 = new Point3D(ep.X + x2, ep.Y + y2, ep.Z);

            if (diag && i >= 2 * path.Count / 3)
            {
                return;
            }

            var p = diag && rn < o * 2 ? ep2 : ep;

            if (SpellHelper.CheckMulti(p, Map))
            {
                var boat = BaseBoat.FindBoatAt(p, Map);

                if (boat != null && !_hasPushed)
                {
                    PushBoat(boat, dir);
                    _hasPushed = true;
                }

                continue;
            }

            var t = Map.Tiles.GetLandTile(p.X, p.Y);

            if (IsSeaTile(t))
            {
                var spawn = new CharydbisEffectSpawn();
                spawn.MoveToWorld(p, Map);
            }
        }
    }

    private class EffectsTimer : Timer
    {
        private readonly Direction _dir;
        private int _i;
        private readonly int _iMax;
        private readonly Point3DList _path;
        private readonly Charydbis _mobile;

        public EffectsTimer(Charydbis mobile, Point3DList path, Direction dir, int imax)
            : base(TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(0.25))
        {
            _dir = dir;
            _i = 1;
            _iMax = imax;
            _path = path;
            _mobile = mobile;
        }

        protected override void OnTick()
        {
            _mobile.OnTick(_path, _dir, _i);

            if (_i >= _iMax || _mobile.Deleted)
            {
                Stop();
                return;
            }

            _i++;
        }
    }

    public void SpawnTentacle()
    {
        if (Combatant == null)
        {
            _nextSpawn = Core.Now + SpawnRate;
            return;
        }

        var map = Map;

        if (map == null || map == Map.Internal)
        {
            return;
        }

        var list = new List<Mobile>();

        foreach (var m in GetMobilesInRange(15))
        {
            if (m == this || !CanBeHarmful(m))
            {
                continue;
            }

            if (m is BaseCreature bc && (bc.Controlled || bc.Summoned || bc.Team != Team))
            {
                list.Add(m);
            }
            else if (m.Player)
            {
                list.Add(m);
            }
        }

        if (list.Count > 0)
        {
            var spawn = list.RandomElement();
            var boat = BaseBoat.FindBoatAt(spawn.Location, map);
            var loc = spawn.Location;

            for (var i = 0; i < 25; i++)
            {
                Point3D spawnLoc;

                if (boat != null)
                {
                    spawnLoc = GetValidPoint(boat, map, 4);
                }
                else
                {
                    var x = Utility.RandomMinMax(loc.X - 10, loc.X + 10);
                    var y = Utility.RandomMinMax(loc.Y - 10, loc.Y + 10);
                    var z = map.GetAverageZ(x, y);

                    spawnLoc = new Point3D(x, y, z);
                }

                if (SpellHelper.CheckMulti(spawnLoc, map))
                {
                    continue;
                }

                var t = map.Tiles.GetLandTile(spawnLoc.X, spawnLoc.Y);

                if (IsSeaTile(t) && spawnLoc != Point3D.Zero)
                {
                    var tent = new GiantTentacle(this);

                    tent.MoveToWorld(spawnLoc, map);
                    tent.Home = tent.Location;
                    tent.RangeHome = 15;
                    tent.Team = Team;

                    if (spawn != this)
                    {
                        tent.Combatant = spawn;
                    }

                    break;
                }
            }
        }

        _nextSpawn = Core.Now + SpawnRate;
    }

    public static bool IsSeaTile(LandTile t) =>
        t.Z == -5 && (t.ID >= 0xA8 && t.ID <= 0xAB || t.ID >= 0x136 && t.ID <= 0x137);

    public override bool OnBeforeDeath()
    {
        if (Map == Map.Internal && _lastMap != null)
        {
            MoveToWorld(_lastLocation, _lastMap);
        }

        if (CharydbisSpawner.SpawnInstance.Charydbis == this)
        {
            CharydbisSpawner.SpawnInstance.OnCharybdisKilled();
        }

        return base.OnBeforeDeath();
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        var drop = Utility.RandomMinMax(2, 5);

        for (var i = 0; i < drop; i++)
        {
            var pie = Loot.Construct(Pies.RandomElement());

            if (pie != null)
            {
                c.DropItem(pie);
            }
        }

        drop = Utility.RandomMinMax(2, 5);

        for (var i = 0; i < drop; i++)
        {
            var steak = Loot.Construct(Steaks.RandomElement());

            if (steak != null)
            {
                c.DropItem(steak);
            }
        }

        c.DropItem(new MessageInABottle(c.Map));
        c.DropItem(new SpecialFishingNet());
        c.DropItem(new SpecialFishingNet());
        c.DropItem(new SpecialFishingNet());
        c.DropItem(new SpecialFishingNet());

        c.DropItem(new FishingPole());
    }

    public override void Delete()
    {
        var tents = new List<Mobile>(_tentacles);

        foreach (var tent in tents)
        {
            tent?.Kill();
        }

        base.Delete();
    }

    /// <summary>ServUO's pie table (cc-P269): the sixteen fish pies the corpse holds two to five of.</summary>
    public static readonly Type[] Pies =
    {
        typeof(AutumnDragonfishPie), typeof(BlueLobsterPie), typeof(BullFishPie), typeof(CrystalFishPie),
        typeof(FairySalmonPie), typeof(FireFishPie), typeof(GiantKoiPie), typeof(GreatBarracudaPie),
        typeof(HolyMackerelPie), typeof(LavaFishPie), typeof(ReaperFishPie), typeof(SpiderCrabPie),
        typeof(StoneCrabPie), typeof(SummerDragonfishPie), typeof(UnicornFishPie), typeof(YellowtailBarracudaPie)
    };

    /// <summary>ServUO's steak table: the sixteen rare fish steaks and meats the corpse holds two to five of.</summary>
    public static readonly Type[] Steaks =
    {
        typeof(AutumnDragonfishSteak), typeof(BlueLobsterMeat), typeof(BullFishSteak), typeof(CrystalFishSteak),
        typeof(FairySalmonSteak), typeof(FireFishSteak), typeof(GiantKoiSteak), typeof(GreatBarracudaSteak),
        typeof(HolyMackerelSteak), typeof(LavaFishSteak), typeof(ReaperFishSteak), typeof(SpiderCrabMeat),
        typeof(StoneCrabMeat), typeof(SummerDragonfishSteak), typeof(UnicornFishSteak), typeof(YellowtailBarracudaSteak)
    };

    public override void GenerateLoot()
    {
        AddLoot(LootPack.SuperBoss, 8);
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        _nextSpawn = Core.Now;
        _nextTeleport = Core.Now + TeleportRate;
    }
}

/// <summary>The water spout of Charybdis's wave: a frozen, unkillable water elemental body that dies and vanishes after two seconds.</summary>
[SerializationGenerator(0, false)]
public partial class CharydbisEffectSpawn : BaseCreature
{
    public CharydbisEffectSpawn() : base(AIType.AI_Vendor)
    {
        Body = 16;
        BaseSoundID = 278;
        CantWalk = true;
        CanSwim = false;
        Frozen = true;

        SetHits(150000);

        SetResistance(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Fire, 100);
        SetResistance(ResistanceType.Cold, 100);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 100);

        Timer.DelayCall(TimeSpan.FromSeconds(2), DoDelete);
    }

    public override bool DeleteCorpseOnDeath => true;

    public void DoDelete()
    {
        if (!Deleted && Alive)
        {
            Kill();
        }
    }

    public override void OnDelete()
    {
        if (Map != null && Map != Map.Internal)
        {
            Effects.SendLocationEffect(Location, Map, 0x352D, 16, 4);
            Effects.PlaySound(Location, Map, 0x364);
            Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x3728, 1, 14, 0, 7, 9915, 0);
        }

        base.OnDelete();
    }

    public override bool AutoDispel => true;
    public override double AutoDispelChance => 1.0;
    public override bool BardImmune => true;
    public override bool Unprovokable => true;
    public override bool Uncalmable => true;
    public override Poison PoisonImmune => Poison.Lethal;

    public override int Meat => 1;

    [AfterDeserialization(false)]
    private void AfterDeserialization() => Timer.DelayCall(TimeSpan.FromSeconds(2), DoDelete);
}

[SerializationGenerator(0, false)]
public partial class GiantTentacle : BaseCreature
{
    [SerializableField(0, setter: "private")]
    private Mobile _master;

    [Constructible]
    public GiantTentacle() : this(null)
    {
    }

    public GiantTentacle(Mobile master) : base(AIType.AI_Mage, FightMode.Closest)
    {
        if (master is Charydbis charybdis)
        {
            _master = master;
            charybdis.AddTentacle(this);
        }

        Body = 1245;
        BaseSoundID = 0x161;

        CanSwim = true;

        SetStr(127, 155);
        SetDex(66, 85);
        SetInt(102, 123);

        SetHits(105, 113);

        SetDamage(10, 15);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Cold, 50);

        SetResistance(ResistanceType.Physical, 35, 45);
        SetResistance(ResistanceType.Fire, 10, 25);
        SetResistance(ResistanceType.Cold, 10, 25);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 5, 10);

        SetSkill(SkillName.Wrestling, 52.0, 70.0);
        SetSkill(SkillName.Tactics, 0.0);
        SetSkill(SkillName.MagicResist, 100.4, 113.5);
        SetSkill(SkillName.Anatomy, 1.0, 0.0);
        SetSkill(SkillName.Magery, 60.2, 72.4);
        SetSkill(SkillName.EvalInt, 60.1, 73.4);
        SetSkill(SkillName.Meditation, 100.0);

        Fame = 2500;
        Karma = -2500;
    }

    public override string DefaultName => "a giant tentacle";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 1);
    }

    public override void Delete()
    {
        if (_master is Charydbis charybdis)
        {
            charybdis.RemoveTentacle(this);
        }

        base.Delete();
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        if (_master is Charydbis charybdis && !charybdis.Deleted)
        {
            charybdis.AddTentacle(this);
        }
    }
}

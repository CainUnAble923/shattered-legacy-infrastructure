// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/Corgul/CorgulTheSoulbinder.cs -> ours, High Seas,
// second half. Corgul the Soulbinder, the sea champion of the hidden island (Trammel and Felucca, around 6431,1236):
// 65,000 hits, a necromancer-mage that pulls a player (or a player's pet) to himself every 10 to 50 seconds, blasts
// everyone within 12 tiles with a dismounting physical hit (60% of their hits, 10 to 75; two minutes between), sends a
// ring of fire out to 18 tiles (100 to 150 fire, half the time a bleed; one to three minutes between), and is pulled
// back to the island's heart when he strays more than 15 tiles while on the island. SpawnHelpers raises his crew: 15
// soulbound at each of the island's five landing spots; when all of them are dead he raises them once more. Loot: one
// of his own artifacts (the enchanted sash, the handbook on the undead) to one of the top five damagers; then 8% (12%
// in Felucca) a shared artifact (Helm of Vengeance, Ring of the Soulbinder, Rune Engraved Peg Leg, Culling Blade) and
// 25% (40% in Felucca) an enchanted blade or vortex deed to a damager; a message in a bottle and two special fishing
// nets in the corpse; his crew die with him.
//
// How players reach him in ServUO: two sacrifices at Corgul's altar (a treasure map, then a world map, 2453,865) mark a
// warp spot at sea on an island map; a boat carrying that map sails into the spot and is moved to the island; three
// hours to kill him. That route (CorgulAltar, CorgulIslandMap, CorgulRegion, CorgulWarpRegion) moves boats from inside
// BaseBoat's movement and is left for the boat port (shard-migration notes/cc-P268-high-seas-ships-assessment.md);
// until then staff place him on the island ([add CorgulTheSoulBinder, then SpawnCrew true in his properties).
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - AI_NecroMage is stock MageAI (AGENTS.md: a known equivalent), so AIType.AI_Mage; the necromancy skills are set.
// - Corgul's Handbook on Mysticism is not in the unique list: the shard has no Mysticism spellbook (MysticBook).
// - TaintedLifeAura, TreasureMapChance, IsSoulbound and the Treasures event's ethereal sand shower have no pinned
//   equivalent and are left out, as for Osiredon (D-139).
// - ServUO's TeleportsTo is a BaseCreature feature pinned lacks; its TryTeleport is ported into OnThink here.
// - The fire ring's bleed starts pinned's bleed directly (BleedAttack.BeginBleed); ServUO ran the weapon ability's
//   OnHit, whose weapon and mana checks a creature without a weapon ability does not meet the same way.
// - "On the island" is the island's rectangle (CorgulAltar.CorgulBounds in ServUO), not its CorgulRegion, which waits
//   with the altar.
// - Pinned's BaseChampion gives champion power scrolls to the damagers when a champion dies in Felucca; ServUO's Corgul
//   gives none (CanGivePowerscrolls false). Pinned has no switch for it, so here he does, as Charybdis and Osiredon do.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class CorgulTheSoulBinder : BaseSeaChampion
{
    public static readonly Point3D SpawnLoc = new(6431, 1236, 10);
    public static readonly Rectangle2D IslandBounds = new(6337, 1156, 186, 186);

    private const int SpawnPerLoc = 15;
    private const int TeleportRange = 16;

    private static readonly Point3D[] _spawnLocs =
    {
        new(6447, 1262, 10),
        new(6424, 1279, 10),
        new(6406, 1250, 10),
        new(6423, 1220, 10),
        new(6461, 1237, 10)
    };

    private static readonly int[] _offsets = { -1, -1, -1, 0, -1, 1, 0, -1, 0, 1, 1, -1, 1, 0, 1, 1 };

    [SerializableField(0)]
    private bool _hasDone2ndSpawn;

    // The crew SpawnHelpers raised (the generated Helpers property, read by staff and the tests).
    [SerializableField(1, setter: "private")]
    private List<BaseCreature> _helpers = new();

    private DateTime _nextDismount;
    private DateTime _nextArea;
    private DateTime _nextReturn;
    private DateTime _nextTeleport;

    [Constructible]
    public CorgulTheSoulBinder() : base(null, AIType.AI_Mage, FightMode.Closest)
    {
        Name = "Corgul the Soulbinder";
        BaseSoundID = 609;
        Body = 0x4C;
        Hue = 2076;

        _nextDismount = Core.Now;
        _nextArea = Core.Now;

        SetStr(800, 900);
        SetDex(121, 165);
        SetInt(300, 400);

        SetMana(4500);
        SetHits(65000);

        SetDamage(19, 24);

        SetDamageType(ResistanceType.Physical, 10);
        SetDamageType(ResistanceType.Fire, 10);
        SetDamageType(ResistanceType.Cold, 30);
        SetDamageType(ResistanceType.Poison, 40);
        SetDamageType(ResistanceType.Energy, 10);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 80, 90);
        SetResistance(ResistanceType.Cold, 85, 95);
        SetResistance(ResistanceType.Poison, 80, 90);
        SetResistance(ResistanceType.Energy, 80, 90);

        SetSkill(SkillName.Wrestling, 110.0, 120.0);
        SetSkill(SkillName.Tactics, 110.0, 120.0);
        SetSkill(SkillName.Magery, 110.9, 120.0);
        SetSkill(SkillName.EvalInt, 110.9, 120.0);
        SetSkill(SkillName.Meditation, 110.9, 120.0);
        SetSkill(SkillName.Necromancy, 110.9, 120.0);
        SetSkill(SkillName.SpiritSpeak, 110.9, 120.0);

        Fame = 25000;
        Karma = -25000;

        _nextReturn = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(120, 180));
    }

    public override int Meat => 5;
    public override int TreasureMapLevel => 7;
    public override Poison PoisonImmune => Poison.Deadly;
    public override bool NoGoodies => true;

    public override Type[] UniqueList => new[] { typeof(CorgulsEnchantedSash), typeof(CorgulsHandbookOnTheUndead) };

    public override Type[] SharedList =>
        new[] { typeof(HelmOfVengence), typeof(RingOfTheSoulbinder), typeof(RuneEngravedPegLeg), typeof(CullingBlade) };

    public override Type[] DecorativeList => new[] { typeof(EnchantedBladeDeed), typeof(EnchantedVortexDeed) };

    public double SharedChance => Map?.Rules == MapRules.FeluccaRules ? .12 : .08;
    public double DecorativeChance => Map?.Rules == MapRules.FeluccaRules ? .40 : .25;

    public override bool OnBeforeDeath()
    {
        // One of his own artifacts to a random one of the top five damagers (ServUO's sorted looting rights).
        var rights = GetLootingRights(DamageEntries, HitsMax);

        if (rights.Count > 0)
        {
            rights.Sort();

            var winner = rights[Utility.Random(Math.Min(5, rights.Count))].m_Mobile;

            if (winner != null)
            {
                GiveArtifact(winner, CreateArtifact(UniqueList));
            }
        }

        return base.OnBeforeDeath();
    }

    public override Item RollArtifact()
    {
        var random = Utility.RandomDouble();

        if (SharedChance >= random)
        {
            return CreateArtifact(SharedList);
        }

        return DecorativeChance >= random ? CreateArtifact(DecorativeList) : null;
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool SpawnCrew
    {
        get => _helpers.Count > 0;
        set
        {
            if (value)
            {
                SpawnHelpers();
            }
        }
    }

    public void SpawnHelpers()
    {
        foreach (var pnt in _spawnLocs)
        {
            for (var i = 0; i < SpawnPerLoc; i++)
            {
                BaseCreature bc = Utility.Random(7) switch
                {
                    1 => new SoulboundApprenticeMage(),
                    2 => new SoulboundBattleMage(),
                    3 => new SoulboundPirateCaptain(),
                    4 => new SoulboundPirateRaider(),
                    5 => new SoulboundSpellSlinger(),
                    6 => new SoulboundSwashbuckler(),
                    _ => new BoundSoul()
                };

                AddToHelpers(bc);
                SpawnMobile(bc, pnt);
            }
        }
    }

    public void SpawnMobile(BaseCreature bc, Point3D p)
    {
        if (Map == null || bc == null)
        {
            bc?.Delete();
            return;
        }

        for (var i = 0; i < 25; i++)
        {
            var x = Utility.RandomMinMax(p.X - 4, p.X + 4);
            var y = Utility.RandomMinMax(p.Y - 4, p.Y + 4);
            var z = Map.GetAverageZ(x, y);

            if (Map.CanSpawnMobile(x, y, z))
            {
                p = new Point3D(x, y, z);
                break;
            }
        }

        bc.MoveToWorld(p, Map);
        bc.Home = p;
        bc.RangeHome = 5;
    }

    public override void OnThink()
    {
        base.OnThink();

        if (_nextReturn < Core.Now)
        {
            if (Map != null && IslandBounds.Contains(Location) && !Utility.InRange(Location, SpawnLoc, 15))
            {
                PlaySound(0x1FE);
                FixedParticles(0x376A, 9, 32, 0x13AF, EffectLayer.Waist);

                Location = SpawnLoc;
                ProcessDelta();

                PlaySound(0x1FE);
                FixedParticles(0x376A, 9, 32, 0x13AF, EffectLayer.Waist);

                _nextReturn = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(120, 180));
            }
        }

        if (Combatant == null)
        {
            return;
        }

        // ServUO BaseCreature.OnThink's TeleportsTo: every 10 to 50 seconds, always.
        if (Core.Now >= _nextTeleport)
        {
            TryTeleport();
            _nextTeleport = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(10, 50));
        }

        if (Core.Now > _nextDismount && Utility.RandomDouble() < 0.1)
        {
            DoDismount();
        }
        else if (Core.Now > _nextArea && Utility.RandomDouble() < 0.1)
        {
            DoAreaAttack();
        }

        if (!_hasDone2ndSpawn && _helpers.Count > 0 && !_helpers.Exists(bc => bc is { Alive: true, Deleted: false }))
        {
            Timer.StartTimer(TimeSpan.FromSeconds(5), SpawnHelpers);
            HasDone2ndSpawn = true;
        }
    }

    /// <summary>ServUO BaseCreature.TryTeleport: pull a player (or a player's pet) to a tile beside him.</summary>
    public void TryTeleport()
    {
        if (Deleted || Map == null)
        {
            return;
        }

        var toTeleport = GetTeleportTarget();

        if (toTeleport == null)
        {
            return;
        }

        var offset = Utility.Random(8) * 2;
        var to = Location;

        for (var i = 0; i < _offsets.Length; i += 2)
        {
            var x = X + _offsets[(offset + i) % _offsets.Length];
            var y = Y + _offsets[(offset + i + 1) % _offsets.Length];

            if (Map.CanSpawnMobile(x, y, Z))
            {
                to = new Point3D(x, y, Z);
                break;
            }

            var z = Map.GetAverageZ(x, y);

            if (Map.CanSpawnMobile(x, y, z))
            {
                to = new Point3D(x, y, z);
                break;
            }
        }

        var from = toTeleport.Location;
        toTeleport.MoveToWorld(to, Map);

        Spells.SpellHelper.Turn(this, toTeleport);
        Spells.SpellHelper.Turn(toTeleport, this);

        toTeleport.ProcessDelta();

        Effects.SendLocationParticles(EffectItem.Create(from, Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
        Effects.SendLocationParticles(EffectItem.Create(to, Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);

        toTeleport.PlaySound(0x1FE);

        Combatant = toTeleport;
    }

    private Mobile GetTeleportTarget()
    {
        var list = new List<Mobile>();

        foreach (var m in GetMobilesInRange(TeleportRange))
        {
            var isPet = m is BaseCreature bc && bc.GetMaster() is PlayerMobile;

            if (m != this && (m.Player || isPet) && CanBeHarmful(m) && CanSee(m))
            {
                list.Add(m);
            }
        }

        return list.Count > 0 ? list.RandomElement() : null;
    }

    public void DoDismount()
    {
        var targets = new List<Mobile>();

        foreach (var mob in GetMobilesInRange(12))
        {
            if (!CanBeHarmful(mob) || mob == this)
            {
                continue;
            }

            if (mob is BaseCreature bc && (bc.Controlled || bc.Summoned || bc.Team != Team))
            {
                targets.Add(mob);
            }
            else if (mob is PlayerMobile && mob.Alive)
            {
                targets.Add(mob);
            }
        }

        PlaySound(0x2F3);

        foreach (var m in targets)
        {
            if (m is PlayerMobile pm && (pm.Mounted || pm.Flying))
            {
                pm.SetMountBlock(BlockMountType.DismountRecovery, TimeSpan.FromSeconds(10), true);
            }

            var damage = Math.Clamp(m.Hits * 0.6, 10.0, 75.0);
            DoHarmful(m);
            AOS.Damage(m, this, (int)damage, 100, 0, 0, 0, 0);

            if (m.Alive && m.Body.IsHuman && !m.Mounted)
            {
                m.Animate(20, 7, 1, true, false, 0);
            }
        }

        _nextDismount = Core.Now + TimeSpan.FromMinutes(2);
    }

    public void DoAreaAttack()
    {
        const int range = 18;

        // ServUO's InternalTimer: a ring of flame grows by one tile every tenth of a second out to the range.
        var tick = 1;
        Timer.StartTimer(
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(100),
            range,
            () => DoRing(tick++)
        );

        foreach (var m in GetMobilesInRange(range))
        {
            if ((m is PlayerMobile || m is BaseCreature bc && bc.GetMaster() is PlayerMobile) && CanBeHarmful(m))
            {
                var target = m;
                Timer.StartTimer(TimeSpan.FromSeconds(1), () => DoDamage(target));
            }
        }

        _nextArea = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(60, 180));
    }

    public void DoDamage(Mobile m)
    {
        if (m == null || Deleted)
        {
            return;
        }

        DoHarmful(m);
        AOS.Damage(m, this, Utility.RandomMinMax(100, 150), 0, 100, 0, 0, 0);

        if (Utility.RandomBool())
        {
            BleedAttack.BeginBleed(m, this);
            m.SendLocalizedMessage(1060160); // You are bleeding!
        }
    }

    private void DoRing(int radius)
    {
        if (Deleted || Map == null)
        {
            return;
        }

        // ServUO's Geometry.Circle2D: every tile at this radius from him.
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                var d = (int)Math.Round(Math.Sqrt(x * x + y * y));

                if (d == radius)
                {
                    DoEffect(new Point3D(X + x, Y + y, Z), Map);
                }
            }
        }
    }

    public static void DoEffect(Point3D p, Map map)
    {
        Effects.PlaySound(p, map, 0x307);
        Effects.SendLocationEffect(p, map, Utility.RandomBool() ? 14000 : 14013, 20);
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.SuperBoss, 6);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        c.DropItem(new MessageInABottle(c.Map));
        c.DropItem(new SpecialFishingNet());
        c.DropItem(new SpecialFishingNet());

        foreach (var bc in _helpers.ToArray())
        {
            RegisterDamageTo(bc);

            if (bc?.Alive == true)
            {
                bc.Kill();
            }
        }
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        _helpers.RemoveAll(bc => bc == null);
        _nextDismount = Core.Now;
        _nextArea = Core.Now;
        _nextReturn = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(120, 180));
    }
}

// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/OsiredonTheScalisEnforcer.cs -> ours, the High Seas
// fishing port. Osiredon the Scalis Enforcer: 75,000 hits, a necromancer-mage that throws a water ball every minute (30%
// of the target's hits, 10 to 40, dismounting a rider for ten seconds), explodes fire on everyone within eight tiles every
// 15 to 20 seconds, and answers spells and arrows with parasitic eels (a quarter of the time, up to 25). Drops the
// Enchanted Coral Bracelet, Wand of Thundering Glory, Leviathan Hide Bracers and Smiling Moon Blade (unique), enchanted
// blade and vortex deeds (decorative), a message in a bottle, two special fishing nets and a fishing pole.
//
// Nothing spawns Osiredon in ServUO pub57 either (no `new Osiredon` outside this file), so, as there, it is placed by
// staff or a spawner.
//
// Changes from ServUO (shard-migration notes/cc-P193-port-high-seas-fishing.md):
// - AI_NecroMage is stock MageAI (AGENTS.md: a known equivalent), so AIType.AI_Mage; the necromancy skills are set.
// - TaintedLifeAura (a ServUO BaseCreature aura) has no pinned equivalent and is left out.
// - The shared artifact, MiniSoulForgeDeed, is not in pinned or ours (Imbuing here is the Artificers' system); the shared
//   list is empty, so the 10% shared roll gives nothing. Reverses when a mini soul forge deed exists.
// - The corpse's fishing pole is a plain one (pinned's pole carries no magic attributes); SkillMasteryPrimer is not here.
// - Boats are pushed, not damaged (BaseSeaChampion).
// - The eel list is not saved on Osiredon; each eel saves its master and re-registers on load.
// - SetHits(75000) stands, but pinned BaseCreature.HitsMax clamps at 65,000 (Mobiles/BaseCreature.cs:1043), so Osiredon has
//   65,000 hits here (D-140 in the notes).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Multis;
using Server.Spells;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Osiredon : BaseSeaChampion
{
    public static readonly TimeSpan SpawnRate = TimeSpan.FromSeconds(30);
    public static readonly int SpawnMax = 25;

    private readonly List<Mobile> _eels = new();
    private DateTime _nextSpawn;
    private DateTime _nextSpecial;
    private DateTime _nextWaterBall;

    [Constructible]
    public Osiredon() : this(null)
    {
    }

    public Osiredon(Mobile fisher) : base(fisher, AIType.AI_Mage, FightMode.Closest)
    {
        Body = 1068;
        BaseSoundID = 589;

        Combatant = fisher;

        _nextSpawn = Core.Now + SpawnRate;
        _nextSpecial = Core.Now;
        _nextWaterBall = Core.Now;

        CanSwim = true;
        CantWalk = true;

        SetStr(805, 900);
        SetDex(121, 165);
        SetInt(125, 137);

        SetMana(4000);
        SetHits(75000);

        SetDamage(19, 26);

        SetDamageType(ResistanceType.Physical, 40);
        SetDamageType(ResistanceType.Cold, 30);
        SetDamageType(ResistanceType.Energy, 30);

        SetResistance(ResistanceType.Physical, 80, 90);
        SetResistance(ResistanceType.Fire, 80, 90);
        SetResistance(ResistanceType.Cold, 85, 95);
        SetResistance(ResistanceType.Poison, 80, 90);
        SetResistance(ResistanceType.Energy, 80, 90);

        SetSkill(SkillName.Wrestling, 122.9, 128.0);
        SetSkill(SkillName.Tactics, 127.7, 132.9);
        SetSkill(SkillName.MagicResist, 120.9, 129.4);
        SetSkill(SkillName.Necromancy, 122.9, 128.0);
        SetSkill(SkillName.SpiritSpeak, 160.1, 220.0);
        SetSkill(SkillName.Magery, 120.1, 129.4);
        SetSkill(SkillName.EvalInt, 100.1, 120.0);
        SetSkill(SkillName.DetectHidden, 100.0);

        Fame = 25000;
        Karma = -25000;
    }

    public override string DefaultName => "osiredon the scalis enforcer";

    public override bool CanDamageBoats => true;
    public override TimeSpan BoatDamageCooldown => TimeSpan.FromSeconds(120);
    public override int MinBoatDamage => 3;
    public override int MaxBoatDamage => 8;
    public override int DamageRange => 2;

    public override int Meat => 5;

    public override ScaleType ScaleType => ScaleType.All;
    public override int Scales => 20;

    // ServUO also sets TreasureMapChance .50; pinned BaseCreature has no such member (its map drop is pinned's own rule), as
    // CC4 recorded for Shame's creatures. The level is kept.
    public override int TreasureMapLevel => 7;

    public override Type[] UniqueList =>
        new[] { typeof(EnchantedCoralBracelet), typeof(WandOfThunderingGlory), typeof(LeviathanHideBracers), typeof(SmilingMoonBlade) };

    public override Type[] SharedList => Array.Empty<Type>();

    public override Type[] DecorativeList => new[] { typeof(EnchantedBladeDeed), typeof(EnchantedVortexDeed) };

    public override bool NoGoodies => true;

    [CommandProperty(AccessLevel.GameMaster)]
    public int Eels => _eels.Count;

    public void AddEel(Mobile eel)
    {
        if (eel is ParasiticEel && !_eels.Contains(eel))
        {
            _eels.Add(eel);
        }
    }

    public void RemoveEel(Mobile eel) => _eels.Remove(eel);

    public override void DoDamageBoat(BaseBoat boat)
    {
        DoAreaExplosion();
        base.DoDamageBoat(boat);
    }

    public override void OnDamagedBySpell(Mobile from, int damage)
    {
        base.OnDamagedBySpell(from, damage);

        if (_nextSpawn < Core.Now && _eels.Count < SpawnMax && 0.25 > Utility.RandomDouble())
        {
            SpawnEel(from);
        }
    }

    public override void OnGotMeleeAttack(Mobile attacker, int damage)
    {
        base.OnGotMeleeAttack(attacker, damage);

        if (attacker.Weapon is BaseRanged && _nextSpawn < Core.Now && _eels.Count < SpawnMax && 0.25 > Utility.RandomDouble())
        {
            SpawnEel(attacker);
        }
    }

    public override void OnThink()
    {
        base.OnThink();

        if (_nextSpecial < Core.Now)
        {
            DoAreaExplosion();
        }
    }

    public override void OnActionCombat()
    {
        var combatant = Combatant;

        if (combatant == null || combatant.Deleted || combatant.Map != Map || !InRange(combatant, 12) ||
            !CanBeHarmful(combatant) || !InLOS(combatant))
        {
            return;
        }

        if (Core.Now >= _nextWaterBall)
        {
            var damage = Math.Clamp(combatant.Hits * 0.3, 10.0, 40.0);

            DoHarmful(combatant);
            MovingParticles(combatant, 0x36D4, 5, 0, false, false, 195, 0, 9502, 3006, 0, 0, 0);
            AOS.Damage(combatant, this, (int)damage, 100, 0, 0, 0, 0);

            if (combatant is PlayerMobile pm && pm.Mount != null)
            {
                pm.SetMountBlock(BlockMountType.DismountRecovery, TimeSpan.FromSeconds(10), true);
            }

            _nextWaterBall = Core.Now + TimeSpan.FromMinutes(1);
        }
    }

    public void SpawnEel(Mobile m)
    {
        var map = Map;

        if (map == null || map == Map.Internal || m == null)
        {
            return;
        }

        var loc = m.Location;

        for (var j = 0; j < 3; j++)
        {
            for (var i = 0; i < 25; i++)
            {
                var x = Utility.RandomMinMax(loc.X - 1, loc.X + 1);
                var y = Utility.RandomMinMax(loc.Y - 1, loc.Y + 1);

                if (SpellHelper.CheckMulti(new Point3D(x, y, m.Z), map) || map.CanSpawnMobile(x, y, loc.Z))
                {
                    var eel = new ParasiticEel(this);
                    eel.MoveToWorld(new Point3D(x, y, loc.Z), map);

                    if (m is PlayerMobile)
                    {
                        eel.Combatant = m;
                    }

                    break;
                }
            }
        }

        _nextSpawn = Core.Now + SpawnRate;
    }

    public void DoAreaExplosion()
    {
        var toExplode = new List<Mobile>();

        foreach (var mob in GetMobilesInRange(8))
        {
            if (!CanBeHarmful(mob, false) || mob == this || mob is BaseCreature pet && pet.GetMaster() == this)
            {
                continue;
            }

            if (mob.Player)
            {
                toExplode.Add(mob);
            }
            else if (mob is BaseCreature bc && (bc.Controlled || bc.Summoned || bc.Team != Team))
            {
                toExplode.Add(mob);
            }
        }

        foreach (var mob in toExplode)
        {
            mob.FixedParticles(0x36BD, 20, 10, 5044, EffectLayer.Head);
            mob.PlaySound(0x307);

            var damage = Utility.RandomMinMax(50, 125);
            AOS.Damage(mob, this, damage, 0, 100, 0, 0, 0);
        }

        _nextSpecial = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(15, 20));
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        c.DropItem(new FishingPole());

        c.DropItem(new MessageInABottle(c.Map));
        c.DropItem(new SpecialFishingNet());
        c.DropItem(new SpecialFishingNet());
    }

    public override void Delete()
    {
        var eels = new List<Mobile>(_eels);

        foreach (var eel in eels)
        {
            eel?.Kill();
        }

        base.Delete();
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.SuperBoss, 5);
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        _nextSpawn = Core.Now;
        _nextSpecial = Core.Now;
        _nextWaterBall = Core.Now;
    }
}

/// <summary>Osiredon's eel: a fast swimmer with ServUO's Poison.Parasitic, which is its DeadlyParasitic (ServUO Server/Poison.cs:56); pinned has the same poison.</summary>
[SerializationGenerator(0, false)]
public partial class ParasiticEel : BaseCreature
{
    [SerializableField(0, setter: "private")]
    private Mobile _master;

    [Constructible]
    public ParasiticEel() : this(null)
    {
    }

    public ParasiticEel(Mobile master) : base(AIType.AI_Mage, FightMode.Closest)
    {
        if (master is Osiredon osiredon)
        {
            _master = master;
            osiredon.AddEel(this);
        }

        Body = 0x34;

        SetStr(80, 125);
        SetDex(150, 250);
        SetInt(20, 40);

        SetHits(100);

        SetDamage(4, 12);

        SetDamageType(ResistanceType.Physical, 25);
        SetDamageType(ResistanceType.Cold, 25);
        SetDamageType(ResistanceType.Poison, 50);

        SetResistance(ResistanceType.Physical, 20);
        SetResistance(ResistanceType.Fire, 10, 25);
        SetResistance(ResistanceType.Cold, 10, 25);
        SetResistance(ResistanceType.Poison, 99);
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

    public override string DefaultName => "a parasitic eel";

    public override Poison PoisonImmune => Poison.DeadlyParasitic;
    public override Poison HitPoison => Poison.DeadlyParasitic;

    public override void Delete()
    {
        if (_master is Osiredon osiredon)
        {
            osiredon.RemoveEel(this);
        }

        base.Delete();
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 1);
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        if (_master is Osiredon osiredon && !osiredon.Deleted)
        {
            osiredon.AddEel(this);
        }
    }
}

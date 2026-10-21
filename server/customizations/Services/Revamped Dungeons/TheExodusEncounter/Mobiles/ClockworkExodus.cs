// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Mobiles/ClockworkExodus.cs (cc-P195): Lord Exodus in his
// clockwork body, 50,000 hits, raised in Ver Lor Reg by the controller. Every melee blow given or taken and every
// spell that lands on him casts three death vortices about the attacker (once per spot); his aura of energy burns
// everyone near; a kill rains 500-1,000 gold on every tile within eight and, one time in five, hands a random
// artifact of the ten to a player with looting rights. MinHits is the low-water mark the Summoning Altar reads: a new
// ritual needs him at 75 percent, or at 60 never having been lower.
//
// Conversion notes (notes/cc-P195-port-exodus-encounter.md):
//   AI_Mystic does not exist in pinned; AI_Mage, which casts from the skills he has (Magery none, so his Mysticism
//     120 and Necromancy 120 stand for the record and the resists), is the mapping cc-P194 used for AI_Necro (D).
//   SetWeaponAbility(BleedAttack) is GetWeaponAbility (the recipe); SetAreaEffect(AuraOfEnergy) is HasAura with
//     ServUO's AuraOfEnergy numbers (AreaEffects.cs:234-249: 20-30 energy, 1072073, the particles and sound; range
//     ServUO EffectRange 5; pinned fires every AuraInterval 5 s, ServUO every tick at TriggerChance 1 but on a
//     30-second cooldown: cc-P194's D-147 shape, a deviation).
//   m_MinHits is static in ServUO (one boss at a time makes it per-boss in effect); saved per instance here.
//   OnDamagedBySpell / OnGaveMeleeAttack / OnGotMeleeAttack take a damage argument here.
//   GetSpawnPosition is BasePeerless.GetSpawnPosition (ServUO BaseCreature's, pinned has none).
//   CanBeParagon has no pinned virtual; the boss is placed by MoveToWorld, which never converts.
//   Instances: ServUO never removes a deleted boss; removed on delete here.
//   IRareSelfRepairSource marks him a boss for the shard's loot boxes (F-40: ClusterFLootBoxes.IsBoss) and the rare
//     Self Repair roll; his tier is 5 in ClusterFLootTable.BossTier.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ClockworkExodus : BaseCreature, IRareSelfRepairSource
{
    public static readonly Type[] Artifacts =
    {
        typeof(ScrollofValiantCommendation),
        typeof(BracersofAlchemicalDevastation),
        typeof(HygieiasAmulet),
        typeof(Asclepius),
        typeof(ClockworkLeggings),
        typeof(DupresSword),
        typeof(GargishDupresSword),
        typeof(GargishClockworkLeggings),
        typeof(GargishBracersofAlchemicalDevastation),
        typeof(GargishAsclepius)
    };

    public static List<ClockworkExodus> Instances { get; } = new();

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _minHits;

    private Point3D _lastTarget;

    [Constructible]
    public ClockworkExodus() : base(AIType.AI_Mage)
    {
        Body = 1248;
        BaseSoundID = 639;
        Hue = 2500;
        Female = true;

        SetStr(851, 950);
        SetDex(581, 683);
        SetInt(601, 750);

        SetHits(50000);
        SetStam(507, 669);

        SetDamage(20, 30);

        SetDamageType(ResistanceType.Physical, 75);
        SetDamageType(ResistanceType.Fire, 25);
        SetDamageType(ResistanceType.Energy, 50);

        SetResistance(ResistanceType.Physical, 75, 90);
        SetResistance(ResistanceType.Fire, 50, 65);
        SetResistance(ResistanceType.Cold, 45, 60);
        SetResistance(ResistanceType.Poison, 45, 60);
        SetResistance(ResistanceType.Energy, 45, 60);

        SetSkill(SkillName.EvalInt, 130.0);
        SetSkill(SkillName.Mysticism, 120);
        SetSkill(SkillName.MagicResist, 140.0);
        SetSkill(SkillName.Tactics, 90.1, 105.0);
        SetSkill(SkillName.Wrestling, 90.1, 105.0);
        SetSkill(SkillName.SpiritSpeak, 120.0);
        SetSkill(SkillName.Necromancy, 120.0);
        SetSkill(SkillName.Poisoning, 120.0);
        SetSkill(SkillName.Meditation, 120.0);
        SetSkill(SkillName.Anatomy, 120.0);
        SetSkill(SkillName.Healing, 120.0);

        Fame = 24000;
        Karma = -24000;

        VirtualArmor = 20;

        _minHits = Hits;

        Instances.Add(this);
    }

    public override string CorpseName => "a vile corpse";
    public override string DefaultName => "Clockwork Exodus";

    public override bool Unprovokable => true;
    public virtual double ChangeCombatant => 0.3;
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Greater;
    public override int TreasureMapLevel => 5;

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    // ServUO AreaEffect.AuraOfEnergy: 20-30 energy to everyone within EffectRange 5, with its message and sparks.
    public override bool HasAura => true;
    public override int AuraRange => 5;

    public override void AuraDamage()
    {
        if (!Alive || IsDeadBondedPet)
        {
            return;
        }

        var targets = new List<Mobile>();

        foreach (var m in GetMobilesInRange(AuraRange))
        {
            if (m != this && CanBeHarmful(m, false) && (Core.AOS || InLOS(m)) &&
                (m is BaseCreature bc && (bc.Controlled || bc.Summoned || bc.Team != Team) || m.Player))
            {
                targets.Add(m);
            }
        }

        foreach (var defender in targets)
        {
            AOS.Damage(defender, this, Utility.RandomMinMax(20, 30), 0, 0, 0, 0, 100);

            defender.SendLocalizedMessage(1072073, Name); // : The creature's aura of energy is damaging you!

            DoHarmful(defender);
            defender.FixedParticles(0x374A, 10, 30, 5052, 1278, 0, EffectLayer.Waist);
            defender.PlaySound(0x51D);
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosSuperBoss, 2);
    }

    /// <summary>ServUO DistributeRandomArtifact: one of the ten to a random player with looting rights.</summary>
    public static void DistributeRandomArtifact(BaseCreature bc, Type[] typelist)
    {
        var item = Loot.Construct(typelist[Utility.Random(typelist.Length)]);
        DistributeArtifact(DemonKnight.FindRandomPlayer(bc), item);
    }

    public static void DistributeArtifact(Mobile to, Item artifact)
    {
        if (to == null || artifact == null)
        {
            artifact?.Delete();
            return;
        }

        var pack = to.Backpack;

        if (pack == null || !pack.TryDropItem(to, artifact, false))
        {
            to.BankBox.DropItem(artifact);
        }

        to.SendLocalizedMessage(502088); // A special gift has been placed in your backpack.
    }

    public override bool OnBeforeDeath()
    {
        if (Utility.RandomDouble() < 0.2)
        {
            DistributeRandomArtifact(this, Artifacts);
        }

        var map = Map;

        if (map != null && map != Map.Internal)
        {
            for (var x = -8; x <= 8; ++x)
            {
                for (var y = -8; y <= 8; ++y)
                {
                    var dist = Math.Sqrt(x * x + y * y);

                    if (dist <= 8)
                    {
                        new GoldTimer(map, X + x, Y + y).Start();
                    }
                }
            }
        }

        Instances.Remove(this);

        return base.OnBeforeDeath();
    }

    /// <summary>ServUO SpawnVortices: three death vortices within three tiles of the target, once per target spot.</summary>
    public void SpawnVortices(Mobile target)
    {
        var map = Map;

        if (map == null || map == Map.Internal)
        {
            return;
        }

        MovingParticles(target, 0x1AF6, 5, 0, false, false, 0x816, 0, 3006, 0, 0, (EffectLayer)255, 0);

        for (var i = 0; i < 3; ++i)
        {
            var dv = new DeathVortexTrap();
            dv.MoveToWorld(BasePeerless.GetSpawnPosition(target.Location, map, 3), map);
        }

        target.SendLocalizedMessage(1152693); // The power of the Void surges around you!

        _lastTarget = target.Location;
    }

    public void DoSpecialAbility(Mobile target)
    {
        if (target != null && _lastTarget != target.Location)
        {
            target.SendLocalizedMessage(1152692, Name); // ~1_CREATURE~ casts a deadly vortex at you!
            SpawnVortices(target);
        }
    }

    public override void OnDamagedBySpell(Mobile attacker, int damage)
    {
        base.OnDamagedBySpell(attacker, damage);
        DoSpecialAbility(attacker);
    }

    public override void OnGaveMeleeAttack(Mobile defender, int damage)
    {
        base.OnGaveMeleeAttack(defender, damage); // if it hits you it spawns vortices
        DoSpecialAbility(defender);
    }

    public override void OnGotMeleeAttack(Mobile attacker, int damage)
    {
        base.OnGotMeleeAttack(attacker, damage); // if you hit creature it spawns vortices
        DoSpecialAbility(attacker);
    }

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        base.OnDamage(amount, from, willKill);

        if (Hits < _minHits && Hits < HitsMax * 0.60)
        {
            MinHits = Hits;
        }

        if (Hits >= HitsMax * 0.75)
        {
            MinHits = HitsMax;
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        Instances.Remove(this);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Instances.Add(this);
    }

    private class GoldTimer : Timer
    {
        private readonly Map _map;
        private readonly int _x, _y;

        public GoldTimer(Map map, int x, int y) : base(TimeSpan.FromSeconds(Utility.RandomDouble() * 10.0))
        {
            _map = map;
            _x = x;
            _y = y;
        }

        protected override void OnTick()
        {
            var z = _map.GetAverageZ(_x, _y);
            var canFit = _map.CanFit(_x, _y, z, 6, false, false);

            for (var i = -3; !canFit && i <= 3; ++i)
            {
                canFit = _map.CanFit(_x, _y, z + i, 6, false, false);

                if (canFit)
                {
                    z += i;
                }
            }

            if (!canFit)
            {
                return;
            }

            var g = new Gold(500, 1000);
            g.MoveToWorld(new Point3D(_x, _y, z), _map);

            if (Utility.RandomDouble() <= 0.3)
            {
                Effects.SendLocationParticles(EffectItem.Create(g.Location, g.Map, EffectItem.DefaultDuration), 0x3709, 10, 30, 5052);
                Effects.PlaySound(g.Location, g.Map, 0x208);
            }
        }
    }
}

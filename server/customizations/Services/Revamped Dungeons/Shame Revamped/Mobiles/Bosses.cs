// ServUO: Services/Revamped Dungeons/Shame Revamped/Mobiles/Bosses.cs (CC4 Shame).
//
// ShameGuardian and the three level guardians an altar summons: Quartz Elemental (level 1), Flame Elemental
// (level 2), Wind Elemental (level 3). Each drops 3-5 Crystals of Shame and, always on Felucca and half the
// time elsewhere, its Whetstone of Enervation component. FlameElemental and WindElemental are ALSO named in
// RevampedSpawns/ShameRevamped.xml as ordinary spawns (two rows each); a spawned one has no altar, so its
// Damage gate is open and nothing is unlocked by killing it. Same on ServUO.
//
// Conversion, beyond the recipe's mechanical transformation:
//   Damage(...) returns void here (Mobile.cs:5900); ServUO's "return 0 unless the summoner" is not calling base.
//   SetSpecialAbility(SpecialAbility.DragonBreath) is ModernUO's FireBreath monster ability: the same 0.16
//     hit-point scalar, 100% fire, 30-45 s cooldown, effect 0x36D4, sound 0x227 (FireBreath.cs vs ServUO's
//     default DragonBreathDefinition); a transformation, not a deviation.
//   SetAreaEffect(AreaEffect.AuraDamage) + IAuraCreature.AuraEffect is ModernUO's HasAura/AuraDamage/
//     AuraEffect. ServUO's fireAura definition for FlameElemental is cooldown 5 s, range 5, damage 7, 100%
//     fire; the overrides below reproduce it. ServUO fires it at 40% per think once off cooldown, ModernUO
//     every AuraInterval exactly; paper-level, notes/cc4-shame.md.

using System;
using ModernUO.Serialization;
using Server.Engines.ShameRevamped;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ShameGuardian : BaseCreature
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ShameAltar _altar;

    public ShameGuardian(AIType type) : base(type, FightMode.Aggressor)
    {
        Title = "the guardian";
    }

    public override bool AutoDispel => true;
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Lethal;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        _altar?.OnGuardianKilled();

        c.DropItem(new ShameCrystal(Utility.RandomMinMax(3, 5)));

        Altar = null;
    }

    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (from == null)
        {
            return;
        }

        if (_altar?.Summoner == null)
        {
            base.Damage(amount, from, informMount, ignoreEvilOmen);
            return;
        }

        var good = false;

        if (from == _altar.Summoner ||
            _altar.DeadLine > Core.Now && _altar.DeadLine - Core.Now < TimeSpan.FromMinutes(10))
        {
            good = true;
        }
        else if (from is BaseCreature bc && bc.GetMaster() == _altar.Summoner)
        {
            good = true;
        }
        else if (ShameAltar.AllowParties)
        {
            var p = Server.Engines.PartySystem.Party.Get(from); // Party alone binds to Mobile.Party here

            if (p != null)
            {
                foreach (var info in p.Members)
                {
                    if (info.Mobile == from)
                    {
                        good = true;
                        break;
                    }
                }
            }
        }

        if (good)
        {
            base.Damage(amount, from, informMount, ignoreEvilOmen);
        }
        else
        {
            from.SendLocalizedMessage(1151633); // You did not summon this champion, so you may not attack it at this time.
        }
    }
}

[SerializationGenerator(0, false)]
public partial class QuartzElemental : ShameGuardian
{
    [Constructible]
    public QuartzElemental() : base(AIType.AI_Melee)
    {
        Body = 14;
        BaseSoundID = 268;
        Hue = 2575;

        SetStr(240, 260);
        SetDex(70, 80);
        SetInt(100, 110);

        SetHits(1000, 1100);

        SetDamage(14, 21);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 30, 35);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetResistance(ResistanceType.Energy, 15, 25);

        SetSkill(SkillName.MagicResist, 110, 120);
        SetSkill(SkillName.Tactics, 100, 110.0);
        SetSkill(SkillName.Wrestling, 110, 120);

        Fame = 4500;
        Karma = -4500;
    }

    public override string CorpseName => "a quartz elemental corpse";
    public override string DefaultName => "a quartz elemental";

    public override int TreasureMapLevel => 1;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 1);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (c.Map != null && c.Map.Rules == MapRules.FeluccaRules || 0.5 > Utility.RandomDouble())
        {
            c.DropItem(new QuartzGrit());
        }
    }
}

[SerializationGenerator(0, false)]
public partial class FlameElemental : ShameGuardian
{
    private static MonsterAbility[] _abilities = { MonsterAbilities.FireBreath };

    [Constructible]
    public FlameElemental() : base(AIType.AI_Mage)
    {
        Body = 15;
        BaseSoundID = 838;
        Hue = 1161;

        SetStr(420, 460);
        SetDex(160, 210);
        SetInt(120, 190);

        SetHits(700, 800);
        SetMana(1000, 1300);

        SetDamage(13, 15);

        SetDamageType(ResistanceType.Physical, 25);
        SetDamageType(ResistanceType.Fire, 75);

        SetResistance(ResistanceType.Physical, 40, 60);
        SetResistance(ResistanceType.Fire, 100);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.MagicResist, 90, 140);
        SetSkill(SkillName.Tactics, 90, 130.0);
        SetSkill(SkillName.Wrestling, 90, 120);
        SetSkill(SkillName.Magery, 100, 145);
        SetSkill(SkillName.EvalInt, 90, 140);
        SetSkill(SkillName.Meditation, 80, 120);
        SetSkill(SkillName.Parry, 100, 120);

        Fame = 4500;
        Karma = -4500;

        PackItem(new SulfurousAsh(5));
    }

    public override string CorpseName => "a flame elemental corpse";
    public override string DefaultName => "a flame elemental";

    public override MonsterAbility[] GetMonsterAbilities() => _abilities;

    // ServUO AuraDefinition "fireAura": FlameElemental, FireDaemon, LesserFlameElemental.
    public override bool HasAura => true;
    public override TimeSpan AuraInterval => TimeSpan.FromSeconds(5);
    public override int AuraRange => 5;
    public override int AuraBaseDamage => 7;
    public override int AuraFireDamage => 100;

    public override void AuraEffect(Mobile m)
    {
        m.SendLocalizedMessage(1008112); // The intense heat is damaging you!
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 1);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (c.Map != null && c.Map.Rules == MapRules.FeluccaRules || 0.5 > Utility.RandomDouble())
        {
            c.DropItem(new CorrosiveAsh());
        }
    }
}

[SerializationGenerator(0, false)]
public partial class WindElemental : ShameGuardian
{
    [Constructible]
    public WindElemental() : base(AIType.AI_Mage)
    {
        Body = 13;
        BaseSoundID = 655;
        Hue = 33765;

        SetStr(370, 460);
        SetDex(160, 250);
        SetInt(150, 220);

        SetHits(2500, 2600);
        SetMana(1000, 1300);

        SetDamage(15, 17);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Cold, 40);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 65, 75);
        SetResistance(ResistanceType.Fire, 55, 65);
        SetResistance(ResistanceType.Cold, 55, 65);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 60, 75);

        SetSkill(SkillName.MagicResist, 60, 80);
        SetSkill(SkillName.Tactics, 60, 80.0);
        SetSkill(SkillName.Wrestling, 60, 80);
        SetSkill(SkillName.Magery, 60, 80);
        SetSkill(SkillName.EvalInt, 60, 80);

        Fame = 4500;
        Karma = -4500;
    }

    public override string CorpseName => "a wind elemental corpse";
    public override string DefaultName => "a wind elemental";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 1);
        AddLoot(LootPack.HighScrolls, Utility.RandomMinMax(3, 5));
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (c.Map != null && c.Map.Rules == MapRules.FeluccaRules || 0.5 > Utility.RandomDouble())
        {
            c.DropItem(new CursedOilstone());
        }
    }
}

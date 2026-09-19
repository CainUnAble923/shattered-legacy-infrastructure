// ServUO: Services/Revamped Dungeons/Shame Revamped/Mobiles/Creatures.cs (CC4 Shame).
//
// The twenty-two Shame creatures. Every one drops a Crystal of Shame on death at its own chance, but only when
// its corpse lies in the "Shame" region (Region.Find(...).IsPartOf("Shame")); regions.json has that
// DungeonRegion on Trammel and Felucca, so this needs no region port.
//
// Conversion, beyond the recipe's mechanical transformation (DefaultName/CorpseName, GetWeaponAbility(),
// collapsed constructors, speed arguments dropped):
//
//   Pet Training abilities. SetSpecialAbility/SetAreaEffect are Services/Pet Training (gap register B3,
//   WAIT). Where ModernUO has the same mechanic under another name it is used; where it has none the line is
//   dropped and logged (D-32 shape, notes/cc4-shame.md):
//     DragonBreath  -> MonsterAbilities.FireBreath (same scalar, damage type, delays and effect ids)
//     AuraDamage    -> HasAura + the Aura* overrides, values from ServUO's fireAura definition
//     LifeLeech     -> MonsterAbilities.DrainLifeAttack (the RunUO life drain: 5 energy/s for 5 s, healing
//                      the attacker; ServUO's LifeLeech is the same drain with different trigger tuning)
//     StickySkin (MudPie), ColossalRage (StoneElemental, GreaterEarthElemental, MudElemental),
//     SearingWounds (MoltenEarthElemental): no counterpart, dropped.
//   AIType.AI_Mystic (CrazedMage) does not exist here; AI_Mage, logged.
//   TreasureMapChance (DiseasedBloodElemental, 1.0) has no counterpart: the stock chance applies, logged.
//   ClayGolem: ServUO's Golem calls a virtual SpawnPackItems() that ClayGolem overrides to nothing. ModernUO's
//     Golem packs the ingots and parts inline unless constructed summoned, and everything else the summoned
//     branch changes (hue, fire resistance, fame, karma) ClayGolem sets itself afterwards, so base(true)
//     reproduces ServUO's pack exactly (an executioner's cap and nothing else).
//   OnDamagedBySpell takes (from, damage) here. The mana-taint the three mages share was three copies of the
//     same static table and timer on ServUO; it is one helper, one instance per class, same behaviour.
//   NextTeleport on the two vortices is not serialized on ServUO either.
//   GreaterBloodElemental sets hits twice on ServUO (1350-1500 then 900-1000); the second wins, kept as is.
//   The unused `Table` on MudPie and the unused `scrolls` local on DiseasedBloodElemental are dropped.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.ShameRevamped;
using Server.Items;

namespace Server.Mobiles;

// ---------------------------------------------------------------------------------------------------------
// Level 1

[SerializationGenerator(0, false)]
public partial class MudPie : BaseCreature
{
    [Constructible]
    public MudPie() : base(AIType.AI_Melee, FightMode.Closest)
    {
        Body = 779;
        BaseSoundID = 422;

        Hue = 2012;

        SetStr(140, 210);
        SetDex(70, 100);
        SetInt(90, 110);

        SetHits(280, 340);

        SetDamage(9, 12);

        SetDamageType(ResistanceType.Physical, 80);
        SetDamageType(ResistanceType.Poison, 20);

        SetResistance(ResistanceType.Physical, 30, 45);
        SetResistance(ResistanceType.Fire, 35, 40);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Poison, 35, 45);
        SetResistance(ResistanceType.Energy, 40);

        SetSkill(SkillName.MagicResist, 65, 85);
        SetSkill(SkillName.Tactics, 65, 85);
        SetSkill(SkillName.Wrestling, 65, 85);

        Fame = 500;
        Karma = -500;

        PackReg(1, 2);
        PackGem(1, 2);

        PackItem(new ExecutionersCap());

        if (0.33 > Utility.RandomDouble())
        {
            PackItem(new ExecutionersCap());
        }

        // SetSpecialAbility(SpecialAbility.StickySkin): no counterpart, dropped.
    }

    public override string CorpseName => "a mud pie corpse";
    public override string DefaultName => "a mud pie";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.05 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 1);
    }
}

[SerializationGenerator(0, false)]
public partial class StoneElemental : EarthElemental
{
    [Constructible]
    public StoneElemental()
    {
        Hue = 2401;

        SetStr(140, 210);
        SetDex(80, 110);
        SetInt(90, 120);

        SetHits(900, 1000);

        SetDamage(15, 17);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 60, 65);
        SetResistance(ResistanceType.Fire, 50, 60);
        SetResistance(ResistanceType.Cold, 45, 55);
        SetResistance(ResistanceType.Poison, 55, 60);
        SetResistance(ResistanceType.Energy, 45, 55);

        SetSkill(SkillName.MagicResist, 100.0);
        SetSkill(SkillName.Tactics, 80.0, 96.0);
        SetSkill(SkillName.Wrestling, 80.0, 97.0);

        Fame = 4000;
        Karma = -4000;

        PackReg(1, 2);
        PackGem(1, 2);

        PackItem(new Granite());
        PackItem(new Sand());

        // SetSpecialAbility(SpecialAbility.ColossalRage): no counterpart, dropped.
    }

    public override string CorpseName => "a stone elemental corpse";
    public override string DefaultName => "a stone elemental";

    public override int TreasureMapLevel => 2;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.15 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class CaveTroll : Troll
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ShameWall _wall;

    [Constructible]
    public CaveTroll() : this(null)
    {
    }

    public CaveTroll(ShameWall wall)
    {
        Body = 0x1;
        FightMode = FightMode.Aggressor;

        if (wall != null)
        {
            Title = "the wall guardian";
        }

        Hue = 638;
        _wall = wall;

        SetStr(180, 210);
        SetDex(107, 205);
        SetInt(40, 70);

        SetHits(638, 978);

        SetDamage(15, 17);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 45, 55);
        SetResistance(ResistanceType.Cold, 45, 55);
        SetResistance(ResistanceType.Poison, 35, 45);
        SetResistance(ResistanceType.Energy, 35, 45);

        SetSkill(SkillName.MagicResist, 70, 90);
        SetSkill(SkillName.Tactics, 80, 110);
        SetSkill(SkillName.Wrestling, 80, 110);
        SetSkill(SkillName.DetectHidden, 100.0);

        Fame = 3500;
        Karma = -3500;
        PackGem(1);

        PackItem(new Saltpeter(Utility.RandomMinMax(1, 5)));
        PackItem(new Potash(Utility.RandomMinMax(1, 5)));
        PackItem(new Charcoal(Utility.RandomMinMax(1, 5)));
        PackItem(new BlackPowder(Utility.RandomMinMax(1, 5)));
    }

    public override string CorpseName => "a cave troll corpse";
    public override string DefaultName => "a cave troll";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.ArmorIgnore;

    public override MeatType MeatType => MeatType.Ribs;
    public override int Meat => 2;

    public override int TreasureMapLevel => 1;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        _wall?.OnTrollKilled();

        if (0.10 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class ClayGolem : Golem
{
    // base(summoned: true) skips ModernUO's inline golem pack (ServUO's ClayGolem overrides SpawnPackItems to
    // nothing); every other summoned-branch value is overwritten below. See the file header.
    [Constructible]
    public ClayGolem() : base(true)
    {
        Hue = 654;

        SetStr(450, 600);
        SetDex(100, 150);
        SetInt(100, 150);

        SetHits(700, 900);

        SetDamage(13, 24);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 45, 55);
        SetResistance(ResistanceType.Fire, 50, 60);
        SetResistance(ResistanceType.Cold, 45, 55);
        SetResistance(ResistanceType.Poison, 99);
        SetResistance(ResistanceType.Energy, 35, 45);

        SetSkill(SkillName.MagicResist, 150, 200);
        SetSkill(SkillName.Tactics, 80, 120);
        SetSkill(SkillName.Wrestling, 80, 110);
        SetSkill(SkillName.Parry, 70, 80);
        SetSkill(SkillName.DetectHidden, 70.0, 80.0);

        Fame = 4500;
        Karma = -4500;

        PackItem(new ExecutionersCap());
    }

    public override string CorpseName => "a clay golem corpse";
    public override string DefaultName => "a clay golem";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.2 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class GreaterEarthElemental : EarthElemental
{
    [Constructible]
    public GreaterEarthElemental()
    {
        Hue = 1143;

        SetHits(500, 600);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 65);
        SetResistance(ResistanceType.Fire, 35, 45);
        SetResistance(ResistanceType.Cold, 35, 45);
        SetResistance(ResistanceType.Poison, 45, 55);
        SetResistance(ResistanceType.Energy, 25, 35);

        SetSkill(SkillName.MagicResist, 40, 70);
        SetSkill(SkillName.Tactics, 70, 90);
        SetSkill(SkillName.Wrestling, 80, 95);

        Fame = 2500;
        Karma = -2500;

        // SetSpecialAbility(SpecialAbility.ColossalRage): no counterpart, dropped.
    }

    public override string CorpseName => "a greater earth elemental corpse";
    public override string DefaultName => "a greater earth elemental";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.08 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 1);
    }
}

[SerializationGenerator(0, false)]
public partial class MudElemental : EarthElemental
{
    [Constructible]
    public MudElemental()
    {
        Hue = 542;

        SetStr(400, 550);
        SetHits(650, 850);
        SetDamage(17, 19);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Fire, 50);

        SetResistance(ResistanceType.Physical, 50, 65);
        SetResistance(ResistanceType.Fire, 55, 65);
        SetResistance(ResistanceType.Cold, 45, 50);
        SetResistance(ResistanceType.Poison, 55, 65);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.MagicResist, 100);
        SetSkill(SkillName.Tactics, 100);
        SetSkill(SkillName.Wrestling, 120);
        SetSkill(SkillName.Parry, 120);

        Fame = 3500;
        Karma = -3500;

        PackItem(new FertileDirt());
        PackItem(new ExecutionersCap());

        // SetSpecialAbility(SpecialAbility.ColossalRage): no counterpart, dropped.
    }

    public override string CorpseName => "a mud elemental corpse";
    public override string DefaultName => "a mud elemental";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.08 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

// ---------------------------------------------------------------------------------------------------------
// Level 2

[SerializationGenerator(0, false)]
public partial class GreaterAirElemental : AirElemental
{
    [Constructible]
    public GreaterAirElemental()
    {
        SetStr(250, 315);
        SetHits(800, 900);
        SetDamage(15, 17);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Cold, 40);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 75, 85);
        SetResistance(ResistanceType.Fire, 55, 65);
        SetResistance(ResistanceType.Cold, 55, 65);
        SetResistance(ResistanceType.Poison, 55, 65);
        SetResistance(ResistanceType.Energy, 45, 55);

        SetSkill(SkillName.MagicResist, 100, 120);
        SetSkill(SkillName.Tactics, 100, 120);
        SetSkill(SkillName.Wrestling, 100, 120);
        SetSkill(SkillName.Magery, 100, 120);
        SetSkill(SkillName.EvalInt, 100, 120);

        Fame = 4500;
        Karma = -4500;
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.10 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class MoltenEarthElemental : GreaterEarthElemental
{
    private static MonsterAbility[] _abilities = { MonsterAbilities.FireBreath };

    [Constructible]
    public MoltenEarthElemental()
    {
        Hue = 442;

        SetStr(400, 550);
        SetHits(1200, 1400);
        SetDamage(17, 19);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Fire, 50);

        SetResistance(ResistanceType.Physical, 50, 70);
        SetResistance(ResistanceType.Fire, 50, 60);
        SetResistance(ResistanceType.Cold, 40, 50);
        SetResistance(ResistanceType.Poison, 55, 65);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.MagicResist, 100);
        SetSkill(SkillName.Tactics, 100);
        SetSkill(SkillName.Wrestling, 120);
        SetSkill(SkillName.Parry, 120);

        Fame = 5000;
        Karma = -5000;

        // SetSpecialAbility(SpecialAbility.SearingWounds): no counterpart, dropped.
        // SetSpecialAbility(SpecialAbility.DragonBreath): FireBreath, below.
    }

    public override string CorpseName => "a molten earth elemental corpse";
    public override string DefaultName => "a molten earth elemental";

    public override MonsterAbility[] GetMonsterAbilities() => _abilities;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.10 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class LesserFlameElemental : BaseCreature
{
    private static MonsterAbility[] _abilities = { MonsterAbilities.FireBreath };

    [Constructible]
    public LesserFlameElemental() : base(AIType.AI_Mage, FightMode.Closest)
    {
        Body = 15;
        BaseSoundID = 838;
        Hue = 1161;

        SetStr(420, 460);
        SetDex(160, 210);
        SetInt(120, 190);

        SetHits(700, 800);
        SetMana(1000, 1200);

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

        Fame = 3500;
        Karma = -3500;

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

    public override int TreasureMapLevel => 2;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.10 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class LesserWindElemental : BaseCreature
{
    [Constructible]
    public LesserWindElemental() : base(AIType.AI_Mage, FightMode.Closest)
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

        Fame = 3500;
        Karma = -3500;
    }

    public override string CorpseName => "a wind elemental corpse";
    public override string DefaultName => "a wind elemental";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.10 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

// ---------------------------------------------------------------------------------------------------------
// Level 3

[SerializationGenerator(0, false)]
public partial class EternalGazer : ElderGazer
{
    [Constructible]
    public EternalGazer()
    {
        SetStr(450, 600);
        SetDex(125, 165);
        SetInt(350, 550);

        SetHits(7250, 7600);
        SetMana(2500, 2900);
        SetDamage(18, 21);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Energy, 50);

        SetResistance(ResistanceType.Physical, 65, 75);
        SetResistance(ResistanceType.Fire, 60, 70);
        SetResistance(ResistanceType.Cold, 70, 75);
        SetResistance(ResistanceType.Poison, 65, 75);
        SetResistance(ResistanceType.Energy, 65, 75);

        SetSkill(SkillName.MagicResist, 125, 140);
        SetSkill(SkillName.Tactics, 115, 130);
        SetSkill(SkillName.Wrestling, 110, 130);
        SetSkill(SkillName.Anatomy, 75, 90);
        SetSkill(SkillName.Magery, 120, 130);
        SetSkill(SkillName.EvalInt, 120, 130);
    }

    public override string CorpseName => "an eternal gazer corpse";
    public override string DefaultName => "an eternal gazer";

    public override MeatType MeatType => MeatType.Ribs;
    public override int Meat => 1;

    public override void AlterSpellDamageFrom(Mobile from, ref int damage)
    {
        if (from is BaseCreature bc && (bc.Summoned || bc.Controlled))
        {
            damage /= 2;
        }

        base.AlterSpellDamageFrom(from, ref damage);
    }

    public override void AlterMeleeDamageFrom(Mobile from, ref int damage)
    {
        if (from is BaseCreature bc && (bc.Summoned || bc.Controlled))
        {
            damage /= 2;
        }

        base.AlterMeleeDamageFrom(from, ref damage);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.15 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 3);
    }
}

/// <summary>
///     The mana taint the three corrupted mages inflict on spellcasters who hurt them. ServUO carries this as
///     one static table and timer per class; each class below owns one instance, which keeps the tables separate
///     exactly as they were. Every 1.5 s: 30-40 mana taken, a tenth of it (at least 1) dealt as direct damage,
///     then a coin flip to end it.
/// </summary>
public sealed class CorruptedManaTaint
{
    private readonly Dictionary<Mobile, TimerExecutionToken> _table = new();

    public bool IsUnderEffects(Mobile from) => from != null && _table.ContainsKey(from);

    public void DoEffects(Mobile from, Mobile mob)
    {
        if (from == null || _table.ContainsKey(from))
        {
            return;
        }

        Timer.StartTimer(TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(1.5), () => SapMana(from, mob), out var token);
        _table[from] = token;

        from.SendLocalizedMessage(1151482); // Your mana has been tainted!
        from.SendLocalizedMessage(1151485); // Your mana is being diverted.
    }

    private void SapMana(Mobile from, Mobile mob)
    {
        if (!IsUnderEffects(from))
        {
            return;
        }

        if (mob.Alive && from.Alive)
        {
            from.SendLocalizedMessage(1151484); // You feel extra mana being drawn from you.
            from.SendLocalizedMessage(1151481); // Channeling the corrupted mana has damaged you!

            var toSap = Math.Min(from.Mana, Utility.RandomMinMax(30, 40));
            from.Mana -= toSap;

            AOS.Damage(from, mob, Math.Max(1, toSap / 10), false, 0, 0, 0, 0, 0, 0, 100);

            if (0.5 > Utility.RandomDouble())
            {
                EndEffects(from);
            }
        }
        else
        {
            EndEffects(from);
        }
    }

    public void EndEffects(Mobile from)
    {
        if (from != null && _table.Remove(from, out var token))
        {
            token.Cancel();

            from.SendLocalizedMessage(1151486); // Your mana is no longer being diverted.
            from.SendLocalizedMessage(1151483); // Your mana is no longer corrupted.
        }
    }
}

[SerializationGenerator(0, false)]
public partial class BurningMage : BaseCreature
{
    private static readonly CorruptedManaTaint _taint = new();
    private static MonsterAbility[] _abilities = { MonsterAbilities.FireBreath };

    [Constructible]
    public BurningMage() : base(AIType.AI_Mage, FightMode.Weakest)
    {
        Name = NameList.RandomName("male");
        Title = "the burning";
        SetStr(100, 125);

        Body = 0x190;
        Hue = 1281;

        SetHits(3000);
        SetMana(600, 800);
        SetDamage(10, 15);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Fire, 50);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 50, 60);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.MagicResist, 125, 140);
        SetSkill(SkillName.Tactics, 100, 120);
        SetSkill(SkillName.Wrestling, 110, 130);
        SetSkill(SkillName.Magery, 120, 130);
        SetSkill(SkillName.EvalInt, 120, 130);

        AddItem(new Robe(1156));
        AddItem(new Sandals());

        PackReg(31);

        Utility.AssignRandomHair(this);

        Fame = 22000;
        Karma = -22000;
    }

    public override string CorpseName => "a burning mage corpse";

    public override MonsterAbility[] GetMonsterAbilities() => _abilities;

    public override bool CanRummageCorpses => true;
    public override bool AlwaysMurderer => true;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.33 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal(4));
        }
    }

    public override void OnDamagedBySpell(Mobile from, int damage)
    {
        base.OnDamagedBySpell(from, damage);

        if (!IsUnderEffects(from) && 0.50 > Utility.RandomDouble())
        {
            DoEffects(from);
        }
    }

    public static bool IsUnderEffects(Mobile from) => _taint.IsUnderEffects(from);

    public void DoEffects(Mobile from) => _taint.DoEffects(from, this);

    public static void EndEffects(Mobile from) => _taint.EndEffects(from);

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
        AddLoot(LootPack.HighScrolls, Utility.RandomMinMax(5, 20));
    }
}

[SerializationGenerator(0, false)]
public partial class CrazedMage : BaseCreature
{
    private static readonly CorruptedManaTaint _taint = new();

    // ServUO: AIType.AI_Mystic. ModernUO has no Mysticism AI; AI_Mage, logged in notes/cc4-shame.md.
    [Constructible]
    public CrazedMage() : base(AIType.AI_Mage, FightMode.Weakest)
    {
        Name = NameList.RandomName("male");
        Title = "the crazed";

        Body = 0x190;
        SetStr(225, 400);

        SetHits(3500);
        SetMana(600, 800);
        SetDamage(15, 21);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 60, 80);
        SetResistance(ResistanceType.Fire, 50, 60);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.MagicResist, 125, 140);
        SetSkill(SkillName.Tactics, 100, 120);
        SetSkill(SkillName.Wrestling, 140);
        SetSkill(SkillName.Anatomy, 100, 120);
        SetSkill(SkillName.Magery, 100, 110);
        SetSkill(SkillName.EvalInt, 100, 110);

        AddItem(new Robe(1157));
        AddItem(new Sandals());

        Utility.AssignRandomHair(this);
        Hue = Race.RandomSkinHue();

        Fame = 15000;
        Karma = -15000;
    }

    public override string CorpseName => "a crazed corpse";

    public override bool CanRummageCorpses => true;
    public override bool AlwaysMurderer => true;

    public override void OnDamagedBySpell(Mobile from, int damage)
    {
        base.OnDamagedBySpell(from, damage);

        if (!IsUnderEffects(from) && 0.50 > Utility.RandomDouble())
        {
            DoEffects(from);
        }
    }

    public static bool IsUnderEffects(Mobile from) => _taint.IsUnderEffects(from);

    public void DoEffects(Mobile from) => _taint.DoEffects(from, this);

    public static void EndEffects(Mobile from) => _taint.EndEffects(from);

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.33 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal(5));
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class CorruptedMage : EvilMage
{
    private static readonly CorruptedManaTaint _taint = new();

    [Constructible]
    public CorruptedMage()
    {
        Title = "the corrupted mage";

        SetStr(150, 170);
        SetInt(100, 120);
        SetDex(110, 120);

        SetHits(1200, 1250);
        SetMana(800, 900);
        SetDamage(14, 17);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 55, 70);
        SetResistance(ResistanceType.Fire, 70, 80);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 65, 75);

        SetSkill(SkillName.MagicResist, 115, 120);
        SetSkill(SkillName.Tactics, 110, 120);
        SetSkill(SkillName.Wrestling, 100, 110);
        SetSkill(SkillName.Magery, 120, 130);
        SetSkill(SkillName.EvalInt, 120, 130);
        SetSkill(SkillName.Meditation, 100, 110);
    }

    public override string CorpseName => "a corrupted mage corpse";

    public override bool CanRummageCorpses => true;
    public override bool AlwaysMurderer => true;

    public override void OnDamagedBySpell(Mobile from, int damage)
    {
        base.OnDamagedBySpell(from, damage);

        if (!IsUnderEffects(from) && 0.10 > Utility.RandomDouble())
        {
            DoEffects(from);
        }
    }

    public static bool IsUnderEffects(Mobile from) => _taint.IsUnderEffects(from);

    public void DoEffects(Mobile from) => _taint.DoEffects(from, this);

    public static void EndEffects(Mobile from) => _taint.EndEffects(from);

    public override int TreasureMapLevel => 2;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.33 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal(3));
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class VileMage : CorruptedMage
{
    [Constructible]
    public VileMage()
    {
        Title = "the vile mage";

        SetStr(150, 170);
        SetInt(150, 170);
        SetDex(100, 110);

        SetHits(500, 900);
        SetMana(550, 600);
        SetDamage(11, 13);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 55, 70);
        SetResistance(ResistanceType.Fire, 55, 65);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 55, 65);
        SetResistance(ResistanceType.Energy, 65, 75);

        SetSkill(SkillName.MagicResist, 110, 115);
        SetSkill(SkillName.Tactics, 110, 115);
        SetSkill(SkillName.Wrestling, 100, 110);
        SetSkill(SkillName.Magery, 110, 115);
        SetSkill(SkillName.EvalInt, 115, 125);
    }

    public override string CorpseName => "a vile mage corpse";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.33 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal(3));
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class ChaosVortex : BaseCreature
{
    [Constructible]
    public ChaosVortex() : base(AIType.AI_Melee, FightMode.Weakest)
    {
        Body = 164;
        Hue = 34212;

        SetStr(450);
        SetDex(200);
        SetInt(100);

        SetHits(27000);
        SetMana(0);

        SetDamage(21, 23);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Fire, 20);
        SetDamageType(ResistanceType.Cold, 20);
        SetDamageType(ResistanceType.Poison, 20);
        SetDamageType(ResistanceType.Energy, 20);

        SetResistance(ResistanceType.Physical, 65, 75);
        SetResistance(ResistanceType.Fire, 65, 75);
        SetResistance(ResistanceType.Cold, 65, 75);
        SetResistance(ResistanceType.Poison, 65, 75);
        SetResistance(ResistanceType.Energy, 65, 75);

        SetSkill(SkillName.MagicResist, 100, 110);
        SetSkill(SkillName.Tactics, 110, 130);
        SetSkill(SkillName.Wrestling, 124, 140);

        Fame = 22500;
        Karma = -22500;
    }

    public override string CorpseName => "a chaos vortex corpse";
    public override string DefaultName => "a chaos vortex";

    public override int GetAngerSound() => 0x15;

    public override int GetAttackSound() => 0x28;

    public override bool AlwaysMurderer => true;
    public override bool BleedImmune => true;
    public override Poison PoisonImmune => Poison.Lethal;

    private DateTime _nextTeleport;

    public override void AlterSpellDamageFrom(Mobile from, ref int damage)
    {
        if (from is BaseCreature bc && (bc.Summoned || bc.Controlled))
        {
            damage /= 2;
        }

        if (_nextTeleport < Core.Now)
        {
            DoTeleport(from);
        }

        base.AlterSpellDamageFrom(from, ref damage);
    }

    public override void AlterMeleeDamageFrom(Mobile from, ref int damage)
    {
        if (from is BaseCreature bc && (bc.Summoned || bc.Controlled))
        {
            damage /= 2;
        }

        if (_nextTeleport < Core.Now)
        {
            DoTeleport(from);
        }

        base.AlterMeleeDamageFrom(from, ref damage);
    }

    public void DoTeleport(Mobile m) => VortexTeleport.Pull(this, m, ref _nextTeleport);

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.33 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal(5));
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
    }
}

/// <summary>
///     The vortices' pull: when hit from more than a tile away, the attacker is moved to a spawnable tile beside
///     the vortex with particles at both ends and sound 0x1FE, then not again for 30-60 s. ServUO has this
///     body twice (ChaosVortex, UnboundEnergyVortex); it is one static here.
/// </summary>
public static class VortexTeleport
{
    public static void Pull(BaseCreature vortex, Mobile m, ref DateTime nextTeleport)
    {
        if (!vortex.InRange(m.Location, 1))
        {
            var p = Point3D.Zero;

            for (var i = 0; i < 25; i++)
            {
                var x = Utility.RandomMinMax(vortex.X - 1, vortex.X + 1);
                var y = Utility.RandomMinMax(vortex.Y - 1, vortex.Y + 1);
                var z = vortex.Map.GetAverageZ(x, y);

                if (vortex.Map.CanSpawnMobile(x, y, z) && (x != vortex.X || y != vortex.Y))
                {
                    p = new Point3D(x, y, z);
                    break;
                }
            }

            if (p == Point3D.Zero)
            {
                p = vortex.Location;
            }

            var from = m.Location;

            Effects.SendLocationParticles(EffectItem.Create(from, m.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
            Effects.SendLocationParticles(EffectItem.Create(p, m.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);

            m.MoveToWorld(p, vortex.Map);

            m.PlaySound(0x1FE);
        }

        nextTeleport = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 60));
    }
}

[SerializationGenerator(0, false)]
public partial class UnboundEnergyVortex : BaseCreature
{
    [Constructible]
    public UnboundEnergyVortex() : base(AIType.AI_Melee, FightMode.Weakest)
    {
        Body = 13;

        SetStr(450);
        SetDex(200);
        SetInt(100);

        SetHits(20000);
        SetMana(0);

        SetDamage(21, 23);

        SetDamageType(ResistanceType.Physical, 0);
        SetDamageType(ResistanceType.Energy, 100);

        SetResistance(ResistanceType.Physical, 65, 75);
        SetResistance(ResistanceType.Fire, 65, 75);
        SetResistance(ResistanceType.Cold, 65, 75);
        SetResistance(ResistanceType.Poison, 65, 75);
        SetResistance(ResistanceType.Energy, 100);

        SetSkill(SkillName.MagicResist, 100, 110);
        SetSkill(SkillName.Tactics, 110, 130);
        SetSkill(SkillName.Wrestling, 124, 140);

        Fame = 22500;
        Karma = -22500;
    }

    public override string CorpseName => "an unbound energy vortex corpse";
    public override string DefaultName => "an unbound energy vortex";

    public override bool AlwaysMurderer => true;
    public override bool BleedImmune => true;
    public override Poison PoisonImmune => Poison.Lethal;

    public override int GetAngerSound() => 0x15;

    public override int GetAttackSound() => 0x28;

    private DateTime _nextTeleport;

    public override void AlterSpellDamageFrom(Mobile from, ref int damage)
    {
        if (from is BaseCreature bc && (bc.Summoned || bc.Controlled))
        {
            damage /= 2;
        }

        if (_nextTeleport < Core.Now)
        {
            DoTeleport(from);
        }

        base.AlterSpellDamageFrom(from, ref damage);
    }

    public override void AlterMeleeDamageFrom(Mobile from, ref int damage)
    {
        if (from is BaseCreature bc && (bc.Summoned || bc.Controlled))
        {
            damage /= 2;
        }

        if (_nextTeleport < Core.Now)
        {
            DoTeleport(from);
        }

        base.AlterMeleeDamageFrom(from, ref damage);
    }

    public void DoTeleport(Mobile m) => VortexTeleport.Pull(this, m, ref _nextTeleport);

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.33 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal(5));
        }

        if (0.2 > Utility.RandomDouble())
        {
            c.DropItem(new VoidCore());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class DiseasedBloodElemental : BloodElemental
{
    private static MonsterAbility[] _abilities = { MonsterAbilities.DrainLifeAttack };

    [Constructible]
    public DiseasedBloodElemental()
    {
        Body = 0x9F;
        Hue = 1779;

        SetStr(650, 750);
        SetDex(70, 80);
        SetInt(300, 400);

        SetHits(2500, 2700);
        SetMana(1400, 1600);

        SetDamage(19, 27);

        SetDamageType(ResistanceType.Poison, 50);
        SetDamageType(ResistanceType.Energy, 50);

        SetResistance(ResistanceType.Physical, 65, 75);
        SetResistance(ResistanceType.Fire, 55, 65);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.MagicResist, 110, 125);
        SetSkill(SkillName.Tactics, 130, 140);
        SetSkill(SkillName.Wrestling, 120, 140);
        SetSkill(SkillName.Poisoning, 100);
        SetSkill(SkillName.Magery, 110, 120);
        SetSkill(SkillName.EvalInt, 115, 130);
        SetSkill(SkillName.Meditation, 130, 155);
        SetSkill(SkillName.DetectHidden, 80.0);
        SetSkill(SkillName.Parry, 90.0, 100.0);

        PackReg(7, 11);

        Fame = 8500;
        Karma = -8500;

        // SetSpecialAbility(SpecialAbility.LifeLeech): DrainLifeAttack, above.
    }

    public override string CorpseName => "a diseased blood elemental";
    public override string DefaultName => "a diseased blood elemental";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override MonsterAbility[] GetMonsterAbilities() => _abilities;

    public override bool AutoDispel => true;
    public override double AutoDispelChance => 1.0;
    public override int TreasureMapLevel => 5;
    // TreasureMapChance => 1.0 on ServUO: no counterpart here, the stock chance applies (notes/cc4-shame.md).
    public override Poison HitPoison => Poison.Lethal;
    public override Poison PoisonImmune => Poison.DeadlyParasitic; // ServUO's Poison.Parasitic is GetPoison("DeadlyParasitic")

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.33 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal(5));
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
        AddLoot(LootPack.HighScrolls, Utility.RandomMinMax(1, 8));
    }
}

[SerializationGenerator(0, false)]
public partial class GreaterWaterElemental : WaterElemental
{
    [Constructible]
    public GreaterWaterElemental()
    {
        SetStr(400, 500);
        SetDex(150, 160);
        SetInt(120, 140);

        SetHits(500, 600);
        SetMana(600, 700);

        SetDamage(14, 16);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Cold, 50);

        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 70, 80);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.MagicResist, 100, 110);
        SetSkill(SkillName.Tactics, 90, 110);
        SetSkill(SkillName.Wrestling, 90, 110);
        SetSkill(SkillName.Magery, 90, 110);
        SetSkill(SkillName.EvalInt, 90, 100);

        Fame = 3500;
        Karma = -3500;
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 1);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.10 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }
}

[SerializationGenerator(0, false)]
public partial class ShameGreaterPoisonElemental : PoisonElemental
{
    [Constructible]
    public ShameGreaterPoisonElemental()
    {
        Hue = 32854;

        SetStr(400, 500);
        SetDex(170, 175);
        SetInt(400, 450);

        SetHits(950, 1050);

        SetDamage(16, 19);

        SetDamageType(ResistanceType.Physical, 10);
        SetDamageType(ResistanceType.Poison, 90);

        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 50, 60);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.MagicResist, 110, 120);
        SetSkill(SkillName.Tactics, 90, 120);
        SetSkill(SkillName.Wrestling, 100, 115);
        SetSkill(SkillName.Magery, 90, 110);
        SetSkill(SkillName.EvalInt, 90, 100);
        SetSkill(SkillName.Meditation, 100, 120);
        SetSkill(SkillName.DetectHidden, 85.1);
        SetSkill(SkillName.Parry, 80, 100);
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 1);
        AddLoot(LootPack.FilthyRich, 1);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.10 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal(5));
        }
    }
}

[SerializationGenerator(0, false)]
public partial class GreaterBloodElemental : BloodElemental
{
    [Constructible]
    public GreaterBloodElemental()
    {
        SetStr(500, 600);
        SetDex(60, 90);
        SetInt(230, 350);

        SetHits(1350, 1500);
        SetHits(900, 1000); // ServUO sets hits twice; the second call wins there too.

        SetDamage(17, 27);

        SetDamageType(ResistanceType.Poison, 50);
        SetDamageType(ResistanceType.Energy, 50);

        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 40, 50);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.MagicResist, 115, 120);
        SetSkill(SkillName.Tactics, 100, 120);
        SetSkill(SkillName.Wrestling, 110, 120);
        SetSkill(SkillName.Magery, 80, 100);
        SetSkill(SkillName.EvalInt, 110, 120);
        SetSkill(SkillName.Meditation, 120, 140);
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 1);
        AddLoot(LootPack.FilthyRich, 1);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.10 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal(5));
        }
    }
}

[SerializationGenerator(0, false)]
public partial class ShameEarthElemental : EarthElemental
{
    [Constructible]
    public ShameEarthElemental()
    {
        SetHits(300, 400);
        SetDamage(11, 13);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 30, 35);
        SetResistance(ResistanceType.Fire, 25, 30);
        SetResistance(ResistanceType.Cold, 25, 30);
        SetResistance(ResistanceType.Poison, 25, 30);
        SetResistance(ResistanceType.Energy, 20, 25);

        SetSkill(SkillName.MagicResist, 65, 85);
        SetSkill(SkillName.Tactics, 65, 90);
        SetSkill(SkillName.Wrestling, 80, 85);

        Fame = 3500;
        Karma = -3500;
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.08 > Utility.RandomDouble() && Region.Find(c.Location, c.Map).IsPartOf("Shame"))
        {
            c.DropItem(new ShameCrystal());
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 1);
    }
}

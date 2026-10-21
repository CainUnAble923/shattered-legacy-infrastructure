// cc-P364 (F-24): the League hunt bestiary, batch 2: the mid-level culling packs and the Tunnelmaw hunt target. Brine
// Lurker (Vert-I-Go's kuo-toa), Moonmaw (EmuResource's werewolf), Thornwisp (EmuResource's forest spirit), Pale Drifter
// (Kalamarr's shadow), Slagheart (Kalamarr's lava golem), Glasswarden (EmuResource's crystal golem), Tunnelmaw (Kalamarr's
// umber hulk). Each drops its part (PackItem) and sits in ClusterFLeagueBestiary's tables. Numbers are first proposals
// scaled from the nearest pinned creature (notes, Part C table).
//
// Bodies 2078 (the werewolf) and 2081 (the lava golem) are in anim7's animal slots (low-layout .vd files), so the
// server types them Animal (ShardBodyTypes); the rest are Monster.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>Base: Lizardman (Reptile/Melee/Lizardman.cs) up a step, on Vert-I-Go's kuo-toa body 2077; swims; its spear strikes cold.</summary>
[SerializationGenerator(0, false)]
public partial class BrineLurker : BaseCreature
{
    [Constructible]
    public BrineLurker() : base(AIType.AI_Melee)
    {
        Name = "a brine lurker";
        Body = 2077;
        BaseSoundID = 417; // the lizardman's

        SetStr(85, 100);
        SetDex(70, 85);
        SetInt(40, 55);
        SetHits(55, 70);
        SetDamage(6, 10);
        SetDamageType(ResistanceType.Physical, 70);
        SetDamageType(ResistanceType.Cold, 30);
        SetResistance(ResistanceType.Physical, 25, 30);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetResistance(ResistanceType.Energy, 10, 20);
        SetSkill(SkillName.MagicResist, 40.1, 55.0);
        SetSkill(SkillName.Tactics, 55.1, 70.0);
        SetSkill(SkillName.Wrestling, 55.1, 70.0);

        Fame = 1500;
        Karma = -1500;
        VirtualArmor = 28;

        CanSwim = true;

        PackItem(new LurkerGills());
        PackItem(new RawFishSteak(2));
    }

    public override string CorpseName => "a brine lurker corpse";
    public override bool CanRummageCorpses => true;
    public override int Meat => 1;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

/// <summary>Base: DireWolf and Troll on EmuResource's werewolf body 2078 (an animal slot): a woodland pack whose bite bleeds.</summary>
[SerializationGenerator(0, false)]
public partial class Moonmaw : BaseCreature
{
    [Constructible]
    public Moonmaw() : base(AIType.AI_Melee)
    {
        Name = "a moonmaw";
        Body = 2078;
        BaseSoundID = 0xE5; // the dire wolf's

        SetStr(100, 120);
        SetDex(90, 110);
        SetInt(30, 45);
        SetHits(70, 85);
        SetDamage(8, 13);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 25, 35);
        SetResistance(ResistanceType.Fire, 10, 20);
        SetResistance(ResistanceType.Cold, 15, 25);
        SetResistance(ResistanceType.Poison, 10, 20);
        SetResistance(ResistanceType.Energy, 10, 20);
        SetSkill(SkillName.MagicResist, 45.1, 60.0);
        SetSkill(SkillName.Tactics, 60.1, 75.0);
        SetSkill(SkillName.Wrestling, 65.1, 80.0);

        Fame = 2500;
        Karma = -2500;
        VirtualArmor = 30;

        PackItem(new MoonmawPelt());
    }

    public override string CorpseName => "a moonmaw corpse";
    public override int Meat => 2;
    public override int Hides => 10;
    public override PackInstinct PackInstinct => PackInstinct.Canine;
    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
    }
}

/// <summary>Base: Reaper (Plant/Magic/Reaper.cs) cut down, on EmuResource's forest spirit body 2079: a caster whose touch poisons.</summary>
[SerializationGenerator(0, false)]
public partial class Thornwisp : BaseCreature
{
    [Constructible]
    public Thornwisp() : base(AIType.AI_Mage)
    {
        Name = "a thornwisp";
        Body = 2079;
        BaseSoundID = 466; // the wisp's

        SetStr(60, 80);
        SetDex(60, 75);
        SetInt(80, 100);
        SetHits(50, 65);
        SetDamage(6, 10);
        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Poison, 50);
        SetResistance(ResistanceType.Physical, 20, 30);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 30, 40);
        SetSkill(SkillName.EvalInt, 45.1, 60.0);
        SetSkill(SkillName.Magery, 45.1, 60.0);
        SetSkill(SkillName.MagicResist, 45.1, 60.0);
        SetSkill(SkillName.Tactics, 35.1, 50.0);
        SetSkill(SkillName.Wrestling, 35.1, 50.0);

        Fame = 2500;
        Karma = -2500;
        VirtualArmor = 26;

        PackItem(new ThornwispSpark());
        PackItem(new Log(5));
        PackReg(3);
    }

    public override string CorpseName => "a thornwisp corpse";
    public override Poison HitPoison => Poison.Regular;
    public override double HitPoisonChance => 0.4;
    public override bool BleedImmune => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
        AddLoot(LootPack.LowScrolls);
    }
}

/// <summary>Base: Shade (Humanoid/Magic/Shade.cs) as a melee creature on Kalamarr's shadow body 2080: cold touch, no poison troubles it.</summary>
[SerializationGenerator(0, false)]
public partial class PaleDrifter : BaseCreature
{
    [Constructible]
    public PaleDrifter() : base(AIType.AI_Melee)
    {
        Name = "a pale drifter";
        Body = 2080;
        BaseSoundID = 0x482; // the shade's

        SetStr(60, 80);
        SetDex(80, 100);
        SetInt(40, 60);
        SetHits(50, 65);
        SetDamage(7, 11);
        SetDamageType(ResistanceType.Physical, 40);
        SetDamageType(ResistanceType.Cold, 60);
        SetResistance(ResistanceType.Physical, 20, 30);
        SetResistance(ResistanceType.Fire, 10, 20);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 20, 30);
        SetSkill(SkillName.MagicResist, 55.1, 70.0);
        SetSkill(SkillName.Tactics, 50.1, 65.0);
        SetSkill(SkillName.Wrestling, 50.1, 65.0);

        Fame = 3000;
        Karma = -3000;
        VirtualArmor = 28;

        PackItem(new DrifterEctoplasm());
    }

    public override string CorpseName => "a pale drifter corpse";
    public override Poison PoisonImmune => Poison.Lethal;
    public override bool BleedImmune => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

/// <summary>Base: Golem (Misc/Melee/Golem.cs) scaled down and our LavaElemental, on Kalamarr's lava golem body 2081 (an animal slot): burns to the touch.</summary>
[SerializationGenerator(0, false)]
public partial class Slagheart : BaseCreature
{
    [Constructible]
    public Slagheart() : base(AIType.AI_Melee)
    {
        Name = "a slagheart";
        Body = 2081;
        BaseSoundID = 268; // the earth elemental's

        SetStr(150, 180);
        SetDex(40, 55);
        SetInt(30, 45);
        SetHits(95, 115);
        SetDamage(10, 15);
        SetDamageType(ResistanceType.Physical, 40);
        SetDamageType(ResistanceType.Fire, 60);
        SetResistance(ResistanceType.Physical, 40, 50);
        SetResistance(ResistanceType.Fire, 70, 80);
        SetResistance(ResistanceType.Cold, 0, 5);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 20, 30);
        SetSkill(SkillName.MagicResist, 60.1, 75.0);
        SetSkill(SkillName.Tactics, 60.1, 75.0);
        SetSkill(SkillName.Wrestling, 60.1, 75.0);

        Fame = 4000;
        Karma = -4000;
        VirtualArmor = 40;

        PackItem(new SlagCore());
        PackItem(new SulfurousAsh(5));
    }

    public override string CorpseName => "a slagheart corpse";
    public override Poison PoisonImmune => Poison.Lethal;
    public override bool BleedImmune => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Gems);
    }
}

/// <summary>Base: CrystalElemental (AOS/CrystalElemental.cs) as a melee creature on EmuResource's crystal golem body 2082.</summary>
[SerializationGenerator(0, false)]
public partial class Glasswarden : BaseCreature
{
    [Constructible]
    public Glasswarden() : base(AIType.AI_Melee)
    {
        Name = "a glasswarden";
        Body = 2082;
        BaseSoundID = 278; // the crystal elemental's

        SetStr(140, 170);
        SetDex(50, 65);
        SetInt(50, 70);
        SetHits(95, 115);
        SetDamage(10, 15);
        SetDamageType(ResistanceType.Physical, 70);
        SetDamageType(ResistanceType.Energy, 30);
        SetResistance(ResistanceType.Physical, 45, 55);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 50, 60);
        SetSkill(SkillName.MagicResist, 70.1, 85.0);
        SetSkill(SkillName.Tactics, 65.1, 80.0);
        SetSkill(SkillName.Wrestling, 65.1, 80.0);

        Fame = 4500;
        Karma = -4500;
        VirtualArmor = 44;

        PackItem(new GlasswardenShard());
    }

    public override string CorpseName => "a glasswarden corpse";
    public override Poison PoisonImmune => Poison.Lethal;
    public override bool BleedImmune => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Gems);
    }
}

/// <summary>Base: Cyclops (Humanoid/Melee/Cyclops.cs) cut down, on Kalamarr's umber hulk body 2083: a hunt target whose claws bleed.</summary>
[SerializationGenerator(0, false)]
public partial class Tunnelmaw : BaseCreature
{
    [Constructible]
    public Tunnelmaw() : base(AIType.AI_Melee)
    {
        Name = "a tunnelmaw";
        Body = 2083;
        BaseSoundID = 397; // the scorpion's clicks

        SetStr(220, 260);
        SetDex(55, 70);
        SetInt(25, 40);
        SetHits(140, 170);
        SetDamage(12, 18);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 45, 55);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 20, 30);
        SetSkill(SkillName.MagicResist, 55.1, 70.0);
        SetSkill(SkillName.Tactics, 75.1, 90.0);
        SetSkill(SkillName.Wrestling, 75.1, 90.0);

        Fame = 5000;
        Karma = -5000;
        VirtualArmor = 46;

        PackItem(new TunnelmawClaw());
    }

    public override string CorpseName => "a tunnelmaw corpse";
    public override int Meat => 3;
    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Gems);
    }
}

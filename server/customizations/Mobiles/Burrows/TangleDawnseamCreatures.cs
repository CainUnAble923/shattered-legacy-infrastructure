// cc-P140 (F-45): the Tangle (tier 2, the Roots family: Rootheart Sap and Rootwood) and the Dawnseam (tier 2, the Stone
// family: Dawnstone Shards and Dawnstone ore). Normal spawns.
//
// The Rootling has no body of its own: the three Tree Fellow .vd files render 213 to 390 pixels tall (two to four
// tiles) and run 6 to 17 MB each, over the 5 MB rule, so the prompt's fallback applies: the nearest stock body with a
// hue, the Bogling (779, a walking plant) in a bark hue. Chase's call on the three files is in the notes.
//
// Numbers are first proposals scaled from the nearest pinned creature (notes, Part C table).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>Base: Bogling (Plant/Melee/Bogling.cs) at tier 2, on the stock bogling body 779 with a bark hue (the Tree Fellows fell to the size and file rules).</summary>
[SerializationGenerator(0, false)]
public partial class Rootling : BaseCreature
{
    public const int BarkHue = 1140;

    [Constructible]
    public Rootling() : base(AIType.AI_Melee)
    {
        Name = "a rootling";
        Body = 779;
        Hue = BarkHue;
        BaseSoundID = 422;

        SetStr(70, 90);
        SetDex(60, 80);
        SetInt(20, 40);
        SetHits(45, 60);
        SetDamage(5, 8);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 20, 25);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Cold, 15, 25);
        SetResistance(ResistanceType.Poison, 15, 25);
        SetResistance(ResistanceType.Energy, 15, 25);
        SetSkill(SkillName.MagicResist, 45.1, 60.0);
        SetSkill(SkillName.Tactics, 45.1, 60.0);
        SetSkill(SkillName.Wrestling, 45.1, 60.0);

        Fame = 450;
        Karma = -450;
        VirtualArmor = 24;

        PackItem(new RootheartSap());
        PackItem(new RootwoodLog(2));
    }

    public override string CorpseName => "a rootling corpse";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

/// <summary>Poison, and the Cure lesson. Base: Corpser (Plant/Melee/Corpser.cs) at tier 2, walking, on one of the two myconid bodies (2062 red, 2063 blue).</summary>
[SerializationGenerator(0, false)]
public partial class Sporecap : BaseCreature
{
    public const int RedBody = 2062;
    public const int BlueBody = 2063;

    [Constructible]
    public Sporecap() : base(AIType.AI_Melee)
    {
        Name = "a sporecap";
        Body = Utility.RandomBool() ? RedBody : BlueBody;
        BaseSoundID = 684;

        SetStr(60, 80);
        SetDex(40, 60);
        SetInt(20, 30);
        SetHits(50, 65);
        SetMana(0);
        SetDamage(5, 9);
        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Poison, 40);
        SetResistance(ResistanceType.Physical, 15, 20);
        SetResistance(ResistanceType.Fire, 10, 15);
        SetResistance(ResistanceType.Cold, 10, 20);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 10, 15);
        SetSkill(SkillName.MagicResist, 20.1, 30.0);
        SetSkill(SkillName.Tactics, 40.1, 55.0);
        SetSkill(SkillName.Wrestling, 40.1, 55.0);
        SetSkill(SkillName.Poisoning, 30.1, 50.0);

        Fame = 600;
        Karma = -600;
        VirtualArmor = 18;

        PackItem(new RootheartSap());
    }

    public override string CorpseName => "a sporecap corpse";
    public override Poison PoisonImmune => Poison.Lesser;
    public override Poison HitPoison => Poison.Lesser;
    public override double HitPoisonChance => 0.5;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

/// <summary>The Tangle's elite (the thorned hulk body 2064). Base: Quagmire and Reaper (Plant) at tier 2, with a bleed (thorns) through GetWeaponAbility.</summary>
[SerializationGenerator(0, false)]
public partial class Bramblehulk : BaseCreature
{
    [Constructible]
    public Bramblehulk() : base(AIType.AI_Melee)
    {
        Name = "a bramblehulk";
        Body = 2064;
        BaseSoundID = 352;

        SetStr(120, 150);
        SetDex(40, 60);
        SetInt(30, 50);
        SetHits(110, 130);
        SetDamage(8, 13);
        SetDamageType(ResistanceType.Physical, 80);
        SetDamageType(ResistanceType.Poison, 20);
        SetResistance(ResistanceType.Physical, 35, 45);
        SetResistance(ResistanceType.Fire, 10, 20);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 20, 30);
        SetSkill(SkillName.MagicResist, 55.1, 70.0);
        SetSkill(SkillName.Tactics, 55.1, 70.0);
        SetSkill(SkillName.Wrestling, 60.1, 75.0);
        SetSkill(SkillName.Anatomy, 30.1, 50.0);

        Fame = 2000;
        Karma = -2000;
        VirtualArmor = 32;

        PackItem(new RootheartSap(2));
        PackItem(new RootwoodLog(5));
    }

    public override string CorpseName => "a bramblehulk corpse";
    public override Poison PoisonImmune => Poison.Regular;
    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
    }
}

/// <summary>High physical resistance: bring magic. Base: EarthElemental (Elemental/Melee/EarthElemental.cs) at tier 2, on the cave elemental body 2065.</summary>
[SerializationGenerator(0, false)]
public partial class Dawnshard : BaseCreature
{
    [Constructible]
    public Dawnshard() : base(AIType.AI_Melee)
    {
        Name = "a dawnshard";
        Body = 2065;
        BaseSoundID = 268;

        SetStr(100, 130);
        SetDex(40, 60);
        SetInt(40, 60);
        SetHits(60, 80);
        SetDamage(7, 12);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 10, 20);
        SetResistance(ResistanceType.Cold, 10, 20);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetResistance(ResistanceType.Energy, 10, 20);
        SetSkill(SkillName.MagicResist, 45.1, 60.0);
        SetSkill(SkillName.Tactics, 50.1, 65.0);
        SetSkill(SkillName.Wrestling, 50.1, 65.0);

        Fame = 1500;
        Karma = -1500;
        VirtualArmor = 30;

        PackItem(new DawnstoneShards());
        PackItem(new DawnstoneOre(2));
    }

    public override string CorpseName => "a dawnshard corpse";
    public override bool BleedImmune => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
        AddLoot(LootPack.Gems);
    }
}

/// <summary>The Dawnseam's elite (the stone golem body 2066). Base: Golem (Misc/Melee/Golem.cs) and EarthElemental, at tier 2.</summary>
[SerializationGenerator(0, false)]
public partial class Seamguard : BaseCreature
{
    [Constructible]
    public Seamguard() : base(AIType.AI_Melee)
    {
        Name = "a seamguard";
        Body = 2066;
        BaseSoundID = 268;

        SetStr(180, 220);
        SetDex(40, 60);
        SetInt(50, 70);
        SetHits(130, 160);
        SetDamage(10, 15);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 10, 20);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 20, 30);
        SetSkill(SkillName.MagicResist, 60.1, 80.0);
        SetSkill(SkillName.Tactics, 60.1, 75.0);
        SetSkill(SkillName.Wrestling, 60.1, 75.0);

        Fame = 3000;
        Karma = -3000;
        VirtualArmor = 40;

        PackItem(new DawnstoneShards(2));
        PackItem(new DawnstoneOre(5));
        PackItem(new DawnstoneIngot(1));
    }

    public override string CorpseName => "a seamguard corpse";
    public override bool BleedImmune => true;
    public override Poison PoisonImmune => Poison.Lethal;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Gems);
    }
}

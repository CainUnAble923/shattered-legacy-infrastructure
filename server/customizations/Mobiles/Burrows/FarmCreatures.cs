// cc-P140 (F-45, F-35): the tutorial farm's creatures. Every one but the Burrow Hog is a TutorialCreature (cc-P80:
// engages only tutorial participants, Young protection ignored, NoKillAwards), and each drops a fixed, scripted loot on
// death (Chase, 2026-10-15: "tutorial creatures drop loot", so the looting lesson always works; a deviation from P80's
// "no loot", argued in the notes). NoKillAwards stays: a sheltered scripted kill should not pay fame or karma.
//
// Numbers are first proposals scaled from the nearest pinned creature of each role (notes, Part C table), cut to the
// strength of a new Young character with starter skills. Bodies are cc-P140's registrations (registry.csv kind body;
// ShardBodyTypes types them). Nothing here is OSI's; every number is a deviation for the overseer's register.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>Pests in the pumpkin rows. Base: Bogling (Plant/Melee/Bogling.cs) cut to a first melee lesson.</summary>
[SerializationGenerator(0, false)]
public partial class Gourdling : TutorialCreature
{
    [Constructible]
    public Gourdling()
    {
        Name = "a gourdling";
        Body = 2049;
        BaseSoundID = 422;

        SetStr(20);
        SetDex(25);
        SetInt(5);
        SetHits(18);
        SetDamage(2, 4);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 5, 10);
        SetSkill(SkillName.Wrestling, 20.0);
        SetSkill(SkillName.Tactics, 20.0);
        SetSkill(SkillName.MagicResist, 10.0);
    }

    public override string CorpseName => "a gourdling corpse";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(10));
        c.DropItem(new Pumpkin());
    }
}

/// <summary>
/// The ranged lesson: a flier that flees melee and circles back. Base: Eagle (Animals/Birds/Eagle.cs) cut down, on the
/// stock bird body 6 enlarged 1.5x (body 2071, ANIMAL 28 as the bird) with a black hue (Chase, 2026-10-15). Hit in melee
/// it flees for a few seconds (BaseCreature.BeginFlee), then its AI comes back.
/// </summary>
[SerializationGenerator(0, false)]
public partial class CropCrow : TutorialCreature
{
    public const int BlackHue = 1910;
    public static readonly System.TimeSpan FleeFor = System.TimeSpan.FromSeconds(4);

    [Constructible]
    public CropCrow()
    {
        Name = "a crop crow";
        Body = 2071;
        Hue = BlackHue;
        BaseSoundID = 0x1B;

        SetStr(12);
        SetDex(40);
        SetInt(8);
        SetHits(12);
        SetDamage(1, 3);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 5, 10);
        SetSkill(SkillName.Wrestling, 15.0);
        SetSkill(SkillName.Tactics, 15.0);
        SetSkill(SkillName.MagicResist, 10.0);
    }

    public override string CorpseName => "a crow corpse";
    public override bool CanFly => true;
    public override int Feathers => 10;
    public override MeatType MeatType => MeatType.Bird;
    public override int Meat => 1;

    public override void OnGotMeleeAttack(Mobile attacker, int damage)
    {
        base.OnGotMeleeAttack(attacker, damage);
        BeginFlee(FleeFor);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(6));
    }
}

/// <summary>
/// Enemies shoot back. Base: RatmanArcher (Humanoid/Melee/RatmanArcher.cs) cut to the farm, on the kobold archer body
/// 2048 (cc-P139) with Chase's rust red as hue 1258 (the closest hue in hues.mul; a frame recolour is on the sheet as the
/// alternative). Its bow plays the body's one attack action (BurrowScampTestBow, cc-P139).
/// </summary>
[SerializationGenerator(0, false)]
public partial class BurrowScampSlinger : TutorialCreature
{
    public const int RustHue = 1258;

    [Constructible]
    public BurrowScampSlinger() : base(AIType.AI_Archer)
    {
        Name = "a burrow scamp slinger";
        Body = 2048;
        Hue = RustHue;
        BaseSoundID = 437;

        SetStr(30);
        SetDex(60);
        SetInt(20);
        SetHits(22);
        SetDamage(3, 6);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 10, 15);
        SetSkill(SkillName.Archery, 40.0);
        SetSkill(SkillName.Anatomy, 20.0);
        SetSkill(SkillName.Tactics, 30.0);
        SetSkill(SkillName.Wrestling, 20.0);
        SetSkill(SkillName.MagicResist, 15.0);

        AddItem(new BurrowScampTestBow());
        PackItem(new Arrow(20));
    }

    public override string CorpseName => "a burrow scamp corpse";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(12));
    }
}

/// <summary>The pet lesson's target ("all kill"). Base: Orc (Humanoid/Melee/Orc.cs) cut to the farm.</summary>
[SerializationGenerator(0, false)]
public partial class CacklerScrapper : TutorialCreature
{
    [Constructible]
    public CacklerScrapper()
    {
        Name = "a cackler scrapper";
        Body = 2050;
        BaseSoundID = 0x45A;

        SetStr(40);
        SetDex(35);
        SetInt(10);
        SetHits(30);
        SetDamage(3, 6);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 10, 15);
        SetSkill(SkillName.Wrestling, 30.0);
        SetSkill(SkillName.Tactics, 30.0);
        SetSkill(SkillName.MagicResist, 15.0);
    }

    public override string CorpseName => "a cackler corpse";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(15));
    }
}

/// <summary>Bleed, then the healing lesson. As the Scrapper with a bleed attack (BleedAttack through GetWeaponAbility, the port recipe) and bandages in its drop.</summary>
[SerializationGenerator(0, false)]
public partial class CacklerSlasher : TutorialCreature
{
    [Constructible]
    public CacklerSlasher()
    {
        Name = "a cackler slasher";
        Body = 2051;
        BaseSoundID = 0x45A;

        SetStr(45);
        SetDex(45);
        SetInt(30);
        SetHits(34);
        SetMana(60);
        SetDamage(3, 7);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 10, 15);
        SetSkill(SkillName.Wrestling, 35.0);
        SetSkill(SkillName.Tactics, 35.0);
        SetSkill(SkillName.Anatomy, 30.0);
        SetSkill(SkillName.MagicResist, 15.0);
    }

    public override string CorpseName => "a cackler corpse";
    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(15));
        c.DropItem(new Bandage(3));
    }
}

/// <summary>The tougher pack member. Base: OrcCaptain (Humanoid/Melee/OrcCaptain.cs) cut to the farm.</summary>
[SerializationGenerator(0, false)]
public partial class CacklerVeteran : TutorialCreature
{
    [Constructible]
    public CacklerVeteran()
    {
        Name = "a cackler veteran";
        Body = 2052;
        BaseSoundID = 0x45A;

        SetStr(55);
        SetDex(40);
        SetInt(15);
        SetHits(45);
        SetDamage(4, 8);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 15, 20);
        SetSkill(SkillName.Wrestling, 40.0);
        SetSkill(SkillName.Tactics, 40.0);
        SetSkill(SkillName.MagicResist, 20.0);
    }

    public override string CorpseName => "a cackler corpse";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(20));
    }
}

/// <summary>The pack leader. Base: OrcishLord (Humanoid/Melee/OrcishLord.cs) cut to the farm.</summary>
[SerializationGenerator(0, false)]
public partial class CacklerPackleader : TutorialCreature
{
    [Constructible]
    public CacklerPackleader()
    {
        Name = "a cackler packleader";
        Body = 2053;
        BaseSoundID = 0x45A;

        SetStr(65);
        SetDex(45);
        SetInt(25);
        SetHits(60);
        SetDamage(5, 9);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 20, 25);
        SetResistance(ResistanceType.Fire, 10, 15);
        SetSkill(SkillName.Wrestling, 45.0);
        SetSkill(SkillName.Tactics, 45.0);
        SetSkill(SkillName.MagicResist, 25.0);
    }

    public override string CorpseName => "a cackler corpse";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(30));
        c.DropItem(new Bandage(5));
    }
}

/// <summary>
/// The carving lesson, and the Warren's hog. Base: Boar (Animals/Misc/Boar.cs) a little up, on the D&D boar body 2054
/// (an animal slot). A plain creature, not a tutorial one: it is also a normal Warren spawn, and as an animal it only
/// fights back (FightMode.Aggressor), so a non-participant on the farm can kill it; argued in the notes. Carves
/// Burrowhide (BurrowsCarving) and ribs.
/// </summary>
[SerializationGenerator(0, false)]
public partial class BurrowHog : BaseCreature
{
    public const int BurrowhideHidesCount = 4;

    [Constructible]
    public BurrowHog() : base(AIType.AI_Animal, FightMode.Aggressor)
    {
        Name = "a burrow hog";
        Body = 2054;
        BaseSoundID = 0xC4;

        SetStr(30);
        SetDex(20);
        SetInt(5);
        SetHits(22);
        SetMana(0);
        SetDamage(3, 5);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 10, 15);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Poison, 5, 10);
        SetSkill(SkillName.MagicResist, 9.0);
        SetSkill(SkillName.Tactics, 12.0);
        SetSkill(SkillName.Wrestling, 12.0);

        Fame = 300;
        Karma = 0;
        VirtualArmor = 10;

        Tamable = true;
        ControlSlots = 1;
        MinTameSkill = 29.1;
    }

    public override string CorpseName => "a hog corpse";
    public override int Meat => 2;
    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;

    public override void OnCarve(Mobile from, Corpse corpse, Item with) =>
        BurrowsCarving.Carve(this, from, corpse, BurrowhideHidesCount, Meat);
}

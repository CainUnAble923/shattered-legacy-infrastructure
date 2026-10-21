// cc-P364 (F-24): the League hunt bestiary, batch 2: the low-level targets and packs for New Haven's surroundings (and the
// swamps): Hedge Snatcher and Snatcher Outrider (Kalamarr's goblin troop and goblin rider), Hornback Raider (Kalamarr's
// goatman), Ditch Crawler (Vertigo's crawler), Mire Croaker (Vert-I-Go's bullywog). Each drops its part (PackItem) and
// sits in ClusterFLeagueBestiary's tables. Numbers are first proposals scaled from the nearest pinned creature (notes,
// Part C table); levels are ClusterFCreatureLevels' Bard difficulty, held inside each entry's band by a fact.
//
// Bodies 2072, 2073 are in anim7's animal slots (the .vd files have the low layout), so the server types them Animal
// (ShardBodyTypes): animal hurt and death actions, and they cannot open doors. The rest are Monster.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>Base: our GreenGoblin cut to a New Haven hillside pack (body 2072, Kalamarr's goblin troop L_01).</summary>
[SerializationGenerator(0, false)]
public partial class HedgeSnatcher : BaseCreature
{
    [Constructible]
    public HedgeSnatcher() : base(AIType.AI_Melee)
    {
        Name = "a hedge snatcher";
        Body = 2072;
        BaseSoundID = 0x600;

        SetStr(35, 45);
        SetDex(40, 50);
        SetInt(15, 25);
        SetHits(24, 30);
        SetDamage(3, 6);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 10, 15);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Poison, 5, 10);
        SetSkill(SkillName.MagicResist, 20.1, 30.0);
        SetSkill(SkillName.Tactics, 25.1, 35.0);
        SetSkill(SkillName.Wrestling, 25.1, 35.0);

        Fame = 400;
        Karma = -400;
        VirtualArmor = 14;

        PackItem(new SnatcherEars());
    }

    public override string CorpseName => "a hedge snatcher corpse";
    public override bool CanRummageCorpses => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Poor);
    }
}

/// <summary>The snatchers' mounted leader (body 2073, Kalamarr's goblin rider): the hunt target of the family. Base: Ratman up a step, faster.</summary>
[SerializationGenerator(0, false)]
public partial class SnatcherOutrider : BaseCreature
{
    [Constructible]
    public SnatcherOutrider() : base(AIType.AI_Melee)
    {
        Name = "a snatcher outrider";
        Body = 2073;
        BaseSoundID = 0x600;

        SetStr(60, 75);
        SetDex(70, 85);
        SetInt(20, 30);
        SetHits(50, 65);
        SetDamage(5, 9);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 20, 25);
        SetResistance(ResistanceType.Fire, 10, 15);
        SetResistance(ResistanceType.Cold, 5, 10);
        SetResistance(ResistanceType.Poison, 10, 15);
        SetResistance(ResistanceType.Energy, 5, 10);
        SetSkill(SkillName.MagicResist, 30.1, 40.0);
        SetSkill(SkillName.Tactics, 45.1, 60.0);
        SetSkill(SkillName.Wrestling, 45.1, 60.0);

        Fame = 1200;
        Karma = -1200;
        VirtualArmor = 24;

        ActiveSpeed = 0.15;  // the wolf under it: a mounted pace
        PassiveSpeed = 0.3;

        PackItem(new SnatcherEars(2));
    }

    public override string CorpseName => "a snatcher outrider corpse";
    public override bool CanRummageCorpses => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

/// <summary>Base: Orc (Humanoid/Melee/Orc.cs) on the goatman body 2074 (Kalamarr): a hunt target of the northern hills.</summary>
[SerializationGenerator(0, false)]
public partial class HornbackRaider : BaseCreature
{
    [Constructible]
    public HornbackRaider() : base(AIType.AI_Melee)
    {
        Name = "a hornback raider";
        Body = 2074;
        BaseSoundID = 0x586; // the satyr's

        SetStr(70, 85);
        SetDex(50, 65);
        SetInt(20, 30);
        SetHits(40, 52);
        SetDamage(4, 8);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 15, 25);
        SetResistance(ResistanceType.Fire, 10, 15);
        SetResistance(ResistanceType.Cold, 10, 15);
        SetResistance(ResistanceType.Poison, 10, 15);
        SetResistance(ResistanceType.Energy, 5, 10);
        SetSkill(SkillName.MagicResist, 25.1, 40.0);
        SetSkill(SkillName.Tactics, 35.1, 50.0);
        SetSkill(SkillName.Wrestling, 35.1, 50.0);

        Fame = 800;
        Karma = -800;
        VirtualArmor = 22;

        PackItem(new HornbackHorn());
    }

    public override string CorpseName => "a hornback raider corpse";
    public override bool CanRummageCorpses => true;
    public override int Meat => 1;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

/// <summary>Base: GiantRat and Slime at tier 1 on Vertigo's crawler body 2075: Haven Island's gully pest. Its bite poisons a little.</summary>
[SerializationGenerator(0, false)]
public partial class DitchCrawler : BaseCreature
{
    [Constructible]
    public DitchCrawler() : base(AIType.AI_Melee)
    {
        Name = "a ditch crawler";
        Body = 2075;
        BaseSoundID = 0x388; // the giant spider's

        SetStr(25, 35);
        SetDex(25, 35);
        SetInt(5, 10);
        SetHits(16, 22);
        SetDamage(2, 5);
        SetDamageType(ResistanceType.Physical, 70);
        SetDamageType(ResistanceType.Poison, 30);
        SetResistance(ResistanceType.Physical, 5, 10);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetSkill(SkillName.MagicResist, 10.1, 20.0);
        SetSkill(SkillName.Tactics, 15.1, 25.0);
        SetSkill(SkillName.Wrestling, 15.1, 25.0);

        Fame = 250;
        Karma = -250;
        VirtualArmor = 10;

        PackItem(new CrawlerMandible());
    }

    public override string CorpseName => "a ditch crawler corpse";
    public override Poison HitPoison => Poison.Lesser;
    public override double HitPoisonChance => 0.2;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Poor);
    }
}

/// <summary>Base: Lizardman (Reptile/Melee/Lizardman.cs) cut down, on Vert-I-Go's bullywog with a spear (body 2076): a swamp hunt target that swims.</summary>
[SerializationGenerator(0, false)]
public partial class MireCroaker : BaseCreature
{
    [Constructible]
    public MireCroaker() : base(AIType.AI_Melee)
    {
        Name = "a mire croaker";
        Body = 2076;
        BaseSoundID = 0x266; // the bullfrog's

        SetStr(60, 75);
        SetDex(55, 70);
        SetInt(20, 30);
        SetHits(40, 50);
        SetDamage(4, 8);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 15, 20);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Cold, 15, 20);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetResistance(ResistanceType.Energy, 5, 10);
        SetSkill(SkillName.MagicResist, 25.1, 40.0);
        SetSkill(SkillName.Tactics, 40.1, 55.0);
        SetSkill(SkillName.Wrestling, 40.1, 55.0);

        Fame = 900;
        Karma = -900;
        VirtualArmor = 20;

        CanSwim = true;

        PackItem(new CroakerTongue());
    }

    public override string CorpseName => "a mire croaker corpse";
    public override bool CanRummageCorpses => true;
    public override int Meat => 1;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

// cc-P364 (F-24): the League hunt bestiary, batch 2: the two mini-bosses. The Siegebreaker (uodev's war troll) holds
// Covetous's lower hall; the Hollow Oak (Thagoras's treant) stands in a clearing of the Yew woods. Each is a group's
// hunt: three of its boss part, a stock gold jewel (as the Burrows bosses drop, BurrowsLoot.RandomJewel) and rich
// loot. Neither flees. Numbers are first proposals scaled from pinned's Cyclops, Troll and Reaper (notes, Part C).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>Base: Cyclops and Troll up a step, on uodev's war troll body 2084. Its club crushes armour (Crushing Blow).</summary>
[SerializationGenerator(0, false)]
public partial class TheSiegebreaker : BaseCreature
{
    [Constructible]
    public TheSiegebreaker() : base(AIType.AI_Melee)
    {
        Name = "The Siegebreaker";
        Body = 2084;
        BaseSoundID = 461; // the troll's

        SetStr(350, 400);
        SetDex(70, 90);
        SetInt(40, 60);
        SetHits(320, 380);
        SetDamage(16, 24);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 25, 35);
        SetSkill(SkillName.MagicResist, 70.1, 85.0);
        SetSkill(SkillName.Tactics, 85.1, 100.0);
        SetSkill(SkillName.Wrestling, 85.1, 100.0);
        SetSkill(SkillName.Anatomy, 50.1, 70.0);

        Fame = 9000;
        Karma = -9000;
        VirtualArmor = 56;

        PackItem(new SiegebreakerTusk(3));
        PackItem(BurrowsLoot.RandomJewel());
    }

    public override string CorpseName => "the Siegebreaker's corpse";
    public override bool CanRummageCorpses => true;
    public override bool CanFlee => false;
    public override int Meat => 4;
    public override WeaponAbility GetWeaponAbility() => WeaponAbility.CrushingBlow;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Gems, 2);
    }
}

/// <summary>Base: Reaper (Plant/Magic/Reaper.cs) grown into a boss on Thagoras's treant body 2085: slow, its grasp paralyzes, its sap poisons, it fears fire.</summary>
[SerializationGenerator(0, false)]
public partial class TheHollowOak : BaseCreature
{
    [Constructible]
    public TheHollowOak() : base(AIType.AI_Melee)
    {
        Name = "The Hollow Oak";
        Body = 2085;
        BaseSoundID = 442; // the reaper's

        SetStr(300, 350);
        SetDex(30, 45);
        SetInt(60, 80);
        SetHits(300, 350);
        SetDamage(14, 22);
        SetDamageType(ResistanceType.Physical, 80);
        SetDamageType(ResistanceType.Poison, 20);
        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 5, 15);
        SetResistance(ResistanceType.Cold, 40, 50);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 30, 40);
        SetSkill(SkillName.MagicResist, 70.1, 85.0);
        SetSkill(SkillName.Tactics, 70.1, 85.0);
        SetSkill(SkillName.Wrestling, 75.1, 90.0);

        Fame = 9000;
        Karma = -9000;
        VirtualArmor = 54;

        ActiveSpeed = 0.35;  // a lumbering tree
        PassiveSpeed = 0.7;

        PackItem(new HollowOakHeartwood(3));
        PackItem(BurrowsLoot.RandomJewel());
        PackItem(new Log(20));
    }

    public override string CorpseName => "the Hollow Oak's corpse";
    public override bool CanFlee => false;
    public override Poison PoisonImmune => Poison.Lethal;
    public override Poison HitPoison => Poison.Regular;
    public override double HitPoisonChance => 0.3;
    public override bool BleedImmune => true;
    public override WeaponAbility GetWeaponAbility() => WeaponAbility.ParalyzingBlow;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.Average);
    }
}

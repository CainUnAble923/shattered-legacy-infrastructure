// cc-P140 (F-45): the Gloom (tier 3, the Spiders family: Gloom Glands and Gloomsilk) and the Gloom Queen in her lair.
// Gloomsilk drops by spider PackItem, as P36 says (a cosmetic Cloth subclass). Normal spawns; the Queen is the dungeon's
// end boss, a caster-melee hybrid kept beatable by a small group of graduated Young players.
//
// Webs: the Gloomspinner and the Queen tangle a target they hit (BurrowsWebs.TryWeb: a chance per hit to paralyze a few
// seconds with a message), the shard's version of "webs, paralyze" without a pinned patch or the mana-costed
// ParalyzingBlow ability.
//
// Numbers are first proposals scaled from the nearest pinned creature (notes, Part C table).

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

public static class BurrowsWebs
{
    public static readonly TimeSpan WebFor = TimeSpan.FromSeconds(3);

    /// <summary>Tangle <paramref name="target"/> in webbing: paralyzed for WebFor, with a message. False when it is already held.</summary>
    public static bool TryWeb(Mobile spider, Mobile target)
    {
        if (target == null || !target.Alive || target.Paralyzed || target.Frozen)
        {
            return false;
        }

        target.Paralyze(WebFor);
        target.SendMessage(0x3B2, "You are tangled in webbing!");
        spider.PlaySound(0x4F1);
        return true;
    }
}

/// <summary>Base: GiantSpider (Arachnid/Melee/GiantSpider.cs) cut down, on the hatchling body 2067.</summary>
[SerializationGenerator(0, false)]
public partial class Gloomling : BaseCreature
{
    [Constructible]
    public Gloomling() : base(AIType.AI_Melee)
    {
        Name = "a gloomling";
        Body = 2067;
        BaseSoundID = 0x388;

        SetStr(40, 60);
        SetDex(70, 90);
        SetInt(20, 40);
        SetHits(30, 45);
        SetMana(0);
        SetDamage(4, 8);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 15, 20);
        SetResistance(ResistanceType.Poison, 25, 35);
        SetSkill(SkillName.Poisoning, 40.1, 60.0);
        SetSkill(SkillName.MagicResist, 20.1, 35.0);
        SetSkill(SkillName.Tactics, 30.1, 45.0);
        SetSkill(SkillName.Wrestling, 40.1, 55.0);

        Fame = 400;
        Karma = -400;
        VirtualArmor = 14;

        PackItem(new GloomGlands());
        PackItem(new GloomsilkCloth(1));
    }

    public override string CorpseName => "a gloomling corpse";
    public override PackInstinct PackInstinct => PackInstinct.Arachnid;
    public override Poison PoisonImmune => Poison.Lesser;
    public override Poison HitPoison => Poison.Lesser;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Poor);
    }
}

/// <summary>Webs and paralysis. Base: GiantSpider at tier 3, on the underdark spider body 2068.</summary>
[SerializationGenerator(0, false)]
public partial class Gloomspinner : BaseCreature
{
    public const double WebChance = 0.15;

    [Constructible]
    public Gloomspinner() : base(AIType.AI_Melee)
    {
        Name = "a gloomspinner";
        Body = 2068;
        BaseSoundID = 0x388;

        SetStr(80, 100);
        SetDex(80, 100);
        SetInt(40, 60);
        SetHits(60, 80);
        SetMana(0);
        SetDamage(6, 12);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 20, 30);
        SetResistance(ResistanceType.Fire, 10, 20);
        SetResistance(ResistanceType.Cold, 10, 20);
        SetResistance(ResistanceType.Poison, 35, 45);
        SetResistance(ResistanceType.Energy, 10, 20);
        SetSkill(SkillName.Poisoning, 60.1, 80.0);
        SetSkill(SkillName.MagicResist, 35.1, 50.0);
        SetSkill(SkillName.Tactics, 50.1, 65.0);
        SetSkill(SkillName.Wrestling, 55.1, 70.0);

        Fame = 900;
        Karma = -900;
        VirtualArmor = 20;

        PackItem(new GloomGlands());
        PackItem(new GloomsilkCloth(2));
        PackItem(new SpidersSilk(3));
    }

    public override string CorpseName => "a gloomspinner corpse";
    public override PackInstinct PackInstinct => PackInstinct.Arachnid;
    public override Poison PoisonImmune => Poison.Regular;
    public override Poison HitPoison => Poison.Regular;

    public override void OnGaveMeleeAttack(Mobile defender, int damage)
    {
        base.OnGaveMeleeAttack(defender, damage);

        if (Utility.RandomDouble() < WebChance)
        {
            BurrowsWebs.TryWeb(this, defender);
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

/// <summary>The caster. Base: DreadSpider (Arachnid/Magic/DreadSpider.cs) cut to tier 3, on the spider mage body 2069.</summary>
[SerializationGenerator(0, false)]
public partial class Gloomweaver : BaseCreature
{
    [Constructible]
    public Gloomweaver() : base(AIType.AI_Mage)
    {
        Name = "a gloomweaver";
        Body = 2069;
        BaseSoundID = 1170;

        SetStr(70, 90);
        SetDex(70, 90);
        SetInt(90, 110);
        SetHits(55, 70);
        SetDamage(5, 10);
        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Poison, 50);
        SetResistance(ResistanceType.Physical, 20, 30);
        SetResistance(ResistanceType.Fire, 10, 20);
        SetResistance(ResistanceType.Cold, 10, 20);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 15, 25);
        SetSkill(SkillName.EvalInt, 45.1, 60.0);
        SetSkill(SkillName.Magery, 45.1, 60.0);
        SetSkill(SkillName.Meditation, 40.1, 60.0);
        SetSkill(SkillName.MagicResist, 40.1, 55.0);
        SetSkill(SkillName.Tactics, 40.1, 55.0);
        SetSkill(SkillName.Wrestling, 45.1, 60.0);

        Fame = 1500;
        Karma = -1500;
        VirtualArmor = 22;

        PackItem(new GloomGlands());
        PackItem(new GloomsilkCloth(2));
        PackReg(4);
    }

    public override string CorpseName => "a gloomweaver corpse";
    public override PackInstinct PackInstinct => PackInstinct.Arachnid;
    public override Poison PoisonImmune => Poison.Regular;
    public override Poison HitPoison => Poison.Regular;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
        AddLoot(LootPack.LowScrolls);
    }
}

/// <summary>
/// The end boss (the female drider body 2070, Chase's pick 2026-10-15): spider body, dark-elf woman above, a caster
/// with a bite. Base: TerathanMatriarch (Arachnid/Magic/TerathanMatriarch.cs) cut to a small group of graduated Young
/// players, with the Gloomspinner's web. Drops: jewelry, her fang (the boss part), glands, silk, Rich loot.
/// </summary>
[SerializationGenerator(0, false)]
public partial class TheGloomQueen : BaseCreature
{
    public const double WebChance = 0.2;

    [Constructible]
    public TheGloomQueen() : base(AIType.AI_Mage)
    {
        Name = "The Gloom Queen";
        Body = 2070;
        Female = true;
        BaseSoundID = 599;

        SetStr(180, 220);
        SetDex(90, 110);
        SetInt(150, 180);
        SetHits(220, 260);
        SetDamage(10, 14);
        SetDamageType(ResistanceType.Physical, 70);
        SetDamageType(ResistanceType.Poison, 30);
        SetResistance(ResistanceType.Physical, 40, 50);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 25, 35);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 25, 35);
        SetSkill(SkillName.EvalInt, 60.1, 75.0);
        SetSkill(SkillName.Magery, 60.1, 75.0);
        SetSkill(SkillName.Meditation, 50.1, 65.0);
        SetSkill(SkillName.MagicResist, 60.1, 75.0);
        SetSkill(SkillName.Tactics, 60.1, 80.0);
        SetSkill(SkillName.Wrestling, 60.1, 80.0);
        SetSkill(SkillName.Poisoning, 60.1, 80.0);

        Fame = 6000;
        Karma = -6000;
        VirtualArmor = 36;

        PackItem(new GloomGlands(3));
        PackItem(new GloomsilkCloth(5));
        PackItem(new SpidersSilk(8));
        PackReg(8);
    }

    public override string CorpseName => "the Gloom Queen's corpse";
    public override bool CanFlee => false;
    public override Poison PoisonImmune => Poison.Deadly;
    public override Poison HitPoison => Poison.Regular;

    public override void OnGaveMeleeAttack(Mobile defender, int damage)
    {
        base.OnGaveMeleeAttack(defender, damage);

        if (Utility.RandomDouble() < WebChance)
        {
            BurrowsWebs.TryWeb(this, defender);
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.MedScrolls);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(BurrowsLoot.RandomJewel());
        c.DropItem(new GloomQueensFang());
    }
}

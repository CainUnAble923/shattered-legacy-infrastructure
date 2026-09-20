// ServUO: Mobiles/Normal/FairyDragon.cs (CC6 batch 4). Values verbatim; serialization by the generator. Spawned by one
// Stygian Abyss spawner (post-uoml/termur/Abyss.json). Body 718 is a row in pinned Data/bodyTable.cfg ("Faerie Dragon").
//
// DEVIATION (D-41, cited, no new number): ServUO runs this on AIType.AI_Mystic (MysticAI : MageAI, casting Mysticism
// with a fall-through to Magery). Pinned ModernUO's AIType has no Mysticism AI (Mobiles/AI/BaseAI/AIType.cs, ten
// members), so it runs AI_Mage, the call CC4 Shame made for CrazedMage and batch 1 for Skree. The Mysticism skill
// range is kept. Note what AI_Mage buys a creature with no Magery: MageAI clamps its circle from Magery
// (MageAI.cs:278), so this one casts first-circle Magery it half-fizzles instead of Hail Storm; its poison bite,
// dispel and stats are as ServUO's. Reverses with P1's AI_Mystic row (8 ServUO Mobiles/Normal files).
//
// ServUO's own values, copied: the name is "Fairy Dragon" with no article, the corpse "a Fairy dragon corpse" with a
// capital F (bug list section 3 shape).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class FairyDragon : BaseCreature
{
    [Constructible]
    public FairyDragon() : base(AIType.AI_Mage, FightMode.Closest) // ServUO: AIType.AI_Mystic (D-41)
    {
        Body = 718;
        BaseSoundID = 362;

        SetStr(512, 558);
        SetDex(95, 105);
        SetInt(455, 501);

        SetHits(398, 403);

        SetDamage(15, 18);

        SetDamageType(ResistanceType.Fire, 20, 25);
        SetDamageType(ResistanceType.Cold, 20, 25);
        SetDamageType(ResistanceType.Poison, 20, 25);
        SetDamageType(ResistanceType.Energy, 20, 25);

        SetResistance(ResistanceType.Physical, 16, 30);
        SetResistance(ResistanceType.Fire, 41, 44);
        SetResistance(ResistanceType.Cold, 40, 49);
        SetResistance(ResistanceType.Poison, 40, 49);
        SetResistance(ResistanceType.Energy, 45, 47);

        SetSkill(SkillName.MagicResist, 99.1, 100.0);
        SetSkill(SkillName.Tactics, 60.6, 68.2);
        SetSkill(SkillName.Wrestling, 90.1, 92.5);
        SetSkill(SkillName.Mysticism, 101.8, 108.3);

        Fame = 15000;
        Karma = -15000;

        VirtualArmor = 39;
    }

    public override string CorpseName => "a Fairy dragon corpse";
    public override string DefaultName => "Fairy Dragon";

    public override bool AutoDispel => !Controlled;
    public override int TreasureMapLevel => 3;
    public override int Meat => 9;
    public override Poison HitPoison => Poison.Greater;
    public override double HitPoisonChance => 0.75;
    public override FoodType FavoriteFood => FoodType.Meat;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.MedScrolls, 2);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() <= 0.25)
        {
            c.DropItem(new FairyDragonWing());
        }

        if (Utility.RandomDouble() < 0.10)
        {
            c.DropItem(new DraconicOrb());
        }
    }

    public override int GetAttackSound() => 1513;
    public override int GetAngerSound() => 1558;
    public override int GetDeathSound() => 1514;
    public override int GetHurtSound() => 1515;
    public override int GetIdleSound() => 1516;
}

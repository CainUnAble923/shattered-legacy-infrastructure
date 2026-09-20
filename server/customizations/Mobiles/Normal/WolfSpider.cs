// ServUO: Mobiles/Normal/WolfSpider.cs (CC6 batch 3). Values verbatim; serialization by the generator. ServUO sets no
// Fame or Karma; neither does this. The corpse name "a wolf spider spider corpse" is ServUO's.
//
// Nothing is lost to Pet Training here, although the brief priced it as a loss: ServUO's only
// SetMagicalAbility(MagicalAbility.Poisoning) sits in the Deserialize version-1 upgrade path (:134-137), which new
// content never runs, and ServUO's SetSkill adds that ability to any wild creature with Poisoning anyway
// (BaseCreature.cs:5212). What the ability gates in ServUO, HitPoison landing, is ungated in ModernUO. The version-0
// Hue/Body fixup is likewise for old saves and is not ported.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class WolfSpider : BaseCreature
{
    [Constructible]
    public WolfSpider() : base(AIType.AI_Melee)
    {
        Body = 736;
        Hue = 0;

        SetStr(225, 268);
        SetDex(145, 165);
        SetInt(285, 310);

        SetHits(150, 160);
        SetMana(285, 310);
        SetStam(145, 165);

        SetDamage(15, 18);

        SetDamageType(ResistanceType.Physical, 70);
        SetDamageType(ResistanceType.Poison, 30);

        SetResistance(ResistanceType.Physical, 30, 35);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 25, 35);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 25, 35);

        SetSkill(SkillName.Anatomy, 80.0, 90.0);
        SetSkill(SkillName.MagicResist, 60.0, 75.0);
        SetSkill(SkillName.Poisoning, 62.3, 77.2);
        SetSkill(SkillName.Tactics, 84.1, 95.9);
        SetSkill(SkillName.Wrestling, 80.2, 90.0);
        SetSkill(SkillName.Hiding, 105.0, 110.0);
        SetSkill(SkillName.Stealth, 105.0, 110.0);

        Tamable = true;
        ControlSlots = 2;
        MinTameSkill = 59.1;
    }

    public override string CorpseName => "a wolf spider spider corpse";
    public override string DefaultName => "a Wolf spider";

    public override FoodType FavoriteFood => FoodType.Meat;
    public override PackInstinct PackInstinct => PackInstinct.Arachnid;
    public override Poison PoisonImmune => Poison.Regular;
    public override Poison HitPoison => Poison.Regular;

    public override void GenerateLoot()
    {
        PackItem(new SpidersSilk(8));
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.Gems, 2);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (!Controlled && Utility.RandomDouble() < 0.01)
        {
            c.DropItem(new LuckyCoin());
        }
    }

    public override int GetIdleSound() => 1605;
    public override int GetAngerSound() => 1602;
    public override int GetHurtSound() => 1604;
    public override int GetDeathSound() => 1603;
}

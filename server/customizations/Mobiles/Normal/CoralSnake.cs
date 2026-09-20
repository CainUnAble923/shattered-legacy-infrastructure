// ServUO: Mobiles/Normal/CoralSnake.cs (CC6 batch 3). Values verbatim; serialization by the generator. ServUO sets
// Physical resistance twice (42-50, then 5-20) and never Cold, so the snake runs with 5-20 physical and 0 cold; copied,
// it is ServUO's and of the "missing" kind (S2's D-4 rule). Tamable = false with ControlSlots and MinTameSkill set is
// ServUO's too.
//
// Nothing is lost to Pet Training here, although the brief priced it as a loss: ServUO's only
// SetMagicalAbility(MagicalAbility.Poisoning) sits in the Deserialize version-0 upgrade path (:87-90), which new
// content never runs, and ServUO's SetSkill adds that ability to any wild creature with Poisoning anyway
// (BaseCreature.cs:5212). HitPoison is ungated in ModernUO.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class CoralSnake : BaseCreature
{
    [Constructible]
    public CoralSnake() : base(AIType.AI_Melee)
    {
        Body = 52;
        Hue = 0x21;
        BaseSoundID = 0xDB;

        SetStr(205, 340);
        SetDex(248, 300);
        SetInt(28, 35);

        SetHits(132, 200);
        SetMana(28, 35);

        SetDamage(5, 21);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Poison, 50);

        SetResistance(ResistanceType.Physical, 42, 50);
        SetResistance(ResistanceType.Fire, 5, 20);
        SetResistance(ResistanceType.Physical, 5, 20);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 5, 20);

        SetSkill(SkillName.Poisoning, 99.7, 110.9);
        SetSkill(SkillName.MagicResist, 98.1, 105.0);
        SetSkill(SkillName.Tactics, 82.0, 98.0);
        SetSkill(SkillName.Wrestling, 90.3, 105.0);

        Fame = 300;
        Karma = -300;

        VirtualArmor = 16;

        Tamable = false;
        ControlSlots = 1;
        MinTameSkill = 59.1;
    }

    public override string CorpseName => "a snake corpse";
    public override string DefaultName => "a coral snake";

    public override Poison PoisonImmune => Poison.Lesser;
    public override Poison HitPoison => Poison.Deadly;

    public override int Meat => 1;
    public override FoodType FavoriteFood => FoodType.Eggs;
}

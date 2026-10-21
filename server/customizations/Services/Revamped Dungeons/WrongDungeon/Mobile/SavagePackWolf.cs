// ServUO: Services/Revamped Dungeons/WrongDungeon/Mobile/SavagePackWolf.cs (cc-P194). ServUO's FightMode.Weakest
// is carried (it is not the default); its TypeAlias for the old spelling is carried too.

using ModernUO.Serialization;

namespace Server.Mobiles;

[TypeAlias("Server.Mobiles.SavagePackwolf")]
[SerializationGenerator(0, false)]
public partial class SavagePackWolf : BaseCreature
{
    [Constructible]
    public SavagePackWolf() : base(AIType.AI_Melee, FightMode.Weakest)
    {
        Body = 0xE1;
        BaseSoundID = 0xE5;

        SetStr(100, 116);
        SetDex(51, 61);
        SetInt(11, 21);

        SetHits(650, 671);
        SetMana(0);

        SetDamage(9, 12);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 50, 60);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.MagicResist, 60.7, 74.0);
        SetSkill(SkillName.Tactics, 80.9, 94.4);
        SetSkill(SkillName.Wrestling, 89.0, 97.1);

        Fame = 450;
        Karma = -450;

        VirtualArmor = 26;
        Tamable = false;
    }

    public override string CorpseName => "a wolf corpse";
    public override string DefaultName => "a savage pack wolf";

    public override bool AlwaysMurderer => true;
    public override int Meat => 1;
    public override int Hides => 5;
    public override FoodType FavoriteFood => FoodType.Meat;
    public override PackInstinct PackInstinct => PackInstinct.Canine;
}

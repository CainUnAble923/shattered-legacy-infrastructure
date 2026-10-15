// ServUO: Mobiles/Normal/PalaminoHorse.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Palomino : BaseMount
{
    // ServUO's (string name) constructor is dropped: pinned BaseMount takes no name (stock Horse carries its name as
    // DefaultName). Nothing in ServUO calls it with another name.
    [Constructible]
    public Palomino() : base(1408, 0x3ECD, AIType.AI_Animal)
    {
        BaseSoundID = 0xA8;

        SetStr(55, 98);
        SetDex(56, 75);
        SetInt(6, 10);

        SetHits(28, 45);
        SetMana(0);

        SetDamage(3, 4);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 15, 20);

        SetSkill(SkillName.MagicResist, 25.1, 30.0);
        SetSkill(SkillName.Tactics, 29.3, 44.0);
        SetSkill(SkillName.Wrestling, 29.3, 44.0);

        Fame = 300;
        Karma = 300;

        Tamable = true;
        ControlSlots = 1;
        MinTameSkill = 29.1;
    }

    public override string CorpseName => "a horse corpse";
    public override string DefaultName => "a horse";

    public override int Meat => 3;
    public override int Hides => 10;
    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;
}

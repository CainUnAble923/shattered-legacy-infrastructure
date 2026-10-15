// ServUO: Mobiles/Normal/DreadWarhorse.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class DreadWarhorse : BaseMount
{
    // ServUO's (string name) constructor is dropped: pinned BaseMount takes no name, and stock Nightmare, the same body
    // and item, carries its name as DefaultName. Every ServUO caller passes the default name.
    [Constructible]
    public DreadWarhorse()
        : base(0x74, 0x3EA7, AIType.AI_Mage)
    {
        BaseSoundID = 0xA8;
        // ServUO also sets BodyValue = 116 here: 0x74, the body the base already set. Pinned has no BodyValue.
        Hue = 1175;

        SetStr(500, 555);
        SetDex(89, 125);
        SetInt(100, 160);

        SetHits(555, 650);

        SetDamage(20, 26);

        SetDamageType(ResistanceType.Physical, 40);
        SetDamageType(ResistanceType.Poison, 20);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 65, 75);
        SetResistance(ResistanceType.Fire, 20, 40);
        SetResistance(ResistanceType.Cold, 20, 40);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.EvalInt, 15.2, 19.3);
        SetSkill(SkillName.Magery, 39.5, 49.5);
        SetSkill(SkillName.MagicResist, 91.4, 93.4);
        SetSkill(SkillName.Tactics, 108.1, 110.0);
        SetSkill(SkillName.Wrestling, 97.3, 98.2);

        Fame = 15000;
        Karma = -15000;

        VirtualArmor = 60;

        Tamable = true;
        ControlSlots = 3;
        MinTameSkill = 108.0;
    }

    public override string CorpseName => "a dread warhorse corpse";
    public override string DefaultName => "a dread warhorse";

    public override int Meat => 5;
    public override int Hides => 10;
    public override HideType HideType => HideType.Barbed;
    public override FoodType FavoriteFood => FoodType.Meat;
    public override bool CanAngerOnTame => true;
    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.Average);
        AddLoot(LootPack.LowScrolls);
        AddLoot(LootPack.Potions);
    }

    public override int GetAngerSound()
    {
        if (!Controlled)
        {
            return 0x16A;
        }

        return base.GetAngerSound();
    }
}

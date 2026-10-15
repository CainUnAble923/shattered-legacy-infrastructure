// ServUO: Mobiles/Normal/EffetePutridGargoyle.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class EffetePutridGargoyle : BaseCreature
{
    [Constructible]
    public EffetePutridGargoyle()
        : base(AIType.AI_Mage)
    {
        Body = 4;
        BaseSoundID = 372;

        SetStr(215, 220);
        SetDex(90, 95);
        SetInt(40, 45);

        SetHits(110, 111);

        SetDamage(8, 18);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Cold, 40);

        SetResistance(ResistanceType.Physical, 30, 35);
        SetResistance(ResistanceType.Fire, 25, 35);
        SetResistance(ResistanceType.Cold, 5, 10);
        SetResistance(ResistanceType.Poison, 15, 25);

        SetSkill(SkillName.Anatomy, 6.0, 8.0);
        SetSkill(SkillName.MagicResist, 60.5, 65);
        SetSkill(SkillName.Tactics, 65.7, 66);
        SetSkill(SkillName.Wrestling, 69.6, 70.0);

        Fame = 3500;
        Karma = -3500;

        VirtualArmor = 32;
    }

    public override string CorpseName => "an effete putrid gargoyle corpse";
    public override string DefaultName => "an Effete putrid gargoyle";

    public override int Meat => 1;
    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average, 2);
        AddLoot(LootPack.MedScrolls);
        AddLoot(LootPack.Gems, Utility.RandomMinMax(1, 4));
    }
}

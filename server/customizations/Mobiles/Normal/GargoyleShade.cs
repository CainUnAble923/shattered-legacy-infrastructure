// ServUO: Mobiles/Normal/GargoyleShade.cs (CC6 batch 2). Values verbatim; serialization by the generator.
// ServUO sets no Energy resistance on this creature; neither does this.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class GargoyleShade : BaseCreature
{
    [Constructible]
    public GargoyleShade() : base(AIType.AI_Mage)
    {
        Body = 0x4;
        Hue = 16385;
        BaseSoundID = 0x482;

        SetStr(76, 78);
        SetDex(76, 81);
        SetInt(36, 48);

        SetHits(60, 64);
        SetStam(80, 81);
        SetMana(75, 78);

        SetDamage(7, 14);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 35, 60);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 10, 20);
        SetResistance(ResistanceType.Poison, 20, 35);

        SetSkill(SkillName.EvalInt, 57.1, 65.5);
        SetSkill(SkillName.Magery, 60.6, 70.1);
        SetSkill(SkillName.MagicResist, 52.6, 70.0);
        SetSkill(SkillName.Tactics, 52.7, 60.0);
        SetSkill(SkillName.Wrestling, 47.7, 55.0);
        SetSkill(SkillName.DetectHidden, 30.0, 40.0);

        Fame = 4000;
        Karma = -4000;

        VirtualArmor = 28;

        PackReg(10);
    }

    public override string CorpseName => "a ghostly corpse";
    public override string DefaultName => "a gargoyle shade";

    public override bool BleedImmune => true;
    public override OppositionGroup OppositionGroup => OppositionGroup.FeyAndUndead;
    public override Poison PoisonImmune => Poison.Lethal;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

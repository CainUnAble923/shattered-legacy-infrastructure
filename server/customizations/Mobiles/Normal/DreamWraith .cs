// ServUO: Mobiles/Normal/DreamWraith .cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;
namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class DreamWraith : BaseCreature
{
    [Constructible]
    public DreamWraith()
        : base(AIType.AI_Mage /* ServUO: AI_NecroMage (Q-050) */)
    {
        Body = 740;
        BaseSoundID = 0x482;

        SetStr(200, 300);
        SetDex(100, 200);
        SetInt(600, 700);

        SetHits(550, 650);

        SetDamage(18, 25);

        SetDamageType(ResistanceType.Physical, 10);
        SetDamageType(ResistanceType.Cold, 45);
        SetDamageType(ResistanceType.Energy, 45);

        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 30, 50);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 20, 30);

        SetSkill(SkillName.Necromancy, 100.0, 120.0);
        SetSkill(SkillName.SpiritSpeak, 100.0, 120.0);
        SetSkill(SkillName.Anatomy, 0.0, 10.0);
        SetSkill(SkillName.EvalInt, 100.0, 120.0);
        SetSkill(SkillName.Magery, 100.0, 120.0);
        SetSkill(SkillName.Meditation, 100.0, 110.0);
        SetSkill(SkillName.MagicResist, 120.0, 150.0);
        SetSkill(SkillName.Tactics, 70.0, 80.0);
        SetSkill(SkillName.Wrestling, 90.0, 100.0);

        Fame = 4000;
        Karma = -4000;

        VirtualArmor = 28;

        PackReg(10);
    }

    public override string CorpseName => "a dream wraith corpse";
    public override string DefaultName => "a Dream Wraith";

    public override bool BleedImmune => true;
    public override OppositionGroup OppositionGroup => OppositionGroup.FeyAndUndead;
    public override Poison PoisonImmune => Poison.Lethal;
    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
    }

    public override int GetIdleSound() => 0x5F4;

    public override int GetAngerSound() => 0x5F1;

    public override int GetDeathSound() => 0x5F2;

    public override int GetHurtSound() => 0x5F3;
}

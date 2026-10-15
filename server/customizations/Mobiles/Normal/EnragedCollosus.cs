// ServUO: Mobiles/Normal/EnragedCollosus.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;
namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class EnragedColossus : BaseCreature
{
    [Constructible]
    public EnragedColossus()
        : base(AIType.AI_Melee)
    {
        Body = 829;

        SetStr(600);
        SetDex(70);
        SetInt(80);

        SetDamage(18, 21);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 56, 63);
        SetResistance(ResistanceType.Fire, 29, 30);
        SetResistance(ResistanceType.Cold, 53, 54);
        SetResistance(ResistanceType.Poison, 54, 58);
        SetResistance(ResistanceType.Energy, 26, 29);

        SetSkill(SkillName.MagicResist, 115.0, 140.0);
        SetSkill(SkillName.Tactics, 120.0, 130.0);
        SetSkill(SkillName.Wrestling, 120.0, 140);

        VirtualArmor = 58;
        ControlSlots = 5;
    }

    public override string CorpseName => "the remains of an enraged colossus";
    public override string DefaultName => "Rising Colossus";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 4);
        AddLoot(LootPack.Gems, 8);
    }

    public override double DispelDifficulty => 125.0;
    public override double DispelFocus => 45.0;
    public override bool BleedImmune => true;
    public override Poison PoisonImmune => Poison.Lethal; // ServUO: "Immune to poison?"
    public override int GetAttackSound() => 0x627;

    public override int GetHurtSound() => 0x629;
}

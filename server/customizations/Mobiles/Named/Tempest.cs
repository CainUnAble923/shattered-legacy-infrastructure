// ServUO: Mobiles/Named/Tempest.cs (CC6 batch 5). Values verbatim; serialization by the generator. One of the four
// Labyrinth named (shared/malas/Labyrinth.json spells it "tempest"). ControlSlots = 2 on a creature that is not
// tameable is ServUO's. Dropped: nothing.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Tempest : BaseCreature
{
    [Constructible]
    public Tempest() : base(AIType.AI_Mage)
    {
        Body = 13;
        Hue = 1175;
        BaseSoundID = 263;

        SetStr(116, 135);
        SetDex(166, 185);
        SetInt(101, 125);

        SetHits(602);

        SetDamage(18, 20); // Erica's

        SetDamageType(ResistanceType.Energy, 80);
        SetDamageType(ResistanceType.Cold, 20);

        SetResistance(ResistanceType.Physical, 46);
        SetResistance(ResistanceType.Fire, 39);
        SetResistance(ResistanceType.Cold, 33);
        SetResistance(ResistanceType.Poison, 36);
        SetResistance(ResistanceType.Energy, 58);

        SetSkill(SkillName.EvalInt, 99.6);
        SetSkill(SkillName.Magery, 101.0);
        SetSkill(SkillName.MagicResist, 104.6);
        SetSkill(SkillName.Tactics, 111.8);
        SetSkill(SkillName.Wrestling, 116.0);

        Fame = 4500;
        Karma = -4500;

        VirtualArmor = 40;
        ControlSlots = 2;
    }

    public override string CorpseName => "the remains of Tempest";
    public override string DefaultName => "Tempest";

    public override bool GivesMLMinorArtifact => true;

    public override double DispelDifficulty => 117.5;
    public override double DispelFocus => 45.0;
    public override bool BleedImmune => true;
    public override int TreasureMapLevel => 2;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Meager);
        AddLoot(LootPack.LowScrolls);
        AddLoot(LootPack.MedScrolls);
    }
}

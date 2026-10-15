// ServUO: Mobiles/Normal/ArchDaemon.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;
using Server.Ethics;
using Server.Factions;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ArchDaemon : BaseCreature
{
    [Constructible]
    public ArchDaemon()
        : base(AIType.AI_Mage)
    {
        Body = 40;
        Hue = 1755;
        BaseSoundID = 357;

        SetStr(986, 1185);
        SetDex(177, 255);
        SetInt(151, 250);

        SetHits(592, 711);

        SetDamage(22, 29);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Fire, 25);
        SetDamageType(ResistanceType.Energy, 25);

        SetResistance(ResistanceType.Physical, 65, 80);
        SetResistance(ResistanceType.Fire, 60, 80);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.Anatomy, 25.1, 50.0);
        SetSkill(SkillName.EvalInt, 90.1, 100.0);
        SetSkill(SkillName.Magery, 95.5, 100.0);
        SetSkill(SkillName.Meditation, 25.1, 50.0);
        SetSkill(SkillName.MagicResist, 100.5, 150.0);
        SetSkill(SkillName.Tactics, 90.1, 100.0);
        SetSkill(SkillName.Wrestling, 90.1, 100.0);

        Fame = 24000;
        Karma = -24000;

        VirtualArmor = 90;
    }

    public override string CorpseName => "a arch daemon corpse";
    public override string DefaultName => "an Arch Deamon";

    public override double DispelDifficulty => 125.0;
    public override double DispelFocus => 45.0;
    public override Faction FactionAllegiance => Shadowlords.Instance;
    public override Ethic EthicAllegiance => Ethic.Evil;
    public override bool CanRummageCorpses => true;
    public override Poison PoisonImmune => Poison.Regular;
    public override int TreasureMapLevel => 4;
    public override int Meat => 1;
    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.Average, 2);
        AddLoot(LootPack.MedScrolls, 2);
    }
}

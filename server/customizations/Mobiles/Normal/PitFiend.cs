// ServUO: Mobiles/Normal/PitFiend.cs (CC6 batch 2). Values verbatim; serialization by the generator. ServUO's
// own header says "Copied from deamon, still have to get detailed information on Pit Fiend"; the Shadowlords
// faction and Evil ethic allegiances are the daemon's and both exist here unchanged.

using ModernUO.Serialization;
using Server.Ethics;
using Server.Factions;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class PitFiend : BaseCreature
{
    [Constructible]
    public PitFiend() : base(AIType.AI_Mage)
    {
        Body = 43;
        Hue = 1863;
        BaseSoundID = 357;

        SetStr(376, 405);
        SetDex(176, 195);
        SetInt(201, 225);

        SetHits(226, 243);

        SetDamage(15, 20);

        SetSkill(SkillName.EvalInt, 80.1, 90.0);
        SetSkill(SkillName.Magery, 80.1, 90.0);
        SetSkill(SkillName.MagicResist, 75.1, 85.0);
        SetSkill(SkillName.Tactics, 80.1, 90.0);
        SetSkill(SkillName.Wrestling, 80.1, 100.0);

        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 10, 20);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetResistance(ResistanceType.Energy, 30, 40);

        Fame = 18000;
        Karma = -18000;

        VirtualArmor = 60;
    }

    public override string CorpseName => "a pit fiend corpse";
    public override string DefaultName => "a Pit fiend";

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

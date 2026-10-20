// ServUO: Services/Revamped Dungeons/WrongDungeon/Mobile/CaveTrollWrong.cs (cc-P194). The Wrong cave troll
// (not Shame's CaveTroll, which carries the cannon powder ingredients).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class CaveTrollWrong : BaseCreature
{
    [Constructible]
    public CaveTrollWrong() : base(AIType.AI_Melee)
    {
        Body = Utility.RandomList(53, 54);
        BaseSoundID = 461;
        Hue = 674;

        SetStr(118, 118);
        SetDex(58, 58);
        SetInt(65, 65);

        SetHits(2136, 2136);

        SetDamage(8, 14);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 76, 76);
        SetResistance(ResistanceType.Fire, 55, 55);
        SetResistance(ResistanceType.Cold, 72, 72);
        SetResistance(ResistanceType.Poison, 75, 75);
        SetResistance(ResistanceType.Energy, 50, 50);

        SetSkill(SkillName.MagicResist, 87.4, 87.4);
        SetSkill(SkillName.Tactics, 125.8, 125.8);
        SetSkill(SkillName.Wrestling, 137.5, 137.5);

        Fame = 3500;
        Karma = -3500;

        VirtualArmor = 40;
    }

    public override string CorpseName => "a Cave Troll corpse";
    public override string DefaultName => "a Cave Troll";

    public override bool CanRummageCorpses => true;
    public override int TreasureMapLevel => 2;
    public override int Meat => 2;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
    }
}

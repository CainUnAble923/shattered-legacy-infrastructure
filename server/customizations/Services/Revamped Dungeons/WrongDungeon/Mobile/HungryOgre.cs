// ServUO: Services/Revamped Dungeons/WrongDungeon/Mobile/HungryOgre.cs (cc-P194).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class HungryOgre : BaseCreature
{
    [Constructible]
    public HungryOgre() : base(AIType.AI_Melee)
    {
        Body = 0x1;
        BaseSoundID = 427;

        SetStr(188, 223);
        SetDex(62, 79);
        SetInt(49, 59);

        SetHits(1107, 1205);
        SetMana(49, 59);

        SetDamage(15, 20);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 70, 80);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 70, 80);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.MagicResist, 61.1, 69.9);
        SetSkill(SkillName.Tactics, 102.3, 109.6);
        SetSkill(SkillName.Wrestling, 100.9, 108.7);

        Fame = 12000;
        Karma = -12000;

        VirtualArmor = 32;

        PackItem(new Club());
    }

    public override string CorpseName => "a hungry ogre corpse";
    public override string DefaultName => "Hungry Ogre";

    public override bool CanRummageCorpses => true;
    public override int TreasureMapLevel => 1;
    public override int Meat => 2;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Potions);
    }
}

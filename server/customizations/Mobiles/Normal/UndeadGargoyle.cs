// ServUO: Mobiles/Normal/UndeadGargoyle.cs (CC6 batch 8, Part D). One of the six creatures Medusa keeps petrified
// around her and releases when struck (Medusa.GetRandomStoneMonster); the other five are stock. ServUO's own
// header: "Based on Gargoyle, still no infos on Undead Gargoyle... Have to get also the correct body ID". Body 722
// is a bodyTable.cfg row ("Undead Gargoyle"). Nothing else in pinned or ours names it; no spawn entry does.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class UndeadGargoyle : BaseCreature
{
    [Constructible]
    public UndeadGargoyle() : base(AIType.AI_Melee)
    {
        Body = 722;
        BaseSoundID = 372;

        SetStr(250, 350);
        SetDex(120, 140);
        SetInt(250, 350);

        SetHits(200, 300);

        SetDamage(15, 27);

        SetDamageType(ResistanceType.Physical, 10);
        SetDamageType(ResistanceType.Cold, 50);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 45, 55);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 40, 55);
        SetResistance(ResistanceType.Poison, 55, 65);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.EvalInt, 90.1, 110.0);
        SetSkill(SkillName.Magery, 120);
        SetSkill(SkillName.MagicResist, 100.1, 120.0);
        SetSkill(SkillName.Tactics, 60.1, 70.0);
        SetSkill(SkillName.Wrestling, 60.1, 70.0);
        SetSkill(SkillName.Necromancy, 70, 120);
        SetSkill(SkillName.SpiritSpeak, 62.9, 113.7);

        Fame = 3500;
        Karma = -3500;

        VirtualArmor = 32;

        if (0.025 > Utility.RandomDouble())
        {
            PackItem(new GargoylesPickaxe());
        }
    }

    public override string CorpseName => "an undead gargoyle corpse";
    public override string DefaultName => "an Undead Gargoyle";

    public override int TreasureMapLevel => 1;
    public override int Meat => 1;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.MedScrolls);
        AddLoot(LootPack.Gems, Utility.RandomMinMax(1, 4));
    }
}

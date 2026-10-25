// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/Corgul/SoulboundApprenticeMage.cs -> ours,
// High Seas, second half. A soulbound apprentice mage: an evil mage, 128 hits.
// One of Corgul the Soulbinder's crew: CorgulTheSoulBinder.SpawnHelpers raises 15 at each of the island's five landing
// spots, and they die with him.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SoulboundApprenticeMage : EvilMage
{
    [Constructible]
    public SoulboundApprenticeMage()
    {
        Title = "the soulbound apprentice mage";

        SetStr(115);
        SetDex(97);
        SetInt(106);

        SetHits(128);
        SetMana(210);

        SetDamage(5, 10);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 20);
        SetResistance(ResistanceType.Fire, 21);
        SetResistance(ResistanceType.Cold, 22);
        SetResistance(ResistanceType.Poison, 20);
        SetResistance(ResistanceType.Energy, 25);

        SetSkill(SkillName.Wrestling, 40.0, 50.0);
        SetSkill(SkillName.MagicResist, 40.0, 50.0);
        SetSkill(SkillName.Magery, 60.2, 72.4);
        SetSkill(SkillName.EvalInt, 60.1, 73.4);
        SetSkill(SkillName.Meditation, 40.0, 50.0);

        Fame = 1000;
        Karma = -1000;
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 3);
    }
}

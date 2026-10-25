// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/Corgul/SoulboundBattleMage.cs -> ours,
// High Seas, second half. A soulbound battle mage: an evil mage lord, 419 hits, strong in melee too.
// One of Corgul the Soulbinder's crew: CorgulTheSoulBinder.SpawnHelpers raises 15 at each of the island's five landing
// spots, and they die with him.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SoulboundBattleMage : EvilMageLord
{
    [Constructible]
    public SoulboundBattleMage()
    {
        Title = "the soulbound battle mage";

        SetStr(156);
        SetDex(101);
        SetInt(181);

        SetHits(419);
        SetMana(619);

        SetDamage(12, 17);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Fire, 20);
        SetDamageType(ResistanceType.Cold, 20);
        SetDamageType(ResistanceType.Poison, 20);
        SetDamageType(ResistanceType.Energy, 20);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 50, 60);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.Wrestling, 120.0, 125.0);
        SetSkill(SkillName.Tactics, 110.0, 120.0);
        SetSkill(SkillName.MagicResist, 100.0, 110.0);
        SetSkill(SkillName.Anatomy, 1.0, 0.0); // ServUO's own range, max under min
        SetSkill(SkillName.Magery, 105.0, 110.0);
        SetSkill(SkillName.EvalInt, 95.0, 100.0);
        SetSkill(SkillName.Meditation, 20.0, 30.0);

        Fame = 5000;
        Karma = -5000;
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 3);
    }
}

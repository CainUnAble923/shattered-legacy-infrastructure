// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/Corgul/SoulboundSpellSlinger.cs -> ours,
// High Seas, second half. A soulbound spellslinger: an evil mage lord, 190-200 hits.
// One of Corgul the Soulbinder's crew: CorgulTheSoulBinder.SpawnHelpers raises 15 at each of the island's five landing
// spots, and they die with him.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SoulboundSpellSlinger : EvilMageLord
{
    [Constructible]
    public SoulboundSpellSlinger()
    {
        Title = "the soulbound spellslinger";

        SetStr(120, 130);
        SetDex(90, 100);
        SetInt(120, 150);

        SetHits(190, 200);
        SetMana(400, 500);

        SetDamage(8, 12);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 30, 40);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.Wrestling, 90.0, 100.0);
        SetSkill(SkillName.Tactics, 80.0, 90.0);
        SetSkill(SkillName.MagicResist, 90, 100.0);
        SetSkill(SkillName.Anatomy, 1.0, 0.0); // ServUO's own range, max under min
        SetSkill(SkillName.Magery, 100.0, 110.0);
        SetSkill(SkillName.EvalInt, 80.0, 90.0);
        SetSkill(SkillName.Meditation, 20.0, 30.0);

        Fame = 3000;
        Karma = -3000;
    }

    public override int TreasureMapLevel => 3;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 1);
    }
}

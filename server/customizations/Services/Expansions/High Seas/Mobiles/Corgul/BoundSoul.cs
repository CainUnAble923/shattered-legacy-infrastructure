// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/Corgul/BoundSoul.cs -> ours,
// High Seas, second half. A bound soul: a murderous spirit (body 0x3CA), melee, 600-620 hits.
// One of Corgul the Soulbinder's crew: CorgulTheSoulBinder.SpawnHelpers raises 15 at each of the island's five landing
// spots, and they die with him.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class BoundSoul : BaseCreature
{
    public override bool AlwaysMurderer => true;

    [Constructible]
    public BoundSoul()
        : base(AIType.AI_Melee)
    {
        Name = "a bound soul";
        Body = 0x3CA;
        Hue = 0x4001;

        SetStr(150, 180);
        SetDex(120, 150);
        SetInt(20, 40);

        SetHits(600, 620);

        SetDamage(17, 22);

        SetDamageType(ResistanceType.Physical, 10);
        SetDamageType(ResistanceType.Cold, 30);
        SetDamageType(ResistanceType.Poison, 30);
        SetDamageType(ResistanceType.Energy, 30);

        SetResistance(ResistanceType.Physical, 80, 95);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 70, 80);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.Wrestling, 100.0, 110.0);
        SetSkill(SkillName.Tactics, 110.0);
        SetSkill(SkillName.MagicResist, 100.0, 115.0);

        Fame = 5000;
        Karma = -5000;
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 3);
    }

    public override int TreasureMapLevel => 3;
}

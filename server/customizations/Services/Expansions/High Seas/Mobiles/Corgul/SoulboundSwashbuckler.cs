// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/Corgul/SoulboundSwashbuckler.cs -> ours,
// High Seas, second half. A soulbound swashbuckler: a cutlass fighter, 100-125 hits.
// One of Corgul the Soulbinder's crew: CorgulTheSoulBinder.SpawnHelpers raises 15 at each of the island's five landing
// spots, and they die with him.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SoulboundSwashbuckler : BaseCreature
{
    public override bool ClickTitle => false;
    public override bool AlwaysMurderer => true;

    [Constructible]
    public SoulboundSwashbuckler()
        : base(AIType.AI_Melee)
    {
        Name = "a soulbound swashbuckler";
        Body = 0x190;
        Hue = Race.Human.RandomSkinHue();
        Utility.AssignRandomHair(this);

        SetStr(120, 130);
        SetDex(105, 115);
        SetInt(95, 110);

        SetHits(100, 125);

        SetDamage(12, 18);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 30, 40);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.MagicResist, 25.0, 47.5);
        SetSkill(SkillName.Swords, 65.0, 87.5);
        SetSkill(SkillName.Tactics, 65.0, 87.5);
        SetSkill(SkillName.Anatomy, 65.0, 87.5);

        Fame = 2000;
        Karma = -2000;

        AddItem(new Bandana());
        AddItem(new LeatherArms());
        AddItem(new FancyShirt());
        AddItem(new ShortPants());
        AddItem(new Cutlass());
        AddItem(new Boots(Utility.RandomNeutralHue()));
        AddItem(new SilverEarrings());

    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 1);
    }
}

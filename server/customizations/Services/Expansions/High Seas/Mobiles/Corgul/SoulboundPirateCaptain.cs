// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Mobiles/Corgul/SoulboundPirateCaptain.cs -> ours,
// High Seas, second half. A soulbound pirate captain: a cutlass fighter, 450-600 hits.
// One of Corgul the Soulbinder's crew: CorgulTheSoulBinder.SpawnHelpers raises 15 at each of the island's five landing
// spots, and they die with him.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SoulboundPirateCaptain : BaseCreature
{
    public override bool ClickTitle => false;
    public override bool AlwaysMurderer => true;

    [Constructible]
    public SoulboundPirateCaptain()
        : base(AIType.AI_Melee)
    {
        Name = "a soulbound pirate captain";
        Body = 0x190;
        Hue = Race.Human.RandomSkinHue();
        Utility.AssignRandomHair(this);

        SetStr(150, 200);
        SetDex(150);
        SetInt(95, 110);

        SetHits(450, 600);

        SetDamage(20, 28);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 45, 55);
        SetResistance(ResistanceType.Fire, 45, 55);
        SetResistance(ResistanceType.Cold, 45, 55);
        SetResistance(ResistanceType.Poison, 45, 55);
        SetResistance(ResistanceType.Energy, 45, 55);

        SetSkill(SkillName.MagicResist, 100.0, 120.0);
        SetSkill(SkillName.Swords, 110.0, 120.0);
        SetSkill(SkillName.Tactics, 110.0, 120.0);
        SetSkill(SkillName.Anatomy, 110.0, 120.0);

        Fame = 8000;
        Karma = -8000;

        AddItem(new TricorneHat(1));
        AddItem(new LeatherArms());
        AddItem(new FancyShirt(1));
        AddItem(new ShortPants(1));
        AddItem(new Cutlass());
        AddItem(new Boots(Utility.RandomNeutralHue()));
        AddItem(new GoldEarrings());

    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 3);
    }
}

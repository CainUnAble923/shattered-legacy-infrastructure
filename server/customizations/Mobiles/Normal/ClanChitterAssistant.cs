// ServUO: Mobiles/Normal/ClanCA.cs (CC6 batch 7). Values verbatim; serialization by the generator. The type is named
// by the spawn-data spelling (post-uoml/termur/Abyss.json, [982,491] with RakktaviRenowned), not ServUO's ClanCA:
// Q-056, the naming rule for a port whose ServUO type name differs from its spawn entry. The player-visible name is
// ServUO's. ServUO's Deserialize carries a body fixup (42 -> 0x8E) for its own older saves; new content at version 0
// never runs it, so it is not ported. Not in pinned's Repond slayer group or Vermin talisman group (D-77, D-78).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ClanChitterAssistant : BaseCreature
{
    [Constructible]
    public ClanChitterAssistant() : base(AIType.AI_Archer)
    {
        Body = 0x8E;
        BaseSoundID = 437;

        SetStr(146, 175);
        SetDex(101, 130);
        SetInt(120, 135);

        SetHits(120, 145);

        SetDamage(4, 10);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 23, 35);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 30, 50);
        SetResistance(ResistanceType.Poison, 15, 20);
        SetResistance(ResistanceType.Energy, 10, 20);

        SetSkill(SkillName.Anatomy, 0);
        SetSkill(SkillName.Archery, 80.1, 90.0);
        SetSkill(SkillName.MagicResist, 81.1, 90.0);
        SetSkill(SkillName.Tactics, 53.8, 75.0);
        SetSkill(SkillName.Wrestling, 62.3, 75.0);

        Fame = 6500;
        Karma = -6500;

        VirtualArmor = 56;

        AddItem(new Bow());
        PackItem(new Arrow(Utility.RandomMinMax(50, 70)));
    }

    public override string CorpseName => "a clan chitter assistant corpse";
    public override string DefaultName => "Clan Chitter Assistant";

    public override bool CanRummageCorpses => true;
    public override int Hides => 8;
    public override HideType HideType => HideType.Spined;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
    }
}

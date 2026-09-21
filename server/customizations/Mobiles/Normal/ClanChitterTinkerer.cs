// ServUO: Mobiles/Normal/ClanCT.cs (CC6 batch 7). Values verbatim; serialization by the generator. The type is named
// by the spawn-data spelling (post-uoml/termur/Abyss.json, [982,491] with RakktaviRenowned), not ServUO's ClanCT
// (Q-056).
//
// ONE DEVIATION, D-76: the name. ServUO's file says Name = "Clan Scratch Tinkerer" and "a clan scratch tinkerer
// corpse", disagreeing with its own class name (C-T), with the spawn data (ClanChitterTinkerer), with the spawner it
// shares with the Chitter clan's boss Rakktavi, and with UOGuide, whose page is "Clan Chitter Tinkerer" and which has
// no "Clan Scratch Tinkerer" (checked 2026-09-21). The fidelity principle is what a player sees against UOGuide, so
// the name is the OSI one; a ServUO bug fixed, docs/bug-list.md §4's kind. Everything else is ServUO's, including the
// Deserialize body fixup that is not ported (see ClanChitterAssistant.cs). Not in pinned's Repond slayer group or
// Vermin talisman group (D-77, D-78).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ClanChitterTinkerer : BaseCreature
{
    [Constructible]
    public ClanChitterTinkerer() : base(AIType.AI_Archer)
    {
        Body = 0x8E;
        BaseSoundID = 437;

        SetStr(300, 330);
        SetDex(220, 240);
        SetInt(240, 275);

        SetHits(2025, 2068);

        SetDamage(4, 10);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 20, 30);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 35, 50);
        SetResistance(ResistanceType.Poison, 10, 20);
        SetResistance(ResistanceType.Energy, 10, 20);

        SetSkill(SkillName.Anatomy, 62.5, 82.6);
        SetSkill(SkillName.Archery, 80.1, 90.0);
        SetSkill(SkillName.MagicResist, 76.8, 99.3);
        SetSkill(SkillName.Tactics, 64.2, 84.4);
        SetSkill(SkillName.Wrestling, 62.8, 85.0);

        Fame = 6500;
        Karma = -6500;

        VirtualArmor = 56;

        AddItem(new Bow());
        PackItem(new Arrow(Utility.RandomMinMax(50, 70)));
    }

    public override string CorpseName => "a clan chitter tinkerer corpse"; // D-76: ServUO "a clan scratch tinkerer corpse"
    public override string DefaultName => "Clan Chitter Tinkerer";         // D-76: ServUO "Clan Scratch Tinkerer"

    public override bool CanRummageCorpses => true;
    public override int Hides => 8;
    public override HideType HideType => HideType.Spined;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

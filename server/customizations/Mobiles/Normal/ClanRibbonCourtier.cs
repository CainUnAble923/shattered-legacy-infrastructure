// ServUO: Mobiles/Normal/ClanRC.cs (CC6 batch 7). Values verbatim; serialization by the generator. The type is named
// by the spawn-data spelling (post-uoml/termur/Abyss.json, [919,502] with VitaviRenowned), not ServUO's ClanRC
// (Q-056). Not in pinned's Repond slayer group or Vermin talisman group (D-77, D-78).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ClanRibbonCourtier : BaseCreature
{
    [Constructible]
    public ClanRibbonCourtier() : base(AIType.AI_Melee)
    {
        Body = 42;
        Hue = 2207;
        BaseSoundID = 437;

        SetStr(231);
        SetDex(252);
        SetInt(125);

        SetHits(2054, 2100);

        SetDamage(7, 14);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 40);
        SetResistance(ResistanceType.Fire, 10, 12);
        SetResistance(ResistanceType.Cold, 15, 20);
        SetResistance(ResistanceType.Poison, 10, 12);
        SetResistance(ResistanceType.Energy, 10, 12);

        SetSkill(SkillName.MagicResist, 113.5, 115.0);
        SetSkill(SkillName.Tactics, 65.1, 70.0);
        SetSkill(SkillName.Wrestling, 50.5, 55.0);

        Fame = 1500;
        Karma = -1500;

        VirtualArmor = 48;
    }

    public override string CorpseName => "a clan ribbon courtier corpse";
    public override string DefaultName => "Clan Ribbon Courtier";

    public override bool CanRummageCorpses => true;
    public override int Hides => 8;
    public override HideType HideType => HideType.Spined;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.Average);
    }
}

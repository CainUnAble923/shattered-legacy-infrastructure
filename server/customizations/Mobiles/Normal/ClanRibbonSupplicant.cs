// ServUO: Mobiles/Normal/ClanRS.cs (CC6 batch 7). Values verbatim; serialization by the generator. The type is named
// by the spawn-data spelling (post-uoml/termur/Abyss.json, [919,502] with VitaviRenowned), not ServUO's ClanRS
// (Q-056). Not in pinned's Repond slayer group or Vermin talisman group (D-77, D-78).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ClanRibbonSupplicant : BaseCreature
{
    [Constructible]
    public ClanRibbonSupplicant() : base(AIType.AI_Melee)
    {
        Body = 42;
        Hue = 2952;
        BaseSoundID = 437;

        SetStr(173);
        SetDex(117);
        SetInt(207);

        SetHits(127);

        SetDamage(7, 14);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 55, 60);
        SetResistance(ResistanceType.Fire, 30, 35);
        SetResistance(ResistanceType.Cold, 80, 85);
        SetResistance(ResistanceType.Poison, 45, 50);
        SetResistance(ResistanceType.Energy, 25, 30);

        SetSkill(SkillName.MagicResist, 78.5, 80.0);
        SetSkill(SkillName.Tactics, 62.1, 65.0);
        SetSkill(SkillName.Wrestling, 56.5, 60.0);

        Fame = 1500;
        Karma = -1500;

        VirtualArmor = 48;
    }

    public override string CorpseName => "a clan ribbon supplicant corpse";
    public override string DefaultName => "Clan Ribbon Supplicant";

    public override bool CanRummageCorpses => true;
    public override int Hides => 8;
    public override HideType HideType => HideType.Spined;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 3);
    }
}

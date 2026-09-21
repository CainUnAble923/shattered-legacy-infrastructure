// ServUO: Mobiles/Normal/ClanSH.cs (CC6 batch 7). Values verbatim; serialization by the generator. The type is named
// by the spawn-data spelling (post-uoml/termur/Abyss.json, [949,555] with TikitaviRenowned), not ServUO's ClanSH
// (Q-056). Not in pinned's Repond slayer group or Vermin talisman group (D-77, D-78).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ClanScratchHenchrat : BaseCreature
{
    [Constructible]
    public ClanScratchHenchrat() : base(AIType.AI_Melee)
    {
        Body = 42;
        BaseSoundID = 437;

        SetStr(227);
        SetDex(183);
        SetInt(93);

        SetHits(2065);

        SetDamage(5, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 26, 30);
        SetResistance(ResistanceType.Fire, 29, 35);
        SetResistance(ResistanceType.Cold, 30, 35);
        SetResistance(ResistanceType.Poison, 15, 20);
        SetResistance(ResistanceType.Energy, 13, 15);

        SetSkill(SkillName.MagicResist, 35.4, 40.0);
        SetSkill(SkillName.Tactics, 61.1, 65.0);
        SetSkill(SkillName.Wrestling, 64.0, 65.0);
        SetSkill(SkillName.Anatomy, 74.0, 75.0);

        Fame = 1500;
        Karma = -1500;

        VirtualArmor = 48;
    }

    public override string CorpseName => "a clan scratch henchrat corpse";
    public override string DefaultName => "Clan Scratch Henchrat";

    public override bool CanRummageCorpses => true;
    public override int Hides => 8;
    public override HideType HideType => HideType.Spined;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 3);
    }
}

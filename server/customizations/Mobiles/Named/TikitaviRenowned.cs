// ServUO: Mobiles/Named/TikitaviRenowned.cs (CC6 batch 5). Values verbatim; serialization by the generator.
// SetHits(50000) is ServUO's (note section 5). Dropped: AllureImmune => true (no reader in pinned ModernUO).

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Misc;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class TikitaviRenowned : BaseRenowned
{
    [Constructible]
    public TikitaviRenowned() : base(AIType.AI_Melee)
    {
        Title = "[Renowned]";
        Body = 42;
        BaseSoundID = 437;

        SetStr(315, 354);
        SetDex(139, 177);
        SetInt(243, 288);

        SetHits(50000);
        SetMana(243, 288);
        SetStam(139, 177);

        SetDamage(7, 9);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 26, 28);
        SetResistance(ResistanceType.Fire, 22, 25);
        SetResistance(ResistanceType.Cold, 30, 38);
        SetResistance(ResistanceType.Poison, 14, 17);
        SetResistance(ResistanceType.Energy, 15, 18);

        SetSkill(SkillName.MagicResist, 40.4);
        SetSkill(SkillName.Tactics, 73.6);
        SetSkill(SkillName.Wrestling, 66.5);

        Fame = 1500;
        Karma = -1500;

        VirtualArmor = 28;
    }

    public override string CorpseName => "Tikitavi [Renowned] corpse";
    public override string DefaultName => "Tikitavi";

    public override Type[] UniqueSAList => new[] { typeof(BasiliskHideBreastplate) };
    public override Type[] SharedSAList => new[] { typeof(LegacyOfDespair), typeof(MysticsGarb) };

    public override InhumanSpeech SpeechType => InhumanSpeech.Ratman;
    public override bool CanRummageCorpses => true;
    public override int Hides => 8;
    public override HideType HideType => HideType.Spined;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 3);
    }
}

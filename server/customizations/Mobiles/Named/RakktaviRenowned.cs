// ServUO: Mobiles/Named/RakktaviRenowned.cs (CC6 batch 5). Values verbatim; serialization by the generator.
// SetHits(50000) is ServUO's (note section 5). Dropped: AllureImmune => true (no reader in pinned ModernUO).

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Misc;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class RakktaviRenowned : BaseRenowned
{
    [Constructible]
    public RakktaviRenowned() : base(AIType.AI_Archer)
    {
        Title = "[Renowned]";
        Body = 0x8E;
        BaseSoundID = 437;

        SetStr(119);
        SetDex(279);
        SetInt(327);

        SetHits(50000);
        SetMana(327);
        SetStam(279);

        SetDamage(8, 10);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 20, 30);
        SetResistance(ResistanceType.Fire, 10, 25);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Poison, 10, 20);
        SetResistance(ResistanceType.Energy, 10, 20);

        SetSkill(SkillName.Anatomy, 0);
        SetSkill(SkillName.Archery, 80.1, 90.0);
        SetSkill(SkillName.MagicResist, 66.0);
        SetSkill(SkillName.Tactics, 68.1);
        SetSkill(SkillName.Wrestling, 85.5);

        Fame = 6500;
        Karma = -6500;

        VirtualArmor = 56;

        AddItem(new Bow());
        PackItem(new Arrow(Utility.RandomMinMax(10, 30)));
    }

    public override string CorpseName => "Rakktavi [Renowned] corpse";
    public override string DefaultName => "Rakktavi";

    public override Type[] UniqueSAList => Array.Empty<Type>();
    public override Type[] SharedSAList => new[] { typeof(CavalrysFolly), typeof(TorcOfTheGuardians) };

    public override InhumanSpeech SpeechType => InhumanSpeech.Ratman;
    public override bool CanRummageCorpses => true;
    public override int Hides => 8;
    public override HideType HideType => HideType.Spined;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 3);
    }
}

// ServUO: Mobiles/Named/VitaviRenowned.cs (CC6 batch 5). Values verbatim; serialization by the generator.
// AIType.AI_Mystic is AI_Mage: pinned ModernUO has no Mysticism AI (D-41, P1's AI_Mystic row; the CrazedMage, Skree
// and FairyDragon call). Unlike the fairy dragon this one sets Magery 70.1-80.0, so MageAI has a real skill to cast
// from. SetHits(45000, 50000) is ServUO's (note section 5).
// Dropped: AllureImmune => true (no reader in pinned ModernUO).

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Misc;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class VitaviRenowned : BaseRenowned
{
    [Constructible]
    public VitaviRenowned() : base(AIType.AI_Mage)
    {
        Title = "[Renowned]";
        Body = 0x8F;
        BaseSoundID = 437;

        SetStr(300, 350);
        SetDex(250, 300);
        SetInt(300, 350);

        SetHits(45000, 50000);

        SetDamage(7, 14);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 30, 50);
        SetResistance(ResistanceType.Cold, 60, 80);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.EvalInt, 70.1, 80.0);
        SetSkill(SkillName.Magery, 70.1, 80.0);
        SetSkill(SkillName.MagicResist, 75.1, 100.0);
        SetSkill(SkillName.Tactics, 70.1, 75.0);
        SetSkill(SkillName.Wrestling, 50.1, 75.0);

        Fame = 7500;
        Karma = -7500;

        VirtualArmor = 44;

        PackReg(6);

        if (0.02 > Utility.RandomDouble())
        {
            PackStatue();
        }
    }

    public override string CorpseName => "Vitavi [Renowned] corpse";
    public override string DefaultName => "Vitavi";

    public override Type[] UniqueSAList => Array.Empty<Type>();
    public override Type[] SharedSAList => new[] { typeof(AxeOfAbandon), typeof(DemonBridleRing), typeof(VoidInfusedKilt) };

    public override InhumanSpeech SpeechType => InhumanSpeech.Ratman;
    public override bool CanRummageCorpses => true;
    public override int Meat => 1;
    public override int Hides => 8;
    public override HideType HideType => HideType.Spined;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 3);
    }
}

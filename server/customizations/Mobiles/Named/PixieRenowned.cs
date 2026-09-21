// ServUO: Mobiles/Named/PixieRenowned.cs (CC6 batch 5). Values verbatim; serialization by the generator.
// SetStr(-350, 380) is ServUO's: RawStr clamps at 1 on both emulators, so about half of these pixies have 1 Str.
// SetInt(700, 8500) and Karma +7000 are ServUO's too (note section 5). Dropped: nothing.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class PixieRenowned : BaseRenowned
{
    [Constructible]
    public PixieRenowned() : base(AIType.AI_Mage)
    {
        Title = "[Renowned]";
        Body = 128;
        BaseSoundID = 0x467;

        SetStr(-350, 380);
        SetDex(450, 600);
        SetInt(700, 8500);

        SetHits(9100, 9200);
        SetStam(450, 600);
        SetMana(700, 800);

        SetDamage(9, 15);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 70, 90);
        SetResistance(ResistanceType.Fire, 60, 70);
        SetResistance(ResistanceType.Cold, 70, 80);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.EvalInt, 100.0, 100.0);
        SetSkill(SkillName.Magery, 90.1, 110.0);
        SetSkill(SkillName.Meditation, 100.0, 100.0);
        SetSkill(SkillName.MagicResist, 110.5, 150.0);
        SetSkill(SkillName.Tactics, 100.1, 120.0);
        SetSkill(SkillName.Wrestling, 100.1, 120.0);

        Fame = 7000;
        Karma = 7000;

        VirtualArmor = 100;

        if (0.02 > Utility.RandomDouble())
        {
            PackStatue();
        }
    }

    public override string CorpseName => "Pixie [Renowned] corpse";
    public override string DefaultName => "Pixie";

    public override Type[] UniqueSAList => new[] { typeof(DemonHuntersStandard), typeof(DragonJadeEarrings) };
    public override Type[] SharedSAList => new[] { typeof(PillarOfStrength), typeof(SwordOfShatteredHopes) };

    public override bool InitialInnocent => true;
    public override HideType HideType => HideType.Spined;
    public override int Hides => 5;
    public override int Meat => 1;
    public override OppositionGroup OppositionGroup => OppositionGroup.FeyAndUndead;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}

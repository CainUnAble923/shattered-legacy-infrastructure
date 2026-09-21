// ServUO: Mobiles/Named/AncientLichRenowned.cs (CC6 batch 5). Values verbatim; serialization by the generator.
// AIType.AI_NecroMage is AI_Mage: pinned MageAI already casts Necromancy from the creature's own skill (Q-050, the
// SkeletalLich precedent; no D-number). Dropped: nothing.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class AncientLichRenowned : BaseRenowned
{
    [Constructible]
    public AncientLichRenowned() : base(AIType.AI_Mage)
    {
        Title = "[Renowned]";
        Body = 78;
        BaseSoundID = 412;

        SetStr(250, 305);
        SetDex(96, 115);
        SetInt(966, 1045);

        SetHits(2000, 2500);

        SetDamage(15, 27);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Cold, 40);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 25, 30);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 25, 30);

        SetSkill(SkillName.EvalInt, 120.1, 130.0);
        SetSkill(SkillName.Magery, 120.1, 130.0);
        SetSkill(SkillName.Meditation, 100.1, 101.0);
        SetSkill(SkillName.MagicResist, 175.2, 200.0);
        SetSkill(SkillName.Tactics, 90.1, 100.0);
        SetSkill(SkillName.Wrestling, 75.1, 100.0);

        Fame = 23000;
        Karma = -23000;

        VirtualArmor = 60;

        PackNecroReg(30, 275);
    }

    public override string CorpseName => "Ancient Lich [Renowned] corpse";
    public override string DefaultName => "Ancient Lich";

    public override Type[] UniqueSAList => new[] { typeof(SpinedBloodwormBracers), typeof(DefenderOfTheMagus) };
    public override Type[] SharedSAList => new[] { typeof(SummonersKilt) };

    public override OppositionGroup OppositionGroup => OppositionGroup.FeyAndUndead;
    public override bool Unprovokable => true;
    public override bool BleedImmune => true;
    public override Poison PoisonImmune => Poison.Lethal;

    public override int GetIdleSound() => 0x19D;
    public override int GetAngerSound() => 0x175;
    public override int GetDeathSound() => 0x108;
    public override int GetAttackSound() => 0xE2;
    public override int GetHurtSound() => 0x28B;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
    }
}

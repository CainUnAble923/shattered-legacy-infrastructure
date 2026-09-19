// ServUO: Mobiles/Normal/UndeadGuardian.cs (CC6 batch 1). Values verbatim; serialization by the generator. ServUO
// sets no Fame, Karma, VirtualArmor, hue or sound id; none is added. Its own comment on the reagents is kept.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class UndeadGuardian : BaseCreature
{
    [Constructible]
    public UndeadGuardian() : base(AIType.AI_Melee)
    {
        Body = 722;

        SetStr(212);
        SetDex(76);
        SetInt(56);

        SetHits(138);

        SetDamage(8, 18);

        SetDamageType(ResistanceType.Physical, 40);
        SetDamageType(ResistanceType.Cold, 60);

        SetResistance(ResistanceType.Physical, 38);
        SetResistance(ResistanceType.Fire, 24);
        SetResistance(ResistanceType.Cold, 58);
        SetResistance(ResistanceType.Poison, 28);
        SetResistance(ResistanceType.Energy, 38);

        SetSkill(SkillName.MagicResist, 66.6);
        SetSkill(SkillName.Tactics, 86.2);
        SetSkill(SkillName.Wrestling, 86.9);

        PackNecroReg(10, 15); // Stratics didn't specify
    }

    public override string CorpseName => "an undead guardian corpse";
    public override string DefaultName => "an undead guardian";

    public override int Meat => 1;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 3);
    }

    public override int GetIdleSound() => 1609;
    public override int GetAngerSound() => 1606;
    public override int GetHurtSound() => 1608;
    public override int GetDeathSound() => 1607;
}

// ServUO: Mobiles/Normal/PutridUndeadGuardian.cs (CC6 batch 1). Values verbatim (including the article in the name
// and the corpse name); serialization by the generator. ServUO sets no VirtualArmor, hue or sound id; none is
// added. Its own comment on the reagents is kept.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class PutridUndeadGuardian : BaseCreature
{
    [Constructible]
    public PutridUndeadGuardian() : base(AIType.AI_Melee)
    {
        Body = 722;

        SetStr(79);
        SetDex(63);
        SetInt(187);

        SetHits(553);

        SetDamage(3, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 40);
        SetResistance(ResistanceType.Fire, 23);
        SetResistance(ResistanceType.Cold, 57);
        SetResistance(ResistanceType.Poison, 29);
        SetResistance(ResistanceType.Energy, 39);

        SetSkill(SkillName.MagicResist, 62.7);
        SetSkill(SkillName.Tactics, 45.4);
        SetSkill(SkillName.Wrestling, 50.7);

        Fame = 3000;
        Karma = -3000;

        PackNecroReg(10, 15); // Stratics didn't specify
    }

    public override string CorpseName => "an putrid undead guardian corpse";
    public override string DefaultName => "an putrid undead guardian";

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

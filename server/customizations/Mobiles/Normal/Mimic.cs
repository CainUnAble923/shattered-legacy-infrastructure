// ServUO: Mobiles/Normal/Mimic.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).
//
// OSI's Mimic, under its own name. The shard's ChestMimic (cc-P50, F-32) is a different type that borrows this one's
// level-4 stats, sounds and loot; it is unchanged, and this creature is not a chest.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Mimic : BaseCreature
{
    [Constructible]
    public Mimic()
        : base(AIType.AI_Mage)
    {
        Body = 729;

        SetStr(281);
        SetDex(140);
        SetInt(261);

        SetHits(543);

        SetDamage(13, 20);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Fire, 20);
        SetDamageType(ResistanceType.Cold, 20);
        SetDamageType(ResistanceType.Poison, 20);
        SetDamageType(ResistanceType.Energy, 20);

        SetResistance(ResistanceType.Physical, 63);
        SetResistance(ResistanceType.Fire, 43);
        SetResistance(ResistanceType.Cold, 36);
        SetResistance(ResistanceType.Poison, 37);
        SetResistance(ResistanceType.Energy, 42);

        SetSkill(SkillName.EvalInt, 100.0);
        SetSkill(SkillName.Magery, 107.5);
        SetSkill(SkillName.Meditation, 100.0);
        SetSkill(SkillName.MagicResist, 126.5);
        SetSkill(SkillName.Tactics, 98.5);
        SetSkill(SkillName.Wrestling, 92.2);

        PackReg(20);
    }

    public override string CorpseName => "a mimic corpse";
    public override string DefaultName => "a mimic";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 4);
        AddLoot(LootPack.MedScrolls);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.03)
        {
            c.DropItem(new LuckyCoin());
        }
    }

    public override int GetIdleSound() => 1561;

    public override int GetAngerSound() => 1558;

    public override int GetHurtSound() => 1560;

    public override int GetDeathSound() => 1559;
}

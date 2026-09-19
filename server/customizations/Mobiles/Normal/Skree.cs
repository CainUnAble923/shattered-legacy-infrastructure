// ServUO: Mobiles/Normal/Skree.cs (CC6 batch 1). Values verbatim; serialization by the generator.
//
// DEVIATION: ServUO runs this on AI_Mystic (MysticAI : MageAI, casting Mysticism with a 50% fall-through to
// Magery when Magery >= 20). ModernUO's AIType has no Mysticism AI, so it runs AI_Mage and casts Magery only;
// the same call CC4 Shame made for CrazedMage (D-41). The Mysticism skill range is kept. ServUO sets no Fame,
// Karma or VirtualArmor; none is added.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Skree : BaseCreature
{
    [Constructible]
    public Skree() : base(AIType.AI_Mage)
    {
        Body = 733;

        SetStr(297, 330);
        SetDex(96, 124);
        SetInt(188, 260);

        SetHits(205, 300);

        SetDamage(5, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 45, 55);
        SetResistance(ResistanceType.Cold, 25, 40);
        SetResistance(ResistanceType.Poison, 55, 65);
        SetResistance(ResistanceType.Energy, 25, 40);

        SetSkill(SkillName.EvalInt, 90.6, 115.0);
        SetSkill(SkillName.Magery, 90.2, 114.2);
        SetSkill(SkillName.Meditation, 65.3, 75.0);
        SetSkill(SkillName.MagicResist, 75.1, 90.0);
        SetSkill(SkillName.Tactics, 20.2, 24.7);
        SetSkill(SkillName.Wrestling, 101.9, 117.9);
        SetSkill(SkillName.Mysticism, 80, 105.0);
        SetSkill(SkillName.Parry, 75, 85);

        Tamable = true;
        ControlSlots = 4;
        MinTameSkill = 95.1;
    }

    public override string CorpseName => "a skree corpse";
    public override string DefaultName => "a skree";

    public override bool CanAngerOnTame => true;

    public override int Meat => 3;
    public override MeatType MeatType => MeatType.Bird;
    public override int Hides => 5;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
    }

    public override int GetIdleSound() => 1585;
    public override int GetAngerSound() => 1582;
    public override int GetHurtSound() => 1584;
    public override int GetDeathSound() => 1583;
}

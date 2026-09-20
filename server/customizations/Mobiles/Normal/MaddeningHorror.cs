// ServUO: Mobiles/Normal/MaddeningHorror.cs (CC6 batch 3). Values verbatim; serialization by the generator. Drops
// VileTentacles (ours) 20% of the time.
//
// AI: ServUO runs it on AI_NecroMage. It runs AI_Mage here, and that is NOT a deviation (Q-050, correcting D-47):
// pinned MageAI carries the necromancer branch itself (IsNecromancer at Necromancy > 50, MageAI.cs:70; UseNecromancy
// weighted Random(magery + necro) >= magery, :252-257; the same damage and curse spell sets as ServUO's NecromageAI).
// At Magery 120-130 and Necromancy 120 this casts Necromancy about 48% of the time against ServUO's flat 50%.
//
// Dropped: SetSpecialAbility(SpecialAbility.ManaDrain) (D-66: Pet Training, declined B3, the Saurosaurus precedent).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class MaddeningHorror : BaseCreature
{
    [Constructible]
    public MaddeningHorror() : base(AIType.AI_Mage)
    {
        Body = 721;

        SetStr(270, 290);
        SetDex(80, 100);
        SetInt(850);

        SetHits(660);

        SetDamage(15, 27);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Cold, 40);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 55, 65);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.EvalInt, 120.0, 130.0);
        SetSkill(SkillName.Magery, 120.0, 130.0);
        SetSkill(SkillName.Meditation, 100.0, 110.0);
        SetSkill(SkillName.MagicResist, 180.0, 195.0);
        SetSkill(SkillName.Tactics, 95.0, 100.0);
        SetSkill(SkillName.Wrestling, 80.0, 85.0);
        SetSkill(SkillName.Poisoning, 110.0);
        SetSkill(SkillName.DetectHidden, 100.0);
        SetSkill(SkillName.Necromancy, 120.0);
        SetSkill(SkillName.SpiritSpeak, 120.0);

        Fame = 23000;
        Karma = -23000;
    }

    public override string CorpseName => "a maddening horror corpse";
    public override string DefaultName => "a maddening horror";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.2)
        {
            c.DropItem(new VileTentacles());
        }
    }

    public override int GetIdleSound() => 1553;
    public override int GetAngerSound() => 1550;
    public override int GetHurtSound() => 1552;
    public override int GetDeathSound() => 1551;
}

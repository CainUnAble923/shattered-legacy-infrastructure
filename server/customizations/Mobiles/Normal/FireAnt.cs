// ServUO: Mobiles/Normal/FireAnt.cs (CC6 batch 3). Values verbatim; serialization by the generator. ServUO sets no
// Fame or Karma; neither does this. Drops SearedFireAntGoo (ours, extracted from SAQuestItems.cs) 25% of the time.
//
// Dropped: SetAreaEffect(AreaEffect.ExplosiveGoo) (D-65: Pet Training, declined B3, the Saurosaurus precedent).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class FireAnt : BaseCreature
{
    [Constructible]
    public FireAnt() : base(AIType.AI_Melee)
    {
        Body = 738;

        SetStr(225);
        SetDex(108);
        SetInt(25);

        SetHits(299);

        SetDamage(15, 18);

        SetDamageType(ResistanceType.Physical, 40);
        SetDamageType(ResistanceType.Fire, 60);

        SetResistance(ResistanceType.Physical, 52);
        SetResistance(ResistanceType.Fire, 96);
        SetResistance(ResistanceType.Cold, 36);
        SetResistance(ResistanceType.Poison, 40);
        SetResistance(ResistanceType.Energy, 36);

        SetSkill(SkillName.Anatomy, 8.7);
        SetSkill(SkillName.MagicResist, 53.1);
        SetSkill(SkillName.Tactics, 77.2);
        SetSkill(SkillName.Wrestling, 75.4);
    }

    public override string CorpseName => "a fire ant corpse";
    public override string DefaultName => "a fire ant";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average, 2);
    }

    public override int TreasureMapLevel => 3;

    public override int GetIdleSound() => 846;
    public override int GetAngerSound() => 849;
    public override int GetHurtSound() => 852;
    public override int GetDeathSound() => 850;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.25)
        {
            c.DropItem(new SearedFireAntGoo());
        }
    }
}

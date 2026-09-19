// ServUO: Mobiles/Normal/LavaElemental.cs (CC6 batch 2). Values verbatim; serialization by the generator.
// ServUO sets no Fame or Karma on this creature; neither does this.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class LavaElemental : BaseCreature
{
    [Constructible]
    public LavaElemental() : base(AIType.AI_Mage)
    {
        Body = 720;

        SetStr(446, 510);
        SetDex(160, 190);
        SetInt(360, 430);

        SetHits(270, 290);

        SetDamage(12, 18);

        SetDamageType(ResistanceType.Physical, 10);
        SetDamageType(ResistanceType.Fire, 90);

        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.EvalInt, 84.8, 92.6);
        SetSkill(SkillName.Magery, 80.0, 92.7);
        SetSkill(SkillName.Meditation, 97.8, 120.0);
        SetSkill(SkillName.MagicResist, 101.9, 106.2);
        SetSkill(SkillName.Tactics, 80.3, 94.0);
        SetSkill(SkillName.Wrestling, 71.7, 85.4);
        SetSkill(SkillName.Poisoning, 90.0, 100.0);
        SetSkill(SkillName.DetectHidden, 75.1);

        PackItem(new Nightshade(4));
        PackItem(new SulfurousAsh(5));
        PackItem(new LesserPoisonPotion());
    }

    public override string CorpseName => "a lava elemental corpse";
    public override string DefaultName => "a lava elemental";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 3);
        AddLoot(LootPack.Gems, 2);
        AddLoot(LootPack.MedScrolls);
    }

    public override int GetAttackSound() => 0x60A;
    public override int GetDeathSound() => 0x60B;
    public override int GetHurtSound() => 0x60C;
    public override int GetIdleSound() => 0x60D;
}

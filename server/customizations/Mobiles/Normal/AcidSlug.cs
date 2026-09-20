// ServUO: Mobiles/Normal/AcidSlug.cs (CC6 batch 3). Values verbatim; serialization by the generator. ServUO sets no
// Fame or Karma; neither does this. The IAcidCreature marker ServUO declares in AcidElemental.cs is
// Mobiles/IAcidCreature.cs here. Packs an AcidSac 75% of the time and a CongealedSlugAcid always (both ours, extracted
// from SAQuestItems.cs). CheckMovement keeps it from climbing while in the Underworld, as written; Region.IsPartOf
// (Server/Regions/Region.cs:545) has the same shape. Nothing is dropped.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class AcidSlug : BaseCreature, IAcidCreature
{
    [Constructible]
    public AcidSlug() : base(AIType.AI_Melee)
    {
        Body = 51;
        Hue = Utility.RandomList(242, 243, 244, 245);

        SetStr(213, 294);
        SetDex(80, 82);
        SetInt(18, 22);

        SetHits(333, 370);

        SetDamage(21, 28);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 10, 15);
        SetResistance(ResistanceType.Fire, 0);
        SetResistance(ResistanceType.Cold, 10, 15);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 10, 15);

        SetSkill(SkillName.MagicResist, 25.0);
        SetSkill(SkillName.Tactics, 30.0, 50.0);
        SetSkill(SkillName.Wrestling, 30.0, 80.0);

        if (Utility.RandomDouble() < 0.75)
        {
            PackItem(new AcidSac());
        }

        PackItem(new CongealedSlugAcid());
    }

    public override string CorpseName => "an acid slug corpse";
    public override string DefaultName => "an acid slug";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
    }

    public override int GetIdleSound() => 1499;
    public override int GetAngerSound() => 1496;
    public override int GetHurtSound() => 1498;
    public override int GetDeathSound() => 1497;

    public override bool CheckMovement(Direction d, out int newZ)
    {
        if (!base.CheckMovement(d, out newZ))
        {
            return false;
        }

        return !(Region.IsPartOf("Underworld") && newZ > Location.Z);
    }
}

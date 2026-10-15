// ServUO: Mobiles/Normal/Ortanord.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Ortanord : BaseCreature
{
    [Constructible]
    public Ortanord()
        : base(AIType.AI_Melee)
    {
        Body = 58;
        Hue = 2071;
        BaseSoundID = 466;

        SetStr(50, 50);
        SetDex(50, 50);
        SetInt(51, 51);

        SetHits(100, 100);
        SetMana(1001, 1001);
        SetStam(50, 50);

        SetDamage(5, 8);

        SetDamageType(ResistanceType.Energy, 100);

        SetResistance(ResistanceType.Physical, 80, 90);
        SetResistance(ResistanceType.Fire, 80, 90);
        SetResistance(ResistanceType.Cold, 80, 90);
        SetResistance(ResistanceType.Poison, 80, 90);
        SetResistance(ResistanceType.Energy, 80, 90);

        SetSkill(SkillName.MagicResist, 104.4, 108.0);
        SetSkill(SkillName.Tactics, 19.0, 19.8);
        SetSkill(SkillName.Anatomy, 15.6, 16.8);
        SetSkill(SkillName.Wrestling, 15.4, 16.6);
        SetSkill(SkillName.Magery, 104.7, 107.3);
        SetSkill(SkillName.Meditation, 20.0, 20.0);

        Fame = 8000;
        Karma = -8000;

        VirtualArmor = 40;

        if (0.25 > Utility.RandomDouble())
        {
            PackItem(new DaemonBone(10));
        }
    }

    public override string CorpseName => "an ortanord corpse";
    public override string DefaultName => "Ortanord";

    public override bool BardImmune => !Core.AOS;
    public override Poison PoisonImmune => Poison.Lethal;
    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average, 2);
    }

    // ServUO's OnDeath only calls base; not ported.
}

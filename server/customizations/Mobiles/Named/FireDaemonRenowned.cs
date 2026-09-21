// ServUO: Mobiles/Named/FireDaemonRenowned.cs (CC6 batch 5). Values verbatim; serialization by the generator.
// SetWeaponAbility(ConcussionBlow) is GetWeaponAbility (CC3). Dropped: nothing.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class FireDaemonRenowned : BaseRenowned
{
    [Constructible]
    public FireDaemonRenowned() : base(AIType.AI_Mage)
    {
        Title = "[Renowned]";
        Body = 40;
        BaseSoundID = 357;

        Hue = 243;

        SetStr(800, 1199);
        SetDex(200, 250);
        SetInt(202, 336);

        SetHits(1111, 1478);

        SetDamage(22, 29);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Fire, 25);
        SetDamageType(ResistanceType.Energy, 25);

        SetResistance(ResistanceType.Physical, 60, 93);
        SetResistance(ResistanceType.Fire, 60, 100);
        SetResistance(ResistanceType.Cold, 40, 70);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 37, 50);

        SetSkill(SkillName.MagicResist, 110.1, 132.6);
        SetSkill(SkillName.Tactics, 86.9, 95.5);
        SetSkill(SkillName.Wrestling, 42.2, 98.8);
        SetSkill(SkillName.Magery, 97.1, 100.8);
        SetSkill(SkillName.EvalInt, 91.1, 91.8);
        SetSkill(SkillName.Meditation, 45.4, 94.1);
        SetSkill(SkillName.Anatomy, 45.4, 74.1);

        Fame = 7000;
        Karma = -10000;

        VirtualArmor = 55;
    }

    public override string CorpseName => "Fire Daemon [Renowned] corpse";
    public override string DefaultName => "Fire Daemon";

    // Type name as the class declares it: ResonantStaffofEnlightenment (lower-case "of"), in a file named
    // ResonantStaffOfEnlightenment.cs. ServUO's typeof spells it this way too.
    public override Type[] UniqueSAList => new[] { typeof(ResonantStaffofEnlightenment), typeof(MantleOfTheFallen) };
    public override Type[] SharedSAList => Array.Empty<Type>();

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.ConcussionBlow;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
    }
}

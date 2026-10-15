// ServUO: Mobiles/Normal/AbyssalAbomination.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class AbyssalAbomination : BaseCreature
{
    [Constructible]
    public AbyssalAbomination()
        : base(AIType.AI_Mage /* ServUO: AI_NecroMage (Q-050) */)
    {
        Body = 312;
        Hue = 769;
        BaseSoundID = 0x451;

        SetStr(401, 420);
        SetDex(81, 90);
        SetInt(401, 420);

        SetHits(600, 750);

        SetDamage(13, 17);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Poison, 50);

        SetResistance(ResistanceType.Physical, 30, 35);
        SetResistance(ResistanceType.Fire, 100);
        SetResistance(ResistanceType.Cold, 50, 55);
        SetResistance(ResistanceType.Poison, 60, 65);
        SetResistance(ResistanceType.Energy, 77, 80);

        SetSkill(SkillName.EvalInt, 200.0);
        SetSkill(SkillName.Magery, 112.6, 117.5);
        SetSkill(SkillName.SpiritSpeak, 200.0);
        SetSkill(SkillName.Necromancy, 112.6, 117.5);
        SetSkill(SkillName.Meditation, 200.0);
        SetSkill(SkillName.MagicResist, 117.6, 120.0);
        SetSkill(SkillName.Tactics, 100.0);
        SetSkill(SkillName.Wrestling, 84.1, 88.0);

        Fame = 26000;
        Karma = -26000;

        VirtualArmor = 54;
    }

    public override string CorpseName => "an abyssal abomination corpse";
    public override string DefaultName => "an Abyssal Abomination";

    public override bool IgnoreYoungProtection => Core.ML;
    public override bool BardImmune => !Core.SE;
    public override bool Unprovokable => Core.SE;
    public override bool AreaPeaceImmune => Core.SE;
    public override Poison PoisonImmune => Poison.Lethal;

    // ServUO: SetWeaponAbility(MortalStrike) and SetWeaponAbility(WhirlwindAttack), one picked per use.
    public override WeaponAbility GetWeaponAbility() =>
        Utility.RandomBool() ? WeaponAbility.MortalStrike : WeaponAbility.WhirlwindAttack;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}

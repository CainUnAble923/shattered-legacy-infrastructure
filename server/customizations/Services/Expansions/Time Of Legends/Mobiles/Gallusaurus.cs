// ServUO: Services/Expansions/Time Of Legends/Mobiles/Gallusaurus.cs (cc-P198). Values verbatim; serialization by
// the generator.
//
// SetWeaponAbility(Block) is pinned's GetWeaponAbility (port recipe, CC3). SetSpecialAbility(GraspingClaw) is kept
// as written: the ability core (cc-P121) stores it on the profile (BloodFox's shape). AttacksFocus (!Controlled) is
// absent from pinned and dropped (port recipe). ServUO writes version 1 and its version-0 branch re-sets both
// abilities; new content starts at 0 here and never needs it.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Gallusaurus : BaseCreature
{
    [Constructible]
    public Gallusaurus() : base(AIType.AI_Melee)
    {
        Body = 1286;
        BaseSoundID = 0x275;

        SetStr(477, 511);
        SetDex(155, 168);
        SetInt(221, 274);

        SetDamage(11, 17);

        SetHits(700, 900);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 20, 30);

        SetSkill(SkillName.MagicResist, 70.0, 80.0);
        SetSkill(SkillName.Tactics, 80.0, 90.0);
        SetSkill(SkillName.Wrestling, 80.0, 91.0);
        SetSkill(SkillName.Bushido, 110.0, 120.0);
        SetSkill(SkillName.DetectHidden, 25.0, 35.0);

        Fame = 8100;
        Karma = -8100;

        Tamable = true;
        ControlSlots = 3;
        MinTameSkill = 102.0;

        SetSpecialAbility(SpecialAbility.GraspingClaw);
    }

    public override string CorpseName => "a gallusaurus corpse";
    public override string DefaultName => "a gallusaurus";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.Block;

    public override int Meat => 3;
    public override bool CanAngerOnTame => true;
    public override int TreasureMapLevel => 1;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich);
    }
}

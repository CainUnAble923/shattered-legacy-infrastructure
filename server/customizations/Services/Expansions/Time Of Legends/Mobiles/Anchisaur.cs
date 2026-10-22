// ServUO: Services/Expansions/Time Of Legends/Mobiles/Anchisaur.cs (cc-P198). Values verbatim; serialization by the
// generator.
//
// The two SetWeaponAbility calls (Disarm, ParalyzingBlow) are pinned's GetWeaponAbility (port recipe, CC3).
// Dropped, each absent from pinned and ours (port recipe's absent-member table): AttacksFocus, DragonBlood, and the
// OnThink that casts the Rampage mastery or arms special move 740 every 45-70 seconds in combat. Skill Masteries
// are not on this shard (no SkillMasterySpell, RampageSpell or move 740 in either tree); a deviation, in the notes.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Anchisaur : BaseCreature
{
    [Constructible]
    public Anchisaur() : base(AIType.AI_Melee)
    {
        Body = 1292;
        BaseSoundID = 422;

        SetStr(441, 511);
        SetDex(166, 185);
        SetInt(362, 431);

        SetDamage(16, 19);

        SetHits(2663, 3718);

        SetResistance(ResistanceType.Physical, 3, 4);
        SetResistance(ResistanceType.Fire, 3, 4);
        SetResistance(ResistanceType.Cold, 1);
        SetResistance(ResistanceType.Poison, 2, 3);
        SetResistance(ResistanceType.Energy, 2, 3);

        SetDamageType(ResistanceType.Physical, 100);

        SetSkill(SkillName.MagicResist, 105.0, 115.0);
        SetSkill(SkillName.Tactics, 95.0, 105.0);
        SetSkill(SkillName.Wrestling, 100.0, 110.0);
        SetSkill(SkillName.Anatomy, 95.0, 105.0);
        SetSkill(SkillName.DetectHidden, 75.0, 85.0);
        SetSkill(SkillName.Parry, 75.0, 85.0);

        Fame = 8000;
        Karma = -8000;
    }

    public override string CorpseName => "an anchisaur corpse";
    public override string DefaultName => "an anchisaur";

    public override WeaponAbility GetWeaponAbility() =>
        Utility.RandomBool() ? WeaponAbility.Disarm : WeaponAbility.ParalyzingBlow;

    public override int Meat => 6;
    public override int Hides => 11;
    public override int TreasureMapLevel => 1;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich);
    }
}

// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Creatures/AbominableSnowman.cs (cc-P408), the fourth
// boss of the Treasures of Sorcerer's Dungeon rotation (one of its winter-holiday creatures; it is part of the same
// single October event, not a Christmas event of its own: notes).
//
// The port recipe: base(..., 10, 1, 0.2, 0.4) is base(AI_Melee) (Q-008); SetWeaponAbility(ConcussionBlow) and
// SetWeaponAbility(CrushingBlow) are GetWeaponAbility, random of the two; SetSpecialAbility(TrueFear) goes to the
// cc-P121 ability profile, as ServUO's does.

using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

[SerializationGenerator(0, false)]
public partial class AbominableSnowman : BaseCreature
{
    [Constructible]
    public AbominableSnowman() : base(AIType.AI_Melee)
    {
        Name = "the abominable snowman";

        Body = 241;
        BaseSoundID = 367;
        Hue = 1150;

        SetStr(800);
        SetDex(150);
        SetInt(1200);

        SetHits(8000);

        SetDamage(21, 27);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Cold, 50);

        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 100);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.Anatomy, 115, 120);
        SetSkill(SkillName.Poisoning, 120);
        SetSkill(SkillName.MagicResist, 115, 120);
        SetSkill(SkillName.Tactics, 100.0);
        SetSkill(SkillName.Wrestling, 120, 130);

        Fame = 12000;
        Karma = -12000;

        SetSpecialAbility(SpecialAbility.TrueFear);
    }

    public override string CorpseName => "an abominable snowmans corpse";
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Deadly;

    public override WeaponAbility GetWeaponAbility() =>
        Utility.RandomBool() ? WeaponAbility.ConcussionBlow : WeaponAbility.CrushingBlow;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}

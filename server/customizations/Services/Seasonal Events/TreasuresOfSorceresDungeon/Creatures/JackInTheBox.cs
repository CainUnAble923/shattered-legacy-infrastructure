// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Creatures/JackInTheBox.cs (cc-P408), the second boss
// of the Treasures of Sorcerer's Dungeon rotation.
//
// RENAMED: ServUO's class is Server.Engines.SorcerersDungeon.JackInTheBox, and the event's reward toy in the same
// folder is Server.Items.JackInTheBox. Both can be made by [add and a spawner, which look a type up by its SHORT name
// (pinned AssemblyHandler.FindTypeByName, first match wins), so one of the two would always shadow the other, and the
// shard's type-identity gate (tests/TypeIdentity, EverySpawnableTypeResolvesToItselfByShortName) fails the build on
// it. The creature takes the new name; the player sees the same "jack in the box" either way (deviation 3, notes).
//
// The port recipe: AI_NecroMage is AI_Mage (Q-050); base(..., 10, 1, 0.2, 0.4) is base(AI_Mage) (Q-008);
// SetWeaponAbility(ArmorIgnore) and SetWeaponAbility(BleedAttack) are GetWeaponAbility, random of the two (the recipe,
// as StygianDragon): ServUO's TryGetWeaponAbility draws one at random from the list per swing too.

using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

[SerializationGenerator(0, false)]
public partial class JackInTheBoxMonster : BaseCreature
{
    [Constructible]
    public JackInTheBoxMonster() : base(AIType.AI_Mage)
    {
        Name = "jack in the box";
        Body = 1428;

        SetStr(200, 300);
        SetDex(150, 200);
        SetInt(700, 800);

        SetHits(8000);

        SetDamage(21, 27);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 70, 80);
        SetResistance(ResistanceType.Fire, 70, 80);
        SetResistance(ResistanceType.Cold, 70, 80);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.Anatomy, 25);
        SetSkill(SkillName.MagicResist, 120);
        SetSkill(SkillName.Tactics, 100, 110);
        SetSkill(SkillName.Wrestling, 130, 140);
        SetSkill(SkillName.Parry, 20, 30);

        SetSkill(SkillName.Magery, 120);
        SetSkill(SkillName.EvalInt, 120);
        SetSkill(SkillName.Necromancy, 120);
        SetSkill(SkillName.SpiritSpeak, 120);
        SetSkill(SkillName.Meditation, 120);
        SetSkill(SkillName.Focus, 70, 80);

        Fame = 12000;
        Karma = -12000;
    }

    public override string CorpseName => "a jack in the box corpse";
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Deadly;

    public override WeaponAbility GetWeaponAbility() =>
        Utility.RandomBool() ? WeaponAbility.ArmorIgnore : WeaponAbility.BleedAttack;

    public override int GetIdleSound() => 0x218;
    public override int GetAngerSound() => 0x26C;
    public override int GetDeathSound() => 0x211;
    public override int GetAttackSound() => 0x232;
    public override int GetHurtSound() => 0x140;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}

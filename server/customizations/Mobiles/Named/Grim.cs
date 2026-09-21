// ServUO: Mobiles/Named/Grim.cs (CC6 batch 5). Values verbatim; serialization by the generator. One of the four
// Labyrinth named (shared/malas/Labyrinth.json spells it "grim").
//
// A subclass of STOCK Drake (Mobiles/Monsters/Reptile/Melee/Drake.cs, [SerializationGenerator(0, false)]), on batch 4's
// subclass rule: this level carries its own attribute, its own (Serial) constructor and its own version slot after the
// parent's, exactly as stock Saliva : Harpy does. Reviewed against the PINNED Drake, not ServUO's: the two agree member
// for member (random body 60/61, sound 362, tameable at 84.3 with 2 slots, PackReg(3), ReacquireOnMovement, TML 2,
// meat 10, hides 20, horned, 2 scales yellow/red by body, meat-and-fish, CanFly) except that ServUO's has DragonBlood
// => 8 (D-48's row) and SetSpecialAbility(DragonBreath), which pinned Drake carries as GetMonsterAbilities() =>
// [MonsterAbilities.FireBreath] (Drake.cs:56-57). Grim's own SetSpecialAbility(DragonBreath) is therefore redundant on
// both emulators and is inherited here; SetWeaponAbility(CrushingBlow) is GetWeaponAbility (CC3).
//
// Dropped: nothing.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Grim : Drake
{
    [Constructible]
    public Grim()
    {
        Hue = 1744;

        SetStr(527, 580);
        SetDex(284, 322);
        SetInt(249, 386);

        SetHits(1762, 2502);

        SetDamage(17, 25);

        SetDamageType(ResistanceType.Physical, 80);
        SetDamageType(ResistanceType.Fire, 20);

        SetResistance(ResistanceType.Physical, 55, 60);
        SetResistance(ResistanceType.Fire, 62, 68);
        SetResistance(ResistanceType.Cold, 52, 57);
        SetResistance(ResistanceType.Poison, 30, 40);
        SetResistance(ResistanceType.Energy, 40, 44);

        SetSkill(SkillName.MagicResist, 105.8, 115.6);
        SetSkill(SkillName.Tactics, 102.8, 120.8);
        SetSkill(SkillName.Wrestling, 111.7, 119.2);
        SetSkill(SkillName.Anatomy, 105.0, 128.4);

        Fame = 17500;
        Karma = -5500;

        VirtualArmor = 54;

        Tamable = false;

        for (var i = 0; i < Utility.RandomMinMax(0, 1); i++)
        {
            PackItem(Loot.RandomScroll(0, Loot.ArcanistScrollTypes.Length, SpellbookType.Arcanist));
        }
    }

    public override string CorpseName => "the remains of Grim";
    public override string DefaultName => "Grim";

    public override bool GivesMLMinorArtifact => true;

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.CrushingBlow;

    public override bool ReacquireOnMovement => true;
    public override int Meat => 10;
    public override int Hides => 20;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 3);
        AddLoot(LootPack.MedScrolls);
        AddLoot(LootPack.HighScrolls, 2);
    }
}

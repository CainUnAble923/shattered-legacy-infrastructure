// ServUO: Mobiles/Normal/GiantTurkey.cs (cc-P129). Values verbatim; serialization by the generator.
// Three SetWeaponAbility calls (ParalyzingBlow, BleedAttack, Dismount) become one GetWeaponAbility over
// Utility.Random(3), stock DemonKnight's shape (port recipe). EssenceOfDisease and HowlOfCacophony go to the profile.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class GiantTurkey : BaseCreature
{
    [Constructible]
    public GiantTurkey() : base(AIType.AI_Animal, FightMode.Aggressor)
    {
        Body = 1026;
        BaseSoundID = 0x66A;

        SetStr(1200, 1400);
        SetDex(170, 260);
        SetInt(430, 560);

        SetHits(25000);
        SetMana(1000);

        SetDamage(20, 30);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 55, 70);
        SetResistance(ResistanceType.Fire, 70, 90);
        SetResistance(ResistanceType.Cold, 35, 45);
        SetResistance(ResistanceType.Poison, 45, 55);
        SetResistance(ResistanceType.Energy, 45, 55);

        SetSkill(SkillName.MagicResist, 85.0, 100.0);
        SetSkill(SkillName.Tactics, 100.0, 110.0);
        SetSkill(SkillName.Wrestling, 100.0, 120.0);
        SetSkill(SkillName.Anatomy, 75.0, 80.0);

        SetAreaEffect(AreaEffect.EssenceOfDisease);
        SetSpecialAbility(SpecialAbility.HowlOfCacophony);
    }

    public override string CorpseName => "a giant turkey corpse";
    public override string DefaultName => "a giant turkey";

    public override WeaponAbility GetWeaponAbility() =>
        Utility.Random(3) switch
        {
            0 => WeaponAbility.ParalyzingBlow,
            1 => WeaponAbility.BleedAttack,
            _ => WeaponAbility.Dismount
        };

    public override int Meat => 1;
    public override MeatType MeatType => MeatType.Bird;
    public override FoodType FavoriteFood => FoodType.GrainsAndHay;
    public override int Feathers => 25;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich);
    }

    public override int GetIdleSound() => 0x66A;
    public override int GetAngerSound() => 0x66A;
    public override int GetHurtSound() => 0x66B;
    public override int GetDeathSound() => 0x66B;
}

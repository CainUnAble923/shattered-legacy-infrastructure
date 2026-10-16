// ServUO: Mobiles/Normal/FrostDragon.cs (cc-P129). Values verbatim; serialization by the generator.
//
// SetWeaponAbility(BleedAttack) is GetWeaponAbility. SetSpecialAbility(DragonBreath): no stock breath, so the profile's
// pinned twin runs ServUO's FrostDragon definition (100% cold) through DragonBreathDefinitions. SetAreaEffect(AuraDamage):
// pinned's own aura with ServUO's coldAura definition (15 cold every 5 s, range 10), ColdDrake's shape.
// OnAfterTame (Title = null) dropped: pinned has no such hook (port recipe), and this dragon never has a title, so the
// line was a no-op. DragonBlood => 8 dropped: no property, no item.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class FrostDragon : BaseCreature
{
    [Constructible]
    public FrostDragon() : base(AIType.AI_Mage)
    {
        Body = Utility.RandomList(12, 59);
        BaseSoundID = 362;

        Hue = Utility.RandomMinMax(1319, 1327);

        SetStr(1300, 1400);
        SetDex(100, 125);
        SetInt(600, 700);

        SetHits(2050, 2250);

        SetDamage(24, 33);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Cold, 50);

        SetResistance(ResistanceType.Physical, 85, 92);
        SetResistance(ResistanceType.Fire, 55, 70);
        SetResistance(ResistanceType.Cold, 85, 95);
        SetResistance(ResistanceType.Poison, 65, 70);
        SetResistance(ResistanceType.Energy, 65, 75);

        SetSkill(SkillName.EvalInt, 50, 60);
        SetSkill(SkillName.Magery, 120, 130);
        SetSkill(SkillName.MagicResist, 115, 135);
        SetSkill(SkillName.Tactics, 120, 135);
        SetSkill(SkillName.Wrestling, 120, 130);
        SetSkill(SkillName.Meditation, 1, 50);

        Fame = 25000;
        Karma = -25000;

        VirtualArmor = 60;

        Tamable = true;
        ControlSlots = 5;
        MinTameSkill = 105.0;

        SetSpecialAbility(SpecialAbility.DragonBreath);
        SetAreaEffect(AreaEffect.AuraDamage);
    }

    public override string CorpseName => "a dragon corpse";
    public override string DefaultName => "a frost dragon";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
        AddLoot(LootPack.Gems, 8);
    }

    public override bool CanAngerOnTame => true;
    public override bool StatLossAfterTame => true;
    public override bool ReacquireOnMovement => !Controlled;
    public override bool AutoDispel => !Controlled;
    public override int TreasureMapLevel => 4;
    public override int Meat => 19;
    public override int Hides => 33;
    public override HideType HideType => HideType.Barbed;
    public override FoodType FavoriteFood => FoodType.Meat;

    // ServUO: AreaEffect.AuraDamage with the coldAura definition.
    public override bool HasAura => true;
    public override TimeSpan AuraInterval => TimeSpan.FromSeconds(5);
    public override int AuraRange => 10;
    public override int AuraBaseDamage => 15;
    public override int AuraFireDamage => 0;
    public override int AuraColdDamage => 100;

    public override void AuraEffect(Mobile m)
    {
        m.FixedParticles(0x374A, 10, 30, 5052, Hue, 0, EffectLayer.Waist);
        m.PlaySound(0x5C6);

        m.SendLocalizedMessage(1008111); //  : The intense cold is damaging you!
    }
}

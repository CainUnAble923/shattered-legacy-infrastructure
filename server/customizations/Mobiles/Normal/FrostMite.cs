// ServUO: Mobiles/Normal/FrostMite.cs (cc-P129). Values verbatim; serialization by the generator.
//
// DEVIATION (the Wight's, cc-P119, extended here as that note said it would be): ServUO's only weapon ability is
// SetWeaponAbility(WeaponAbility.ColdWind), and pinned has no Cold Wind weapon ability (WeaponAbility.cs, 32 slots), so
// the mite has none. Reverses when a ColdWind WeaponAbility is added (ServUO Abilities/ColdWind.cs plus its slot).
// SetAreaEffect(AuraDamage): pinned's own aura with ServUO's coldAura definition (15 cold every 5 s, range 10).

using System;
using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class FrostMite : BaseCreature
{
    [Constructible]
    public FrostMite() : base(AIType.AI_Melee)
    {
        Body = 0x590;
        Female = true;

        SetStr(1017);
        SetDex(164);
        SetInt(283);

        SetHits(800, 1000);

        SetDamage(21, 28);

        SetDamageType(ResistanceType.Physical, 0);
        SetDamageType(ResistanceType.Cold, 100);

        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 15, 25);
        SetResistance(ResistanceType.Cold, 90, 100);
        SetResistance(ResistanceType.Poison, 50, 70);
        SetResistance(ResistanceType.Energy, 40, 45);

        SetSkill(SkillName.MagicResist, 50.0, 85.0);
        SetSkill(SkillName.Tactics, 70.0, 105.0);
        SetSkill(SkillName.Wrestling, 70.0, 110.0);
        SetSkill(SkillName.DetectHidden, 60.0, 80.0);
        SetSkill(SkillName.Focus, 100.0, 115.0);

        Tamable = true;
        ControlSlots = 3;
        MinTameSkill = 102.0;

        // ServUO: SetWeaponAbility(WeaponAbility.ColdWind); pinned has no Cold Wind (see the header).
        SetAreaEffect(AreaEffect.AuraDamage);
    }

    public override string CorpseName => "a beetle corpse";
    public override string DefaultName => "Frost Mite";

    public override int GetAngerSound() => 0x4E8;
    public override int GetIdleSound() => 0x4E7;
    public override int GetAttackSound() => 0x4E6;
    public override int GetHurtSound() => 0x4E9;
    public override int GetDeathSound() => 0x4E5;

    public override int Meat => 5;
    public override FoodType FavoriteFood => FoodType.Meat;

    public override bool CanAngerOnTame => true;
    public override bool StatLossAfterTame => true;

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

        m.SendLocalizedMessage(1008111, false, Name); //  : The intense cold is damaging you!
    }
}

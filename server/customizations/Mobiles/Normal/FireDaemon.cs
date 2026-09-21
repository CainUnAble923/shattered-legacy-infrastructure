// ServUO: Mobiles/Normal/FireDaemon.cs (CC6 batch 3). Values verbatim; serialization by the generator.
//
// The aura is NOT dropped. ServUO's SetAreaEffect(AreaEffect.AuraDamage) + IAuraCreature.AuraEffect is pinned
// ModernUO's own aura, under other names: BaseCreature.HasAura / AuraInterval / AuraRange / AuraBaseDamage /
// Aura*Damage (BaseCreature.cs:1113-1123), fired from OnThink (:3651) through AuraDamage() (:5435), which calls the
// AuraEffect(Mobile) virtual (:5471) on each victim, exactly where ServUO calls IAuraCreature.AuraEffect
// (Services/Pet Training/AreaEffects.cs:505). Values from ServUO's fireAura definition (AreaEffects.cs:571-574): 7
// damage, 100% fire, 5 s cooldown. Range is 10, not the definition's 5: AuraDefinition.Range has no reader in ServUO,
// and AuraDamage.EffectRange => 10 (:470) is what DoEffects iterates. Cadence differs slightly: ServUO rolls 40% per
// think once off cooldown, ModernUO fires every AuraInterval; same numbers, a few seconds' jitter.
//
// SetSpecialAbility(DragonBreath) is NOT dropped either (CC6 follow-up, Q-054; D-62 retired, it never was a loss):
// FireDaemon is in no DragonBreathDefinition.Uses list (Services/Pet Training/SpecialAbility.cs:860-1063), so ServUO
// gives it the default definition, 100% fire at 0.16 of current hits every 30-45 s, which pinned ModernUO carries line
// for line as MonsterAbilities.FireBreath through BaseCreature.GetMonsterAbilities() (:1131).

using System;
using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class FireDaemon : BaseCreature
{
    [Constructible]
    public FireDaemon() : base(AIType.AI_Mage)
    {
        Body = 9;
        BaseSoundID = 0x47D;
        Hue = 1636;

        SetStr(504, 539);
        SetDex(126, 145);
        SetInt(329, 364);

        SetHits(1026, 1174);

        SetDamage(7, 14);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Fire, 80);

        SetResistance(ResistanceType.Physical, 45, 60);
        SetResistance(ResistanceType.Fire, 100);
        SetResistance(ResistanceType.Cold, -10, 0);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.Anatomy, 75.5, 84.9);
        SetSkill(SkillName.MagicResist, 95.7, 109.8);
        SetSkill(SkillName.Tactics, 81.0, 98.6);
        SetSkill(SkillName.Wrestling, 40.2, 78.7);
        SetSkill(SkillName.EvalInt, 91.1, 104.5);
        SetSkill(SkillName.Magery, 91.3, 105.0);
        SetSkill(SkillName.Meditation, 90.1, 103.7);
        SetSkill(SkillName.DetectHidden, 66.0);

        Fame = 15000;
        Karma = -15000;

        VirtualArmor = 58;
    }

    public override string CorpseName => "a fire daemon corpse";
    public override string DefaultName => "a fire daemon";

    // ServUO: SetSpecialAbility(SpecialAbility.DragonBreath), the default (fire) definition.
    private static readonly MonsterAbility[] _abilities = { MonsterAbilities.FireBreath };
    public override MonsterAbility[] GetMonsterAbilities() => _abilities;

    public override bool CanRummageCorpses => true;
    public override Poison PoisonImmune => Poison.Regular;
    public override int TreasureMapLevel => 4;
    public override int Meat => 1;

    // ServUO: AreaEffect.AuraDamage with the fireAura definition (see the header).
    public override bool HasAura => true;
    public override TimeSpan AuraInterval => TimeSpan.FromSeconds(5);
    public override int AuraRange => 10;
    public override int AuraBaseDamage => 7;
    public override int AuraFireDamage => 100;

    public override void AuraEffect(Mobile m)
    {
        m.SendLocalizedMessage(1008112); // The intense heat is damaging you!
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich);
        AddLoot(LootPack.Rich);
    }
}

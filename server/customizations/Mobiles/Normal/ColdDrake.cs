// ServUO: Mobiles/Normal/ColdDrake.cs (cc-P129). Values verbatim; serialization by the generator.
//
// SetSpecialAbility(DragonBreath): kept as written. The drake has no stock breath, so the ability core stores it and
// its pinned twin runs DragonBreathDefinitions.For(this), which carries ServUO's ColdDrake definition (100% cold, hue
// 1264). SetAreaEffect(AuraDamage): the aura is pinned's own (HasAura and the Aura* virtuals, FireDaemon's shape, CC6
// batch 3), with ServUO's coldAura definition (AreaEffects.cs:576-580): 15 damage, 100% cold, range 10 as there. The
// profile lists AuraDamage once, as a natural twin. IAuraCreature.AuraEffect is pinned's AuraEffect(Mobile) virtual.
// DragonBlood => 8 dropped: pinned has neither the property nor the item (port recipe). The two random scrolls use
// Loot.*ScrollTypes.Length - 1: ServUO passes Length, which Construct refuses one time in n+1 and packs nothing (a
// bug, D-4 rule, broken not missing).

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ColdDrake : BaseCreature
{
    [Constructible]
    public ColdDrake() : base(AIType.AI_Melee)
    {
        Body = Utility.RandomList(60, 61);
        BaseSoundID = 362;

        Hue = Utility.RandomMinMax(1319, 1327);

        SetStr(610, 670);
        SetDex(130, 160);
        SetInt(150, 190);

        SetHits(450, 500);

        SetDamage(17, 20);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Cold, 50);

        SetResistance(ResistanceType.Physical, 50, 65);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 75, 90);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.MagicResist, 95.0, 110.0);
        SetSkill(SkillName.Tactics, 115.0, 140.0);
        SetSkill(SkillName.Wrestling, 115.0, 126.0);
        SetSkill(SkillName.Parry, 70.0, 80.0);
        SetSkill(SkillName.DetectHidden, 40.0, 50.0);

        Fame = 12000;
        Karma = -12000;

        VirtualArmor = 60;

        Tamable = true;
        ControlSlots = 3;
        MinTameSkill = 96.0;

        PackReg(3);

        for (var i = 0; i <= 1; i++)
        {
            Item item;

            if (Utility.RandomBool())
            {
                item = Loot.RandomScroll(0, Loot.NecromancyScrollTypes.Length - 1, SpellbookType.Necromancer);
            }
            else
            {
                item = Loot.RandomScroll(0, Loot.RegularScrollTypes.Length - 1, SpellbookType.Regular);
            }

            PackItem(item);
        }

        SetSpecialAbility(SpecialAbility.DragonBreath);
        SetAreaEffect(AreaEffect.AuraDamage);
    }

    public override string CorpseName => "a drake corpse";
    public override string DefaultName => "a cold drake";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 3);
    }

    public override bool CanAngerOnTame => true;
    public override bool ReacquireOnMovement => !Controlled;
    public override int TreasureMapLevel => 3;
    public override int Meat => 10;
    public override int Hides => 22;
    public override HideType HideType => HideType.Horned;
    public override FoodType FavoriteFood => FoodType.Fish;

    // ServUO: AreaEffect.AuraDamage with the coldAura definition (see the header).
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

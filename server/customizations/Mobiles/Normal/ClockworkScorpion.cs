// ServUO: Mobiles/Normal/ClockworkScorpion.cs (CC6 batch 3). Values verbatim; serialization by the generator. The
// golem-shaped OnDamage (the master's mana pays for the wounds) and the Controlled-dependent sounds are ported as
// written. ControlSlots = 1 with no Tamable is ServUO's.
//
// DEVIATION D-64: ServUO's IRepairableMobile (Mobiles/MobileInterfaces.cs:10-15: RepairResource, Hits, HitsMax) lets
// a tinker repair it with iron ingots through Services/Craft/Core/Repair.cs. Pinned ModernUO has no such interface;
// its Repair.cs:169 special-cases `targeted is Golem` instead. RepairResource is kept as a plain property so the
// interface can be added to the class when the patch lands. Five ServUO mobiles implement it.
//
// Nothing is lost to Pet Training here, although the brief priced it as a loss: ServUO's only
// SetMagicalAbility(MagicalAbility.Poisoning) sits in the Deserialize version-0 upgrade path (:184-187), which new
// content never runs, and ServUO's SetSkill adds that ability to any wild creature with Poisoning anyway
// (BaseCreature.cs:5212). It has no HitPoison to gate in either emulator.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ClockworkScorpion : BaseCreature
{
    public Type RepairResource => typeof(IronIngot);

    [Constructible]
    public ClockworkScorpion() : base(AIType.AI_Melee)
    {
        Body = 717;

        SetStr(225, 245);
        SetDex(80, 100);
        SetInt(30, 40);

        SetHits(151, 210);

        SetDamage(5, 10);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Poison, 40);

        SetResistance(ResistanceType.Physical, 80, 100);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 60, 80);
        SetResistance(ResistanceType.Poison, 100);
        SetResistance(ResistanceType.Energy, 10, 25);

        SetSkill(SkillName.MagicResist, 30.1, 50.0);
        SetSkill(SkillName.Poisoning, 95.1, 100.0);
        SetSkill(SkillName.Tactics, 70.1, 90.0);
        SetSkill(SkillName.Wrestling, 50.1, 80.0);

        Fame = 3500;
        Karma = -3500;

        ControlSlots = 1;
    }

    public override string CorpseName => "a clockwork scorpion corpse";
    public override string DefaultName => "a clockwork scorpion";

    public override bool IsScaredOfScaryThings => false;
    public override bool IsScaryToPets => true;
    public override bool IsBondable => false;
    public override FoodType FavoriteFood => FoodType.Meat;
    public override bool AutoDispel => !Controlled;
    public override bool BleedImmune => true;
    public override bool DeleteOnRelease => true;
    public override bool BardImmune => !Core.AOS || Controlled;
    public override Poison PoisonImmune => Poison.Lethal;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager, 2);
    }

    public override int GetAngerSound() => 541;

    public override int GetIdleSound() => !Controlled ? 542 : base.GetIdleSound();

    public override int GetDeathSound() => !Controlled ? 545 : base.GetDeathSound();

    public override int GetAttackSound() => 562;

    public override int GetHurtSound() => Controlled ? 320 : base.GetHurtSound();

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        var master = GetMaster();

        if (master != null && master.Player && master.Map == Map && master.InRange(Location, 20))
        {
            if (master.Mana >= amount)
            {
                master.Mana -= amount;
            }
            else
            {
                amount -= master.Mana;
                master.Mana = 0;
                master.Damage(amount);
            }
        }

        base.OnDamage(amount, from, willKill);
    }
}

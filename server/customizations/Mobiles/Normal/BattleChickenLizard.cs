// ServUO: Mobiles/Normal/BattleChickenLizard.cs (CC6 batch 4). Values and logic verbatim; serialization by the generator.
// Not named by any stock spawner: the only source is a mature ChickenLizardEgg that rolled its battle mutation, which
// hatches one in the egg's hiryu hue. Body 716, the chicken lizard's.
//
// A wild one is a coward: every time it acquires a combatant it rolls 5% to stop fleeing and otherwise flees for 30 s,
// and its CheckFlee reads that timer directly instead of the base's self-clearing version. A tamed one uses the base.
//
// DEVIATION (D-71): ServUO's OnAfterTame override (called from AnimalTaming.cs:450 on a successful tame: speeds to
// 0.2/0.4, Frozen cleared, StopFlee) is dropped. Pinned ModernUO has no OnAfterTame virtual (AnimalTaming.cs:444 calls
// the non-virtual SetControlMaster and nothing overridable), so a battle lizard tamed mid-flight keeps fleeing its new
// master for whatever is left of the 30 s. The two speed lines would land on Q-008 anyway: this creature, like every
// port, runs at ModernUO's Medium, wild and tamed. CC3 dropped the same hook on Dimetrosaur.

using System;
using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class BattleChickenLizard : BaseCreature
{
    [Constructible]
    public BattleChickenLizard() : base(AIType.AI_Animal, FightMode.Aggressor)
    {
        Body = 716;

        SetStr(94, 177);
        SetDex(78, 124);
        SetInt(6, 13);

        SetHits(94, 177);

        SetDamage(5, 15);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 15, 20);
        SetResistance(ResistanceType.Fire, 5, 15);

        SetSkill(SkillName.MagicResist, 30.0, 53.0);
        SetSkill(SkillName.Tactics, 50.0, 62.0);
        SetSkill(SkillName.Wrestling, 50.0, 62.0);

        Tamable = true;
        ControlSlots = 1;
        MinTameSkill = 0.0;
    }

    public override string CorpseName => "a chicken lizard corpse";
    public override string DefaultName => "a battle chicken lizard";

    public override int Meat => 3;
    public override MeatType MeatType => MeatType.Bird;
    public override FoodType FavoriteFood => FoodType.GrainsAndHay;

    public override int GetIdleSound() => 1511;
    public override int GetAngerSound() => 1508;
    public override int GetHurtSound() => 1510;
    public override int GetDeathSound() => 1509;

    public override Mobile Combatant
    {
        get => base.Combatant;
        set
        {
            base.Combatant = value;

            if (!Controlled)
            {
                if (0.05 > Utility.RandomDouble())
                {
                    StopFlee();
                }
                else if (!CheckFlee())
                {
                    BeginFlee(TimeSpan.FromSeconds(30));
                }
            }
        }
    }

    public override bool CheckFlee()
    {
        if (Controlled)
        {
            return base.CheckFlee();
        }

        return Core.Now < EndFleeTime;
    }

    // ServUO: public override void OnAfterTame(Mobile tamer) { ActiveSpeed = 0.2; PassiveSpeed = 0.4; if (Frozen)
    // Frozen = false; StopFlee(); } -- no hook here (D-71).
}

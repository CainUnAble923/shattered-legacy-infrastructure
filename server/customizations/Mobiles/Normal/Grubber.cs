// ServUO: Mobiles/Normal/Grubber.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

using System;
using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Grubber : BaseCreature
{
    [Constructible]
    public Grubber()
        : base(AIType.AI_Animal, FightMode.None)
    {
        Body = 270;

        SetStr(15);
        SetDex(2000);
        SetInt(1000);

        SetHits(200);
        SetStam(500);
        SetMana(0);

        SetDamage(1);

        SetDamageType(ResistanceType.Physical, 100);

        SetSkill(SkillName.MagicResist, 200.0);
        SetSkill(SkillName.Tactics, 5.0);
        SetSkill(SkillName.Wrestling, 5.0);

        Fame = 1000;
        Karma = 0;

        VirtualArmor = 4;
    }

    public override string CorpseName => "a grubber corpse";
    public override string DefaultName => "a grubber";

    // ServUO overrides Combatant as IDamageable; pinned's is Mobile (BaseCreature.cs:3703). Same body.
    public override Mobile Combatant
    {
        get => base.Combatant;
        set
        {
            base.Combatant = value;

            if (0.10 > Utility.RandomDouble())
            {
                StopFlee();
            }
            else if (!CheckFlee())
            {
                BeginFlee(TimeSpan.FromSeconds(10));
            }
        }
    }

    public override int Meat => 1;
    public override int Hides => 1;

    public override int GetAttackSound() => 0xC9;
    public override int GetHurtSound() => 0xCA;
    public override int GetDeathSound() => 0xCB;
}

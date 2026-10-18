// cc-P142 (decision 4): the results of the three Thieves' Den recipes built in this prompt (ThievesDenCraftRegistrations;
// the other two proposals wait, notes/cc-P142-monster-stealing-shard-rules.md "For Chase"). Each is a stock base with
// a modest thief's bonus, so a wiki reader is misled by nothing stock: the cloak and the ring carry skill bonuses the
// way pinned's own artifacts do (SkillBonuses.SetValues), and the smoke bomb is pinned's ninja Smoke Bomb opened to a
// thief: it asks for Hiding 50 where the stock one asks for Ninjitsu 50 (SmokeBomb.cs:20-24 at d4531cd9), the rest of
// the use (the skill delay, 10 mana, the Hiding roll with CombatOverride, the smoke) copied line for line.

using ModernUO.Serialization;
using Server.SkillHandlers;

namespace Server.Items;

/// <summary>Tailoring 100, a Den recipe: Stealth +5, Hiding +3.</summary>
[SerializationGenerator(0, false)]
public partial class ShadowsilkCloak : BaseCloak
{
    [Constructible]
    public ShadowsilkCloak() : base(0x1515, 0x455)
    {
        Name = "shadowsilk cloak";
        SkillBonuses.SetValues(0, SkillName.Stealth, 5.0);
        SkillBonuses.SetValues(1, SkillName.Hiding, 3.0);
    }

    public override double DefaultWeight => 5.0;
}

/// <summary>Tinkering 100, a Den recipe: Stealing +5, Snooping +5.</summary>
[SerializationGenerator(0, false)]
public partial class PickpocketsCharm : BaseRing
{
    [Constructible]
    public PickpocketsCharm() : base(0x108A)
    {
        Name = "pickpocket's charm";
        Hue = 0x8A5;
        SkillBonuses.SetValues(0, SkillName.Stealing, 5.0);
        SkillBonuses.SetValues(1, SkillName.Snooping, 5.0);
    }

    public override double DefaultWeight => 0.1;
}

/// <summary>Alchemy 100, a Den recipe: a smoke bomb that hides on Hiding 50 instead of Ninjitsu 50.</summary>
[SerializationGenerator(0, false)]
public partial class ThiefsSmokeBomb : SmokeBomb
{
    public const double HidingRequired = 50.0;

    [Constructible]
    public ThiefsSmokeBomb()
    {
        Name = "thief's smoke bomb";
        Hue = 0x455;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            // The item must be in your backpack to use it.
            from.SendLocalizedMessage(1060640);
        }
        else if (from.Skills.Hiding.Value < HidingRequired)
        {
            // You need at least ~1_SKILL_REQUIREMENT~ ~2_SKILL_NAME~ skill to use that ability.
            from.SendLocalizedMessage(1063013, "50\tHiding");
        }
        else if (Core.TickCount - from.NextSkillTime < 0)
        {
            // You must wait a few seconds before you can use that item.
            from.SendLocalizedMessage(1070772);
        }
        else if (from.Mana < 10)
        {
            // You don't have enough mana to do that.
            from.SendLocalizedMessage(1049456);
        }
        else
        {
            Hiding.CombatOverride = true;

            if (from.UseSkill(SkillName.Hiding))
            {
                from.Mana -= 10;

                from.FixedParticles(0x3709, 1, 30, 9904, 1108, 6, EffectLayer.RightFoot);
                from.PlaySound(0x22F);

                Consume();
            }

            Hiding.CombatOverride = false;
        }
    }
}

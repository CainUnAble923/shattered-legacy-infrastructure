// cc-P80: tutorial-only creatures (spawns and "echo" monsters). Notes: shard-migration notes/cc-P80-new-player-tutorial.md.
//
// They engage only players in the tutorial (ClusterFTutorial.IsParticipant: the account is in the core, the wings or
// remedial training, and this is the character doing it; a pet counts as its master), so veterans cannot farm them. No
// instancing (Part A item 6):
//   - IsEnemy is false for anyone else, so pinned's AI never acquires them (BaseAI.AcquireFocusMob rejects a non-enemy,
//     BaseAI.cs:1073-1075; approach aggro needs IsEnemy, BaseCreature.cs:1353-1358).
//   - CanBeHarmful is false for anyone else, so the creature never strikes them.
//   - cc-P453: anyone else's harmful act on one is refused before it starts (Notoriety-tutorial-creature-outsiders.patch,
//     in Mobile.AllowHarmfulHandler): no combatant, no swing, so no weapon, Tactics or Anatomy roll on a target that
//     never fights back.
//   - Damage from anyone else is ignored, wherever the attacker stands (pinned only asks the attacker's region,
//     Mobile.cs:8366-8374, so a region alone would leave a gap).
//   - No kill awards and no loot, so a kill is worth nothing outside the tutorial.
//   - IgnoreYoungProtection: tutorial players are Young, and these must still fight them (BaseCreature.cs:1313).
//
// TutorialEcho is the first stub creature: a hued stock body ("echo" monsters until their art is registered).

#nullable enable

using ModernUO.Serialization;
using Server.Tutorial;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public abstract partial class TutorialCreature : BaseCreature
{
    protected TutorialCreature(AIType ai = AIType.AI_Melee) : base(ai, FightMode.Closest, 10, 1)
    {
        NoKillAwards = true;
    }

    public override bool IgnoreYoungProtection => true;

    public static bool Engages(Mobile? m) => ClusterFTutorial.IsParticipant(m);

    public override bool IsEnemy(Mobile m) => Engages(m) && base.IsEnemy(m);

    public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness) =>
        Engages(target) && base.CanBeHarmful(target, message, ignoreOurBlessedness);

    public override void Damage(int amount, Mobile? from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (from != null && !Engages(from))
        {
            return;
        }

        base.Damage(amount, from, informMount, ignoreEvilOmen);
    }

    public override void GenerateLoot()
    {
    }
}

/// <summary>An echo: a cracked-time remnant of a creature. A stock body with an echo hue until the new mobiles ship.</summary>
[SerializationGenerator(0, false)]
public partial class TutorialEcho : TutorialCreature
{
    [Constructible]
    public TutorialEcho() : this(0xEE, "an echo of a rat")
    {
    }

    public TutorialEcho(int body, string name)
    {
        Body = body;
        Name = name;
        Hue = 0x0482; // a pale echo hue until the echo art ships
        SetStr(20);
        SetDex(30);
        SetInt(5);
        SetHits(15);
        SetDamage(1, 3);
        SetSkill(SkillName.Wrestling, 20.0);
        SetSkill(SkillName.Tactics, 20.0);
        BaseSoundID = 0xCC;
    }
}

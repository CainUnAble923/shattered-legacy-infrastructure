// cc-P139 (F-46; F-45's archer): the creature body route's proof. A staff-only test creature on body 2048 (0x800), the
// first third-party creature body the player package ships: Vert-I-Go's "Kobold Bow" .vd (a Baldur's Gate rip, cleared
// by Chase 2026-10-15 and renamed in game) at anim7 slot 1, which TazUO reads through the launcher's copies of the
// player's Bodyconv.def ("2048 -1 -1 -1 -1 -1 1") and mobtypes.txt ("2048 MONSTER 0"), player-package\payload\app\
// Build-UoOverrides.ps1. Notes: shard-migration notes\cc-P139-rootdeep-burrows.md.
//
// Nothing places it: [add BurrowScampSlingerBodyTest. The Rootdeep Burrows archer proper (F-45's Burrow Scamp Slinger,
// cc-P140) will wear the same body with its own numbers; these are placeholders in a ratman archer's shape (pinned
// Mobiles/Monsters/Humanoid/Melee/RatmanArcher.cs) cut down for a New Haven monster. Nothing here is OSI's.
//
// The body is typed Monster on the server by ShardBodyTypes (Body.IsMonster: hurt is action 10, BaseWeapon.cs:2716-2720;
// the fidgets are 17 and 18, BaseCreature.cs:3009; the anger animation 18, BaseCreature.cs:1766), and the .vd has
// exactly those actions (0 walk, 1 stand, 2 and 3 die, 4 attack, 10 get hit, 17 and 18 fidget).
//
// The bow. Pinned plays no swing animation at all for a monster body shooting a bow or a crossbow
// (BaseWeapon.PlaySwingAnimation, BaseWeapon.cs:2749-2765: ShootBow and ShootXBow return), and for a melee monster it
// picks actions 4 to 6 at random (Utility.Random(4, 3)); this body has 4 only, and TazUO shows the stand frame for a
// group a monster body lacks (MobileAnimation.cs:679-682). So the test creature's bow plays action 4, the .vd's one
// attack, on every shot. A deviation from pinned for this creature's bow only (notes, Deviations); cc-P140 decides for
// the real Slinger.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class BurrowScampSlingerBodyTest : BaseCreature
{
    /// <summary>The first third-party creature body (registry.csv kind body; records.json bodyAnims; ShardBodyTypes).</summary>
    public const int BodyId = 2048;

    [Constructible]
    public BurrowScampSlingerBodyTest() : base(AIType.AI_Archer)
    {
        Name = "a burrow scamp slinger";
        Body = BodyId;
        BaseSoundID = 437; // the ratman's set, a placeholder until cc-P140 picks the scamp's

        SetStr(46, 60);
        SetDex(61, 80);
        SetInt(26, 40);

        SetHits(28, 36);

        SetDamage(3, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 15, 20);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Cold, 5, 10);
        SetResistance(ResistanceType.Poison, 5, 10);
        SetResistance(ResistanceType.Energy, 5, 10);

        SetSkill(SkillName.Anatomy, 20.1, 35.0);
        SetSkill(SkillName.Archery, 50.1, 65.0);
        SetSkill(SkillName.MagicResist, 20.1, 30.0);
        SetSkill(SkillName.Tactics, 35.1, 50.0);
        SetSkill(SkillName.Wrestling, 30.1, 40.0);

        Fame = 450;
        Karma = -450;

        VirtualArmor = 16;

        AddItem(new BurrowScampTestBow());
        PackItem(new Arrow(Utility.RandomMinMax(20, 30)));
    }

    public override string CorpseName => "a burrow scamp corpse";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Poor);
    }
}

/// <summary>
/// The test creature's bow: on a monster body it plays action 4 (the .vd's one attack) on every shot, where pinned plays
/// nothing (BaseWeapon.PlaySwingAnimation returns for ShootBow on a monster). On any other body it is a bow.
/// </summary>
[SerializationGenerator(0, false)]
public partial class BurrowScampTestBow : Bow
{
    public const int SwingAction = 4;

    [Constructible]
    public BurrowScampTestBow()
    {
        Name = "a burrow scamp's bow";
    }

    /// <summary>The action the swing plays for this wielder, or -1 for pinned's own choice.</summary>
    public static int SwingActionFor(Mobile from) => from.Body.IsMonster ? SwingAction : -1;

    public override void PlaySwingAnimation(Mobile from)
    {
        var action = SwingActionFor(from);

        if (action < 0)
        {
            base.PlaySwingAnimation(from);
            return;
        }

        if (!from.Mounted)
        {
            from.Animate(action, 4, 1, true, false, 0);
        }
    }
}

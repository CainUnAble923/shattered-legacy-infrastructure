// cc-P663 (P481 F3): Tamsin, the Wakening Isle's guide, on the beach where a trainee wakes. She has Rook's core role: her
// window lists the core lessons in order and starts one, and offers the skip. Graduation, the wings and remedial training
// stay with Rook in New Haven. Her replies go to the one player (SayTo, P605's rule for lesson NPCs).
// Notes: shard-migration notes/cc-P663-tutorial-moves-to-isle.md.

#nullable enable

using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Tutorial;

[SerializationGenerator(0, false)]
public partial class TutorialGuide : BaseCreature
{
    [Constructible]
    public TutorialGuide() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        Name = "Tamsin";
        Title = "the isle's guide";
        Body = 0x191;
        Female = true;
        Hue = 0x83EA;
        SetStr(80);
        SetDex(80);
        SetInt(80);
        SetHits(150);
        CantWalk = true;

        AddItem(new FancyShirt { Movable = false, Hue = 0x8AF });
        AddItem(new Skirt { Movable = false, Hue = 0x455 });
        AddItem(new Sandals { Movable = false });
        AddItem(new BodySash { Movable = false, Hue = 0x8AF });
        AddItem(new Item(0x203C) { Movable = false, Hue = 0x47E, Layer = Layer.Hair });
    }

    public override bool IsInvulnerable => true;
    public override bool ClickTitle => true;

    // cc-P718: she speaks first to a trainee who has not used her yet (TutorialGreeting).
    public override void OnMovement(Mobile m, Point3D oldLocation)
    {
        base.OnMovement(m, oldLocation);
        if (m is PlayerMobile pm && TutorialGreeting.NeedsGuide(pm))
        {
            TutorialGreeting.TryGreet(this, pm, TutorialGreeting.TamsinLine);
        }
    }

    public override void OnThink()
    {
        base.OnThink();
        TutorialGreeting.GreetAround(this, TutorialGreeting.NeedsGuide, TutorialGreeting.TamsinLine);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        TutorialGreeting.UsedGuide(pm);

        if (!pm.InRange(Location, 4))
        {
            pm.SendMessage("Move closer, then double-click Tamsin again.");
            return;
        }

        SayTo(pm, ClusterFTutorial.IsParticipant(pm)
            ? "Welcome to the Wakening Isle. My window shows your lessons; take them in order along the road."
            : "Welcome to the Wakening Isle. The moongate at the end of the dock takes you to New Haven.");
        pm.CloseGump<TutorialRecruiterGump>();
        pm.SendGump(new TutorialRecruiterGump(pm, guide: true));
    }

    /// <summary>Tamsin on <paramref name="map"/> (the isle by default), or null.</summary>
    public static TutorialGuide? Find(Map? map = null)
    {
        map ??= TutorialWorld.Isle;
        if (map == null)
        {
            return null;
        }

        foreach (var m in map.GetMobilesInRange(TutorialWorld.Ground(map, TutorialWorld.Guide), 15))
        {
            if (m is TutorialGuide g && !g.Deleted)
            {
                return g;
            }
        }

        return null;
    }

    /// <summary>An arrow from the trainee to Tamsin on their lesson map, when they are on it.</summary>
    public static void PointTo(PlayerMobile pm)
    {
        if (Find(TutorialWorld.MapOf(pm)) is { } guide && guide.Map == pm.Map)
        {
            pm.QuestArrow?.Stop();
            pm.QuestArrow = new GuildDirectionArrow(pm, guide, guide.Location, guide.Map, "Tamsin, your guide");
        }
    }
}

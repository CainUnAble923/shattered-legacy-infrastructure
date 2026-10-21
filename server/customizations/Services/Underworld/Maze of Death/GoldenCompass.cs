// ServUO: Services/Underworld/Maze of Death/GoldenCompass.cs (cc-P197). The golden compass (1113578, hue 0x501)
// at the maze's mouth (1070,1055,-34): unmovable and timeless where it stands, a double-click within three tiles by
// a player with no compass in the pack hands out a movable copy with an 1800-second life (1113584, "Please return
// what you borrow!"); double-clicked in the pack inside the Maze of Death it opens the direction gump. Saved: the
// span, as ServUO; on load the base restarts a borrowed compass's timer through StartTimer, which gives it a fresh
// 1800 seconds, as ServUO's Deserialize does.

using ModernUO.Serialization;
using Server.Gumps;
using Server.Regions;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class GoldenCompass : BaseDecayingItem
{
    [SerializableField(0)]
    private int _span;

    [Constructible]
    public GoldenCompass() : base(459)
    {
        Weight = 1;
        Hue = 0x501;
        _span = 0;
        Movable = false;
    }

    public override int Lifespan => _span;
    public override int LabelNumber => 1113578; // a golden compass

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack) && from.Region?.IsPartOf<MazeOfDeathRegion>() == true)
        {
            from.CloseGump<CompassDirectionGump>();
            from.SendGump(new CompassDirectionGump(from));
        }
        else if (RootParent == null && from.InRange(GetWorldLocation(), 3) && !Movable && !IsLockedDown && !IsSecure)
        {
            if (from.Backpack != null && _span == 0 && from.Backpack.FindItemByType<GoldenCompass>() == null)
            {
                var c = new GoldenCompass();
                c.StartTimer();
                from.Backpack.DropItem(c);
                from.SendLocalizedMessage(1113584); // Please return what you borrow!
            }
        }
    }

    public override void StartTimer()
    {
        TimeLeft = 1800;
        _span = 1800;
        Movable = true;
        base.StartTimer();
        InvalidateProperties();
    }

    public override void OnDelete()
    {
        base.OnDelete();

        if (RootParent is Mobile m)
        {
            m.CloseGump<CompassDirectionGump>();
        }
    }
}

// ServUO: Services/Revamped Dungeons/WrongDungeon/Items/Artifacts/BlanketOfDarknessArtifact.cs (cc-P194).
//
// A bedroll hued 2076, artifact rarity 8, that behaves exactly like a stock bedroll: double-click to unroll it
// facing you, double-click again to roll it up, and if you are in a secured camp you are offered the camping
// logout. ServUO flags the player with its own BlanketOfDarknessLogout, which its PlayerMobile reads beside
// BedrollLogout in one place (the instant-logout test, PlayerMobile.cs:6179); pinned's PlayerMobile has only
// BedrollLogout (PlayerMobile.cs:356, read at :4013), so the same flag is set here: the effect is identical.
//
// Conversion notes: pinned Bedroll keeps its LogoutGump private (Items/Skill Items/Camping/Bedroll.cs), so the
// gump is reproduced here from ServUO's; IsArtifact has no pinned reader (the recipe's Items section).

using System;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BlanketOfDarkness : Item
{
    [Constructible]
    public BlanketOfDarkness() : base(0xA57)
    {
        Hue = 2076;
    }

    public override double DefaultWeight => 10.0;

    public override int LabelNumber => 1152304; // Blanket Of Darkness

    public override void OnDoubleClick(Mobile from)
    {
        if (Parent != null || !VerifyMove(from))
        {
            return;
        }

        if (!from.InRange(this, 2))
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            return;
        }

        if (ItemID == 0xA57) // rolled
        {
            var dir = PlayerMobile.GetDirection4(from.Location, Location);

            ItemID = dir is Direction.North or Direction.South ? 0xA55 : 0xA56;
        }
        else // unrolled
        {
            ItemID = 0xA57;

            if (!from.HasGump<LogoutGump>())
            {
                var entry = Campfire.GetEntry(from);

                if (entry?.Safe == true)
                {
                    from.SendGump(new LogoutGump(entry, this));
                }
            }
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1061078, "8"); // artifact rarity ~1_val~
    }

    private class LogoutGump : Gump
    {
        private readonly BlanketOfDarkness _blanket;
        private readonly Timer _closeTimer;
        private readonly CampfireEntry _entry;

        public LogoutGump(CampfireEntry entry, BlanketOfDarkness blanket) : base(100, 0)
        {
            _entry = entry;
            _blanket = blanket;

            _closeTimer = Timer.DelayCall(TimeSpan.FromSeconds(10.0), CloseGump);

            AddBackground(0, 0, 400, 350, 0xA28);

            AddHtmlLocalized(100, 20, 200, 35, 1011015); // <center>Logging out via camping</center>

            /* Using a bedroll in the safety of a camp will log you out of the game safely.
             * If this is what you wish to do choose CONTINUE and you will be logged out.
             * Otherwise, select the CANCEL button to avoid logging out at this time.
             * The camp will remain secure for 10 seconds at which time this window will close
             * and you not be logged out.
             */
            AddHtmlLocalized(50, 55, 300, 140, 1011016, true, true);

            AddButton(45, 298, 0xFA5, 0xFA7, 1);
            AddHtmlLocalized(80, 300, 110, 35, 1011011); // CONTINUE

            AddButton(200, 298, 0xFA5, 0xFA7, 0);
            AddHtmlLocalized(235, 300, 110, 35, 1011012); // CANCEL
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var pm = _entry.Player;

            _closeTimer.Stop();

            if (Campfire.GetEntry(pm) != _entry)
            {
                return;
            }

            if (info.ButtonID == 1 && _entry.Safe && _blanket.Parent == null && _blanket.IsAccessibleTo(pm) &&
                _blanket.VerifyMove(pm) && _blanket.Map == pm.Map && pm.InRange(_blanket, 2))
            {
                pm.PlaceInBackpack(_blanket);

                pm.BedrollLogout = true;
                sender.Disconnect("Camping logout (Blanket of Darkness)");
            }

            Campfire.RemoveEntry(_entry);
        }

        private void CloseGump()
        {
            Campfire.RemoveEntry(_entry);
            _entry.Player.CloseGump<LogoutGump>();
        }
    }
}

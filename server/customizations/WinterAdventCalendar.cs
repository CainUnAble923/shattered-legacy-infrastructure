// cc-P266 (F-22, part 5): the advent calendar, a small daily-gift item for the winter decor. MrRiots' "Christmas
// Calendar [Gump/Item]" (servuo.dev 2069; "Use as you wish, edit as you wish"): the calendar hangs on a wall (our art
// 0x33B5 west wall, 0x33B6 north wall) and double-click opens his 400 x 400 calendar gump (our gump 22469, 25 doors in
// a 5 x 5 grid of 80 px cells, numbered 1 to 25). Notes in shard-migration notes/cc-P266-new-haven-winter-decor.md.
//
// The rule: door N opens from December N on, in December only (the shard's local date, the winter switch's clock),
// once per character per year. Opening it puts that day's gift in the character's backpack (the bank if the backpack
// is full). The gifts are PLACEHOLDERS for Chase: ClusterFWinterAdventGifts.ForDay returns a gift box holding a candy
// cane, named for the day; change that table and nothing else. Doors already opened show a check mark; a door not yet
// due says when it opens.
//
// The seeder places the calendar as a row of ClusterFWinterDecorLayout whose layer is "calendar" and tracks it by
// serial like every other piece, so it comes down with the decor. Who opened what is saved on the item (per year, so
// next December starts fresh). Not constructible by [add; not movable; does not decay.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Items
{
    public enum AdventDoorResult
    {
        Opened,
        AlreadyOpened,
        NotYet,
        NotDecember,
        BadDay
    }

    /// <summary>The gifts behind the doors. Placeholders for Chase: every day is a gift box with a candy cane, named for its day.</summary>
    public static class ClusterFWinterAdventGifts
    {
        public const int Doors = 25;

        public static Item ForDay(int day)
        {
            var box = new GiftBox { Name = $"an advent calendar gift (December {day})" };
            box.DropItem(new CandyCane());
            return box;
        }
    }

    [SerializationGenerator(0, false)]
    public partial class WinterAdventCalendar : Item, IWinterDecorItem
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _key;

        // The December these openings belong to; a new year clears them.
        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _year;

        // Per character, a bit per door opened (bit N-1 for door N).
        [SerializableField(2, setter: "private")]
        private Dictionary<Mobile, int> _opened;

        public WinterAdventCalendar(ClusterFWinterDecorLayout.Row row) : base(row.ItemId)
        {
            _key = row.Key;
            Name = row.Name;
            Hue = row.Hue;
            Movable = false;
            _opened = new Dictionary<Mobile, int>();
        }

        public override bool Decays => false;

        /// <summary>The shard's local date, from the winter switch (tests replace its clock).</summary>
        public static DateTime Today() => ClusterFWinterDecor.Season.Clock();

        private void RollYear(DateTime local)
        {
            if (_year != local.Year)
            {
                _year = local.Year;
                _opened ??= new Dictionary<Mobile, int>();
                _opened.Clear();
                this.MarkDirty();
            }
        }

        public int OpenedMask(Mobile m, DateTime local)
        {
            RollYear(local);
            return _opened != null && _opened.TryGetValue(m, out var mask) ? mask : 0;
        }

        public bool IsOpened(Mobile m, int day, DateTime local) => day is >= 1 and <= ClusterFWinterAdventGifts.Doors && (OpenedMask(m, local) & (1 << (day - 1))) != 0;

        /// <summary>Tries to open door N for the character on the given local date. Opened hands over the gift.</summary>
        public AdventDoorResult TryOpen(Mobile from, int day, DateTime local)
        {
            if (day is < 1 or > ClusterFWinterAdventGifts.Doors)
            {
                return AdventDoorResult.BadDay;
            }

            if (local.Month != 12)
            {
                return AdventDoorResult.NotDecember;
            }

            RollYear(local);
            if (day > local.Day)
            {
                return AdventDoorResult.NotYet;
            }

            _opened ??= new Dictionary<Mobile, int>();
            var mask = _opened.GetValueOrDefault(from);
            if ((mask & (1 << (day - 1))) != 0)
            {
                return AdventDoorResult.AlreadyOpened;
            }

            _opened[from] = mask | (1 << (day - 1));
            this.MarkDirty();
            var gift = ClusterFWinterAdventGifts.ForDay(day);
            if (!from.AddToBackpack(gift))
            {
                from.BankBox?.DropItem(gift);
            }

            return AdventDoorResult.Opened;
        }

        public static string Say(AdventDoorResult r, int day) => r switch
        {
            AdventDoorResult.Opened => $"You open door {day}. A gift is in your backpack.",
            AdventDoorResult.AlreadyOpened => $"You have opened door {day} already this year.",
            AdventDoorResult.NotYet => $"Door {day} opens on December {day}.",
            AdventDoorResult.NotDecember => "The calendar's doors open in December.",
            _ => "That is not a door."
        };

        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(GetWorldLocation(), 3))
            {
                from.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            from.CloseGump<WinterAdventCalendarGump>();
            from.SendGump(new WinterAdventCalendarGump(this, from));
        }
    }
}

namespace Server.Gumps
{
    /// <summary>MrRiots' calendar picture with a small button on each of the 25 doors; opened doors carry a check mark.</summary>
    public class WinterAdventCalendarGump : Gump
    {
        public const int GumpId = 22469;   // player-package\vendor\shattered-legacy-art\gumps\22469.png, 400 x 400
        public const int Cell = 80;        // 5 x 5 doors
        public const int ButtonArt = 4005; // the small arrow button, pressed 4007
        public const int CheckArt = 0xD3;  // a ticked box

        private readonly WinterAdventCalendar _calendar;

        public WinterAdventCalendarGump(WinterAdventCalendar calendar, Mobile from) : base(60, 40)
        {
            _calendar = calendar;
            var today = WinterAdventCalendar.Today();
            var mask = calendar.OpenedMask(from, today);
            AddPage(0);
            AddImage(0, 0, GumpId);
            for (var day = 1; day <= ClusterFWinterAdventGifts.Doors; day++)
            {
                var cx = (day - 1) % 5 * Cell;
                var cy = (day - 1) / 5 * Cell;
                if ((mask & (1 << (day - 1))) != 0)
                {
                    AddImage(cx + Cell - 24, cy + Cell - 24, CheckArt);
                }
                else
                {
                    AddButton(cx + 4, cy + Cell - 24, ButtonArt, ButtonArt + 2, day);
                }
            }

            var note = today.Month == 12 ? $"December {today.Day}: doors 1 to {Math.Min(today.Day, ClusterFWinterAdventGifts.Doors)} are open." : "The doors open in December.";
            AddHtml(0, 404, 400, 20, $"<basefont color=#FFFFFF><center>{note}</center>");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var from = sender.Mobile;
            if (from == null || info.ButtonID is < 1 or > ClusterFWinterAdventGifts.Doors || _calendar.Deleted)
            {
                return;
            }

            if (!from.InRange(_calendar.GetWorldLocation(), 3))
            {
                from.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            var day = info.ButtonID;
            from.SendMessage(WinterAdventCalendar.Say(_calendar.TryOpen(from, day, WinterAdventCalendar.Today()), day));
            from.SendGump(new WinterAdventCalendarGump(_calendar, from));
        }
    }
}

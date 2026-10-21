// cc-P199: ServUO pub57 Scripts/Gumps/Traps/CircuitTrap.cs. The circuit puzzle that the circuit trap training kit opens
// (ServUO also uses it for its Exodus and Shadowguard locks, neither ported). ServUO's BaseGump becomes pinned's
// DynamicGump: Refresh is a resend of the same gump, User is the player it was sent to. Layout, paths and replies are
// ServUO's.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Mobiles;
using Server.Network;

namespace Server.Gumps
{
    public interface ICircuitTrap
    {
        int GumpTitle { get; }
        int GumpDescription { get; }
        CircuitCount Count { get; }

        bool CanDecipher { get; }

        List<int> Path { get; set; }
        List<int> Progress { get; set; }

        void OnProgress(Mobile m, int pick);
        void OnFailed(Mobile m);
        void OnComplete(Mobile m);
        void OnSelfClose(Mobile m);
    }

    public enum CircuitCount
    {
        Nine = 9,
        Sixteen = 16,
        TwentyFive = 25,
        ThirtySix = 36
    }

    public class CircuitTrapGump : DynamicGump
    {
        public PlayerMobile User { get; }
        public ICircuitTrap Trap { get; set; }
        public List<int> Path => Trap?.Path;
        public List<int> Progress => Trap?.Progress;

        public CircuitCount Count => Trap?.Count ?? CircuitCount.Nine;
        public bool ShowNext { get; set; }

        public CircuitTrapGump(PlayerMobile from, ICircuitTrap item) : base(5, 30)
        {
            User = from;
            Trap = item;
            from.CloseGump<CircuitTrapGump>();
        }

        public void Refresh() => User.SendGump(this);

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            var size = Count switch
            {
                CircuitCount.Sixteen    => 190,
                CircuitCount.TwentyFive => 230,
                CircuitCount.ThirtySix  => 270,
                _                       => 150
            };

            builder.AddPage();

            builder.AddBackground(50, 0, 530, 410, 0xA28);
            builder.AddBackground(95, 20, 442, 90, 0xA28);
            builder.AddBackground(90, 115, size, size, 0xA28);
            builder.AddBackground(100, 125, size - 20, size - 20, 0x1400);
            builder.AddBackground(365, 120, 178, 210, 0x1400);

            builder.AddImage(0, 0, 0x28C8);
            builder.AddImage(547, 0, 0x28C9);
            builder.AddImage(140, 40, 0x28D3);
            builder.AddImage(420, 40, 0x28D3);
            builder.AddImage(365, 115, 0x28D4);
            builder.AddImage(365, 288, 0x28D4);
            builder.AddImage(414, 189, 0x589);
            builder.AddImage(435, 210, 0xA52);

            if (Path == null || Path.Count == 0)
            {
                Trap.Path = GetRandomPath();
            }

            if (Progress == null || Progress.Count == 0)
            {
                Trap.Progress = [0];
            }

            const int sx = 110;
            const int sy = 135;
            var count = (int)Count;
            var sq = Math.Sqrt(count);

            for (var i = 0; i < count; i++)
            {
                var line = (int)(i / sq);
                var col = (int)(i % sq);

                var x = sx + col * 40;
                var y = sy + line * 40;

                builder.AddImage(x, y, i == count - 1 ? 0x9A8 : Progress.Contains(i) ? 0x868 : 0x25F8);

                if (line + 1 < sq)
                {
                    builder.AddImage(x + 10, y + 27, 0x13F9);
                }

                if (col + 1 < sq)
                {
                    builder.AddImage(x + 18, y + 12, 0x13FD);
                }

                if (i == Progress[^1])
                {
                    builder.AddImage(x + 8, y + 8, 0x13A8);
                }

                if (ShowNext && Progress.Count < Path.Count && i == Path[Progress.Count])
                {
                    builder.AddImage(x + 8, y + 8, 2361);
                }
            }

            if (ShowNext)
            {
                ShowNext = false;
            }

            builder.AddHtmlLocalized(210, 35, 212, 45, Trap.GumpTitle);
            builder.AddHtmlLocalized(210, 60, 212, 70, 1153748); // <center>Use the Directional Controls to</center>
            builder.AddHtmlLocalized(210, 75, 212, 85, Trap.GumpDescription);

            if (Trap.CanDecipher && User.Skills.Lockpicking.Base >= 100)
            {
                builder.AddHtmlLocalized(405, 355, 150, 32, 1153750); // Attempt to Decipher the Circuit Path
                builder.AddButton(365, 355, 4005, 4005, 5);
            }

            builder.AddButton(448, 185, 10700, 10701, 1); // up
            builder.AddButton(473, 222, 10710, 10711, 2); // right
            builder.AddButton(448, 243, 10720, 10721, 3); // down
            builder.AddButton(408, 222, 10730, 10731, 4); // left
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (info.ButtonID == 0)
            {
                Trap.OnSelfClose(User);
                return;
            }

            if (info.ButtonID is > 0 and < 5)
            {
                var current = Progress[^1];
                var next = 35;
                var count = (int)Count;
                var perRow = (int)Math.Sqrt(count);

                if (Progress.Count < Path.Count)
                {
                    next = Path[Progress.Count];
                }

                var pick = info.ButtonID switch
                {
                    2 => current + 1,
                    3 => current + perRow,
                    4 => current - 1,
                    _ => current - perRow
                };

                if (pick < 0 || pick > count - 1) // Off board or already chosen spot
                {
                    User.PlaySound(0x5B6);
                    Refresh();
                }
                else if ((current == count - 2 || current == count - 1 - perRow) && pick == count - 1)
                {
                    Trap.Path.Clear();
                    Trap.Progress.Clear();

                    Trap.OnComplete(User);
                }
                else if (pick == next)
                {
                    Trap.OnProgress(User, pick);
                    Trap.Progress.Add(pick);

                    Refresh();
                }
                else
                {
                    Trap.OnFailed(User);
                    Trap.Progress.Clear();
                }
            }
            else if (info.ButtonID == 5 && Trap.CanDecipher)
            {
                ShowNext = true;
                Refresh();
            }
        }

        private static readonly int[][] Paths9 =
        [
            [0, 1, 2, 5, 8],
            [0, 1, 4, 5, 8],
            [0, 1, 4, 3, 6, 7, 8],
            [0, 1, 4, 7, 8],
            [0, 3, 6, 7, 8],
            [0, 3, 4, 5, 8],
            [0, 3, 4, 7, 8],
            [0, 3, 6, 7, 4, 5, 8],
            [0, 3, 6, 7, 4, 1, 2, 5, 8]
        ];

        private static readonly int[][] Paths16 =
        [
            [0, 1, 2, 3, 7, 11, 15],
            [0, 1, 2, 6, 7, 11, 15],
            [0, 1, 2, 3, 7, 6, 5, 4, 8, 9, 10, 11, 15],
            [0, 1, 5, 6, 7, 11, 15],
            [0, 1, 5, 4, 8, 12, 13, 14, 15],
            [0, 1, 5, 4, 8, 9, 10, 14, 15],
            [0, 4, 8, 12, 13, 14, 15],
            [0, 4, 8, 9, 10, 6, 2, 3, 7, 11, 15],
            [0, 4, 8, 9, 5, 6, 7, 11, 15],
            [0, 4, 5, 6, 2, 3, 7, 11, 15],
            [0, 4, 5, 6, 7, 11, 10, 9, 8, 12, 13, 14, 15],
            [0, 4, 5, 9, 10, 11, 15]
        ];

        private static readonly int[][] Paths25 =
        [
            [0, 1, 2, 3, 4, 9, 14, 19, 24],
            [0, 1, 2, 7, 8, 9, 14, 13, 12, 11, 10, 15, 20, 21, 22, 23, 24],
            [0, 1, 2, 3, 4, 9, 14, 13, 12, 17, 22, 23, 24],
            [0, 1, 6, 7, 8, 13, 12, 17, 18, 19, 24],
            [0, 1, 6, 5, 10, 11, 12, 17, 18, 19, 24],
            [0, 1, 6, 5, 10, 15, 16, 17, 12, 7, 8, 9, 14, 19, 24],
            [0, 5, 6, 7, 2, 3, 4, 9, 14, 19, 24],
            [0, 5, 6, 11, 12, 13, 18, 19, 24],
            [0, 5, 6, 11, 16, 21, 22, 17, 12, 7, 8, 9, 14, 19, 24],
            [0, 5, 10, 15, 20, 21, 22, 23, 24],
            [0, 5, 10, 11, 12, 13, 8, 7, 6, 1, 2, 3, 4, 9, 14, 19, 24],
            [0, 5, 10, 11, 16, 17, 18, 23, 24]
        ];

        private static readonly int[][] Paths36 =
        [
            [0, 1, 2, 3, 4, 5, 11, 17, 23, 29, 35],
            [0, 6, 12, 18, 24, 30, 31, 32, 33, 34, 35],
            [0, 1, 2, 8, 14, 15, 16, 22, 28, 29, 35],
            [0, 1, 7, 13, 19, 20, 21, 27, 33, 34, 35],
            [0, 1, 7, 8, 14, 20, 26, 27, 33, 34, 35],
            [0, 1, 2, 3, 9, 10, 16, 15, 21, 27, 28, 34, 35],
            [0, 6, 12, 13, 19, 20, 26, 27, 28, 29, 35],
            [0, 6, 12, 18, 19, 25, 26, 20, 21, 22, 28, 34, 35],
            [0, 6, 7, 8, 14, 20, 21, 27, 28, 29, 35],
            [0, 6, 7, 13, 12, 18, 19, 20, 21, 27, 28, 34, 35],
            [0, 6, 12, 13, 19, 18, 24, 30, 31, 32, 33, 34, 35],
            [0, 1, 2, 8, 9, 15, 16, 10, 11, 17, 23, 29, 35]
        ];

        public static int[][] PathsFor(CircuitCount count) =>
            count switch
            {
                CircuitCount.Sixteen    => Paths16,
                CircuitCount.TwentyFive => Paths25,
                CircuitCount.ThirtySix  => Paths36,
                _                       => Paths9
            };

        public List<int> GetRandomPath()
        {
            var paths = PathsFor(Count);
            return paths[Utility.Random(paths.Length)].ToList();
        }
    }
}

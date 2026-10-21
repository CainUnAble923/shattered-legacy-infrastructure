// cc-P199: ServUO pub57 Scripts/Items/Functional/SliderTrapTrainingKit.cs. A three-by-three sliding picture puzzle (eight
// styles, gump art 0x9CED-0x9D35): Remove Trap on it opens the puzzle; putting the tiles in order rolls Remove Trap at
// 0-100 and shuffles a new one. ServUO's BaseGump becomes pinned's DynamicGump; layout, moves and the solvable-shuffle rule
// are ServUO's.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.SkillHandlers;

namespace Server.Items
{
    public interface ISliderKit : IEntity
    {
        int[] Order { get; }
        int Style { get; }

        void Complete(Mobile m);
    }

    [SerializationGenerator(0, false)]
    public partial class SliderTrapTrainingKit : Item, ISliderKit, IRemoveTrapTrainingKit
    {
        [SerializableField(0, getter: "private", setter: "private")]
        private int _kitStyle;

        [SerializableField(1, getter: "private", setter: "private")]
        private int[] _tileOrder = new int[9];

        [Constructible]
        public SliderTrapTrainingKit() : base(41875)
        {
            _kitStyle = -1;
            Style = Utility.Random(8);
        }

        public override int LabelNumber => 1159016; // Slider Trap Training Kit

        public int[] Order => _tileOrder;

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Test
        {
            get => false;
            set
            {
                if (value)
                {
                    TestOrder();
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Style
        {
            get => _kitStyle;
            set
            {
                if (_kitStyle != value)
                {
                    _kitStyle = Math.Clamp(value, 0, 7);
                    Reset();
                }
            }
        }

        public void Reset()
        {
            for (var i = 0; i < _tileOrder.Length; i++)
            {
                _tileOrder[i] = 0;
            }

            var randomIndex = Utility.Random(_tileOrder.Length);
            var startID = 0x9CEE + _kitStyle * 9; // start at 1+ because the upper left picture is always omitted
            var list = new List<int>();

            for (var i = 0; i < 8; i++)
            {
                list.Add(startID + i);
            }

            for (var i = 0; i < _tileOrder.Length; i++)
            {
                if (i != randomIndex)
                {
                    var add = list[Utility.Random(list.Count)];
                    _tileOrder[i] = add;
                    list.Remove(add);
                }
            }

            var invCount = 0;
            for (var i = 0; i < _tileOrder.Length - 1; i++)
            {
                for (var j = i + 1; j < _tileOrder.Length; j++)
                {
                    if (_tileOrder[j] != 0 && _tileOrder[i] != 0 && _tileOrder[i] > _tileOrder[j])
                    {
                        invCount++;
                    }
                }
            }

            if (invCount % 2 == 1)
            {
                if (randomIndex > 2)
                {
                    (_tileOrder[0], _tileOrder[1]) = (_tileOrder[1], _tileOrder[0]);
                }
                else
                {
                    (_tileOrder[^1], _tileOrder[^2]) = (_tileOrder[^2], _tileOrder[^1]);
                }
            }

            this.MarkDirty();
        }

        public override void OnDoubleClick(Mobile m)
        {
            if (IsChildOf(m.Backpack))
            {
                // That appears to be trapped, using the remove trap skill would yield better results...
                m.SendLocalizedMessage(1159008);
            }
            else
            {
                m.SendMessage("That is not your chest!"); // ServUO: "Could not find cliloc, but this is the message given"
            }
        }

        public void OnRemoveTrap(Mobile from)
        {
            if (from is PlayerMobile pm)
            {
                pm.SendGump(new SliderTrapGump(pm, this));
            }
        }

        public void Complete(Mobile from)
        {
            from.SendLocalizedMessage(1159009); // You successfully disarm the trap!

            from.CheckTargetSkill(SkillName.RemoveTrap, this, 0, 100);

            Reset();
        }

        private void TestOrder()
        {
            var startID = 0x9CEE + _kitStyle * 9;
            var list = new List<int>();

            for (var i = 0; i < 8; i++)
            {
                list.Add(startID + i);
            }

            for (var i = 0; i < _tileOrder.Length; i++)
            {
                if (i != 1)
                {
                    _tileOrder[i] = list[0];
                    list.RemoveAt(0);
                }
                else
                {
                    _tileOrder[i] = 0;
                }
            }

            this.MarkDirty();
        }
    }

    public class SliderTrapGump : DynamicGump
    {
        public PlayerMobile User { get; }
        public ISliderKit Kit { get; set; }
        public int[] Order => Kit.Order;

        public SliderTrapGump(PlayerMobile pm, ISliderKit kit) : base(100, 100)
        {
            User = pm;
            pm.CloseGump<SliderTrapGump>();
            Kit = kit;
        }

        public void Refresh() => User.SendGump(this);

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();

            builder.AddBackground(0, 0, 270, 445, 0x6DB);
            builder.AddImage(15, 20, 0x9CED + Kit.Style * 9);
            builder.AddAlphaRegion(15, 20, 80, 133);

            for (var i = 0; i < Order.Length; i++)
            {
                var order = Order[i];

                if (order == 0)
                {
                    continue;
                }

                var x = i % 3 == 0 ? 15 : i % 3 == 1 ? 95 : 175;
                var y = i <= 2 ? 20 : i <= 5 ? 153 : 286;

                builder.AddButton(x, y, order, order, i + 1);
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (!Kit.Deleted && info.ButtonID is >= 1 and <= 9)
            {
                var pick = info.ButtonID - 1;
                var empty = Array.IndexOf(Order, 0);

                if (ValidMove(pick, empty))
                {
                    User.SendSound(0x42);

                    var id = Order[pick];
                    Order[pick] = 0;
                    Order[empty] = id;
                    (Kit as ISerializable)?.MarkDirty();

                    if (CheckSolution())
                    {
                        Kit.Complete(User);
                    }
                    else
                    {
                        Refresh();
                    }
                }
                else
                {
                    User.SendSound(0x051);
                    Refresh();
                }
            }
        }

        public static bool ValidMove(int pick, int empty) =>
            pick switch
            {
                0 => empty is 1 or 3,
                1 => empty is 0 or 2 or 4,
                2 => empty is 1 or 5,
                3 => empty is 0 or 4 or 6,
                4 => empty is 1 or 3 or 5 or 7,
                5 => empty is 2 or 4 or 8,
                6 => empty is 3 or 7,
                7 => empty is 4 or 6 or 8,
                8 => empty is 5 or 7,
                _ => false
            };

        private bool CheckSolution()
        {
            var start = 0x9CEE + Kit.Style * 9;

            return Order[0] == 0 &&
                   Order[1] == start &&
                   Order[2] == start + 1 &&
                   Order[3] == start + 2 &&
                   Order[4] == start + 3 &&
                   Order[5] == start + 4 &&
                   Order[6] == start + 5 &&
                   Order[7] == start + 6 &&
                   Order[8] == start + 7;
        }
    }
}

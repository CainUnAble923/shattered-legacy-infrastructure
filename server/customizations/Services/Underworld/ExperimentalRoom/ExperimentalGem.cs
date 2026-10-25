// ServUO: Services/Underworld/ExperimentalRoom/ExperimentalGem.cs (cc-P197). The gem the Puzzle Room's mastermind
// board hands out and the Experimental Room is played with. Double-clicked in the pack at the entrance hall
// (980,1117 17x3, Ter Mur) it offers the OK/CANCEL gump (1113407, 1113391); OK activates it (1113405): five seconds
// later a half-second timer starts and the gem takes one of the room's hues (room one: white, pink, red, blue; room
// two adds orange and light green; room three adds dark green and brown). The holder has four seconds to stand on
// the floor colour that neutralises it (GetRevertedHue: the opposite colour, or either of the opposite pair); then
// the gem goes grey (0x356), a point is scored (5-8 for room one, 10-15 for two, 15-25 for three) and after four
// seconds the next hue comes. Missed, a regular hue worsens to its extreme (1113401: pink to red, white to blue,
// orange to brown, light green to dark green), and from an extreme the right floor only brings it back to regular
// (half a point each way), while a second miss expels the holder to the entrance with their pets (1113400) and
// resets the gem. The room's points done, the next room unlocks (1113402, twenty seconds' grace) and after room
// three the gem is Complete (1113403) for the reward chest. Deleting a gem puts its Owner on the controller's
// 24-hour cooldown (1113413); 1113408 while active; 1054107 outside the pack.
//
// Saved: the Owner only, as ServUO (version 1 there, 0 here); Reset runs after a load, so an active gem comes back
// inactive at room one, as ServUO's. The 1800-second "self-destruct" the book promises is ServUO's Lifespan with an
// empty Decay override: the countdown shows and nothing happens at zero. Copied as it is (the D-4 rule's missing
// kind; notes). DateTime.UtcNow is Core.Now; ServUO's hard casts of RootParent are "as Mobile" with the same null
// branches; the gump is pinned's legacy Gump.

using System;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

public enum Room
{
    RoomZero = 0,
    RoomOne = 1,
    RoomTwo = 2,
    RoomThree = 3,
    RoomFour = 4
}

[SerializationGenerator(0, false)]
public partial class ExperimentalGem : BaseDecayingItem
{
    public const int Neutral = 0x356;
    public const int Red = 0x26;
    public const int White = 0x481;
    public const int Blue = 0x4;
    public const int Pink = 0x4B2;
    public const int Orange = 0x30;
    public const int LightGreen = 0x3D;
    public const int DarkGreen = 0x557;
    public const int Brown = 0x747;

    public static readonly TimeSpan HueToHueDelay = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan HueToLocDelay = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan RoomToRoomDelay = TimeSpan.FromSeconds(20);

    public static readonly Rectangle2D Entrance = new(980, 1117, 17, 3);

    private bool _active;
    private bool _isExtremeHue;
    private bool _complete;
    private bool _holding;
    private Room _currentRoom;
    private double _completed;
    private double _toComplete;
    private int _lastIndex;
    private int _currentHue;
    private int _span;
    private Timer _timer;
    private Timer _beginTimer; // cc-P610 (P491 #4): the five-second start, cancelled with the gem
    private DateTime _expire;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _owner;

    [CommandProperty(AccessLevel.GameMaster)]
    public bool Active => _active;

    [CommandProperty(AccessLevel.GameMaster)]
    public bool IsExtremeHue => _isExtremeHue;

    [CommandProperty(AccessLevel.GameMaster)]
    public bool Complete
    {
        get => _complete;
        set => _complete = value;
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public Room CurrentRoom
    {
        get => _currentRoom;
        set => _currentRoom = value;
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public double Completed
    {
        get => _completed;
        set => _completed = value;
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public double ToComplete
    {
        get => _toComplete;
        set => _toComplete = value;
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public int CurrentHue => _currentHue;

    [CommandProperty(AccessLevel.GameMaster)]
    public int LastIndex => _lastIndex;

    public override int Lifespan => _span;

    public override int LabelNumber => _active ? 1113409 : 1113380; // An Experimental Gem [Activated] / An Experimental Gem

    [Constructible]
    public ExperimentalGem() : base(6463)
    {
        _lastIndex = -1;
        _isExtremeHue = false;
        _currentHue = Neutral;
        _currentRoom = Room.RoomZero;
        _active = false;
        _expire = DateTime.MaxValue;
        _span = 0;
        _completed = 0;
        _toComplete = 0;

        Hue = Neutral;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1054107); // This item must be in your backpack.
        }
        else if (ExperimentalRoomController.IsInCooldown(from))
        {
            from.SendLocalizedMessage(1113413); // You have recently participated in this challenge. You must wait 24 hours to try again.
        }
        else if (!_active && Entrance.Contains(from.Location) && from.Map == Map.TerMur)
        {
            from.CloseGump<InternalGump>();
            from.SendGump(new InternalGump(this));
        }
        else if (_active)
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1113408); // The gem is already active. You must clear the rooms before it is destroyed!
        }
    }

    public override void Decay()
    {
    }

    public void Activate(Mobile from)
    {
        _active = true;

        StartTimer();

        _currentRoom = Room.RoomOne;
        _toComplete = Utility.RandomMinMax(5, 8);

        _beginTimer?.Stop();
        _beginTimer = Timer.DelayCall(TimeSpan.FromSeconds(5), BeginRoom_Callback);

        from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1113405); // Your gem is now active. You may enter the Experimental Room.

        InvalidateProperties();
    }

    public void BeginRoom_Callback()
    {
        _beginTimer = null;
        if (Deleted || !_active)
        {
            return; // cc-P610: deleted or no longer active within the five seconds: no room timer
        }

        _timer?.Stop();
        _timer = new InternalTimer(this);
        _lastIndex = -1;
        SelectNewHue();
    }

    public override void StartTimer()
    {
        if (_span == 0)
        {
            TimeLeft = 1800;
            _span = 1800;
            base.StartTimer();
            InvalidateProperties();
        }
    }

    private void SelectNewHue()
    {
        int index;
        var list = GetRoomHues();

        do
        {
            index = Utility.Random(list.Length);
        }
        while (index == _lastIndex);

        _expire = Core.Now + HueToLocDelay;
        _holding = false;
        _lastIndex = index;

        if (IsExtreme(list[index]))
        {
            _isExtremeHue = true;
        }

        Hue = list[index];
        _currentHue = Hue;
    }

    public void OnTick()
    {
        if (_holding || _expire > Core.Now)
        {
            return;
        }

        var m = RootParent as Mobile;
        var floorHue = GetFloorHue(m);
        var nextHue = GetRevertedHue(m, _currentHue, floorHue);

        if (m != null && nextHue >= 0) // standing in the right spot
        {
            if (_isExtremeHue && nextHue != Neutral) // from extreme back to regular
            {
                _completed += 0.5;

                Hue = nextHue;
                _currentHue = Hue;
                _isExtremeHue = false;
                _lastIndex = GetIndexFor(nextHue);
                _expire = Core.Now + HueToLocDelay;
                m.PlaySound(0x51);

                _completed += 0.5;
            }
            else // neutralized, new color
            {
                _completed++;

                _isExtremeHue = false;
                _holding = true;
                Hue = Neutral;
                _currentHue = Neutral;

                if (_completed < _toComplete)
                {
                    Timer.DelayCall(HueToHueDelay, SelectNewHue);
                    m.PlaySound(0x51);
                }
            }

            if (_completed >= _toComplete) // next room or complete!
            {
                if (_currentRoom == Room.RoomThree) // puzzle completed
                {
                    _holding = true;
                    Hue = Neutral;
                    _currentHue = Neutral;
                    CompletePuzzle(m);

                    m.PlaySound(0x1FF);
                    m.LocalOverheadMessage(MessageType.Regular, 0x21, 1113403); // Congratulations!! The last room has been unlocked!! Hurry through to claim your reward!
                }
                else // on to the next room!
                {
                    _currentRoom++;

                    _holding = true;
                    Hue = Neutral;
                    _currentHue = Neutral;

                    _completed = 0;

                    _toComplete = _currentRoom switch
                    {
                        Room.RoomTwo   => Utility.RandomMinMax(10, 15),
                        Room.RoomThree => Utility.RandomMinMax(15, 25),
                        _              => Utility.RandomMinMax(5, 8)
                    };

                    _lastIndex = -1;
                    Timer.DelayCall(RoomToRoomDelay, SelectNewHue);

                    m.PlaySound(0x1FF);
                    m.LocalOverheadMessage(MessageType.Regular, 0x21, 1113402); // The next room has been unlocked! Hurry through the door before your gem's state changes again!
                }
            }
        }
        else if (_isExtremeHue) // already extreme, failed
        {
            if (m != null && m.AccessLevel == AccessLevel.Player)
            {
                OnPuzzleFailed(m);
            }
            else
            {
                m?.SendMessage("As a GM, you get another chance!");

                _expire = Core.Now + HueToLocDelay;
            }

            m?.LocalOverheadMessage(MessageType.Regular, 0x21, 1113400); // You fail to neutralize the gem in time and are expelled from the room!!
        }
        else if (Hue != -1) // set to extreme hue
        {
            var hue = GetExtreme(_currentHue);

            Hue = hue;
            _currentHue = hue;
            _isExtremeHue = true;
            _lastIndex = GetIndexFor(hue);

            m?.LocalOverheadMessage(MessageType.Regular, 0x21, 1113401); // The state of your gem worsens!!

            _expire = Core.Now + HueToLocDelay;
        }
    }

    private void CompletePuzzle(Mobile m)
    {
        _timer?.Stop();
        _timer = null;

        _complete = true;
        Hue = Neutral;
        _currentHue = Hue;

        _currentRoom = Room.RoomFour;
    }

    private void OnPuzzleFailed(Mobile m)
    {
        if (m != null)
        {
            var x = Utility.RandomMinMax(Entrance.X, Entrance.X + Entrance.Width);
            var y = Utility.RandomMinMax(Entrance.Y, Entrance.Y + Entrance.Height);
            var z = m.Map.GetAverageZ(x, y);

            var from = m.Location;
            var p = new Point3D(x, y, z);

            m.PlaySound(0x1FE);
            Effects.SendLocationParticles(EffectItem.Create(from, m.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
            Effects.SendLocationParticles(EffectItem.Create(p, m.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);

            BaseCreature.TeleportPets(m, p, Map.TerMur);
            m.MoveToWorld(p, Map.TerMur);
        }

        Reset();
    }

    private void Reset()
    {
        _timer?.Stop();
        _timer = null;

        _complete = false;
        _completed = 0;
        _toComplete = 0;
        _isExtremeHue = false;
        _active = false;
        _currentRoom = Room.RoomOne;
        _lastIndex = -1;
        _expire = DateTime.MaxValue;
        _currentHue = Neutral;
        Hue = Neutral;
        InvalidateProperties();
    }

    public override void OnDelete()
    {
        _timer?.Stop();
        _beginTimer?.Stop();
        _beginTimer = null;

        if (_owner != null)
        {
            ExperimentalRoomController.AddToTable(_owner);
        }

        base.OnDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Reset();
    }

    private class InternalTimer : Timer
    {
        private readonly ExperimentalGem _gem;

        public InternalTimer(ExperimentalGem gem) : base(TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(0.5))
        {
            _gem = gem;
            Start();
        }

        protected override void OnTick()
        {
            if (_gem is { Deleted: false, Active: true }) // cc-P610: a deleted or spent gem's timer stops
            {
                _gem.OnTick();
            }
            else
            {
                Stop();
            }
        }
    }

    private int[] GetRoomHues() =>
        _currentRoom switch
        {
            Room.RoomTwo   => RoomHues[1],
            Room.RoomThree => RoomHues[2],
            _              => RoomHues[0]
        };

    private Rectangle2D GetRoomRec() =>
        _currentRoom switch
        {
            Room.RoomTwo   => RoomRecs[1],
            Room.RoomThree => RoomRecs[2],
            _              => RoomRecs[0]
        };

    public int GetIndexFor(int hue)
    {
        var hues = GetRoomHues();

        for (var i = 0; i < hues.Length; i++)
        {
            if (hue == hues[i])
            {
                return i;
            }
        }

        return White; // ServUO: "Oops, something happened, this should never happened."
    }

    public static int GetExtreme(int hue) =>
        hue switch
        {
            0x4B2 => Red,       // Pink to Red
            0x481 => Blue,      // White to Blue
            0x30  => Brown,     // Orange to Brown
            0x3D  => DarkGreen, // LightGreen to DarkGreen
            _     => -1
        };

    public static int GetRegular(int hue) =>
        hue switch
        {
            0x26  => Pink,       // Red to Pink
            0x4   => White,      // Blue to White
            0x747 => Orange,     // Brown to Orange
            0x557 => LightGreen, // DarkGreen to LightGreen
            _     => -1
        };

    /*  Extreme     Normal      SlowOpposite        FastOpposite
     *
     *  Red         Pink        White               Blue
     *  Blue        White       Pink                Red
     *  Brown       Orange      LightGreen          DarkGreen
     *  DarkGreen   LightGreen  Orange              Brown
     */

    /// <summary>
    /// Where the holder stands against the gem's hue: -1 in the wrong spot, otherwise the gem's next hue.
    /// </summary>
    public int GetRevertedHue(Mobile from, int oldHue, int floorHue)
    {
        if (from == null)
        {
            return -1;
        }

        if (!GetRoomRec().Contains(from.Location))
        {
            return -1;
        }

        switch (oldHue)
        {
            case 0x481: // White
                if (floorHue == Pink || floorHue == Red)
                {
                    return Neutral;
                }

                break;
            case 0x4B2: // Pink
                if (floorHue == White || floorHue == Blue)
                {
                    return Neutral;
                }

                break;
            case 0x30: // Orange
                if (floorHue == LightGreen || floorHue == DarkGreen)
                {
                    return Neutral;
                }

                break;
            case 0x3D: // LightGreen
                if (floorHue == Orange || floorHue == Brown)
                {
                    return Neutral;
                }

                break;
            case 0x26: // Red
                if (floorHue == White)
                {
                    return Pink;
                }

                if (floorHue == Blue)
                {
                    return Neutral;
                }

                break;
            case 0x4: // Blue
                if (floorHue == Pink)
                {
                    return White;
                }

                if (floorHue == Red)
                {
                    return Neutral;
                }

                break;
            case 0x557: // Dark Green
                if (floorHue == Orange)
                {
                    return Orange;
                }

                if (floorHue == Brown)
                {
                    return Neutral;
                }

                break;
            case 0x747: // Brown
                if (floorHue == LightGreen)
                {
                    return Orange;
                }

                if (floorHue == DarkGreen)
                {
                    return Neutral;
                }

                break;
        }

        return -1;
    }

    public static int GetFloorHue(Mobile from)
    {
        if (from == null || from.Map != Map.TerMur)
        {
            return 0;
        }

        for (var i = 0; i < FloorRecs.Length; i++)
        {
            if (FloorRecs[i].Contains(from.Location))
            {
                return FloorHues[i];
            }
        }

        return 0;
    }

    public bool IsExtreme(int hue)
    {
        foreach (var i in ExtremeHues)
        {
            if (i == hue)
            {
                return true;
            }
        }

        return false;
    }

    public static readonly int[] RegularHues = { White, Pink, LightGreen, Orange };

    private static readonly int[] ExtremeHues = { Red, Blue, Brown, DarkGreen };

    public static readonly int[][] RoomHues =
    {
        // Room One
        new[] { White, Pink, Red, Blue },

        // Room Two
        new[] { Pink, Blue, Red, Orange, LightGreen, White },

        // Room Three
        new[] { Blue, Pink, DarkGreen, Orange, Brown, LightGreen, Red, White }
    };

    public static readonly Rectangle2D[] FloorRecs =
    {
        // Room One
        new(977, 1104, 5, 5), // White, opposite of pink
        new(987, 1104, 5, 5), // Pink, opposite of white
        new(977, 1109, 5, 5), // Blue, opposite of red
        new(987, 1109, 5, 5), // Red, opposite of Blue

        // Room Two
        new(977, 1092, 6, 3), // White, opposite of pink
        new(986, 1092, 6, 3), // Red, opposite of Blue
        new(977, 1095, 6, 3), // Blue, opposite of red
        new(986, 1095, 6, 3), // LightGreen, opposite of Orange
        new(977, 1098, 6, 3), // Orange, opposite of LightGreen
        new(986, 1098, 6, 3), // Pink, opposite of white

        // Room Three
        new(977, 1074, 3, 5), // Red, opposite of Blue
        new(980, 1074, 3, 5), // White, opposite of Pink
        new(986, 1074, 3, 5), // Brown, opposite of DarkGreen
        new(989, 1074, 3, 5), // LightGreen, opposite of Orange
        new(977, 1079, 3, 5), // DarkGreen, opposite of Brown
        new(980, 1079, 3, 5), // Orange, opposite of LightGreen
        new(986, 1079, 3, 5), // Blue, opposite of red
        new(989, 1079, 3, 5)  // Pink, opposite of White
    };

    public static readonly int[] FloorHues =
    {
        // Room One
        White, Pink, Red, Blue,

        // Room Two
        White, Red, Blue, LightGreen, Orange, Pink,

        // Room Three
        Red, White, Brown, LightGreen, DarkGreen, Orange, Blue, Pink
    };

    public static readonly Rectangle2D[] RoomRecs =
    {
        new(977, 1104, 15, 10), // RoomOne
        new(977, 1092, 15, 9),  // RoomTwo
        new(977, 1074, 15, 10)  // RoomThree
    };

    private class InternalGump : Gump
    {
        private readonly ExperimentalGem _gem;

        public InternalGump(ExperimentalGem gem) : base(50, 50)
        {
            _gem = gem;

            AddPage(0);
            AddBackground(0, 0, 297, 115, 9200);

            AddImageTiled(5, 10, 285, 25, 2624);
            AddHtmlLocalized(10, 15, 275, 25, 1113407, 0x7FFF); // Experimental Room Access

            AddImageTiled(5, 40, 285, 40, 2624);
            AddHtmlLocalized(10, 40, 275, 40, 1113391, 0x7FFF); // Click CANCEL to read the instruction book or OK to start the timer now.

            AddButton(5, 85, 4017, 4018, 0);
            AddHtmlLocalized(40, 87, 80, 25, 1011012, 0x7FFF); // CANCEL

            AddButton(215, 85, 4023, 4024, 1);
            AddHtmlLocalized(250, 87, 80, 25, 1006044, 0x7FFF); // OK
        }

        public override void OnResponse(NetState state, in RelayInfo info)
        {
            if (info.ButtonID == 1)
            {
                _gem.Activate(state.Mobile);
            }
        }
    }
}

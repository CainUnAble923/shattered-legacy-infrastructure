// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/ExodusNexus.cs (cc-P195): the broken Nexus machines of
// the Exodus Dungeon. An Exotic Toolkit opens the circuit puzzle (ServUO's copy of the power generator's: pinned
// Items/Misc/PowerGenerator.cs is the same game); walking the hidden path with the arrows repairs it and prints a punch
// card, a wrong step is an arc of energy (60 energy, then three 20s while frozen), and a 65 Lockpicking rogue may buy a
// hint. A repaired machine (hue 0) breaks again (hue 1987) ten minutes later. [GenExodusNexus is folded into
// [GenExodusEncounter (ExodusDecoration). Saved as pinned's ControlPanel saves the same state: side length and path.
// Clilocs are ServUO's; the nonlocal spasm line is 1114444 (ServUO sends 1114443 twice, pinned's generator 1114444).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExodusNexus : NexusAddon
{
    public const int BrokenHue = 1987;
    public static readonly TimeSpan UseTimeout = TimeSpan.FromMinutes(2.0);
    public static readonly TimeSpan RepairDuration = TimeSpan.FromMinutes(10.0);

    private readonly HashSet<Mobile> _damageTable = new();

    [SerializableField(1, setter: "private")]
    private Point2D[] _path;

    private int _sideLength;
    private Timer _breakTimer;

    [Constructible]
    public ExodusNexus() : this(Utility.RandomMinMax(3, 6))
    {
    }

    [Constructible]
    public ExodusNexus(int sideLength)
    {
        Hue = BrokenHue;
        SideLength = sideLength;
    }

    [SerializableProperty(0, useField: nameof(_sideLength))]
    [CommandProperty(AccessLevel.GameMaster)]
    public int SideLength
    {
        get => _sideLength;
        set
        {
            value = Math.Clamp(value, 3, 6);

            if (_sideLength != value || _path == null)
            {
                _sideLength = value;
                InitPath();
                this.MarkDirty();
            }
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool Active => Hue == 0;

    public Mobile User { get; private set; }

    public DateTime LastUse { get; private set; }

    /// <summary>ServUO ExodusInitPath: a depth-first walk from (0,0) to the far corner; the path is the solution.</summary>
    public void InitPath()
    {
        var totalNodes = SideLength * SideLength;

        var stack = new Point2D[totalNodes];
        var current = stack[0] = new Point2D(0, 0);
        var stackSize = 1;

        var visited = new bool[SideLength, SideLength];
        visited[0, 0] = true;

        while (true)
        {
            var choices = new PathDirection[4];
            var count = 0;

            if (current.X > 0 && !visited[current.X - 1, current.Y])
            {
                choices[count++] = PathDirection.Left;
            }

            if (current.Y > 0 && !visited[current.X, current.Y - 1])
            {
                choices[count++] = PathDirection.Up;
            }

            if (current.X < SideLength - 1 && !visited[current.X + 1, current.Y])
            {
                choices[count++] = PathDirection.Right;
            }

            if (current.Y < SideLength - 1 && !visited[current.X, current.Y + 1])
            {
                choices[count++] = PathDirection.Down;
            }

            if (count > 0)
            {
                var dir = choices[Utility.Random(count)];

                current = dir switch
                {
                    PathDirection.Left  => new Point2D(current.X - 1, current.Y),
                    PathDirection.Up    => new Point2D(current.X, current.Y - 1),
                    PathDirection.Right => new Point2D(current.X + 1, current.Y),
                    _                   => new Point2D(current.X, current.Y + 1)
                };

                stack[stackSize++] = current;

                if (current.X == SideLength - 1 && current.Y == SideLength - 1)
                {
                    break;
                }

                visited[current.X, current.Y] = true;
            }
            else
            {
                current = stack[--stackSize - 1];
            }
        }

        var path = new Point2D[stackSize];

        for (var i = 0; i < stackSize; i++)
        {
            path[i] = stack[i];
        }

        Path = path;

        if (User != null)
        {
            User.CloseGump<NexusGameGump>();
            User = null;
        }
    }

    /// <summary>ServUO OpenGump: one user at a time, within three tiles, two minutes of idleness frees it.</summary>
    public void OpenGump(Mobile from)
    {
        if (!from.InRange(this, 3))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        if (User != null)
        {
            if (User == from)
            {
                return;
            }

            if (User.Deleted || User.Map != Map || !User.InRange(this, 3) || User.NetState == null ||
                Core.Now - LastUse >= UseTimeout)
            {
                User.CloseGump<NexusGameGump>();
            }
            else
            {
                from.SendLocalizedMessage(1152379); // Someone is currently working at the Nexus.
                return;
            }
        }

        User = from;
        LastUse = Core.Now;

        from.SendGump(new NexusGameGump(this, from, 0, false));
    }

    public void Touch() => LastUse = Core.Now;

    public void ReleaseUser() => User = null;

    /// <summary>ServUO DoDamage: 60 energy and three frozen seconds of shocks.</summary>
    public void DoDamage(Mobile to)
    {
        to.SendLocalizedMessage(1152372); // The Nexus shoots an arc of energy at you!
        to.BoltEffect(0);
        to.LocalOverheadMessage(MessageType.Regular, 0x21, 1114443); // * Your body convulses from electric shock *
        to.NonlocalOverheadMessage(MessageType.Regular, 0x21, 1114444, to.Name); // * ~1_NAME~ spasms from electric shock *

        AOS.Damage(to, to, 60, 0, 0, 0, 0, 100);

        if (!to.Alive)
        {
            return;
        }

        if (_damageTable.Add(to))
        {
            to.Frozen = true;
            new DamageTimer(this, to).Start();
        }
    }

    /// <summary>ServUO Solve: the sparks, a punch card in the pack, and the machine runs for ten minutes.</summary>
    public void Solve(Mobile from)
    {
        Effects.PlaySound(Location, Map, 0x211);
        Effects.PlaySound(Location, Map, 0x1F3);

        Effects.SendLocationEffect(Location, Map, 0x36B0, 4, 4);
        Effects.SendLocationEffect(new Point3D(X - 1, Y - 1, Z + 2), Map, 0x36B0, 4, 4);
        Effects.SendLocationEffect(new Point3D(X - 2, Y - 1, Z + 2), Map, 0x36B0, 4, 4);

        from.SendLocalizedMessage(1152371); // You repair the mysterious Nexus! As it whirs to life it spits out a punch card!
        from.AddToBackpack(new PunchCard());

        Hue = 0;
        StartBreakTimer(RepairDuration);
    }

    /// <summary>
    ///     ServUO NexusGameGump.OnResponse, the one place the puzzle moves so the facts can drive it without a client.
    ///     Returns the step to show next (hint says whether with the hint), or -1 when the session ended: the puzzle
    ///     solved, an arc of energy, a cancel, or the player too far.
    /// </summary>
    public int Respond(Mobile from, int step, int buttonID, out bool hint)
    {
        hint = false;

        if (Deleted || buttonID == 0 || !from.CheckAlive())
        {
            ReleaseUser();
            return -1;
        }

        if (from.Map != Map || !from.InRange(this, 3))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            ReleaseUser();
            return -1;
        }

        var path = Path;

        if (path == null || step < 0 || step + 1 >= path.Length)
        {
            ReleaseUser();
            return -1;
        }

        var nextNode = path[step + 1];

        if (buttonID == 5) // Attempt to Decipher
        {
            var lockpicking = from.Skills.Lockpicking.Value;

            if (lockpicking < 65.0)
            {
                return -1;
            }

            from.PlaySound(0x241);

            if (40.0 + Utility.RandomDouble() * 80.0 < lockpicking)
            {
                Touch();
                hint = true;
                return step;
            }

            DoDamage(from);
            ReleaseUser();
            return -1;
        }

        var curNode = path[step];
        int newX, newY;

        switch (buttonID)
        {
            case 1: // Left
                newX = curNode.X - 1;
                newY = curNode.Y;
                break;
            case 2: // Up
                newX = curNode.X;
                newY = curNode.Y - 1;
                break;
            case 3: // Right
                newX = curNode.X + 1;
                newY = curNode.Y;
                break;
            case 4: // Down
                newX = curNode.X;
                newY = curNode.Y + 1;
                break;
            default:
                return -1;
        }

        if (nextNode.X == newX && nextNode.Y == newY)
        {
            if (step + 1 == path.Length - 1)
            {
                Solve(from);
                ReleaseUser();
                return -1;
            }

            from.PlaySound(0x1F4);
            Touch();
            return step + 1;
        }

        DoDamage(from);
        ReleaseUser();
        return -1;
    }

    /// <summary>The arrow (1 left, 2 up, 3 right, 4 down) that moves from one node to the next.</summary>
    public static int ButtonFor(Point2D from, Point2D to)
    {
        if (to.X == from.X - 1 && to.Y == from.Y) return 1;
        if (to.X == from.X && to.Y == from.Y - 1) return 2;
        if (to.X == from.X + 1 && to.Y == from.Y) return 3;
        if (to.X == from.X && to.Y == from.Y + 1) return 4;
        return 0;
    }

    private void StartBreakTimer(TimeSpan delay)
    {
        _breakTimer?.Stop();
        _breakTimer = Timer.DelayCall(delay, () =>
        {
            if (!Deleted)
            {
                Hue = BrokenHue;
            }
        });
    }

    public override void OnAfterDelete()
    {
        _breakTimer?.Stop();
        _breakTimer = null;
        base.OnAfterDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (_path == null || _path.Length == 0)
        {
            InitPath();
        }

        if (Hue == 0)
        {
            StartBreakTimer(RepairDuration);
        }
    }

    private enum PathDirection
    {
        Left,
        Up,
        Right,
        Down
    }

    public class DamageTimer : Timer
    {
        private readonly ExodusNexus _nexus;
        private readonly Mobile _to;
        private int _step;

        public DamageTimer(ExodusNexus nexus, Mobile to) : base(TimeSpan.FromSeconds(5.0), TimeSpan.FromSeconds(5.0))
        {
            _nexus = nexus;
            _to = to;
        }

        protected override void OnTick()
        {
            if (_nexus.Deleted || _to.Deleted || !_to.Alive)
            {
                End();
                return;
            }

            _to.PlaySound(0x28);

            _to.LocalOverheadMessage(MessageType.Regular, 0xC9, true, "* Your body convulses from electric shock *");
            _to.NonlocalOverheadMessage(MessageType.Regular, 0xC9, true, $"* {_to.Name} spasms from electric shock *");

            AOS.Damage(_to, _to, 20, 0, 0, 0, 0, 100);

            if (++_step >= 3 || !_to.Alive)
            {
                End();
            }
        }

        private void End()
        {
            _nexus._damageTable.Remove(_to);
            _to.Frozen = false;
            Stop();
        }
    }

    public class NexusGameGump : Gump
    {
        private readonly ExodusNexus _nexus;
        private readonly Mobile _from;
        private readonly int _step;

        public NexusGameGump(ExodusNexus nexus, Mobile from, int step, bool hint) : base(5, 30)
        {
            _nexus = nexus;
            _from = from;
            _step = step;

            var sideLength = nexus.SideLength;

            AddBackground(50, 0, 530, 410, 0xA28);

            AddImage(0, 0, 0x28C8);
            AddImage(547, 0, 0x28C9);

            AddBackground(95, 20, 442, 90, 0xA28);

            AddHtmlLocalized(165, 35, 300, 45, 1153747, false, false); // <center>GENERATOR CONTROL nexus</center>
            AddHtmlLocalized(165, 60, 300, 70, 1153748, false, false); // <center>Use the Directional Controls to</center>
            AddHtmlLocalized(165, 75, 300, 85, 1153749, false, false); // <center>Close the Grid Circuit</center>

            AddImage(140, 40, 0x28D3);
            AddImage(420, 40, 0x28D3);

            AddBackground(365, 120, 178, 210, 0x1400);

            AddImage(365, 115, 0x28D4);
            AddImage(365, 288, 0x28D4);

            AddImage(414, 189, 0x589);
            AddImage(435, 210, 0xA52);

            AddButton(408, 222, 0x29EA, 0x29EC, 1, GumpButtonType.Reply, 0); // Left
            AddButton(448, 185, 0x29CC, 0x29CE, 2, GumpButtonType.Reply, 0); // Up
            AddButton(473, 222, 0x29D6, 0x29D8, 3, GumpButtonType.Reply, 0); // Right
            AddButton(448, 243, 0x29E0, 0x29E2, 4, GumpButtonType.Reply, 0); // Down

            AddBackground(90, 115, 30 + 40 * sideLength, 30 + 40 * sideLength, 0xA28);
            AddBackground(100, 125, 10 + 40 * sideLength, 10 + 40 * sideLength, 0x1400);

            for (var i = 0; i < sideLength; i++)
            {
                for (var j = 0; j < sideLength - 1; j++)
                {
                    AddImage(120 + 40 * i, 162 + 40 * j, 0x13F9);
                }
            }

            for (var i = 0; i < sideLength - 1; i++)
            {
                for (var j = 0; j < sideLength; j++)
                {
                    AddImage(138 + 40 * i, 147 + 40 * j, 0x13FD);
                }
            }

            var path = nexus.Path;
            var hues = new NodeHue[sideLength, sideLength];

            for (var i = 0; i <= step; i++)
            {
                var n = path[i];
                hues[n.X, n.Y] = NodeHue.Blue;
            }

            var lastNode = path[^1];
            hues[lastNode.X, lastNode.Y] = NodeHue.Red;

            for (var i = 0; i < sideLength; i++)
            {
                for (var j = 0; j < sideLength; j++)
                {
                    AddNode(110 + 40 * i, 135 + 40 * j, hues[i, j]);
                }
            }

            var curNode = path[step];
            AddImage(118 + 40 * curNode.X, 143 + 40 * curNode.Y, 0x13A8);

            if (hint)
            {
                var nextNode = path[step + 1];
                AddImage(119 + 40 * nextNode.X, 143 + 40 * nextNode.Y, 0x939);
            }

            if (from.Skills.Lockpicking.Value >= 65.0)
            {
                AddButton(365, 350, 0xFA6, 0xFA7, 5, GumpButtonType.Reply, 0);
                AddHtmlLocalized(405, 345, 140, 40, 1153750, false, false); // Attempt to Decipher the Circuit Path
            }
        }

        private enum NodeHue
        {
            Gray,
            Blue,
            Red
        }

        private void AddNode(int x, int y, NodeHue hue)
        {
            var id = hue switch
            {
                NodeHue.Gray => 0x25F8,
                NodeHue.Blue => 0x868,
                _            => 0x9A8
            };

            AddImage(x, y, id);
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var next = _nexus.Respond(_from, _step, info.ButtonID, out var hint);

            if (next >= 0)
            {
                _from.SendGump(new NexusGameGump(_nexus, _from, next, hint));
            }
        }
    }
}

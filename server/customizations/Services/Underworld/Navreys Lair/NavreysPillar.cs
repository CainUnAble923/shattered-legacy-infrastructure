// ServUO: Services/Underworld/Navrey's Lair/NavreysPillar.cs (cc-P197; the folder is named without ServUO's
// apostrophe). One of the three stone pillars (0x3BF) around Navrey Night-Eyes' lair, each with a type of Three,
// Six or Nine: Off (hue 0x456) while she is dead or the rockfall has just run, On (hue 0) while she lives, and Hot
// after a player within three tiles double-clicks it, which flickers the pillar between hues 0x461 and 0x462 for
// 3, 6 or 9 seconds (its type) before it cools back to On. Every click asks the controller whether all three are
// Hot at once (NavreysController.CheckPillars), which is the rockfall. The controller rotates the three types once a
// day. Saved: the state, the controller and the type, as ServUO; after a load the state is re-applied through the
// setter so a Hot pillar's timer restarts, as ServUO's Deserialize does.

using System;
using ModernUO.Serialization;

namespace Server.Items;

public enum NavreysPillarState
{
    Off,
    On,
    Hot
}

public enum PillarType
{
    Three = 1,
    Six,
    Nine
}

[SerializationGenerator(0, false)]
public partial class NavreysPillar : Item
{
    private InternalTimer _timer;

    [SerializableField(1)]
    private NavreysController _controller;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private PillarType _type;

    [SerializableProperty(0)]
    public NavreysPillarState State
    {
        get => _state;
        set
        {
            _state = value;

            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }

            switch (_state)
            {
                case NavreysPillarState.Off:
                    Hue = 0x456;
                    break;

                case NavreysPillarState.On:
                    Hue = 0;
                    break;

                case NavreysPillarState.Hot:
                    _timer = new InternalTimer(this);
                    _timer.Start();
                    break;
            }

            this.MarkDirty();
        }
    }

    public NavreysPillar(NavreysController controller, PillarType type) : base(0x3BF)
    {
        _controller = controller;
        _type = type;
        Movable = false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(this, 3) && _state == NavreysPillarState.On)
        {
            State = NavreysPillarState.Hot;
            _controller?.CheckPillars();
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        _timer?.Stop();
        _timer = null;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        State = _state;
    }

    private class InternalTimer : Timer
    {
        private readonly NavreysPillar _pillar;
        private int _ticks;

        public InternalTimer(NavreysPillar pillar) : base(TimeSpan.Zero, TimeSpan.FromSeconds(0.5))
        {
            _pillar = pillar;
            _ticks = 6 * (int)pillar.Type; // 3, 6, 9 seconds
        }

        protected override void OnTick()
        {
            _ticks--;

            _pillar.Hue = 0x461 + _ticks % 2;

            if (_ticks == 0)
            {
                Stop();
                _pillar.State = NavreysPillarState.On;
            }
        }
    }
}

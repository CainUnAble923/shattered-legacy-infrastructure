// cc-P199: ServUO pub57 Scripts/Items/Functional/BaseSwitch.cs. An immovable wall switch with an off and an on art. The
// first flip within two tiles turns it on with sound 0x3E8 and shows message A over the user's head; a working switch
// then runs DoSomethingSpecial (UnderworldSwitchWE in Switches.cs moves the Globe of Sosaria). For two minutes after,
// every further flip toggles the art and shows message B and nothing else; then it resets to off and unused. A message
// of 0 means a random one of 500357-500361 ("If this lever ever did anything, it doesn't do it anymore." and the rest),
// so a switch built with only its two ids is a dead lever. Out of reach: "I can't reach that."
//
// Shape changes forced by ModernUO, none visible to a player: the six saved fields are generated [SerializableField]s in
// ServUO's write order (BaseSwitch.cs:118-127); the load-time reset to off and unused (:137-138) is [AfterDeserialization].
// The two-minute reset is Timer.StartTimer (ServUO Timer.DelayCall, :88), unchanged in delay and effect.

using System;
using ModernUO.Serialization;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BaseSwitch : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _turnOn;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _turnOff;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _locMessageA;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _locMessageB;

    [SerializableField(4)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _working;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _used;

    [Constructible]
    public BaseSwitch(int turnOff, int turnOn, int locMessageA, int locMessageB, bool working) : base(turnOff)
    {
        Movable = false;
        _turnOn = turnOn;
        _turnOff = turnOff;
        _locMessageA = locMessageA;
        _locMessageB = locMessageB;
        _used = false;
        _working = working;
    }

    [Constructible]
    public BaseSwitch(int turnOff, int turnOn) : this(turnOff, turnOn, 0, 0, false)
    {
    }

    public override void OnDoubleClick(Mobile m)
    {
        if (!m.InRange(this, 2))
        {
            m.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            return;
        }

        var messageA = _locMessageA == 0 ? 500357 + Utility.Random(5) : _locMessageA;
        var messageB = _locMessageB == 0 ? 500357 + Utility.Random(5) : _locMessageB;

        if (ItemID == _turnOff && !_used)
        {
            ItemID = _turnOn;
            Used = true;
            Effects.PlaySound(Location, Map, 0x3E8);

            // Message received when it is turned on for the first time.
            m.LocalOverheadMessage(MessageType.Regular, 0, messageA);

            if (_working)
            {
                DoSomethingSpecial(m);
            }

            // Refresh time of two minutes, equal to RunUO's RaiseSwitch.
            Timer.StartTimer(TimeSpan.FromMinutes(2.0), () =>
                {
                    ItemID = _turnOff;
                    Used = false;
                }
            );
        }
        else if (ItemID == _turnOff)
        {
            ItemID = _turnOn;
            Effects.PlaySound(Location, Map, 0x3E8);
            m.LocalOverheadMessage(MessageType.Regular, 0, messageB); // Every click after the first until the refresh.
        }
        else
        {
            ItemID = _turnOff;
            Effects.PlaySound(Location, Map, 0x3E8);
            m.LocalOverheadMessage(MessageType.Regular, 0, messageB); // Every click after the first until the refresh.
        }
    }

    public virtual void DoSomethingSpecial(Mobile from)
    {
        from.LocalOverheadMessage(MessageType.Regular, 0, 1116629); // It does Nothing!
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _used = false;
        ItemID = _turnOff;
    }
}

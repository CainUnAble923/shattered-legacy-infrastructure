// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/SmugglersToolBox.cs -> ours. Hands out 5-12 lockpicks a
// use, 10 uses, one use back per day up to 20. ServUO adds the daily use inside Serialize (on every world save); here the
// same once-a-day top-up runs when the box is used (notes/cc-P127-port-treasure-maps-monster-stealing.md).

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SmugglersToolBox : Item
{
    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _usesRemaining;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextRecharge;

    [Constructible]
    public SmugglersToolBox() : base(0x1EB6)
    {
        Hue = 953;
        _usesRemaining = 10;
        _nextRecharge = Core.Now;
    }

    public override int LabelNumber => 1071520; // Smuggler's Tool Box

    public void Recharge()
    {
        if (_nextRecharge < Core.Now)
        {
            UsesRemaining = Math.Min(20, _usesRemaining + 1);
            _nextRecharge = Core.Now + TimeSpan.FromHours(24);
        }
    }

    public override void OnDoubleClick(Mobile m)
    {
        Recharge();

        if (!IsChildOf(m.Backpack) || _usesRemaining <= 0)
        {
            return;
        }

        var lockpick = new Lockpick(Utility.RandomMinMax(5, 12));

        if (m.Backpack == null || !m.Backpack.TryDropItem(m, lockpick, false))
        {
            m.SendLocalizedMessage(1077971); // Make room in your backpack first!
            lockpick.Delete();
        }
        else
        {
            m.SendLocalizedMessage(1071526); // You take some lockpicks from the tool box.
            UsesRemaining--;
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060584, _usesRemaining); // uses remaining: ~1_val~
    }
}

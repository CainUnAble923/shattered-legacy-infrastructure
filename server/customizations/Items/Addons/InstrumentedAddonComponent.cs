// ServUO: Items/Addons/AddonComponent.cs:343 (CC9 close, 2026-09-29), extracted from ServUO's multi-type file under its
// own name. An addon component that plays a sound when double-clicked within two tiles, at most once a second per
// player. It is the component of the King's Collection instruments (Items/Equipment/Instruments/: Cello, Cowbell,
// Trumpet, WallMountedBell), ported with it. ServUO's MonasteryBell is its fifth user and is not ours.
//
// What changed: the base is pinned's AddonComponent (Items/Addons/AddonComponent.cs:41), whose own OnDoubleClick
// routes to the addon; this override replaces it exactly as ServUO's does. ServUO saves SuccessSound after a version
// int; here it is the generator's field 0 at version 0.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class InstrumentedAddonComponent : AddonComponent
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _successSound;

    [Constructible]
    public InstrumentedAddonComponent(int itemID, int wellSound) : base(itemID) => _successSound = wellSound;

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 2))
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
        }
        else if (from.BeginAction<InstrumentedAddonComponent>())
        {
            Timer.DelayCall(TimeSpan.FromMilliseconds(1000), () => from.EndAction<InstrumentedAddonComponent>());

            from.PlaySound(SuccessSound);
        }
        else
        {
            from.SendLocalizedMessage(500119); // You must wait to perform another action
        }
    }
}

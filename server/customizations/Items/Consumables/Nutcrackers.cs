// cc-P128: ServUO pub57 Scripts/Items/Consumables/Nutcrackers.cs -> ours. The chocolate nutcracker (Time of Legends
// holiday cooking): a wrapped candy that unwraps on double-click into a dark, milk or white chocolate nutcracker, then
// eats as a candy cane. Two things ServUO's version has are not here, both recorded in the notes:
//   - the one-in-ten Card of Semidar under the wrapper (the card is in neither tree);
//   - the three cooking entries (their resource, SweetCocoaButter, is in neither tree), so the kind comes from the
//     constructor ([add ChocolateNutcracker Dark) rather than from CraftItem.Data, which pinned's craft engine lacks.

using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Craft;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ChocolateNutcracker : CandyCane, ICraftable
{
    public enum ChocolateType
    {
        Milk = 0x461,
        Dark = 0x465,
        White = 0x47E
    }

    [SerializableField(0, fieldChanged: nameof(OnTypeChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ChocolateType _type;

    [SerializableField(1, fieldChanged: nameof(OnWrappedChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _wrapped;

    [Constructible]
    public ChocolateNutcracker(ChocolateType type = ChocolateType.Dark) : base(39952)
    {
        Wrapped = true;
        Type = type;
    }

    public override int LabelNumber => _type switch
    {
        ChocolateType.Dark  => _wrapped ? 1156387 : 1156390,
        ChocolateType.White => _wrapped ? 1156389 : 1156392,
        _                   => _wrapped ? 1156388 : 1156391
    };

    private void OnTypeChanged(ChocolateType oldValue, ChocolateType newValue)
    {
        Hue = !_wrapped ? (int)newValue : 0;
        InvalidateProperties();
    }

    private void OnWrappedChanged(bool oldValue, bool newValue)
    {
        InvalidateID();
        InvalidateProperties();
    }

    private void InvalidateID()
    {
        if (_wrapped)
        {
            ItemID = 39952;
            Hue = 0;
        }
        else
        {
            ItemID = 39954;
            Hue = (int)_type;
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack) && Wrapped)
        {
            Unwrap(from);
        }
        else
        {
            base.OnDoubleClick(from);
        }
    }

    private void Unwrap(Mobile from)
    {
        from.PrivateOverheadMessage(MessageType.Regular, 1154, 1156393, from.NetState); // *You carefully peel back the wrapper...*
        from.PlaySound(Utility.Random(0x21F, 4));

        Timer.DelayCall(TimeSpan.FromSeconds(1), () => Wrapped = false);
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        if (!_wrapped)
        {
            base.GetContextMenuEntries(from, ref list);
        }
    }

    public int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, System.Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    ) => quality;
}

// cc-P262 (spec section 5): the accessories gump ([accessories). One row per slot: rings (the paperdoll ring first), then
// bracelets, then toe rings. A filled slot shows its jewel and a Remove button; an open slot says so; a locked slot shows
// its one-line unlock hint; a toe ring that does not count (shoes on) is grayed out. Equip takes a target cursor.
// Dragging onto a slot is not possible in a server gump (a gump takes no drops); it comes with the TazUO paperdoll slots.

#nullable enable

using System.Collections.Generic;
using Server.Gumps;
using Server.Network;
using Server.Targeting;

namespace Server;

public class AccessoriesGump : DynamicGump
{
    private const int LabelHue = 0x480;
    private const int ValueHue = 0x7FF;
    private const int GreyHue = 0x3B2;
    private const int LockedHue = 0x26;

    private const int EquipButton = 1;
    private const int RemoveBase = 100;

    private readonly Mobile _viewer;
    private readonly Mobile _wearer;
    private readonly List<Item> _shown = [];

    public override bool Singleton => true;

    public AccessoriesGump(Mobile viewer, Mobile wearer) : base(60, 40)
    {
        _viewer = viewer;
        _wearer = wearer;
    }

    private static string NameOf(Item item) => item.Name ?? item.GetType().Name;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var rows = ClusterFAccessories.MaxRings + ClusterFAccessories.MaxBracelets + ClusterFAccessories.MaxToeRings + 3;
        var height = 90 + rows * 22;

        builder.AddPage();
        builder.AddBackground(0, 0, 460, height, 9200);
        builder.AddAlphaRegion(10, 10, 440, height - 20);
        builder.AddLabel(20, 14, LabelHue, _wearer == _viewer ? "Your Accessories" : $"{_wearer.Name}'s Accessories");

        var y = 42;
        foreach (var kind in new[] { AccessoryKind.Ring, AccessoryKind.Bracelet, AccessoryKind.ToeRing })
        {
            var (title, max) = kind switch
            {
                AccessoryKind.Ring     => ("Rings", ClusterFAccessories.MaxRings),
                AccessoryKind.Bracelet => ("Bracelets", ClusterFAccessories.MaxBracelets),
                _                      => ("Toe rings", ClusterFAccessories.MaxToeRings)
            };

            builder.AddLabel(20, y, LabelHue, title);
            y += 22;

            var slots = ClusterFAccessories.SlotsFor(_wearer, kind);
            var worn = new List<Item?>();
            if (kind != AccessoryKind.ToeRing &&
                _wearer.FindItemOnLayer(kind == AccessoryKind.Ring ? Layer.Ring : Layer.Bracelet) is { } onLayer)
            {
                worn.Add(onLayer);
            }
            else if (kind != AccessoryKind.ToeRing)
            {
                worn.Add(null);
            }

            worn.AddRange(ClusterFAccessories.InContainer(_wearer, kind));

            for (var slot = 1; slot <= max; slot++)
            {
                var item = slot <= worn.Count ? worn[slot - 1] : null;
                var label = kind == AccessoryKind.ToeRing ? $"{slot}" : slot == 1 ? "1 (paperdoll)" : $"{slot}";

                if (slot > slots && item != null)
                {
                    // cc-P610 (P486 F01): a slot that locked again still holds its jewel; it shows, inactive, and comes off.
                    builder.AddButton(20, y, 4017, 4019, RemoveBase + _shown.Count);
                    _shown.Add(item);
                    builder.AddLabel(55, y, GreyHue, $"{label}: {NameOf(item)} (inactive: slot locked. {ClusterFAccessories.UnlockHint(kind, slot)})");
                }
                else if (slot > slots)
                {
                    builder.AddLabel(40, y, LockedHue, $"{label}: locked. {ClusterFAccessories.UnlockHint(kind, slot)}");
                }
                else if (item == null)
                {
                    builder.AddLabel(40, y, GreyHue, $"{label}: empty");
                }
                else
                {
                    var counts = item.Parent == _wearer || ClusterFAccessories.IsActive(_wearer, item);
                    builder.AddButton(20, y, 4017, 4019, RemoveBase + _shown.Count);
                    _shown.Add(item);
                    builder.AddLabel(55, y, counts ? ValueHue : GreyHue,
                        counts ? $"{label}: {NameOf(item)}" : $"{label}: {NameOf(item)} (inactive: smothered by footwear)");
                }

                y += 22;
            }
        }

        if (_viewer == _wearer)
        {
            builder.AddButton(20, y + 6, 4005, 4007, EquipButton);
            builder.AddLabel(55, y + 8, LabelHue, "Equip a ring, bracelet or toe ring from your backpack");
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_viewer != _wearer || _viewer.Deleted)
        {
            return;
        }

        if (info.ButtonID == EquipButton)
        {
            _viewer.SendMessage("Target the jewelry to wear.");
            _viewer.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
            {
                if (targeted is Item item && ClusterFAccessories.Equip(from, item) is { } why)
                {
                    from.SendMessage(0x22, why);
                }

                from.SendGump(new AccessoriesGump(from, from));
            });
            return;
        }

        var index = info.ButtonID - RemoveBase;
        if (index >= 0 && index < _shown.Count)
        {
            if (ClusterFAccessories.Unequip(_viewer, _shown[index]) is { } why)
            {
                _viewer.SendMessage(0x22, why);
            }

            _viewer.SendGump(new AccessoriesGump(_viewer, _wearer));
        }
    }
}

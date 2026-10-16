// cc-P128: ServUO pub57 Scripts/Items/Consumables/SkinTingeingTincture.cs -> ours. The skin tingeing tincture: a
// one-use skin dye. Double-clicked in the pack, it opens a gump of the player's race's skin hues (57 human, 32 elf, 25
// gargoyle) over the paperdoll; OK applies the chosen hue through the race's ClipSkinHue and deletes the tincture.
// ServUO's gump is a BaseGump (its own gump base, not pinned's); this one is a plain Gump that resends itself.

using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SkinTingeingTincture : Item
{
    [Constructible]
    public SkinTingeingTincture() : base(0xEFF) => Hue = 90;

    public override int LabelNumber => 1114770; // Skin Tingeing Tincture

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1114771); // Apply Directly to Forehead
    }

    public override void OnDoubleClick(Mobile m)
    {
        if (IsChildOf(m.Backpack) && m is PlayerMobile pm && !pm.HasGump<InternalGump>())
        {
            pm.SendGump(new InternalGump(pm, this, 0));
        }
    }

    public static int[] HumanSkinHues { get; } = BuildRange(1001, 57);

    public static int[] ElfSkinHues { get; } =
    {
        0x4DE, 0x76C, 0x835, 0x430, 0x24D, 0x24E, 0x24F, 0x0BF,
        0x4A7, 0x361, 0x375, 0x367, 0x3E8, 0x3DE, 0x353, 0x903,
        0x76D, 0x384, 0x579, 0x3E9, 0x374, 0x389, 0x385, 0x376,
        0x53F, 0x381, 0x382, 0x383, 0x76B, 0x3E5, 0x51D, 0x3E6
    };

    public static int[] GargoyleSkinHues { get; } = BuildRange(1754, 25);

    private static int[] BuildRange(int start, int count)
    {
        var hues = new int[count];

        for (var i = 0; i < hues.Length; i++)
        {
            hues[i] = i + start;
        }

        return hues;
    }

    public static int[] GetHueList(Mobile m)
    {
        if (m.Race == Race.Human)
        {
            return HumanSkinHues;
        }

        if (m.Race == Race.Elf)
        {
            return ElfSkinHues;
        }

        if (m.Race == Race.Gargoyle)
        {
            return GargoyleSkinHues;
        }

        return [];
    }

    /// <summary>What OK does: the chosen hue clipped to the race's skin range, and the tincture used up. False when nothing was chosen.</summary>
    public bool Apply(Mobile user, int selectedHue)
    {
        if (selectedHue == 0 || Deleted)
        {
            return false;
        }

        user.Hue = user.Race.ClipSkinHue(selectedHue & 0x3FFF) | 0x8000;
        Delete();
        return true;
    }

    private class InternalGump : Gump
    {
        private readonly PlayerMobile _user;
        private readonly SkinTingeingTincture _item;
        private readonly int _selectedHue;

        public InternalGump(PlayerMobile pm, SkinTingeingTincture item, int selectedHue) : base(50, 50)
        {
            _user = pm;
            _item = item;
            _selectedHue = selectedHue;

            AddBackground(0, 0, 460, 300, 2620);

            var list = GetHueList(_user);

            var rows = _user.Race == Race.Human ? 8 : 6;
            var start = _user.Race == Race.Human ? 40 : 80;
            var elf = _user.Race == Race.Elf;

            var x = start;
            var y = start;
            int displayHue;

            for (var i = 0; i < list.Length; i++)
            {
                if (i > 0 && i % rows == 0)
                {
                    x = start;
                    y += 22;
                }

                displayHue = elf ? list[i] - 1 : list[i];

                AddImage(x, y, 210, displayHue);
                AddButton(x, y, 212, 212, i + 100);

                x += 21;
            }

            displayHue = _selectedHue != 0 ? _selectedHue : _user.Hue ^ 0x8000;

            if (elf)
            {
                displayHue--;
            }

            AddImage(240, 0, GetPaperdollImage(), displayHue);

            AddButton(250, 260, 239, 238, 1);
            AddButton(50, 260, 242, 241, 0);
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var button = info.ButtonID;

            if (button >= 100)
            {
                button -= 100;

                var list = GetHueList(_user);

                if (button >= 0 && button < list.Length)
                {
                    _user.SendGump(new InternalGump(_user, _item, list[button]));
                }
            }
            else if (button == 1 && _item != null)
            {
                _item.Apply(_user, _selectedHue);
            }
        }

        private int GetPaperdollImage()
        {
            if (_user.Race == Race.Human)
            {
                return _user.Female ? 13 : 12;
            }

            if (_user.Race == Race.Elf)
            {
                return _user.Female ? 15 : 14;
            }

            if (_user.Race == Race.Gargoyle)
            {
                return _user.Female ? 665 : 666;
            }

            return 0;
        }
    }
}

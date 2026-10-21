// cc-P199: ServUO pub57 Scripts/Items/Functional/JewelryBox/JewelryBoxGump.cs. The jewelry box's gump: filter buttons
// (ring, bracelet, earrings, necklace, talisman, all), up to 50 pieces a page shown as buttons with their art and
// properties (clicking one moves it to the backpack), page buttons, and ADD JEWELRY, a repeating target that takes
// jewelry from the backpack, or every piece in a targeted sub-container, until Esc. ServUO's classic Gump becomes
// pinned's DynamicGump; layout, button ids, colors and messages are ServUO's, including ServUO's quirks: an item's
// row is i % 5 of its index in the whole list, and First page sends page 1, not 0.
//
// Differences from ServUO:
// - ServUO closes any open jewelry box gump in the constructor (JewelryBoxGump.cs:102); here the gump is a Singleton,
//   which pinned replaces on send.
// - AddECHandleInput (:188, :192) is an Enhanced Client element; pinned's DynamicGumpBuilder has none, and the classic
//   client ignores it.
// - An item button whose item is no longer in the box does nothing; ServUO hands AddToBackpack a null (:411-412).
// - An item's button id is its serial, as in ServUO; pinned's Serial is unsigned, so it is cast to int and back.

using System.Collections.Generic;
using Server.Gumps;
using Server.Network;
using Server.Targeting;

namespace Server.Items
{
    public class JewelryBoxGump : DynamicGump
    {
        private const int LabelColor = 0x7FFF;

        private readonly Mobile m_From;
        private readonly JewelryBox m_Box;
        private readonly List<Item> m_List;

        private readonly int m_Page;

        public override bool Singleton => true;

        public JewelryBoxGump(Mobile from, JewelryBox box, int page = 0) : base(100, 100)
        {
            m_From = from;
            m_Box = box;
            m_Page = page;

            m_List = new List<Item>();

            foreach (var item in m_Box.Items)
            {
                if (!CheckFilter(item))
                {
                    continue;
                }

                m_List.Add(item);
            }
        }

        public int Page => m_Page;

        public IReadOnlyList<Item> Shown => m_List;

        public bool CheckFilter(Item item)
        {
            var f = m_Box.Filter;

            if (f.IsDefault)
            {
                return true;
            }

            if (f.Ring && item is BaseRing)
            {
                return true;
            }

            if (f.Bracelet && item is BaseBracelet)
            {
                return true;
            }

            if (f.Earrings && item is BaseEarrings)
            {
                return true;
            }

            if (f.Necklace && item is BaseNecklace)
            {
                return true;
            }

            if (f.Talisman && item is BaseTalisman)
            {
                return true;
            }

            return false;
        }

        public int GetPageCount(int count) => (count + 49) / 50;

        public int GetIndexForPage(int page)
        {
            var index = 0;

            while (page-- > 0)
            {
                index += GetCountForIndex(index);
            }

            return index;
        }

        public int GetCountForIndex(int index)
        {
            var slots = 0;
            var count = 0;

            var list = m_List;

            for (var i = index; i >= 0 && i < list.Count; ++i)
            {
                var recipe = list[i];

                if (CheckFilter(recipe))
                {
                    var add = 1;

                    if (slots + add > 50)
                    {
                        break;
                    }

                    slots += add;
                }

                ++count;
            }

            return count;
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            var index = GetIndexForPage(m_Page);
            var count = GetCountForIndex(index);
            var pageCount = GetPageCount(m_List.Count);
            var currentpage = pageCount > 0 ? m_Page + 1 : 0;

            builder.AddPage();

            builder.AddImage(0, 0, 0x9CCA);
            builder.AddHtmlLocalized(40, 2, 500, 20, 1114513, "#1157694", 0x7FF0); // <DIV ALIGN=CENTER>~1_TOKEN~</DIV>

            builder.AddHtmlLocalized(50, 30, 100, 20, 1157695, 0x7FF0); // Select Filter:

            // Items: ~1_NUM~ of ~2_MAX~
            builder.AddHtmlLocalized(41, 350, 123, 20, 1157698, $"{m_List.Count}@{m_Box.DefaultMaxItems}", 0x7FF0);
            // Page ~1_CUR~ of ~2_MAX~
            builder.AddHtmlLocalized(212, 350, 123, 20, 1153561, $"{currentpage}@{pageCount}", 0x7FF0);
            builder.AddHtmlLocalized(416, 350, 100, 20, 1153562, 0x7FF0); // <DIV ALIGN="CENTER">PAGE</DIV>

            var f = m_Box.Filter;

            builder.AddHtmlLocalized(200, 30, 90, 20, 1154607, f.Ring ? 0x421F : LabelColor); // Ring
            builder.AddButton(160, 30, 0xFA5, 0xFA7, 101);

            builder.AddHtmlLocalized(325, 30, 90, 20, 1079905, f.Bracelet ? 0x421F : LabelColor); // Bracelet
            builder.AddButton(285, 30, 0xFA5, 0xFA7, 102);

            builder.AddHtmlLocalized(450, 30, 90, 20, 1079903, f.Earrings ? 0x421F : LabelColor); // Earrings
            builder.AddButton(410, 30, 0xFA5, 0xFA7, 104);

            builder.AddHtmlLocalized(200, 55, 90, 20, 1157697, f.Necklace ? 0x421F : LabelColor); // Necklace
            builder.AddButton(160, 55, 0xFA5, 0xFA7, 108);

            builder.AddHtmlLocalized(325, 55, 90, 20, 1071023, f.Talisman ? 0x421F : LabelColor); // Talisman
            builder.AddButton(285, 55, 0xFA5, 0xFA7, 116);

            builder.AddHtmlLocalized(450, 55, 90, 20, 1062229, f.IsDefault ? 0x421F : LabelColor); // All
            builder.AddButton(410, 55, 0xFA5, 0xFA7, 132);

            builder.AddButton(356, 353, 0x15E3, 0x15E7, 11); // First page
            builder.AddButton(376, 350, 0xFAE, 0xFB0, 1);    // Previous page

            builder.AddButton(526, 350, 0xFA5, 0xFA7, 2);    // Next Page
            builder.AddButton(560, 353, 0x15E1, 0x15E5, 12); // Last page

            builder.AddHtmlLocalized(270, 385, 100, 20, 1157696, LabelColor); // ADD JEWELRY
            builder.AddButton(225, 385, 0xFAB, 0xFAD, 3);

            var x = 0;

            for (var i = index; i < index + count && i >= 0 && i < m_List.Count; ++i)
            {
                var item = m_List[i];

                var xoffset = x / 5 * 50;
                var yoffset = i % 5 * 50;

                x++;

                builder.AddButton(50 + xoffset, 90 + yoffset, 0x92F, 0x92F, (int)item.Serial.Value);
                builder.AddItemProperty(item.Serial);
                builder.AddItem(57 + xoffset, 108 + yoffset, item.ItemID, item.Hue);
            }
        }

        private class InternalTarget : Target
        {
            private readonly JewelryBox m_Box;
            private readonly int m_Page;

            public InternalTarget(Mobile from, JewelryBox box, int page) : base(-1, false, TargetFlags.None)
            {
                m_Box = box;
                m_Page = page;
            }

            public void TryDrop(Mobile from, Item dropped)
            {
                if (!m_Box.CheckAccessible(from, m_Box))
                {
                    from.SendLocalizedMessage(1061637); // You are not allowed to access this.
                }
                else if (!dropped.IsChildOf(from.Backpack))
                {
                    from.SendLocalizedMessage(1157726); // You must be carrying the item to add it to the jewelry box.
                }
                else if (m_Box.IsAccept(dropped))
                {
                    if (m_Box.IsFull)
                    {
                        from.SendLocalizedMessage(1157723); // The jewelry box is full.
                    }
                    else
                    {
                        m_Box.DropItem(dropped);
                        from.Target = new InternalTarget(from, m_Box, m_Page);
                    }
                }
                else if (dropped is Container c)
                {
                    var count = 0;

                    for (var i = c.Items.Count - 1; i >= 0; --i)
                    {
                        if (i < c.Items.Count && m_Box.IsAccept(c.Items[i]))
                        {
                            if (m_Box.IsFull)
                            {
                                from.SendLocalizedMessage(1157723); // The jewelry box is full.
                                break;
                            }

                            m_Box.DropItem(c.Items[i]);
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        from.CloseGump<JewelryBoxGump>();
                        from.SendGump(new JewelryBoxGump(from, m_Box, m_Page));
                    }
                    else
                    {
                        from.SendLocalizedMessage(1157724); // This is not a ring, bracelet, necklace, earring, or talisman.
                        from.SendGump(new JewelryBoxGump(from, m_Box, m_Page));
                    }
                }
                else
                {
                    from.SendLocalizedMessage(1157724); // This is not a ring, bracelet, necklace, earring, or talisman.
                    from.SendGump(new JewelryBoxGump(from, m_Box, m_Page));
                }
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (m_Box?.Deleted == false && targeted is Item item)
                {
                    TryDrop(from, item);
                }
            }

            protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
            {
                if (m_Box?.Deleted == false)
                {
                    from.CloseGump<JewelryBoxGump>();
                    from.SendGump(new JewelryBoxGump(from, m_Box, m_Page));
                    from.SendLocalizedMessage(1157726); // You must be carrying the item to add it to the jewelry box.
                }
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (!m_Box.CheckAccessible(m_From, m_Box))
            {
                m_From.SendLocalizedMessage(1061637); // You are not allowed to access this.
                return;
            }

            var f = m_Box.Filter;

            var index = info.ButtonID;

            switch (index)
            {
                case 0:
                    {
                        break;
                    }
                case 1: // Previous page
                    {
                        m_From.SendGump(new JewelryBoxGump(m_From, m_Box, m_Page > 0 ? m_Page - 1 : m_Page));
                        break;
                    }
                case 2: // Next Page
                    {
                        if (GetIndexForPage(m_Page + 1) < m_List.Count)
                        {
                            m_From.SendGump(new JewelryBoxGump(m_From, m_Box, m_Page + 1));
                        }
                        else
                        {
                            m_From.SendGump(new JewelryBoxGump(m_From, m_Box, m_Page));
                        }

                        return;
                    }
                case 3: // ADD JEWELRY
                    {
                        m_From.Target = new InternalTarget(m_From, m_Box, m_Page);
                        // Target rings, bracelets, necklaces, earrings, or talisman in your backpack. You may also target a
                        // sub-container to add contents to the the jewelry box. When done, press ESC.
                        m_From.SendLocalizedMessage(1157725);
                        m_From.SendGump(new JewelryBoxGump(m_From, m_Box));
                        break;
                    }
                case 11: // First page
                    {
                        m_From.SendGump(new JewelryBoxGump(m_From, m_Box, m_Page > 0 ? 1 : m_Page));
                        break;
                    }
                case 12: // Last Page
                    {
                        var pagecount = GetPageCount(m_List.Count);

                        if (m_Page != pagecount && m_Page >= 1)
                        {
                            m_From.SendGump(new JewelryBoxGump(m_From, m_Box, pagecount));
                        }
                        else
                        {
                            m_From.SendGump(new JewelryBoxGump(m_From, m_Box, m_Page));
                        }

                        break;
                    }
                case 101: // Ring
                    {
                        f.Ring = true;
                        m_From.SendGump(new JewelryBoxGump(m_From, m_Box));
                        break;
                    }
                case 102: // Bracelet
                    {
                        f.Bracelet = true;
                        m_From.SendGump(new JewelryBoxGump(m_From, m_Box));
                        break;
                    }
                case 104: // Earrings
                    {
                        f.Earrings = true;
                        m_From.SendGump(new JewelryBoxGump(m_From, m_Box));
                        break;
                    }
                case 108: // Necklace
                    {
                        f.Necklace = true;
                        m_From.SendGump(new JewelryBoxGump(m_From, m_Box));
                        break;
                    }
                case 116: // Talisman
                    {
                        f.Talisman = true;
                        m_From.SendGump(new JewelryBoxGump(m_From, m_Box));
                        break;
                    }
                case 132: // ALL
                    {
                        f.Clear();
                        m_From.SendGump(new JewelryBoxGump(m_From, m_Box));
                        break;
                    }
                default:
                    {
                        var item = m_Box.Items.Find(x => x.Serial.Value == (uint)index);

                        if (item != null)
                        {
                            m_From.AddToBackpack(item);
                        }

                        m_From.SendGump(new JewelryBoxGump(m_From, m_Box));
                        break;
                    }
            }
        }
    }
}

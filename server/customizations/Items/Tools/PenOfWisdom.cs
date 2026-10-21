// cc-P199: ServUO pub57 Scripts/Items/Tools/PenOfWisdom.cs. The pen of wisdom (ServUO sells it in its Ultima Store,
// UltimaStore.cs:314): a blessed pen (hue 1260, 100 uses) that copies runebook entries. Target a source runebook in the
// backpack (or a locked-down one, as the house owner or a co-owner), then a destination runebook in the backpack; the
// gump lists the source's entries to tick, and Okay adds the ticked ones to the destination, consuming one recall rune
// and one Mark scroll from the top level of the backpack per entry and one use of the pen. ServUO's classic Gump becomes
// pinned's DynamicGump; layout, buttons and messages are ServUO's.
//
// Differences from ServUO:
// - The runic atlas. ServUO also accepts a RunicAtlas as the source (PenOfWisdom.cs:107); pinned and this shard have no
//   runic atlas, so that half of the test is commented out below and the pen works on runebooks only. The messages
//   still name the atlas, as ServUO's do.
// - Free slots. ServUO reads the destination's MaxEntries (:205); pinned's Runebook has no MaxEntries and holds 16
//   (pinned Runebook.cs:296, OnDragDrop's "Entries.Count >= 16"), which is also ServUO's Runebook.MaxEntries.
// - Copying. ServUO adds the source's own RunebookEntry objects to the destination (:341-344), so both books share them.
//   Pinned's RunebookEntry belongs to one book (it carries a back-reference for saving), so each ticked entry is copied
//   into a new entry of the destination with its location, facet, description and house, the way pinned's
//   Runebook.OnAfterDuped copies entries (Runebook.cs:281).
// - The gump keeps the mobile it was built for; ServUO reads sender.Mobile (:303). A button past the entry list is
//   ignored; ServUO would index past the end (:363).
// - ServUO's uses: a use is taken off at every Okay but the pen is never deleted, even at 0 or below, and the "crumbles
//   to dust" message (1115366) is sent every time. Kept as ServUO has it.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Multis;
using Server.Network;
using Server.Targeting;

namespace Server.Items
{
    [Flippable(0x0FBF, 0x0FC0)]
    [SerializationGenerator(0, false)]
    public partial class PenOfWisdom : Item, IUsesRemaining
    {
        [InvalidateProperties]
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _usesRemaining;

        [Constructible]
        public PenOfWisdom(int uses = 100) : base(0x0FC0)
        {
            LootType = LootType.Blessed;
            _usesRemaining = uses;
            Hue = 1260;
        }

        public override int LabelNumber => 1115358; // Pen of Wisdom

        public override double DefaultWeight => 1.0;

        public virtual bool ShowUsesRemaining
        {
            get => true;
            set { }
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add(1060584, _usesRemaining.ToString()); // uses remaining: ~1_val~
        }

        public override void OnDoubleClick(Mobile from)
        {
            base.OnDoubleClick(from);

            if (IsChildOf(from.Backpack))
            {
                // Please select the source runebook. Recall runes and Mark scrolls at the base level of your backpack are consumed.
                from.SendLocalizedMessage(1115359);
                from.Target = new SourceTarget(this);
            }
            else
            {
                from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
            }
        }

        public bool CheckAccess(Mobile m, Runebook book)
        {
            if (!book.IsLockedDown || m.AccessLevel >= AccessLevel.GameMaster)
            {
                return true;
            }

            var house = BaseHouse.FindHouseAt(book);

            return house != null && house.IsCoOwner(m);
        }

        // ServUO's SourceTarget.OnTarget (PenOfWisdom.cs:105-137).
        public void OnSourceTarget(Mobile from, object targ)
        {
            if (targ is Runebook /* || targ is RunicAtlas: no runic atlas on this shard */)
            {
                var book = (Runebook)targ;

                if (!book.IsChildOf(from.Backpack) && book.Movable)
                {
                    from.SendLocalizedMessage(1115329); // Runebooks you wish to copy must be in your backpack.
                }
                else if (!CheckAccess(from, book) && !book.Movable)
                {
                    // Only the house owner and co-owners can copy the lockdowned runebook with the Pen.
                    from.SendLocalizedMessage(1115332);
                }
                else if (book.Entries.Count == 0)
                {
                    from.SendLocalizedMessage(1115362); // Can't copy an empty runebook.
                }
                else if (book.Openers.Count != 0)
                {
                    from.SendLocalizedMessage(1115361); // Someone else is using this runebook.
                }
                else
                {
                    from.SendLocalizedMessage(1115363); // Please select the destination runebook.
                    from.Target = new CopyTarget(this, book);
                }
            }
            else
            {
                from.SendLocalizedMessage(1115333); // This item only works on runebooks or runic atlas.
            }
        }

        // ServUO's CopyTarget.OnTarget (PenOfWisdom.cs:151-179).
        public void OnCopyTarget(Mobile from, Runebook sourceBook, object targ)
        {
            if (targ is Runebook book)
            {
                if (!book.IsChildOf(from.Backpack))
                {
                    from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
                }
                else if (book == sourceBook)
                {
                    from.SendLocalizedMessage(1115360); // You can't select the same runebook!
                }
                else if (book.Openers.Count != 0)
                {
                    from.SendLocalizedMessage(1115361); // Someone else is using this runebook.
                }
                else if (!from.HasGump<PenOfWisdomGump>())
                {
                    from.SendGump(new PenOfWisdomGump(from, this, sourceBook, book, null));
                }
            }
            else
            {
                from.SendLocalizedMessage(1115333); // This item only works on runebooks or runic atlas.
            }
        }

        private class SourceTarget : Target
        {
            private readonly PenOfWisdom _pen;

            public SourceTarget(PenOfWisdom pen) : base(12, false, TargetFlags.None) => _pen = pen;

            protected override void OnTarget(Mobile from, object targ) => _pen.OnSourceTarget(from, targ);
        }

        private class CopyTarget : Target
        {
            private readonly PenOfWisdom _pen;
            private readonly Runebook _sourceBook;

            public CopyTarget(PenOfWisdom pen, Runebook book) : base(12, false, TargetFlags.None)
            {
                _pen = pen;
                _sourceBook = book;
            }

            protected override void OnTarget(Mobile from, object targ) => _pen.OnCopyTarget(from, _sourceBook, targ);
        }
    }

    public class PenOfWisdomGump : DynamicGump
    {
        public readonly int EntryColor = 0xFFFFFF;

        public readonly List<RunebookEntry> Checked;
        public readonly Runebook SourceBook;
        public readonly Runebook CopyBook;
        public readonly int RuneAmount;
        public readonly int MarkScrollAmount;
        public readonly int Blank;
        public readonly PenOfWisdom Pen;

        private readonly Mobile _from;

        public PenOfWisdomGump(Mobile from, PenOfWisdom pen, Runebook sourcebook, Runebook copybook, List<RunebookEntry> list)
            : base(50, 50)
        {
            var bp = from.Backpack;

            _from = from;
            Pen = pen;
            SourceBook = sourcebook;
            CopyBook = copybook;
            MarkScrollAmount = bp.GetAmount(typeof(MarkScroll), true);
            RuneAmount = bp.GetAmount(typeof(RecallRune), true);
            Blank = 16 - copybook.Entries.Count; // pinned's runebook holds 16; ServUO reads copybook.MaxEntries

            Checked = list ?? new List<RunebookEntry>();
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.SetNoClose(); // ServUO: Closable = false, Disposable = true, Dragable = true

            var entrycount = SourceBook.Entries.Count;

            var y = entrycount <= 16 ? 0 : 25;

            builder.AddPage();

            builder.AddBackground(4, 39, 391, 313 + y, 9200);
            builder.AddImageTiled(8, 45, 380, 53, 2624);

            // <CENTER>Pen of Wisdom<br>(Mark Scrolls: ~1_VAL~, Runes: ~2_VAL~ | Selected: ~3_VAL~, Blank: ~4_VAL~)</CENTER>
            builder.AddHtmlLocalized(
                7,
                50,
                380,
                53,
                1115428,
                $"@{MarkScrollAmount}@{RuneAmount}@{Checked.Count}@{Blank}",
                EntryColor
            );

            builder.AddImageTiled(8, 101, 188, 220, 2624);
            builder.AddImageTiled(199, 101, 188, 220, 2624);

            builder.AddButton(12, 325 + y, 4017, 4018, 20);
            builder.AddHtmlLocalized(48, 326 + y, 78, 20, 1006045, EntryColor); // Cancel

            builder.AddButton(153, 325 + y, 4011, 4012, 21);
            builder.AddHtmlLocalized(189, 326 + y, 78, 20, 1115427, EntryColor); // Select All

            builder.AddButton(309, 325 + y, 4023, 4024, 22);
            builder.AddHtmlLocalized(344, 326 + y, 78, 20, 1156596, EntryColor); // Okay

            var page = 1;
            var yy = 0;

            builder.AddPage(page);

            for (var i = 0; i < entrycount; i++)
            {
                if (page > 1)
                {
                    builder.AddButton(50, 325, 4014, 4015, 0, GumpButtonType.Page, page - 1);
                    builder.AddHtmlLocalized(85, 326, 150, 20, 1011067, EntryColor); // Previous page
                }

                var entry = SourceBook.Entries[i];
                var description = entry.Description ?? (i + 1 < 10 ? $"0{i + 1}" : $"{i + 1}");
                var isChecked = Checked.Contains(entry);

                if (yy < 8)
                {
                    builder.AddButton(15, 110 + yy * 25, isChecked ? 211 : 210, isChecked ? 210 : 211, i);
                    builder.AddLabelCropped(45, 110 + yy * 25, 115, 17, RunebookGump.GetMapHue(entry.Map), description);
                }
                else
                {
                    builder.AddButton(205, 110 + (yy - 8) * 25, isChecked ? 211 : 210, isChecked ? 210 : 211, i);
                    builder.AddLabelCropped(235, 110 + (yy - 8) * 25, 115, 17, RunebookGump.GetMapHue(entry.Map), description);
                }

                yy++;

                var pages = (i + 1) % 16 == 0;

                if (pages && entrycount - 1 != i)
                {
                    builder.AddButton(200, 325, 4005, 4006, 0, GumpButtonType.Page, page + 1);
                    builder.AddHtmlLocalized(235, 326, 150, 20, 1011066, EntryColor); // Next page
                    page++;
                    builder.AddPage(page);
                    yy = 0;
                }
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (Checked == null)
            {
                return;
            }

            var from = _from;

            switch (info.ButtonID)
            {
                case 20: // Cancel
                    {
                        break;
                    }
                case 21: // Select All
                    {
                        Checked.Clear();

                        for (var i = 0; i < SourceBook.Entries.Count; i++)
                        {
                            Checked.Add(SourceBook.Entries[i]);
                        }

                        if (!from.HasGump<PenOfWisdomGump>())
                        {
                            from.SendGump(new PenOfWisdomGump(from, Pen, SourceBook, CopyBook, Checked));
                        }

                        break;
                    }
                case 22: // OK
                    {
                        if (MarkScrollAmount < Checked.Count || RuneAmount < Checked.Count)
                        {
                            from.SendLocalizedMessage(1115364); // You don't have enough recall runes and Mark scrolls to do that.
                        }
                        else if (Blank < Checked.Count)
                        {
                            from.SendLocalizedMessage(1115330); // The destination runebook doesn't have enough space.
                        }
                        else if (!SourceBook.IsChildOf(from.Backpack) && SourceBook.Movable || !CopyBook.IsChildOf(from.Backpack))
                        {
                            from.SendLocalizedMessage(1115329); // Runebooks you wish to copy must be in your backpack.
                        }
                        else
                        {
                            foreach (var entry in Checked)
                            {
                                CopyBook.Entries.Add(
                                    new RunebookEntry(CopyBook, entry.Location, entry.Map, entry.Description, entry.House)
                                );
                            }

                            CopyBook.MarkDirty();

                            var bp = from.Backpack;

                            bp.ConsumeTotal(typeof(MarkScroll), Checked.Count, true);
                            bp.ConsumeTotal(typeof(RecallRune), Checked.Count, true);
                            Pen.UsesRemaining -= 1;
                            Pen.InvalidateProperties();

                            from.SendLocalizedMessage(1115331); // The Pen magically marks runes and binds them to the runebook.
                            from.SendLocalizedMessage(1115366); // The pen's magical power is consumed and it crumbles to dust.
                        }

                        break;
                    }
                default:
                    {
                        var index = info.ButtonID;

                        if (index < 0 || index >= SourceBook.Entries.Count)
                        {
                            break;
                        }

                        if (Checked.Contains(SourceBook.Entries[index]))
                        {
                            Checked.Remove(SourceBook.Entries[index]);
                        }
                        else
                        {
                            Checked.Add(SourceBook.Entries[index]);
                        }

                        if (!from.HasGump<PenOfWisdomGump>())
                        {
                            from.SendGump(new PenOfWisdomGump(from, Pen, SourceBook, CopyBook, Checked));
                        }

                        break;
                    }
            }
        }
    }
}

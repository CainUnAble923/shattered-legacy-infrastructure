// cc-P199: ServUO pub57 Scripts/Items/Functional/SeedBox/SeedBoxGump.cs. The seed box's two gumps. SeedBoxGump shows 20
// slots a page (each the slot's picture as a button, the seed's art in its hue and its properties); a slot opens
// SeedInfoGump: remove one, remove all, insert a seed before or after it (a target), or shift it right or left past an
// empty slot. ServUO's BaseGump becomes pinned's DynamicGump; layout, buttons, colors and messages are ServUO's.
//
// Differences from ServUO:
// - Each gump is a Singleton, which pinned closes and replaces on send; ServUO closes the old one in the constructor
//   (SeedBoxGump.cs:22, :152). Refresh is a resend and RefreshParent resends the parent, as ServUO's BaseGump does.
// - The seed's label. ServUO calls Seed.GetLabel (:166), which pinned's Seed has as a private method; SeedLabel below
//   repeats pinned's (Engines/Plants/Seed.cs:70-106) line for line.
// - ServUO closes the info gump from inside its layout when its seed is gone (:157-161); here Refresh closes it instead
//   of sending, and an empty layout is never sent.
// - Shift left also checks the slot is not the first one; ServUO reads Entries[index - 1] (:286) and would throw for a
//   button the gump never draws there.

using System;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.Plants
{
    public class SeedBoxGump : DynamicGump
    {
        public PlayerMobile User { get; }
        public SeedBox Box { get; set; }
        public int Page { get; set; }

        public int Pages => (int)Math.Ceiling(Box.Entries.Count / 20.0);

        public override bool Singleton => true;

        public SeedBoxGump(PlayerMobile user, SeedBox box, int page = 1) : base(100, 100)
        {
            User = user;
            Box = box;
            Page = page;
        }

        public void Refresh() => User.SendGump(this);

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddImage(0, 0, 2172);

            var start = (Page - 1) * 20;
            var index = 0;

            builder.AddHtmlLocalized(100, 345, 300, 20, 1151850, $"{Page}\t{Pages}", 0xFFFF);

            if (Page > 1)
            {
                builder.AddButton(45, 345, 5603, 5603, 1);
            }

            if (Page < Pages)
            {
                builder.AddButton(235, 345, 5601, 5601, 2);
            }

            for (var i = start; i < Box.Entries.Count && i < start + 20; i++)
            {
                var entry = Box.Entries[i];

                if (entry?.Seed == null)
                {
                    index++;
                    continue;
                }

                int x;
                int y;

                if (index < 4)
                {
                    x = 15 + index * 70;
                    y = 15;
                }
                else if (index < 8)
                {
                    x = 15 + (index - 4) * 70;
                    y = 82;
                }
                else if (index < 12)
                {
                    x = 15 + (index - 8) * 70;
                    y = 149;
                }
                else if (index < 16)
                {
                    x = 15 + (index - 12) * 70;
                    y = 216;
                }
                else
                {
                    x = 15 + (index - 16) * 70;
                    y = 283;
                }

                builder.AddButton(x, y, entry.Image, entry.Image, i + 100);
                builder.AddItem(x, y + 30, 0xDCF, entry.Seed.Hue);

                builder.AddItemProperty(entry.Seed.Serial);

                index++;
            }
        }

        public void CheckPage(SeedEntry entry)
        {
            var index = Box.Entries.IndexOf(entry);

            Page = (int)Math.Ceiling((double)(index + 1) / 20);
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (Box.Deleted)
            {
                return;
            }

            switch (info.ButtonID)
            {
                case 0:
                    {
                        break;
                    }
                case 1: // page back
                    {
                        Page--;

                        if (Page < 1)
                        {
                            Page = 1;
                        }

                        Refresh();
                        break;
                    }
                case 2: // page forward
                    {
                        Page++;

                        if (Page > Pages)
                        {
                            Page = Pages;
                        }

                        Refresh();
                        break;
                    }
                default:
                    {
                        var id = info.ButtonID - 100;

                        if (id >= 0 && id < Box.Entries.Count)
                        {
                            var entry = Box.Entries[id];

                            if (entry == null)
                            {
                                return;
                            }

                            Refresh();
                            new SeedInfoGump(User, Box, entry, this).Refresh();
                        }

                        break;
                    }
            }
        }
    }

    public class SeedInfoGump : DynamicGump
    {
        public static int TextHue = 0x696969;

        public PlayerMobile User { get; }
        public SeedBox Box { get; set; }
        public SeedEntry Entry { get; set; }
        public SeedBoxGump Parent { get; }

        public override bool Singleton => true;

        public SeedInfoGump(PlayerMobile user, SeedBox box, SeedEntry entry, SeedBoxGump par) : base(50, 50)
        {
            User = user;
            Box = box;
            Entry = entry;
            Parent = par;
        }

        public void Refresh()
        {
            if (Entry?.Seed == null)
            {
                User.CloseGump<SeedInfoGump>();
                return;
            }

            User.SendGump(this);
        }

        public void RefreshParent(bool resend = false)
        {
            Parent?.Refresh();

            if (resend)
            {
                Refresh();
            }
        }

        // Pinned Seed.GetLabel (Engines/Plants/Seed.cs:70-106), which is private there.
        public static int SeedLabel(Seed seed, out string args)
        {
            var typeInfo = PlantTypeInfo.GetInfo(seed.PlantType);
            var hueInfo = PlantHueInfo.GetInfo(seed.PlantHue);

            int title;

            if (seed.ShowType || typeInfo.PlantCategory == PlantCategory.Default)
            {
                title = hueInfo.Name;
            }
            else
            {
                title = (int)typeInfo.PlantCategory;
            }

            if (seed.Amount == 1)
            {
                if (seed.ShowType)
                {
                    args = $"#{title}\t#{typeInfo.Name}";
                    return typeInfo.GetSeedLabel(hueInfo);
                }

                args = $"#{title}";
                return hueInfo.IsBright() ? 1060839 : 1060838; // [bright] ~1_val~ seed
            }

            if (seed.ShowType)
            {
                args = $"{seed.Amount}\t#{title}\t#{typeInfo.Name}";
                return typeInfo.GetSeedLabelPlural(hueInfo);
            }

            args = $"{seed.Amount}\t#{title}";
            return hueInfo.IsBright() ? 1113491 : 1113490; // ~1_amount~ [bright] ~2_val~ seeds
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            if (Entry?.Seed == null)
            {
                return;
            }

            var color = Utility.C32216(TextHue);

            builder.AddBackground(0, 0, 300, 280, 5170);

            var seedloc = SeedLabel(Entry.Seed, out var args);
            var index = Box.Entries.IndexOf(Entry);

            builder.AddHtmlLocalized(30, 25, 270, 20, seedloc, args, color);

            builder.AddHtmlLocalized(50, 60, 150, 20, 1151840, color); // Remove: 1
            builder.AddButton(30, 60, 10740, 10740, 1);

            builder.AddHtmlLocalized(50, 90, 150, 20, 1151841, Entry.Seed.Amount.ToString(), color); // Remove: ~1_val~
            builder.AddButton(30, 90, 10740, 10740, 2);

            if (index > 0)
            {
                builder.AddHtmlLocalized(50, 120, 150, 20, 1151851, color); // Insert Seed Before
                builder.AddButton(30, 120, 10740, 10740, 3);
            }
            else
            {
                builder.AddImage(30, 120, 10740);
            }

            if (Box.Entries.Count < SeedBox.MaxUnique)
            {
                builder.AddHtmlLocalized(50, 150, 150, 20, 1151852, color); // Insert Seed After
                builder.AddButton(30, 150, 10740, 10740, 4);
            }
            else
            {
                builder.AddImage(30, 150, 10740);
            }

            if (index < Box.Entries.Count && Box.Entries.Count < SeedBox.MaxUnique)
            {
                builder.AddHtmlLocalized(50, 180, 150, 20, 1151842, color); // Shift Right
                builder.AddButton(30, 180, 10740, 10740, 5);
            }
            else
            {
                builder.AddImage(30, 180, 10740);
            }

            if (index > 0 && Box.Entries[index - 1] == null)
            {
                builder.AddHtmlLocalized(50, 210, 150, 20, 1151843, color); // Shift Left
                builder.AddButton(30, 210, 10740, 10740, 6);
            }
            else
            {
                builder.AddImage(30, 210, 10740);
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (Box.Deleted)
            {
                return;
            }

            var index = Box.Entries.IndexOf(Entry);

            switch (info.ButtonID)
            {
                case 0:
                    {
                        break;
                    }
                case 1:
                    {
                        Box.DropSeed(User, Entry, 1);

                        RefreshParent();
                        break;
                    }
                case 2:
                    {
                        Box.DropSeed(User, Entry, Entry.Seed.Amount);

                        RefreshParent();
                        break;
                    }
                case 3:
                    {
                        User.SendLocalizedMessage(1151849); // Click this button and target a seed to add it here.
                        User.BeginTarget(
                            -1,
                            false,
                            TargetFlags.None,
                            (from, targeted) =>
                            {
                                if (targeted is Seed seed)
                                {
                                    if (Box?.Deleted == false && index > 0)
                                    {
                                        Box.TryAddSeed(User, seed, index);
                                    }
                                }
                                else
                                {
                                    from.SendLocalizedMessage(1151838); // This item cannot be stored in the seed box.
                                }

                                RefreshParent();
                            }
                        );
                        break;
                    }
                case 4:
                    {
                        if (Box.Entries.Count < SeedBox.MaxUnique)
                        {
                            User.SendLocalizedMessage(1151849); // Click this button and target a seed to add it here.
                            User.BeginTarget(
                                -1,
                                false,
                                TargetFlags.None,
                                (from, targeted) =>
                                {
                                    if (targeted is Seed seed)
                                    {
                                        if (Box?.Deleted == false && index > 0)
                                        {
                                            Box.TryAddSeed(User, seed, index + 1);
                                        }
                                    }
                                    else
                                    {
                                        from.SendLocalizedMessage(1151838); // This item cannot be stored in the seed box.
                                    }

                                    RefreshParent();
                                }
                            );
                        }

                        break;
                    }
                case 5: // shift right
                    {
                        if (index >= 0 && index < Box.Entries.Count && Box.Entries.Count < SeedBox.MaxUnique)
                        {
                            Box.Entries.Insert(index, null);

                            if (index + 2 < Box.Entries.Count && Box.Entries[index + 2] == null)
                            {
                                Box.Entries.RemoveAt(index + 2);
                            }

                            Parent?.CheckPage(Entry);

                            Box.SyncSaved();
                            RefreshParent(true);
                        }

                        break;
                    }
                case 6: // shift left
                    {
                        if (index > 0 && index < Box.Entries.Count && Box.Entries[index - 1] == null)
                        {
                            Box.Entries.Remove(Entry);
                            Box.Entries.Insert(index - 1, Entry);
                            Box.TrimEntries();

                            Parent?.CheckPage(Entry);

                            Box.SyncSaved();
                            RefreshParent(true);
                        }

                        break;
                    }
            }
        }
    }
}

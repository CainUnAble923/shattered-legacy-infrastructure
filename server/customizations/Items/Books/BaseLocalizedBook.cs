// Ported from ServUO Scripts/Items/Books/BaseLocalizedBook.cs.
//
// A book whose pages are clilocs rather than authored text. ModernUO's BaseBook stores
// BookPageInfo strings and has no cliloc-page mode, so this is additive, not a duplicate.
//
// Needed by GargishDocumentBook, one of the two Collect-objective item families for the
// S5 quest spike (notes/s5-quest-spike.md).

using System;
using ModernUO.Serialization;
using Server.Gumps;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public abstract partial class BaseLocalizedBook : Item
    {
        public BaseLocalizedBook() : base(4082)
        {
        }

        public virtual object Title => "a book";
        public virtual object Author => "unknown";

        public abstract int[] Contents { get; }

        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(GetWorldLocation(), 2))
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            }
            else
            {
                from.CloseGump<LocalizedBookGump>();
                from.SendGump(new LocalizedBookGump(this));
                from.SendSound(0x55);
            }
        }

        private class LocalizedBookGump : DynamicGump
        {
            private const int Page1X = 40;
            private const int Page2X = 230;
            private const int StartY = 30;
            private const int Width = 140;
            private const int Height = 175;

            private readonly BaseLocalizedBook _book;

            public LocalizedBookGump(BaseLocalizedBook book) : base(50, 50) => _book = book;

            protected override void BuildLayout(ref DynamicGumpBuilder builder)
            {
                var contents = _book.Contents;
                var page = 0;
                var pages = (int)Math.Ceiling(contents.Length / 2.0);

                builder.AddPage(page);
                builder.AddImage(0, 0, 500);

                page++;
                builder.AddPage(page);

                switch (_book.Title)
                {
                    case int titleCliloc:
                        builder.AddHtmlLocalized(Page1X, 60, Width, 48, titleCliloc);
                        break;
                    case string titleText:
                        builder.AddHtml(Page1X, 60, Width, 48, titleText);
                        break;
                    default:
                        builder.AddLabel(Page1X, 60, 0, "A Book");
                        break;
                }

                builder.AddHtml(40, 130, 200, 16, "by");

                switch (_book.Author)
                {
                    case int authorCliloc:
                        builder.AddHtmlLocalized(Page1X, 155, Width, 16, authorCliloc);
                        break;
                    case string authorText:
                        builder.AddHtml(Page1X, 155, Width, 16, authorText);
                        break;
                    default:
                        builder.AddLabel(Page1X, 155, 0, "unknown");
                        break;
                }

                for (var i = 0; i < contents.Length; i++)
                {
                    var cliloc = contents[i];

                    if (cliloc <= 0)
                    {
                        continue;
                    }

                    var endPage = false;
                    var x = Page1X;

                    if (page == 1)
                    {
                        x = Page2X;
                        endPage = true;
                    }
                    else if ((i + 1) % 2 == 0)
                    {
                        x = Page1X;
                    }
                    else if (page <= pages)
                    {
                        endPage = true;
                        x = Page2X;
                    }

                    builder.AddHtmlLocalized(x, StartY, Width, Height, cliloc);

                    if (page < pages)
                    {
                        builder.AddButton(356, 0, 502, 502, 0, GumpButtonType.Page, page + 1);
                    }

                    if (page > 0)
                    {
                        builder.AddButton(0, 0, 501, 501, 0, GumpButtonType.Page, page - 1);
                    }

                    if (endPage)
                    {
                        page++;
                        builder.AddPage(page);
                    }
                }
            }
        }
    }
}

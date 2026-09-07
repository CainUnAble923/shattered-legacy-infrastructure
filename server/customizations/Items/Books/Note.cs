// Ported from ServUO Scripts/Items/Books/Note.cs.
//
// A single-page readable note. ModernUO has BaseBook (multi-page, authored) but nothing
// that renders one block of text or one cliloc in a scroll gump, so this is additive
// rather than a duplicate: searched Projects/UOContent for a localized single-page
// reader and found none.
//
// Needed by GargishDocumentNote, which is the Collect-objective target for the S5 quest
// spike (notes/s5-quest-spike.md).

using ModernUO.Serialization;
using Server.Gumps;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class Note : Item
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private string _noteString;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _number;

        [Constructible]
        public Note() : base(5357)
        {
        }

        [Constructible]
        public Note(string content) : base(5357) => _noteString = content;

        [Constructible]
        public Note(int number) : base(5357) => _number = number;

        public override void OnDoubleClick(Mobile m)
        {
            if (m.InRange(GetWorldLocation(), 3))
            {
                m.CloseGump<NoteGump>();
                m.SendGump(new NoteGump(this));
            }
        }

        private class NoteGump : DynamicGump
        {
            private readonly Note _note;

            public NoteGump(Note note) : base(50, 50) => _note = note;

            protected override void BuildLayout(ref DynamicGumpBuilder builder)
            {
                builder.AddImage(0, 0, 9380);
                builder.AddImage(114, 0, 9381);
                builder.AddImage(171, 0, 9382);
                builder.AddImage(0, 140, 9386);
                builder.AddImage(114, 140, 9387);
                builder.AddImage(171, 140, 9388);

                if (_note.NoteString != null)
                {
                    builder.AddHtml(38, 55, 200, 178, _note.NoteString, "#000000", scrollbar: true);
                }
                else if (_note.Number > 0)
                {
                    builder.AddHtmlLocalized(38, 55, 200, 178, _note.Number, 1, false, true);
                }
            }
        }
    }
}

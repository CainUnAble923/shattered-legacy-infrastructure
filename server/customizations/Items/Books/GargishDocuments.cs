// Ported from ServUO Scripts/Items/Books/GargishDocuments.cs.
//
// The ten Gargish documents are the Collect-objective targets of the S5 quest spike,
// and ChronicleOfTheGargoyleQueen1 is its reward item. See notes/s5-quest-spike.md.
//
// Deviation: ServUO's GargishDocumentBook.AddNameProperty overrides the *name* line to
// read "Gargish Document - <title>". ModernUO's Item.AddNameProperty takes IPropertyList
// rather than ObjectPropertyList; the call is otherwise the same and is kept.

using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Multis;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public abstract partial class GargishDocumentBook : BaseLocalizedBook, ISecurable
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private SecureLevel _level;

        public GargishDocumentBook()
        {
        }

        public override int[] Contents => Array.Empty<int>();

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);
            SetSecureLevelEntry.AddTo(from, this, ref list);
        }

        public override void AddNameProperty(IPropertyList list)
        {
            switch (Title)
            {
                case int cliloc:
                    list.Add(1150928, $"#{cliloc}"); // Gargish Document - ~1_NAME~
                    break;
                case string text:
                    list.Add(1150928, text); // Gargish Document - ~1_NAME~
                    break;
                default:
                    base.AddNameProperty(list);
                    break;
            }
        }
    }

    [SerializationGenerator(0, false)]
    public abstract partial class GargishDocumentNote : Note
    {
        public GargishDocumentNote()
        {
        }

        public GargishDocumentNote(int content) : base(content)
        {
        }

        public virtual int Title => 0;

        public override void AddNameProperty(IPropertyList list)
        {
            list.Add(1150928, $"#{Title}"); // Gargish Document - ~1_NAME~
        }
    }

    [SerializationGenerator(0, false)]
    public partial class ChallengeRite : GargishDocumentBook
    {
        [Constructible]
        public ChallengeRite() => Hue = 1007;

        public override object Title => 1150904; // The Challenge Rite
        public override object Author => "unknown";

        public override int[] Contents { get; } =
        {
            1150915, 1150916, 1150917, 1150918, 1150919, 1150920, 1150921, 1150922
        };
    }

    [SerializationGenerator(0, false)]
    public partial class OnTheVoid : GargishDocumentBook
    {
        [Constructible]
        public OnTheVoid() => Hue = 404;

        public override object Title => 1150907; // On the Void
        public override object Author => "Prugyilonus";

        public override int[] Contents { get; } = { 1150894, 1150895, 1150896 };
    }

    [SerializationGenerator(0, false)]
    public partial class InMemory : GargishDocumentBook
    {
        [Constructible]
        public InMemory() => Hue = 375;

        public override object Title => 1150913; // In Memory
        public override object Author => "Queen Zhah";

        public override int[] Contents { get; } = { 1151071, 1151072, 1151073 };
    }

    [SerializationGenerator(0, false)]
    public partial class ChronicleOfTheGargoyleQueen1 : GargishDocumentBook
    {
        private static readonly int[] _contents = BuildContents();

        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _charges;

        [Constructible]
        public ChronicleOfTheGargoyleQueen1()
        {
            Hue = 567;
            _charges = 500;
        }

        public override object Title => 1150914; // Chronicle of the Gargoyle Queen Vol. 1
        public override object Author => "Queen Zhah";
        public override int[] Contents => _contents;

        // ServUO fills this in a static Initialize(); the values are constant, so build it
        // in a static initializer rather than relying on Initialize() being discovered.
        private static int[] BuildContents()
        {
            var contents = new int[34];

            for (var i = 0; i < contents.Length; i++)
            {
                contents[i] = i == 0 ? 1150901 : 1150943 + (i - 1);
            }

            return contents;
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add(1153098, _charges); // charges: ~1_val~
        }
    }

    [SerializationGenerator(0, false)]
    public partial class AnthenaeumDecree : GargishDocumentNote
    {
        [Constructible]
        public AnthenaeumDecree() : base(1150891)
        {
        }

        public override int Title => 1150905; // Athenaeum Decree
    }

    [SerializationGenerator(0, false)]
    public partial class LetterFromTheKing : GargishDocumentNote
    {
        private const string Content =
            "To Her Honor the High Broodmother, Lady Zhah from his majesty, King Trajalem:<br><br>\tHigh Broodmother, I have received your latest petition regarding your desires and I once again must remind you that I have absolutely no interest in altering tradition or granting you the freedom from the slavery you have deluded yourself into believing makes up your life.<br><br>Please remember that your office may be stripped by me if you are deemed unfit to lead the other Broodmothers. Be happy with your place and do not forget it; this is the last time I will lower myself to respond to these ridiculous accusations and requests.";

        [Constructible]
        public LetterFromTheKing() => NoteString = Content;

        public override int Title => 1150906; // A Letter from the King
    }

    [SerializationGenerator(0, false)]
    public partial class ShilaxrinarsMemorial : GargishDocumentNote
    {
        [Constructible]
        public ShilaxrinarsMemorial() : base(1150899)
        {
        }

        public override int Title => 1150908; // Shilaxrinar's Memorial
    }

    [SerializationGenerator(0, false)]
    public partial class ToTheHighScholar : GargishDocumentNote
    {
        [Constructible]
        public ToTheHighScholar() : base(1151062)
        {
        }

        public override int Title => 1150909; // To the High Scholar
    }

    [SerializationGenerator(0, false)]
    public partial class ToTheHighBroodmother : GargishDocumentNote
    {
        [Constructible]
        public ToTheHighBroodmother() : base(1151064)
        {
        }

        public override int Title => 1150910; // To the High Broodmother
    }

    [SerializationGenerator(0, false)]
    public partial class ReplyToTheHighScholar : GargishDocumentNote
    {
        [Constructible]
        public ReplyToTheHighScholar() : base(1151066)
        {
        }

        public override int Title => 1150911; // Reply to the High Scholar
    }

    [SerializationGenerator(0, false)]
    public partial class AccessToTheIsle : GargishDocumentNote
    {
        [Constructible]
        public AccessToTheIsle() : base(1151069)
        {
        }

        public override int Title => 1150912; // Access to the Isle
    }
}

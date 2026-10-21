// ServUO: Services/Underworld/ExperimentalRoom/ExperimentalBook.cs (cc-P197). Sir Wilber's "Read Me!" at the
// Experimental Room's entrance (995,1114,-36): the rules of the gem game, seven pages, ServUO's text verbatim.
// BrownBook(false) is pinned's read-only brown book; ItemID 4030 as ServUO sets it.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExperimentalBook : BrownBook
{
    public static readonly BookContent Content = new(
        "Read Me!",
        "Sir Wilber",
        new BookPageInfo(
            "Hello again!",
            "This is Part II of my",
            "experiment. Beyond these",
            "doors is a challenge for",
            "those carrying an ",
            "Experimental Gem."
        ),
        new BookPageInfo(
            "INSTRUCTIONS:",
            "Activate the Gem to start",
            "the self-destruct timer.",
            "You have 30 minutes to",
            "reach the final room."
        ),
        new BookPageInfo(
            "Each room has colored",
            "areas matching various states:",
            "White = Cold",
            "Pink = Warm",
            "Blue = Freezing",
            "Red = Blazing"
        ),
        new BookPageInfo(
            "Green = Poison",
            "Orange = Cure",
            "Dark Green= Lethal",
            "Brown=Greater Cure",
            "Your gem's state will cycle",
            "randomly through these"
        ),
        new BookPageInfo(
            "colors. Each time the gem",
            "shifts, you must enter the",
            "colored region that will",
            "bring your gem back to neutral",
            "state (grey). "
        ),
        new BookPageInfo(
            "Failing when the gem reaches",
            "the extreme of any state get you",
            "expelled from the room and you",
            "will have to start over. The",
            "self-destruct"
        ),
        new BookPageInfo(
            "timer will not reset. Once you",
            "reach the final room, place your",
            "gem in the box to receive",
            "your reward."
        )
    );

    [Constructible]
    public ExperimentalBook() : base(false) => ItemID = 4030;

    public override int LabelNumber => 1113479;

    public override BookContent DefaultContent => Content;
}

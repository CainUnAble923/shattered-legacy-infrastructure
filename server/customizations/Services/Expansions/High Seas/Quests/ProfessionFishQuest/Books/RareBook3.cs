// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Quests/ProfessionFishQuest/Books/RareBook3.cs -> ours,
// High Seas, second half. "Volume 3 - Uncommon Dungeon Fish", one of Captain Piddlewash's six fishing guides, a
// profession fish quest reward (FishMonger, the rare book category); read only, its text ServUO's verbatim.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class FishingGuideBook3 : BaseBook
{
    public static readonly BookContent Content = new(
        null, "Cpt. Piddlewash",
        new BookPageInfo(
            "Crag Snapper:",
            "Crag Snapper be fine",
            "eating. Just mind yer",
            "fingers. "
        ),
        new BookPageInfo(
            "Cutthrout Trout:",
            "This dungeon menace",
            "'tis the very one that",
            "gave rise to the ol'",
            "saying. 'Ne'er take a",
            "bath in dungeon water. "
        ),
        new BookPageInfo(
            "Darkfish:",
            "Ye find this fish in",
            "undergroun rivers and",
            "lakes. But only dark",
            "undergroun rivers and",
            "lakes. "
        ),
        new BookPageInfo(
            "Demon Trout:",
            "Beware, this big devil",
            "comes out of the water",
            "spicy. "
        ),
        new BookPageInfo(
            "Drakefish:",
            "The smaller cousin o'",
            "the dragonfish, this",
            "beauty be much easier",
            "to catch and thus more",
            "commonly used in cooking."
        ),
        new BookPageInfo(
            "Dungeon Chub:",
            "This be the only",
            "subterranean member",
            "o' the chub family. "
        ),
        new BookPageInfo(
            "Grim Cisco:",
            "This fish is sought",
            "for medicinal",
            "purposes. They say",
            "it be the best cure",
            "for hysteria. "
        ),
        new BookPageInfo(
            "Infernal Tuna:",
            "This fish be deadly",
            "poisonous unless ye",
            "cook it in butter",
            "with a bit o' ",
            "thyme and serve it",
            "will ale. "
        ),
        new BookPageInfo(
            "Lurker Fish:",
            "These fish like to",
            "hide up under",
            "corpses floating",
            "in underground rivers."
        ),
        new BookPageInfo(
            "Orc Bass:",
            "If ye be ever ",
            "chased by orcs, ",
            "throw one down an",
            "keep runnin! Ever",
            "since I started",
            "tellin' folks this,",
            "I been sellin more",
            "orc bass. "
        ),
        new BookPageInfo(
            "Snaggletoth Bass:",
            "This dungeon lurker",
            "be resemblin' a ",
            "large mouth bass",
            "excepting it be",
            "having huge jagged",
            "teeth."
        ),
        new BookPageInfo(
            "Tormented Pike:",
            "This pike be hunted",
            "by every monster in",
            "Sosaria except few a",
            "few."
        )
    );

    [Constructible]
    public FishingGuideBook3() : base(Utility.Random(0xFF1, 2), false) => Name = "Volume 3 - Uncommon Dungeon Fish";

    public override BookContent DefaultContent => Content;
}

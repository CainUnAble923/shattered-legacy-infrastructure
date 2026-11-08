// cc-P762 part 5 (Chase, 2026-11-01: "All new characters go to tutorial island. if they want to skip they could run
// straight for moon gate. They get a snarky achievement/loot box with pet choice scroll (dog/cat) and can get to haven."):
// a new character (TutorialStart.IsIsleStarter) that walks into the dock's moongate on its first visit, before its lessons
// are done (or visiting, not in them), crosses to New Haven the normal way and gets, once per character:
//   - "Didn't Read the Sign" (tutorial.gateSkip, ClusterFAchievements), once per account as every achievement is;
//   - a loot box (ClusterFLootBoxes, source "tutorial:gateSkip", once per character) holding a pet choice scroll: Dog or
//     Cat, a stock town dog or cat that follows, guards and takes orders like a tamed pet. Not a combat pet, no taming
//     skill gained, and it uses a follower slot.
// A trainee in the lessons is skipped as the skip does it (ClusterFTutorial.SkipByGate); a visitor's account record is
// left alone. The skip is final: IsleExit "skipped", and Rook's remedial training refuses that character. A character
// that finishes the lessons leaves with its normal rewards and never gets this one. A remedial trainee (not a new
// character) is still told to finish its lessons first. Notes: shard-migration notes/cc-P762-new-character-lockdown.md.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial;

public static class TutorialGateSkip
{
    public const string BoxSource = "tutorial:gateSkip";
    public const string BoxLabel = "running for the moongate";

    public const string SnarkLine =
        "You ran straight past every lesson and through the moongate. The sign said \"Lessons this way.\" Bold choice.";

    /// <summary>
    /// The moongate counts this crossing as the skip: a new character on its first visit, either ahead of its lessons
    /// (<paramref name="earlyInLessons"/>) or visiting, not in them. Staff never.
    /// </summary>
    public static bool Eligible(PlayerMobile pm, bool earlyInLessons)
    {
        if (pm.AccessLevel > AccessLevel.Player || pm.Account?.AccessLevel > AccessLevel.Player || !TutorialStart.StillOnFirstVisit(pm))
        {
            return false;
        }

        return earlyInLessons || !ClusterFTutorial.IsParticipant(pm);
    }

    /// <summary>After the crossing: the skip recorded, the achievement and the box with the scroll.</summary>
    public static void Grant(PlayerMobile pm)
    {
        TutorialStart.MarkIsleExit(pm, TutorialStart.ExitSkipped);
        if (ClusterFTutorial.IsParticipant(pm))
        {
            ClusterFTutorial.SkipByGate(pm);
        }
        else
        {
            ClusterFTutorial.SystemLine(pm, "You left the Wakening Isle. There is no going back for this character.");
        }

        ClusterFTutorial.SystemLine(pm, SnarkLine);
        if (pm.Account is IAccount acct)
        {
            ClusterFAchievementSystem.TryGrant(acct, ClusterFTutorial.GateSkipAchievement);
        }

        Rewards(pm);
    }

    /// <summary>The box with the pet choice scroll. Replaceable for facts that do not roll boxes.</summary>
    public static Action<PlayerMobile> Rewards { get; set; } = pm =>
    {
        if (ClusterFLootBoxes.Award(pm, BoxSource, BoxTheme.Explorer, 1, null, BoxLabel, out var box) != ClusterFLootBoxes.AwardResult.Box ||
            box == null)
        {
            return;
        }

        var scroll = new PetChoiceScroll();
        scroll.Internalize();
        box.Contents.Add(scroll);
        box.MarkDirty();
        ClusterFTutorial.SystemLine(pm, "Your box holds a pet choice scroll: a dog or a cat. Double-click the box here in town to open it; boxes open only in guarded towns or your own house.");
    };

    /// <summary>The pet the scroll gives: a stock dog or cat, tamed to the owner, following them.</summary>
    public static BaseCreature? GivePet(PlayerMobile pm, bool dog)
    {
        BaseCreature pet = dog ? new Dog() : new Cat();
        if (!pet.SetControlMaster(pm))
        {
            pet.Delete();
            return null;
        }

        pet.IsBonded = false;
        pet.ControlTarget = pm;
        pet.ControlOrder = OrderType.Follow;
        pet.MoveToWorld(pm.Location, pm.Map);
        return pet;
    }
}

/// <summary>cc-P762: the moongate skipper's scroll. Double-click it in your backpack: Dog or Cat.</summary>
[SerializationGenerator(0, false)]
public partial class PetChoiceScroll : Item
{
    [Constructible]
    public PetChoiceScroll() : base(0x0E34)
    {
        Name = "a pet choice scroll";
        Hue = TutorialStyle.SupplyHue;
        Weight = 1.0;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Double-click: choose a dog or a cat");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        pm.CloseGump<PetChoiceGump>();
        pm.SendGump(new PetChoiceGump(this));
    }

    /// <summary>The choice: the pet and a pet leash (cc-P764) given and the scroll used up, or the scroll kept when there is no follower slot.</summary>
    public BaseCreature? Choose(PlayerMobile pm, bool dog)
    {
        if (Deleted || !IsChildOf(pm.Backpack))
        {
            return null;
        }

        var pet = TutorialGateSkip.GivePet(pm, dog);
        if (pet == null)
        {
            pm.SendMessage(0x3B2, "You have no room for another pet. Free a follower slot, then use the scroll again.");
            return null;
        }

        pm.SendMessage(0x3B2, $"Your new {(dog ? "dog" : "cat")} follows you. Say \"all follow\", \"all guard\" or \"all stay\", or click it for its orders.");
        // cc-P764: a pet leash with it, to carry the pet as a figurine.
        pm.AddToBackpack(new PetLeash());
        pm.SendMessage(0x3B2, "A pet leash comes with it. Double-click the leash and target your pet to carry it as a figurine.");
        Delete();
        return pet;
    }
}

/// <summary>
/// cc-P762: two buttons, Dog and Cat. cc-P813: drawn on Chase's Claude Design scroll (DesignGumpArt): the text-free
/// parchment, a Dog tile and a Cat tile with their icons as the buttons, and every word drawn here.
/// </summary>
public class PetChoiceGump : Gump
{
    public const string Heading = "A companion for the road";
    public const string Lead = "Read the scroll aloud and one friend answers. Choose carefully: the scroll is used up either way.";
    public const string Footer = "Your pet follows, guards and comes when called. It will not fight dragons for you.";

    private readonly PetChoiceScroll _scroll;

    public PetChoiceGump(PetChoiceScroll scroll) : base(160, 120)
    {
        _scroll = scroll;
        AddPage(0);
        AddImage(0, 0, DesignGumpArt.PetScrollBackground);
        AddHtml(20, 20, 300, 26, $"<BIG>{DesignGumpArt.Color(DesignGumpArt.Gold, Heading)}</BIG>", false, false);
        AddHtml(20, 62, 300, 72, DesignGumpArt.Color(DesignGumpArt.Ink, Lead), false, false);
        AddButton(21, 143, DesignGumpArt.PetDogTile, DesignGumpArt.PetDogTilePressed, 1);
        AddLabel(DesignGumpArt.CenterX(21, 143, "Dog"), 199, DesignGumpArt.HueDesignBrown, "Dog");
        AddButton(176, 143, DesignGumpArt.PetCatTile, DesignGumpArt.PetCatTilePressed, 2);
        AddLabel(DesignGumpArt.CenterX(176, 143, "Cat"), 199, DesignGumpArt.HueDesignBrown, "Cat");
        AddHtml(20, 244, 300, 34, DesignGumpArt.Color(DesignGumpArt.Muted, Footer), false, false);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (sender.Mobile is PlayerMobile pm && info.ButtonID is 1 or 2)
        {
            _scroll.Choose(pm, info.ButtonID == 1);
        }
    }
}

// ServUO: Skills/DetectHidden.cs:16-23 (cc-P195): an item Detect Hidden can find. ServUO's skill handler reveals
// one when the targeted spot is in range (CheckReveal) and tells a passing player something is hidden nearby
// (CheckPassiveDetect, from PlayerMobile's movement). Here the active half is the one hunk of
// server/patches/DetectHidden-revealable-items.patch; the passive half is each item's own business (the Exodus chest
// does it from the small region it keeps around itself), since pinned's PlayerMobile has no such walk.

namespace Server.Items;

public interface IRevealableItem
{
    /// <summary>The item reveals itself to a Detect Hidden use that reaches it.</summary>
    bool CheckReveal(Mobile m);

    /// <summary>A player walking nearby senses it (the item decides range and odds).</summary>
    bool CheckPassiveDetect(Mobile m);

    void OnRevealed(Mobile m);

    /// <summary>True: only while hidden (Visible false) does the skill consider it.</summary>
    bool CheckWhenHidden { get; }
}

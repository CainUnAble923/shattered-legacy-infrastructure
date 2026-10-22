// cc-P289: action evidence for one active trial. No client acknowledgement completes a trial.
namespace Server.Tutorial;

public enum TutorialAction
{
    FirstMark, SecondMark, Equipped, OpenedBag, SaidPhrase, EchoDeath,
    HealerOffer, Resurrected, Deposited, UsedGate, TalkedToLeague, VisionStarted, VisionFinished
}

public sealed class TutorialTrialProgress(string key)
{
    public string Key { get; } = key;
    public int Step { get; private set; }
    public bool Done { get; private set; }
    public void Reset() { Step = 0; Done = false; }
    // Returns true only on the transition to done. Ordered steps cannot be replayed out of order.
    public bool Observe(TutorialAction action)
    {
        if (Done) { return false; }
        TutorialAction[] expected = Key switch
        {
            "core.movement" => [TutorialAction.FirstMark, TutorialAction.SecondMark],
            "core.paperdoll" => [TutorialAction.Equipped],
            "core.backpack" => [TutorialAction.OpenedBag],
            "core.chat" => [TutorialAction.SaidPhrase],
            "core.death" => [TutorialAction.EchoDeath, TutorialAction.HealerOffer, TutorialAction.Resurrected],
            "core.banking" => [TutorialAction.Deposited],
            "core.travel" => [TutorialAction.UsedGate],
            "core.league" => [TutorialAction.TalkedToLeague],
            "core.loop" => [TutorialAction.VisionStarted, TutorialAction.VisionFinished],
            _ => []
        };
        if (Step >= expected.Length || action != expected[Step]) { return false; }
        Done = ++Step == expected.Length;
        return Done;
    }
}


using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialTrialProgressVerification
{
    [Theory]
    [InlineData("core.movement", TutorialAction.FirstMark, TutorialAction.SecondMark)]
    [InlineData("core.paperdoll", TutorialAction.Equipped, TutorialAction.Equipped)]
    [InlineData("core.backpack", TutorialAction.OpenedBag, TutorialAction.OpenedBag)]
    [InlineData("core.chat", TutorialAction.SaidPhrase, TutorialAction.SaidPhrase)]
    [InlineData("core.death", TutorialAction.EchoDeath, TutorialAction.Resurrected)]
    [InlineData("core.banking", TutorialAction.Deposited, TutorialAction.Deposited)]
    [InlineData("core.travel", TutorialAction.UsedGate, TutorialAction.UsedGate)]
    [InlineData("core.league", TutorialAction.TalkedToLeague, TutorialAction.TalkedToLeague)]
    [InlineData("core.loop", TutorialAction.VisionStarted, TutorialAction.VisionFinished)]
    public void EachCoreRequiresItsOwnActionAndCanBeReset(string key, TutorialAction first, TutorialAction last)
    {
        var trial = new TutorialTrialProgress(key);
        Assert.False(trial.Done);
        // Unrelated actions, including a bare acknowledgement, cannot finish another trial.
        Assert.False(trial.Observe(key == "core.chat" ? TutorialAction.Deposited : TutorialAction.SaidPhrase));
        trial.Observe(first);
        if (key == "core.death")
        {
            Assert.False(trial.Observe(last)); // being alive again without visiting the healer is insufficient
            trial.Observe(TutorialAction.HealerOffer);
        }
        trial.Observe(last);
        Assert.True(trial.Done);
        Assert.False(trial.Observe(last)); // one completion only
        trial.Reset();
        Assert.False(trial.Done);
        Assert.Equal(0, trial.Step);
    }

    [Theory]
    [InlineData("core.movement", TutorialAction.SecondMark)]
    [InlineData("core.death", TutorialAction.Resurrected)]
    [InlineData("core.loop", TutorialAction.VisionFinished)]
    public void LaterEvidenceCannotSkipTheFirstAction(string key, TutorialAction action)
    {
        var trial = new TutorialTrialProgress(key);
        Assert.False(trial.Observe(action));
        Assert.False(trial.Done);
        Assert.Equal(0, trial.Step);
        // Also fails after reset: stale late evidence is not a new trial.
        trial.Reset();
        Assert.False(trial.Observe(action));
    }
}

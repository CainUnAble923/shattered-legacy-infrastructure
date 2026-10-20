// testCenter.skillGainMultiplier: RETIRED by cc-P190. It scaled every skill's GainFactor for everyone at startup on a
// Test Center shard, so the test shard never ran at the real shard's rates and nothing that changes gain (P189's Old
// Haven apprentice boost, P191's rate) could be play-tested there. The Test Crystal (TestCrystal.cs) replaces it: the
// same multiplier at the same spot (the gain chance, SkillCheck.cs:133), for one character, Off until its owner sets it.
//
// What is left: a startup line naming the key when a shard still sets it to anything but 1.0, so nobody believes it is
// still in force. GainFactor is never changed. The key is read with GetSetting, not GetOrUpdateSetting, so a server that
// never sets it does not have it written into its modernuo.json: the live config stays byte-identical, as before.

using Server.Logging;

namespace Server.Misc;

public static class TestCenterSkillGain
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(TestCenterSkillGain));

    public const string SettingKey = "testCenter.skillGainMultiplier";
    public const double DefaultMultiplier = 1.0;

    public static void Initialize() => Notice(ServerConfiguration.GetSetting(SettingKey, DefaultMultiplier));

    // Returns true when the line was logged.
    public static bool Notice(double multiplier)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (multiplier == DefaultMultiplier)
        {
            return false;
        }

        logger.Warning(
            "{Key} is {Multiplier}, but it is retired (cc-P190) and changes nothing. Each tester sets their own skill gain " +
            "with a Test Crystal ([TCFill puts one in the bank). Remove the key from modernuo.json.",
            SettingKey,
            multiplier
        );
        return true;
    }
}

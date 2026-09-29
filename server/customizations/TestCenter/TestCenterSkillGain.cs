// testCenter.skillGainMultiplier: scales every skill's GainFactor once at startup, on a Test Center shard only.
//
// GainFactor has exactly one reader in pinned: SkillCheck.cs:119, where it multiplies the chance of a gain (floored at
// 0.01 just after). So this raises the odds of each gain roll and nothing else. ClusterFSkillGain's extra attempts
// and extra gains (ClusterFSkillGain.cs:96-138) sit on top of it unchanged; its bonus gains call SkillCheck.Gain
// directly and never read GainFactor.
//
// Read with GetSetting, not GetOrUpdateSetting, so a server that never sets the key does not have it written into its
// modernuo.json: the live config stays byte-identical.
//
// Runs in Initialize: SkillInfo.Table is loaded in SkillsInfo.Configure (SkillsInfo.cs:135) and TestCenter.Enabled in
// TestCenter.Configure, both at the default priority, so neither is reliably set during another Configure.

using System;
using System.Collections.Generic;
using Server.Logging;

namespace Server.Misc;

public static class TestCenterSkillGain
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(TestCenterSkillGain));

    public const string SettingKey = "testCenter.skillGainMultiplier";
    public const double DefaultMultiplier = 1.0;

    // Each SkillInfo's factor as it was before the first Apply. Applying always starts from these, so a second call
    // sets the same values instead of compounding.
    private static readonly Dictionary<SkillInfo, double> _original = new();

    public static void Initialize() =>
        Apply(TestCenter.Enabled, ServerConfiguration.GetSetting(SettingKey, DefaultMultiplier));

    // Returns true when the table was changed.
    public static bool Apply(bool testCenterEnabled, double multiplier)
    {
        // Exactly 1.0 is the default and means nothing to do; any other value is a request.
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (multiplier == DefaultMultiplier)
        {
            return false;
        }

        if (!testCenterEnabled)
        {
            logger.Warning(
                "{Key} is {Multiplier} but testCenter.enable is false; the multiplier only applies on a Test Center shard. Nothing changed.",
                SettingKey,
                multiplier
            );
            return false;
        }

        if (!double.IsFinite(multiplier) || multiplier <= 0)
        {
            logger.Error("{Key} is {Multiplier}; it must be a positive number. Nothing changed.", SettingKey, multiplier);
            return false;
        }

        var table = SkillInfo.Table;

        for (var i = 0; i < table.Length; i++)
        {
            var info = table[i];

            if (!_original.TryGetValue(info, out var original))
            {
                _original[info] = original = info.GainFactor;
            }

            info.GainFactor = original * multiplier;
        }

        logger.Information(
            "Test Center skill gain multiplier {Multiplier} applied to {Count} skills",
            multiplier,
            table.Length
        );
        return true;
    }

    // Puts every factor Apply changed back. For tests; nothing in the shard calls it.
    public static void Restore()
    {
        foreach (var (info, original) in _original)
        {
            info.GainFactor = original;
        }

        _original.Clear();
    }
}

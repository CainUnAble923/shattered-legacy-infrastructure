using System;
using Server.Misc;

namespace Server;

public static class ClusterFSkillGain
{
    private const int DefaultChanceAttempts = 3;
    private const int DefaultAmountMultiplier = 5;
    private const int MinimumMultiplier = 1;
    private const int MaximumMultiplier = 20;

    private static bool _enabled;
    private static int _chanceAttempts;
    private static int _amountMultiplier;

    private static SkillCheckLocationHandler _baseLocationHandler;
    private static SkillCheckDirectLocationHandler _baseDirectLocationHandler;
    private static SkillCheckTargetHandler _baseTargetHandler;
    private static SkillCheckDirectTargetHandler _baseDirectTargetHandler;

    public static void Configure()
    {
        _enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.skillGain.enabled", true);
        _chanceAttempts = ClampMultiplier(
            ServerConfiguration.GetOrUpdateSetting("clusterf.skillGain.chanceAttempts", DefaultChanceAttempts)
        );
        _amountMultiplier = ClampMultiplier(
            ServerConfiguration.GetOrUpdateSetting("clusterf.skillGain.amountMultiplier", DefaultAmountMultiplier)
        );

        CommandSystem.Register("ClusterFSkillGain", AccessLevel.Administrator, ClusterFSkillGain_OnCommand);
    }

    [CallPriority(100)]
    public static void Initialize()
    {
        _baseLocationHandler = Mobile.SkillCheckLocationHandler;
        _baseDirectLocationHandler = Mobile.SkillCheckDirectLocationHandler;
        _baseTargetHandler = Mobile.SkillCheckTargetHandler;
        _baseDirectTargetHandler = Mobile.SkillCheckDirectTargetHandler;

        Mobile.SkillCheckLocationHandler = ClusterFSkillCheckLocation;
        Mobile.SkillCheckDirectLocationHandler = ClusterFSkillCheckDirectLocation;
        Mobile.SkillCheckTargetHandler = ClusterFSkillCheckTarget;
        Mobile.SkillCheckDirectTargetHandler = ClusterFSkillCheckDirectTarget;

        Console.WriteLine(
            $"ClusterF skill gain {(_enabled ? "enabled" : "disabled")}: {_chanceAttempts} chance attempt(s), {_amountMultiplier}x gain amount."
        );
    }

    [Usage("ClusterFSkillGain")]
    [Description("Reports the active ClusterF player skill gain acceleration settings.")]
    private static void ClusterFSkillGain_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage(
            $"ClusterF skill gain {(_enabled ? "enabled" : "disabled")}: {_chanceAttempts} chance attempt(s), {_amountMultiplier}x gain amount."
        );
    }

    private static bool ClusterFSkillCheckLocation(Mobile from, SkillName skillName, double minSkill, double maxSkill)
    {
        var skill = from.Skills[skillName];
        return skill != null && _baseLocationHandler != null &&
               RunBoostedCheck(from, skill, () => _baseLocationHandler(from, skillName, minSkill, maxSkill));
    }

    private static bool ClusterFSkillCheckDirectLocation(Mobile from, SkillName skillName, double chance)
    {
        var skill = from.Skills[skillName];
        return skill != null && _baseDirectLocationHandler != null &&
               RunBoostedCheck(from, skill, () => _baseDirectLocationHandler(from, skillName, chance));
    }

    private static bool ClusterFSkillCheckTarget(
        Mobile from,
        SkillName skillName,
        object target,
        double minSkill,
        double maxSkill
    )
    {
        var skill = from.Skills[skillName];
        return skill != null && _baseTargetHandler != null &&
               RunBoostedCheck(from, skill, () => _baseTargetHandler(from, skillName, target, minSkill, maxSkill));
    }

    private static bool ClusterFSkillCheckDirectTarget(Mobile from, SkillName skillName, object target, double chance)
    {
        var skill = from.Skills[skillName];
        return skill != null && _baseDirectTargetHandler != null &&
               RunBoostedCheck(from, skill, () => _baseDirectTargetHandler(from, skillName, target, chance));
    }

    private static bool RunBoostedCheck(Mobile from, Skill skill, Func<bool> attempt)
    {
        var result = ObserveAttempt(from, skill, attempt);

        if (!ShouldBoost(from, skill))
        {
            return result;
        }

        for (var attemptIndex = 1; attemptIndex < _chanceAttempts && ShouldBoost(from, skill); attemptIndex++)
        {
            ObserveAttempt(from, skill, attempt);
        }

        return result;
    }

    private static bool ObserveAttempt(Mobile from, Skill skill, Func<bool> attempt)
    {
        var before = skill.BaseFixedPoint;
        var result = attempt();

        if (ShouldBoost(from, skill) && skill.BaseFixedPoint > before)
        {
            ApplyAmountBonus(from, skill);
        }

        return result;
    }

    private static void ApplyAmountBonus(Mobile from, Skill skill)
    {
        for (var gainIndex = 1; gainIndex < _amountMultiplier && ShouldBoost(from, skill); gainIndex++)
        {
            var before = skill.BaseFixedPoint;
            SkillCheck.Gain(from, skill);

            if (skill.BaseFixedPoint <= before)
            {
                break;
            }
        }
    }

    private static bool ShouldBoost(Mobile from, Skill skill) =>
        _enabled &&
        from?.Player == true &&
        from.Alive &&
        from.Skills.Cap > 0 &&
        skill?.Lock == SkillLock.Up &&
        skill.Base < skill.Cap;

    private static int ClampMultiplier(int value) => Math.Clamp(value, MinimumMultiplier, MaximumMultiplier);
}

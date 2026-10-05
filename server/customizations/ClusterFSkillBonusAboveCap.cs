// cc-P53 Part C (Chase 2026-10-04, deviation D-113): item and temporary skill bonuses count above the skill's cap.
//
// Pinned clamps a SkillMod with ObeyCap at the skill's Cap and adds one without it on top (Skills.cs:317-327). Three
// constructions in pinned set ObeyCap: every item "Skill +X" property (AosSkillBonuses.AddTo, Misc/AOS.cs:1357-1358,
// which also carries talismans, armour set bonuses and the Jacob pickaxes) and Ninjitsu Animal Form's Stealth +20 and
// Stealing +10 (Spells/Ninjitsu/AnimalForm.cs:237, :245). Every other SkillMod in pinned and in customizations is
// already built without it. OSI clamps; this shard does not.
//
// One hook rather than three edits: server/patches/PlayerMobile-skill-bonus-above-cap.patch overrides
// PlayerMobile.AddSkillMod (pinned Mobile.AddSkillMod is virtual, Mobile.cs:3500) to call Admit first, so a source
// added later is covered too. Only the bonus layer moves: skill gain reads Base, never Value
// (SkillCheck.cs:248, :278-280), so Base still stops at Cap. Material and other requirements that read Base are
// unchanged. Players only; creatures keep pinned's behaviour.
//
// Off switch: clusterf.skillCaps.bonusAboveCap = False restores pinned's clamp for mods added from then on.

namespace Server;

public static class ClusterFSkillBonusAboveCap
{
    public static bool Enabled { get; set; } = true;

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.skillCaps.bonusAboveCap", true);
    }

    /// <summary>Called by PlayerMobile.AddSkillMod before the mod is attached.</summary>
    public static void Admit(SkillMod mod)
    {
        if (Enabled && mod != null)
        {
            mod.ObeyCap = false;
        }
    }
}

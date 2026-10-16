// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Core/BaseThieveConsumable.cs -> ours. The thief's balms and
// lotions: one effect at a time per player, 30 minutes by default, ended by a timer or by the next balm.
//
// Two changes from ServUO, argued in notes/cc-P127-port-treasure-maps-monster-stealing.md:
//   - ServUO's base never answers a double-click (only LifeShieldLotion overrides OnDoubleClick), so every other balm was
//     inert. OnDoubleClick here calls OnUse for a player holding the balm in the pack: a bug that made the items broken,
//     fixed (D-4 rule).
//   - Stone Skin's three resistance mods are kept on the effect record and removed by reference, instead of ServUO's scan
//     of the mobile's list for mods with matching offsets.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

public enum ThieveConsumableEffect
{
    None,
    BalmOfStrengthEffect,
    BalmOfWisdomEffect,
    BalmOfSwiftnessEffect,
    BalmOfProtectionEffect,
    StoneSkinLotionEffect,
    LifeShieldLotionEffect
}

public class ThieveConsumableInfo
{
    public ThieveConsumableEffect Effect;
    public Timer EffectTimer;
    public List<ResistanceMod> ResistanceMods;

    public ThieveConsumableInfo(Timer t, ThieveConsumableEffect e)
    {
        Effect = e;
        EffectTimer = t;
    }
}

[SerializationGenerator(0, false)]
public abstract partial class BaseThieveConsumable : Item
{
    private static readonly Dictionary<PlayerMobile, ThieveConsumableInfo> _effectTable = new();

    [SerializableField(0)]
    private ThieveConsumableEffect _effectType;

    [SerializableField(1)]
    private TimeSpan _effectDuration;

    public BaseThieveConsumable(int itemId) : base(itemId)
    {
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

        OnUse(pm);
    }

    protected virtual void OnUse(PlayerMobile by)
    {
    }

    protected virtual void ApplyEffect(PlayerMobile pm)
    {
        if (_effectDuration == TimeSpan.Zero)
        {
            _effectDuration = TimeSpan.FromMinutes(30);
        }

        if (_effectTable.ContainsKey(pm))
        {
            RemoveEffect(pm, _effectTable[pm].Effect);
        }

        var effect = _effectType;
        var timer = Timer.DelayCall(_effectDuration, () => RemoveEffect(pm, effect));

        _effectTable[pm] = new ThieveConsumableInfo(timer, effect);
        Consume();
    }

    /// <summary>Stone Skin records its mods here so RemoveEffect can take exactly them off again.</summary>
    protected static void RecordResistanceMods(PlayerMobile pm, List<ResistanceMod> mods)
    {
        if (_effectTable.TryGetValue(pm, out var info))
        {
            info.ResistanceMods = mods;
        }
    }

    protected static void RemoveEffect(PlayerMobile pm, ThieveConsumableEffect effectType)
    {
        if (!_effectTable.TryGetValue(pm, out var info))
        {
            return;
        }

        info.EffectTimer?.Stop();
        _effectTable.Remove(pm);

        pm.SendLocalizedMessage(1095134); // The effects of the balm or lotion have worn off.

        if (effectType is ThieveConsumableEffect.BalmOfStrengthEffect or ThieveConsumableEffect.BalmOfSwiftnessEffect
            or ThieveConsumableEffect.BalmOfWisdomEffect)
        {
            pm.RemoveStatMod("Balm");
        }
        else if (effectType == ThieveConsumableEffect.StoneSkinLotionEffect && info.ResistanceMods != null)
        {
            foreach (var mod in info.ResistanceMods)
            {
                pm.RemoveResistanceMod(mod);
            }
        }
    }

    public static bool CanUse(PlayerMobile pm, BaseThieveConsumable consum) =>
        CheckThieveConsumable(pm) == ThieveConsumableEffect.None;

    public static bool IsUnderThieveConsumableEffect(PlayerMobile pm, ThieveConsumableEffect eff) =>
        _effectTable.TryGetValue(pm, out var info) && info.Effect == eff;

    public static ThieveConsumableEffect CheckThieveConsumable(PlayerMobile pm) =>
        _effectTable.TryGetValue(pm, out var info) ? info.Effect : ThieveConsumableEffect.None;

    /// <summary>Tests: end a player's effect now, as the timer would.</summary>
    public static void EndEffect(PlayerMobile pm)
    {
        if (_effectTable.TryGetValue(pm, out var info))
        {
            RemoveEffect(pm, info.Effect);
        }
    }
}

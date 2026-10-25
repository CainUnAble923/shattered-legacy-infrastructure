// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Fish/FishPies.cs -> ours, High Seas fishing, second
// half. Sixteen pies baked from the rare fish steaks and meats (Cooking, CookingCraftRegistrations' Fish Pies group, from
// 55 or 61 Cooking, at an oven). Eating one (double-click in the pack) gives its effect for five minutes, one of each kind
// at a time ("You are already under a similar effect."), shown under the fish pie buff icon:
//   Autumn Dragonfish +10 Meditation, Spider Crab +10 Focus (skill mods that obey the cap);
//   Fire Fish, Crystal Fish, Blue Lobster, Reaper Fish, Stone Crab: each hit of fire, energy, cold, poison or physical
//     damage lands up to 5 points lighter (the damage times that type's share, capped at 5; never player against
//     player on an Endless Journey shard);
//   Bull Fish +5 damage increase, Great Barracuda +8 hit chance, Giant Koi +8 defense chance, Summer Dragonfish +5 spell
//     damage; Yellowtail Barracuda +3 hit point, Unicorn Fish +3 stamina, Holy Mackerel +3 mana regeneration (players);
//   Fairy Salmon +2 casting focus, Lava Fish a stronger soul charge.
// The soaks are read by AOS-fish-pies.patch in AOS.Damage, the attribute bonuses at the end of AosAttributes.GetValue,
// both calling into this file, as ServUO's AOS.cs:188 and :663-:883 do.
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - The Fairy Salmon pie's casting focus and the Lava Fish pie's soul charge have nothing to act on: the shard has no
//   casting focus check in spellcasting and no soul charge property (D-21), the same as the artifacts that carry them.
//   They are eaten, shown and timed like the others.
// - A pie is exceptional or not (a flag); pinned has no ItemQuality enum. ServUO's pies show "exceptional" and nothing
//   else reads the quality.
// - The effect list lives in memory, as in ServUO: a restart ends every pie's effect.
// - Eating takes one pie off the stack. ServUO's pies stack, and eating deleted the whole stack (a bug with a broken
//   result is fixed, docs/bug-list.md D-4 rule).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.BuffIcons;
using Server.Engines.Craft;
using Server.Mobiles;

namespace Server.Items;

public enum FishPieEffect
{
    None,
    MedBoost,
    FocusBoost,
    ColdSoak,
    EnergySoak,
    FireSoak,
    PoisonSoak,
    PhysicalSoak,
    WeaponDam,
    HitChance,
    DefChance,
    SpellDamage,
    ManaRegen,
    HitsRegen,
    StamRegen,
    SoulCharge,
    CastFocus
}

[SerializationGenerator(0, false)]
public abstract partial class BaseFishPie : Item, ICraftable
{
    private static readonly Dictionary<Mobile, List<FishPieEffect>> _effectsList = new();

    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(0)]
    private bool _exceptional;

    public BaseFishPie() : base(4161) => Stackable = true;

    public virtual TimeSpan Duration => TimeSpan.FromMinutes(5);
    public virtual int BuffName => 0;
    public virtual int BuffAmount => 0;
    public virtual FishPieEffect Effect => FishPieEffect.None;

    public int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        Exceptional = quality == 2;
        return quality;
    }

    public static bool IsUnderEffects(Mobile from, FishPieEffect type) =>
        from != null && _effectsList.TryGetValue(from, out var list) && list.Contains(type);

    public static bool TryAddBuff(Mobile from, FishPieEffect type)
    {
        if (IsUnderEffects(from, type))
        {
            return false;
        }

        if (!_effectsList.TryGetValue(from, out var list))
        {
            _effectsList[from] = list = new List<FishPieEffect>();
        }

        list.Add(type);
        return true;
    }

    public static void RemoveBuff(Mobile from, FishPieEffect type)
    {
        if (!_effectsList.TryGetValue(from, out var list))
        {
            return;
        }

        list.Remove(type);

        if (list.Count == 0)
        {
            _effectsList.Remove(from);
        }

        // ServUO takes the icon down when any one pie wears off, others still running; kept.
        (from as PlayerMobile)?.RemoveBuff(BuffIcon.FishPie);
        from.Delta(MobileDelta.WeaponDamage);
    }

    /// <summary>
    /// ServUO BaseFishPie.ScaleDamage (AOS.Damage, after resistances): each soak takes the damage times its type's share,
    /// at most 5, off the hit. Never between two players on an Endless Journey shard.
    /// </summary>
    public static void ScaleDamage(Mobile from, Mobile to, ref int totalDamage, int phys, int fire, int cold, int pois, int nrgy)
    {
        if (to == null || !_effectsList.ContainsKey(to) || Core.EJ && from is PlayerMobile && to is PlayerMobile)
        {
            return;
        }

        if (phys > 0 && IsUnderEffects(to, FishPieEffect.PhysicalSoak))
        {
            totalDamage -= (int)Math.Min(5.0, totalDamage * (phys / 100.0));
        }

        if (fire > 0 && IsUnderEffects(to, FishPieEffect.FireSoak))
        {
            totalDamage -= (int)Math.Min(5.0, totalDamage * (fire / 100.0));
        }

        if (cold > 0 && IsUnderEffects(to, FishPieEffect.ColdSoak))
        {
            totalDamage -= (int)Math.Min(5.0, totalDamage * (cold / 100.0));
        }

        if (pois > 0 && IsUnderEffects(to, FishPieEffect.PoisonSoak))
        {
            totalDamage -= (int)Math.Min(5.0, totalDamage * (pois / 100.0));
        }

        if (nrgy > 0 && IsUnderEffects(to, FishPieEffect.EnergySoak))
        {
            totalDamage -= (int)Math.Min(5.0, totalDamage * (nrgy / 100.0));
        }
    }

    /// <summary>
    /// What a pie adds to an AOS attribute, ServUO AOS.cs:663 (damage increase +5), :695 (spell damage +5), :810 (hit
    /// chance +8), :837 (defense chance +8), :849, :865, :883 (hit point, stamina and mana regeneration +3, players only).
    /// </summary>
    public static int GetAttributeBonus(Mobile m, AosAttribute attribute)
    {
        if (m == null || !_effectsList.ContainsKey(m))
        {
            return 0;
        }

        return attribute switch
        {
            AosAttribute.WeaponDamage when IsUnderEffects(m, FishPieEffect.WeaponDam)  => 5,
            AosAttribute.SpellDamage when IsUnderEffects(m, FishPieEffect.SpellDamage) => 5,
            AosAttribute.AttackChance when IsUnderEffects(m, FishPieEffect.HitChance)  => 8,
            AosAttribute.DefendChance when IsUnderEffects(m, FishPieEffect.DefChance)  => 8,
            AosAttribute.RegenHits when m is PlayerMobile && IsUnderEffects(m, FishPieEffect.HitsRegen) => 3,
            AosAttribute.RegenStam when m is PlayerMobile && IsUnderEffects(m, FishPieEffect.StamRegen) => 3,
            AosAttribute.RegenMana when m is PlayerMobile && IsUnderEffects(m, FishPieEffect.ManaRegen) => 3,
            _ => 0
        };
    }

    public virtual bool Apply(Mobile from)
    {
        if (!TryAddBuff(from, Effect))
        {
            from.SendLocalizedMessage(502173); // You are already under a similar effect.
            return false;
        }

        switch (Effect)
        {
            case FishPieEffect.MedBoost:
                {
                    from.AddSkillMod(
                        new TimedSkillMod(SkillName.Meditation, "FishPieMeditation", true, 10.0, Duration) { ObeyCap = true }
                    );
                    break;
                }
            case FishPieEffect.FocusBoost:
                {
                    from.AddSkillMod(
                        new TimedSkillMod(SkillName.Focus, "FishPieFocus", true, 10.0, Duration) { ObeyCap = true }
                    );
                    break;
                }
        }

        if (Effect != FishPieEffect.None)
        {
            var effect = Effect;
            Timer.StartTimer(Duration, () => RemoveBuff(from, effect));

            (from as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.FishPie, 1116340, LabelNumber));
        }

        return true;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        // ServUO's AddCraftedProperties; pinned's Item has no such step, so it goes first here.
        if (_exceptional)
        {
            list.Add(1060636); // exceptional
        }

        list.Add(BuffName, BuffAmount);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
        }
        else if (Apply(from))
        {
            from.FixedEffect(0x375A, 10, 15);
            from.PlaySound(0x1E7);
            from.SendLocalizedMessage(1116285, $"{LabelNumber:#}"); // You eat the ~1_val~.  Mmm, tasty!
            Consume();
        }
    }
}

[SerializationGenerator(0, false)]
public partial class AutumnDragonfishPie : BaseFishPie
{
    [Constructible]
    public AutumnDragonfishPie() => Hue = FishInfo.GetFishHue(typeof(AutumnDragonfish));

    public override int LabelNumber => 1116224;
    public override int BuffName => 1116280;
    public override int BuffAmount => 10;
    public override FishPieEffect Effect => FishPieEffect.MedBoost;
}

[SerializationGenerator(0, false)]
public partial class BullFishPie : BaseFishPie
{
    [Constructible]
    public BullFishPie() => Hue = FishInfo.GetFishHue(typeof(BullFish));

    public override int LabelNumber => 1116220;
    public override int BuffName => 1116276;
    public override int BuffAmount => 5;
    public override FishPieEffect Effect => FishPieEffect.WeaponDam;
}

[SerializationGenerator(0, false)]
public partial class CrystalFishPie : BaseFishPie
{
    [Constructible]
    public CrystalFishPie() => Hue = FishInfo.GetFishHue(typeof(CrystalFish));

    public override int LabelNumber => 1116219;
    public override int BuffName => 1116275;
    public override int BuffAmount => 5;
    public override FishPieEffect Effect => FishPieEffect.EnergySoak;
}

[SerializationGenerator(0, false)]
public partial class FairySalmonPie : BaseFishPie
{
    [Constructible]
    public FairySalmonPie() => Hue = FishInfo.GetFishHue(typeof(FairySalmon));

    public override int LabelNumber => 1116222;
    public override int BuffName => 1116278;
    public override int BuffAmount => 2;
    public override FishPieEffect Effect => FishPieEffect.CastFocus;
}

[SerializationGenerator(0, false)]
public partial class FireFishPie : BaseFishPie
{
    [Constructible]
    public FireFishPie() => Hue = FishInfo.GetFishHue(typeof(FireFish));

    public override int LabelNumber => 1116217;
    public override int BuffName => 1116271;
    public override int BuffAmount => 5;
    public override FishPieEffect Effect => FishPieEffect.FireSoak;
}

[SerializationGenerator(0, false)]
public partial class GiantKoiPie : BaseFishPie
{
    [Constructible]
    public GiantKoiPie() => Hue = FishInfo.GetFishHue(typeof(GiantKoi));

    public override int LabelNumber => 1116216;
    public override int BuffName => 1116270;
    public override int BuffAmount => 5;
    public override FishPieEffect Effect => FishPieEffect.DefChance;
}

[SerializationGenerator(0, false)]
public partial class GreatBarracudaPie : BaseFishPie
{
    [Constructible]
    public GreatBarracudaPie() => Hue = 1287;

    public override int LabelNumber => 1116214;
    public override int BuffName => 1116269;
    public override int BuffAmount => 5;
    public override FishPieEffect Effect => FishPieEffect.HitChance;
}

[SerializationGenerator(0, false)]
public partial class HolyMackerelPie : BaseFishPie
{
    [Constructible]
    public HolyMackerelPie() => Hue = FishInfo.GetFishHue(typeof(HolyMackerel));

    public override int LabelNumber => 1116225;
    public override int BuffName => 1116283;
    public override int BuffAmount => 3;
    public override FishPieEffect Effect => FishPieEffect.ManaRegen;
}

[SerializationGenerator(0, false)]
public partial class LavaFishPie : BaseFishPie
{
    [Constructible]
    public LavaFishPie() => Hue = FishInfo.GetFishHue(typeof(LavaFish));

    public override int LabelNumber => 1116223;
    public override int BuffName => 1116279;
    public override int BuffAmount => 5;
    public override FishPieEffect Effect => FishPieEffect.SoulCharge;
}

[SerializationGenerator(0, false)]
public partial class ReaperFishPie : BaseFishPie
{
    [Constructible]
    public ReaperFishPie() => Hue = 1152;

    public override int LabelNumber => 1116218;
    public override int BuffName => 1116274;
    public override int BuffAmount => 5;
    public override FishPieEffect Effect => FishPieEffect.PoisonSoak;
}

[SerializationGenerator(0, false)]
public partial class SummerDragonfishPie : BaseFishPie
{
    [Constructible]
    public SummerDragonfishPie() => Hue = FishInfo.GetFishHue(typeof(SummerDragonfish));

    public override int LabelNumber => 1116221;
    public override int BuffName => 1116277;
    public override int BuffAmount => 5;
    public override FishPieEffect Effect => FishPieEffect.SpellDamage;
}

[SerializationGenerator(0, false)]
public partial class UnicornFishPie : BaseFishPie
{
    [Constructible]
    public UnicornFishPie() => Hue = FishInfo.GetFishHue(typeof(UnicornFish));

    public override int LabelNumber => 1116226;
    public override int BuffName => 1116284;
    public override int BuffAmount => 3;
    public override FishPieEffect Effect => FishPieEffect.StamRegen;
}

[SerializationGenerator(0, false)]
public partial class YellowtailBarracudaPie : BaseFishPie
{
    [Constructible]
    public YellowtailBarracudaPie() => Hue = FishInfo.GetFishHue(typeof(YellowtailBarracuda));

    public override int LabelNumber => 1116215;
    public override int BuffName => 1116282;
    public override int BuffAmount => 3;
    public override FishPieEffect Effect => FishPieEffect.HitsRegen;
}

[SerializationGenerator(0, false)]
public partial class StoneCrabPie : BaseFishPie
{
    [Constructible]
    public StoneCrabPie() => Hue = FishInfo.GetFishHue(typeof(StoneCrab));

    public override int LabelNumber => 1116227;
    public override int BuffName => 1116272;
    public override int BuffAmount => 3;
    public override FishPieEffect Effect => FishPieEffect.PhysicalSoak;
}

[SerializationGenerator(0, false)]
public partial class SpiderCrabPie : BaseFishPie
{
    [Constructible]
    public SpiderCrabPie() => Hue = FishInfo.GetFishHue(typeof(SpiderCrab));

    public override int LabelNumber => 1116229;
    public override int BuffName => 1116281;
    public override int BuffAmount => 10;
    public override FishPieEffect Effect => FishPieEffect.FocusBoost;
}

[SerializationGenerator(0, false)]
public partial class BlueLobsterPie : BaseFishPie
{
    [Constructible]
    public BlueLobsterPie() => Hue = FishInfo.GetFishHue(typeof(BlueLobster));

    public override int LabelNumber => 1116228;
    public override int BuffName => 1116273;
    public override int BuffAmount => 5;
    public override FishPieEffect Effect => FishPieEffect.ColdSoak;
}

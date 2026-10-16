// cc-P128: ServUO pub57 Scripts/Items/Consumables/EodonPotions.cs -> ours. The six Eodon tribal potions (Time of
// Legends, Alchemy 51 with a recipe each) and the Eodon resources they are brewed from, except PerfectBanana, which CC3
// ported (Items/Consumables/PerfectBanana.cs) and which is not repeated here.
//
// One context per potion kind per mobile, ticked once a second while any is running, dropped when it expires (Cooldown:
// 20 minutes, Kurak 10) or when the player dies. What each does, and where its consumer is:
//   Barrab   HP regeneration +100 for 10 minutes: ours Misc/RegenRates.cs (a replacement file) reads HPRegenBonus, as
//            ServUO's RegenRates.cs:236 does.
//   Jukari   fire resistance +10 for 10 minutes.
//   Kurak    a buff only the Myrmidex invasion battle reads (ServUO BattleRegion.cs:58); not ported, so the icon shows
//            and nothing else happens, as on a ServUO shard with the invasion off.
//   Barako   physical +10 and cold +5 for 10 minutes. ServUO adds its physical mod twice and never its cold mod, then
//            removes each once, which leaves +10 physical on the player for good: a bug that broke the potion, fixed
//            here (D-4 rule) by adding and removing the two mods it names.
//   Urali    +10 mana a second for 10 minutes; +10 max mana for 5 minutes and casting at Protection's speed penalty while
//            it lasts (server/patches/PlayerMobile-urali-mana.patch and Spell-urali-trance.patch, ServUO's own lines).
//   Sakkhra  poison +10 and energy +5 for 10 minutes.
// The unused GetHitBuff and GetStamBuff (ServUO defines them and calls neither) are kept for the same reason.
//
// The six PotionEffect values are server/patches/BasePotion-sa-potion-effects.patch.

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Engines.BuffIcons;
using Server.Mobiles;
using Server.Network;
using Server.Spells.Bushido;

namespace Server.Items;

public class EodonPotionContext
{
    public EodonPotionContext(EodonianPotion potion)
    {
        Potion = potion;
        Expires = Core.Now + potion.Cooldown;
        StartTime = Core.Now;
    }

    public PotionEffect Type => Potion.PotionEffect;
    public DateTime StartTime { get; set; }
    public DateTime Expires { get; set; }
    public EodonianPotion Potion { get; set; }

    public void OnTick(Mobile m)
    {
        if (Core.Now >= Expires)
        {
            Potion.EndEffects(m);
        }
        else
        {
            Potion.OnTick(m);
        }
    }
}

[SerializationGenerator(0, false)]
public abstract partial class EodonianPotion : BasePotion
{
    public static Dictionary<Mobile, List<EodonPotionContext>> Contexts { get; private set; }
    private static TimerExecutionToken _timerToken;

    public EodonianPotion(int id, PotionEffect effect) : base(id, effect)
    {
    }

    public virtual TimeSpan Cooldown => TimeSpan.FromMinutes(20);

    public override int LabelNumber => PotionEffect switch
    {
        PotionEffect.Jukari  => 1156726,
        PotionEffect.Kurak   => 1156728,
        PotionEffect.Barako  => 1156729,
        PotionEffect.Urali   => 1156734,
        PotionEffect.Sakkhra => 1156732,
        _                    => 1156724 // Barrab
    };

    public override void Drink(Mobile m)
    {
        if (!CanDoEffects(m))
        {
            return;
        }

        m.FixedEffect(0x375A, 10, 15);
        m.PlaySound(0x1E7);

        DoEffects(m);
        PlayDrinkEffect(m);

        Consume();
    }

    public virtual void OnTick(Mobile m)
    {
    }

    public virtual bool CanDoEffects(Mobile m)
    {
        if (IsUnderEffects(m, PotionEffect))
        {
            m.SendLocalizedMessage(502173); // You are already under a similar effect.
            return false;
        }

        return true;
    }

    public virtual void DoEffects(Mobile m)
    {
        Contexts ??= new Dictionary<Mobile, List<EodonPotionContext>>();

        if (!Contexts.TryGetValue(m, out var list) || list == null)
        {
            Contexts[m] = list = new List<EodonPotionContext>();
        }

        AddBuff(m);

        list.Add(new EodonPotionContext(this));
        BeginTimer();
    }

    public virtual void EndEffects(Mobile m)
    {
        RemoveContext(m, PotionEffect);
    }

    public virtual void AddBuff(Mobile m)
    {
        switch (PotionEffect)
        {
            case PotionEffect.Barrab:
                (m as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.BarrabHemolymphConcentrate, LabelNumber, 1156738, TimeSpan.Zero, "100\t10\t10\t5"));
                break;
            case PotionEffect.Jukari:
                (m as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.JukariBurnPoiltice, LabelNumber, 1156739, TimeSpan.Zero, "10\t10\t10\t5"));
                break;
            case PotionEffect.Kurak:
                (m as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.KurakAmbushersEssence, LabelNumber, 1156740, TimeSpan.Zero, "200"));
                break;
            case PotionEffect.Barako:
                (m as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.BarakoDraftOfMight, LabelNumber, 1156741, TimeSpan.Zero, "10\t10\t5\t10"));
                break;
            case PotionEffect.Urali:
                (m as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.UraliTranceTonic, LabelNumber, 1156742, TimeSpan.Zero, "10\t10\t10\t5"));
                break;
            case PotionEffect.Sakkhra:
                (m as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.SakkhraProphylaxis, LabelNumber, 1156743, TimeSpan.Zero, "10\t10\t5\t10"));
                break;
        }
    }

    public virtual void RemoveBuff(Mobile m)
    {
        switch (PotionEffect)
        {
            case PotionEffect.Barrab:
                (m as PlayerMobile)?.RemoveBuff(BuffIcon.BarrabHemolymphConcentrate);
                break;
            case PotionEffect.Jukari:
                (m as PlayerMobile)?.RemoveBuff(BuffIcon.JukariBurnPoiltice);
                break;
            case PotionEffect.Kurak:
                (m as PlayerMobile)?.RemoveBuff(BuffIcon.KurakAmbushersEssence);
                break;
            case PotionEffect.Barako:
                (m as PlayerMobile)?.RemoveBuff(BuffIcon.BarakoDraftOfMight);
                break;
            case PotionEffect.Urali:
                (m as PlayerMobile)?.RemoveBuff(BuffIcon.UraliTranceTonic);
                break;
            case PotionEffect.Sakkhra:
                (m as PlayerMobile)?.RemoveBuff(BuffIcon.SakkhraProphylaxis);
                break;
        }
    }

    public static bool IsUnderEffects(Mobile m, PotionEffect effect) => GetContext(m, effect) != null;

    public static void RemoveEffects(Mobile m, PotionEffect effect) => RemoveContext(m, GetContext(m, effect));

    public static EodonPotionContext GetContext(Mobile m, PotionEffect effect)
    {
        if (m == null || Contexts == null || !Contexts.TryGetValue(m, out var list) || list == null)
        {
            return null;
        }

        foreach (var c in list)
        {
            if (c.Type == effect)
            {
                return c;
            }
        }

        return null;
    }

    public static void RemoveContext(Mobile m, PotionEffect effect) => RemoveContext(m, GetContext(m, effect));

    public static void RemoveContext(Mobile m, EodonPotionContext context)
    {
        if (context == null || Contexts == null)
        {
            return;
        }

        context.Potion?.RemoveBuff(m);

        if (Contexts.TryGetValue(m, out var list))
        {
            list?.Remove(context);

            if (list == null || list.Count == 0)
            {
                Contexts.Remove(m);
            }

            if (Contexts.Count == 0)
            {
                EndTimer();
            }

            m.Delta(MobileDelta.WeaponDamage);
        }
    }

    public static void BeginTimer()
    {
        if (_timerToken.Running)
        {
            return;
        }

        Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), OnTick, out _timerToken);
    }

    public static void EndTimer()
    {
        _timerToken.Cancel();
    }

    public static void OnTick()
    {
        if (Contexts == null || Contexts.Count == 0)
        {
            EndTimer();
            return;
        }

        foreach (var kvp in new List<KeyValuePair<Mobile, List<EodonPotionContext>>>(Contexts))
        {
            foreach (var context in new List<EodonPotionContext>(kvp.Value))
            {
                context.OnTick(kvp.Key);
            }
        }
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile m)
    {
        if (Contexts == null || m == null)
        {
            return;
        }

        if (Contexts.TryGetValue(m, out var list))
        {
            foreach (var context in list)
            {
                context.Potion?.RemoveBuff(m);
            }

            Contexts.Remove(m);
        }

        if (Contexts.Count == 0)
        {
            EndTimer();
        }
    }
}

[SerializationGenerator(0, false)]
public partial class BarrabHemolymphConcentrate : EodonianPotion
{
    [Constructible]
    public BarrabHemolymphConcentrate(int amount = 1) : base(3846, PotionEffect.Barrab)
    {
        Hue = 1272;
        Stackable = true;
        Amount = amount;
    }

    public override bool CanDoEffects(Mobile m)
    {
        if (MortalStrike.IsWounded(m))
        {
            m.SendLocalizedMessage(1156869); // You may not use this with a mortal wound!
        }
        else if (m.Poison != null)
        {
            m.SendLocalizedMessage(1156868); // You may not use this while poisoned!
        }
        else if (Confidence.IsConfident(m))
        {
            m.SendLocalizedMessage(1156873); // You may not use this while under the effects of confidence!
        }

        return base.CanDoEffects(m);
    }

    public static int GetHitBuff(Mobile m)
    {
        var c = GetContext(m, PotionEffect.Barrab);

        return c != null && c.StartTime + TimeSpan.FromMinutes(5) > Core.Now ? 10 : 0;
    }

    public static int HPRegenBonus(Mobile m)
    {
        var c = GetContext(m, PotionEffect.Barrab);

        return c != null && c.StartTime + TimeSpan.FromMinutes(10) > Core.Now ? 100 : 0;
    }
}

[SerializationGenerator(0, false)]
public partial class JukariBurnPoiltice : EodonianPotion
{
    [Constructible]
    public JukariBurnPoiltice(int amount = 1) : base(3846, PotionEffect.Jukari)
    {
        Hue = 2727;
        Stackable = true;
        Amount = amount;
    }

    public override void DoEffects(Mobile m)
    {
        base.DoEffects(m);

        var mod = new ResistanceMod(ResistanceType.Fire, "JukariBurnPoiltice", 10);
        m.AddResistanceMod(mod);

        Timer.StartTimer(TimeSpan.FromMinutes(10), () => m.RemoveResistanceMod(mod));
    }

    public static int GetStamBuff(Mobile m)
    {
        var c = GetContext(m, PotionEffect.Jukari);

        return c != null && c.StartTime + TimeSpan.FromMinutes(5) > Core.Now ? 10 : 0;
    }
}

[SerializationGenerator(0, false)]
public partial class KurakAmbushersEssence : EodonianPotion
{
    [Constructible]
    public KurakAmbushersEssence(int amount = 1) : base(3846, PotionEffect.Kurak)
    {
        Hue = 1260;
        Stackable = true;
        Amount = amount;
    }

    public override TimeSpan Cooldown => TimeSpan.FromMinutes(10);
}

[SerializationGenerator(0, false)]
public partial class BarakoDraftOfMight : EodonianPotion
{
    [Constructible]
    public BarakoDraftOfMight(int amount = 1) : base(3846, PotionEffect.Barako)
    {
        Hue = 1072;
        Stackable = true;
        Amount = amount;
    }

    public override void DoEffects(Mobile m)
    {
        base.DoEffects(m);

        var mod1 = new ResistanceMod(ResistanceType.Physical, "BarakoDraftOfMightPhysical", 10);
        m.AddResistanceMod(mod1);

        Timer.StartTimer(TimeSpan.FromMinutes(10), () => m.RemoveResistanceMod(mod1));

        var mod2 = new ResistanceMod(ResistanceType.Cold, "BarakoDraftOfMightCold", 5);
        m.AddResistanceMod(mod2);

        Timer.StartTimer(TimeSpan.FromMinutes(10), () => m.RemoveResistanceMod(mod2));
    }
}

[SerializationGenerator(0, false)]
public partial class UraliTranceTonic : EodonianPotion
{
    [Constructible]
    public UraliTranceTonic(int amount = 1) : base(3846, PotionEffect.Urali)
    {
        Hue = 1098;
        Stackable = true;
        Amount = amount;
    }

    public override void OnTick(Mobile m)
    {
        var context = GetContext(m, PotionEffect);

        if (context != null && context.StartTime + TimeSpan.FromMinutes(10) > Core.Now)
        {
            m.Mana += 10;
        }
    }

    public static int GetManaBuff(Mobile m)
    {
        var c = GetContext(m, PotionEffect.Urali);

        return c != null && c.StartTime + TimeSpan.FromMinutes(5) > Core.Now ? 10 : 0;
    }
}

[SerializationGenerator(0, false)]
public partial class SakkhraProphylaxisPotion : EodonianPotion
{
    [Constructible]
    public SakkhraProphylaxisPotion(int amount = 1) : base(3846, PotionEffect.Sakkhra)
    {
        Hue = 2531;
        Stackable = true;
        Amount = amount;
    }

    public override void DoEffects(Mobile m)
    {
        base.DoEffects(m);

        var mod1 = new ResistanceMod(ResistanceType.Poison, "SakkhraProphylaxisPoison", 10);
        m.AddResistanceMod(mod1);

        Timer.StartTimer(TimeSpan.FromMinutes(10), () => m.RemoveResistanceMod(mod1));

        var mod2 = new ResistanceMod(ResistanceType.Energy, "SakkhraProphylaxisEnergy", 5);
        m.AddResistanceMod(mod2);

        Timer.StartTimer(TimeSpan.FromMinutes(10), () => m.RemoveResistanceMod(mod2));
    }
}

// resources

[SerializationGenerator(0, false)]
public partial class MyrmidexEggsac : Item, ICommodity
{
    [Constructible]
    public MyrmidexEggsac(int amount = 1) : base(10248)
    {
        Hue = 1272;
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1156725; // Myrmidex Eggsac

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}

[SerializationGenerator(0, false)]
public partial class LavaBerry : Item
{
    [Constructible]
    public LavaBerry(int amount = 1) : base(22326)
    {
        Hue = 1955;
        Stackable = true;
        Weight = 1.0;
        Amount = amount;
    }

    public override int LabelNumber => 1156727; // Lava Berry
}

[SerializationGenerator(0, false)]
public partial class LavaBerryBush : Item
{
    [Constructible]
    public LavaBerryBush() : base(Utility.RandomBool() ? 0xDC4 : 0xDC5)
    {
        Hue = 2075;
        Movable = false;
        Weight = 0.0;
    }

    public override int LabelNumber => 1156735; // Lava Berry Bush

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(Location, 2))
        {
            from.AddToBackpack(new LavaBerry());
            from.PrivateOverheadMessage(MessageType.Regular, 1154, 1156736, "#1156727", from.NetState); // You harvest ~1_NAME~.

            Delete();
        }
        else
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
        }
    }
}

[SerializationGenerator(0, false)]
public partial class RiverMossDecorate : Item
{
    [Constructible]
    public RiverMossDecorate() : base(3378)
    {
        Hue = 1272;
        Movable = false;
        Weight = 0.0;
    }

    public override int LabelNumber => 1156731; // River Moss

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(Location, 2))
        {
            from.AddToBackpack(new RiverMoss());
            from.PrivateOverheadMessage(MessageType.Regular, 1154, 1156736, "#1156731", from.NetState); // You harvest ~1_NAME~.

            Delete();
        }
        else
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
        }
    }
}

[SerializationGenerator(0, false)]
public partial class RiverMoss : Item, ICommodity
{
    [Constructible]
    public RiverMoss(int amount = 1) : base(22333)
    {
        Hue = 1272;
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1156731; // River Moss

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;
}

[SerializationGenerator(0, false)]
public partial class BlueCorn : EarOfCorn
{
    [Constructible]
    public BlueCorn(int amount = 1) : base(amount)
    {
        Hue = 1284;
    }

    public override int LabelNumber => 1156733; // Blue Corn
}

[SerializationGenerator(0, false)]
public partial class CornStalk : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _used;

    [Constructible]
    public CornStalk() : base(3197)
    {
        _used = Utility.RandomMinMax(1, 4);
        Movable = false;
    }

    public override int LabelNumber => 1035639; // corn stalk

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(Location, 3))
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            return;
        }

        Item corn;

        if (Utility.RandomDouble() < 0.70)
        {
            corn = new EarOfCorn();
            from.PrivateOverheadMessage(MessageType.Regular, 1154, 1156736, "#1156737", from.NetState); // You harvest ~1_NAME~.
        }
        else
        {
            corn = new BlueCorn();
            from.PrivateOverheadMessage(MessageType.Regular, 1154, 1156736, "#1156733", from.NetState); // You harvest ~1_NAME~.
        }

        from.AddToBackpack(corn);

        if (Used > 1)
        {
            Used--;
        }
        else
        {
            Delete();
        }
    }
}

[SerializationGenerator(0, false)]
public partial class MoonstoneCrystalShard : Item
{
    [Constructible]
    public MoonstoneCrystalShard(int amount = 1) : base(40118)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1124142; // Moonstone Crystal Shards
}

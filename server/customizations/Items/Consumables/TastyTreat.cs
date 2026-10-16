// cc-P128: ServUO pub57 Scripts/Items/Consumables/TastyTreat.cs -> ours. A tasty treat for a pet (SA): fed to your own
// pet by double-click or by dropping it on the pet, it raises the pet's Str, Int and Dex by Bonus (5%, 10% or 15%
// for the three kinds) for Duration, sets loyalty to the maximum, and refuses a second treat until Duration + CoolDown
// has passed. The irresistibly tasty treat also adds ServUO's TempDamageBonus: pinned's BaseCreature has no such field,
// so the value is kept here (DamageBonusOf) and server/patches/BaseCreature-tasty-treat-damage.patch applies ServUO's
// line in AlterMeleeDamageTo (damage += damage / bonus, ServUO BaseCreature.cs:2093-2097).
//
// ServUO's table (m_Table) is in memory and so is this one: a restart forgets a treat, as it does there.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class TastyTreat : Item
{
    private static readonly Dictionary<BaseCreature, DateTime> _table = new();
    private static readonly Dictionary<BaseCreature, int> _damageBonus = new();

    [Constructible]
    public TastyTreat(int amount = 1) : base(2424)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1112774; // Tasty Treat

    public virtual double Bonus => 0.05;
    public virtual TimeSpan Duration => TimeSpan.FromMinutes(20);
    public virtual TimeSpan CoolDown => TimeSpan.FromMinutes(2);
    public virtual int DamageBonus => 0;

    public override void AddNameProperties(IPropertyList list)
    {
        base.AddNameProperties(list);

        list.Add(1113213); // * For Pets Only *

        if (Bonus == 0.10)
        {
            list.Add(1113215); // Stats Increased by 10%
        }
        else if (Bonus == 0.15)
        {
            list.Add(1113216); // Stats Increased by 15%
        }
        else
        {
            list.Add(1113214); // Stats Increased by 5%
        }

        list.Add(1113212, $"{Duration.TotalMinutes}"); // Duration: ~1_val~ minutes
        list.Add(1113218, $"{CoolDown.TotalMinutes}"); // Cooldown: ~1_val~ minutes
    }

    public override bool DropToMobile(Mobile from, Mobile target, Point3D p)
    {
        TryFeed(from, target);
        return false;
    }

    public override void OnDoubleClick(Mobile m)
    {
        if (IsChildOf(m.Backpack))
        {
            m.Target = new InternalTarget(this);
        }
    }

    public void TryFeed(Mobile from, Mobile target)
    {
        if (target is not BaseCreature bc || bc.IsDeadBondedPet)
        {
            return;
        }

        if (UnderInfluence(bc))
        {
            if (_table[bc] + (CoolDown + Duration) < Core.Now)
            {
                from.SendLocalizedMessage(1113049); // Your pet is still recovering from the last tasty treat.
            }
            else
            {
                from.SendLocalizedMessage(1113051); // Your pet is still enjoying the last tasty treat!
            }
        }
        else if (bc.ControlMaster == from)
        {
            from.SendLocalizedMessage(1113050); // Your pet looks much happier.
            DoEffects(bc);
        }
    }

    public bool DoEffects(BaseCreature bc)
    {
        bc.AddStatMod(new StatMod(StatType.Str, $"{bc.Serial}Str", (int)(bc.RawStr * Bonus), Duration));
        bc.AddStatMod(new StatMod(StatType.Int, $"{bc.Serial}Int", (int)(bc.RawInt * Bonus), Duration));
        bc.AddStatMod(new StatMod(StatType.Dex, $"{bc.Serial}Dex", (int)(bc.RawDex * Bonus), Duration));

        bc.PlaySound(0x1EA);
        bc.FixedParticles(0x373A, 10, 15, 5018, EffectLayer.Waist);

        if (DamageBonus > 0)
        {
            _damageBonus[bc] = DamageBonus;
        }
        else
        {
            _damageBonus.Remove(bc);
        }

        bc.Loyalty = BaseCreature.MaxLoyalty;

        _table[bc] = Core.Now;
        Timer.DelayCall(Duration + CoolDown, () => RemoveInfluence(bc));

        Consume();
        return true;
    }

    public static bool UnderInfluence(BaseCreature bc) => _table.ContainsKey(bc);

    /// <summary>ServUO's BaseCreature.TempDamageBonus: the divisor of the melee damage bonus while the treat lasts, else 0.</summary>
    public static int DamageBonusOf(BaseCreature bc) =>
        _damageBonus.TryGetValue(bc, out var bonus) && _table.ContainsKey(bc) ? bonus : 0;

    public static void RemoveInfluence(BaseCreature bc)
    {
        _table.Remove(bc);
        _damageBonus.Remove(bc);
    }

    private class InternalTarget : Target
    {
        private readonly TastyTreat _treat;

        public InternalTarget(TastyTreat treat) : base(2, false, TargetFlags.Beneficial) => _treat = treat;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (!_treat.Deleted && _treat.IsChildOf(from.Backpack) && targeted is Mobile m)
            {
                _treat.TryFeed(from, m);
            }
        }
    }
}

// cc-P199: ServUO pub57 Scripts/Items/Equipment/Talismans/ManaPhasingOrb.cs. The mana phasing orb (1116230, art 4246,
// hue 1165): a talisman with 50 charges and a 30-second recharge. Double-clicked while worn and charged, it puts the
// wearer "in mana phase" (1116164, ManaPhase buff 1116158/1153816) and spends a charge; ServUO's spell, special move,
// weapon ability and craft code then take the next mana cost from the orb instead of the wearer and end the phase, and
// any damage taken ends it too. Taking the orb off ends it (1116165). Rolled properties as ServUO: lower mana cost 6;
// one of regen hits/mana/stam 1; defend chance 5 or hit chance 5; one of lower reagent cost 10, damage increase 15 or
// spell damage 5.
//
// NOT YET FUNCTIONAL IN PLAY: the phase is kept (IsInManaPhase / RemoveFromTable, public static as in ServUO), but
// nothing in pinned reads it. The readers are five ServUO engine lines with no counterpart here; each needs a patch to
// the matching pinned file (see the cc-P199 report): Spells/Base/Spell.cs:951 (spell mana), Spells/Base/SpecialMove.cs:133
// (special move mana), Abilities/WeaponAbility.cs:255-258 (weapon ability mana), Services/Craft/Core/CraftItem.cs:265-270
// (craft mana) and Misc/AOS.cs:408-409 (damage ends the phase). Until then the orb spends a charge and shows the buff
// and changes nothing else.
//
// Other differences from ServUO, all because pinned's BaseTalisman (Items/Talismans/BaseTalisman.cs) lacks the member:
//   - Attributes.Brittle = 1 (ManaPhasingOrb.cs:21) dropped: pinned's AosAttribute has no Brittle.
//   - InitMinHits/InitMaxHits 255 and IRepairable with DefTinkering (:7,10-12) dropped: pinned talismans have no hit
//     points, so there is nothing to repair. Pinned has no IRepairable either.
//   - The "Mana Phase" property line (1116158) comes from ServUO's BaseTalisman for this type (ServUO
//     BaseTalisman.cs:789-790, after artifact rarity); here it is added after pinned's own lines.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.BuffIcons;
using Server.Mobiles;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class ManaPhasingOrb : BaseTalisman
    {
        [Constructible]
        public ManaPhasingOrb() : base(4246)
        {
            MaxChargeTime = 30;
            Charges = 50;
            MaxCharges = Charges;
            Hue = 1165;
            Attributes.LowerManaCost = 6;

            switch (Utility.Random(3))
            {
                case 0: Attributes.RegenHits = 1; break;
                case 1: Attributes.RegenMana = 1; break;
                case 2: Attributes.RegenStam = 1; break;
            }

            if (Utility.RandomBool())
            {
                Attributes.DefendChance = 5;
            }
            else
            {
                Attributes.AttackChance = 5;
            }

            switch (Utility.Random(3))
            {
                case 0: Attributes.LowerRegCost = 10; break;
                case 1: Attributes.WeaponDamage = 15; break;
                case 2: Attributes.SpellDamage = 5; break;
            }
        }

        public override int LabelNumber => 1116230; // mana phasing orb

        public static List<Mobile> _ManaPhaseTable { get; set; }

        public static bool IsInManaPhase(Mobile from) => _ManaPhaseTable != null && _ManaPhaseTable.Contains(from);

        public override void OnRemoved(IEntity parent)
        {
            if (parent is Mobile m && IsInManaPhase(m))
            {
                RemoveFromTable(m);
            }

            base.OnRemoved(parent);
        }

        public override void OnDoubleClick(Mobile from)
        {
            // As ServUO (:59-80): not worn says 502641 here and falls through to the base, which says it again.
            if (from.Talisman != this)
            {
                from.SendLocalizedMessage(502641); // You must equip this item to use it.
            }
            else
            {
                if (ChargeTime > 0)
                {
                    from.SendLocalizedMessage(1116163); // You must wait a few seconds before attempting to phase mana again.
                    return;
                }

                if (Charges > 0 && !IsInManaPhase(from))
                {
                    AddToTable(from);
                    OnAfterUse(from);

                    (from as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.ManaPhase, 1116158, 1153816));

                    return;
                }
            }

            base.OnDoubleClick(from);
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add(1116158); // Mana Phase
        }

        private static void AddToTable(Mobile from)
        {
            _ManaPhaseTable ??= new List<Mobile>();
            _ManaPhaseTable.Add(from);

            from.SendLocalizedMessage(1116164); // Your next use of magical energy will draw its power from the void.
        }

        public static void RemoveFromTable(Mobile from)
        {
            if (_ManaPhaseTable != null && _ManaPhaseTable.Contains(from))
            {
                _ManaPhaseTable.Remove(from);
            }

            (from as PlayerMobile)?.RemoveBuff(BuffIcon.ManaPhase);

            from.SendLocalizedMessage(1116165); // You will no longer attempt to draw magical energy from the void.
        }
    }
}

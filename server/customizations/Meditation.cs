using System;
using Server.Engines.BuffIcons;
using Server.Items;
using Server.Misc;
using Server.Mobiles;

namespace Server.SkillHandlers
{
    internal static class Meditation
    {
        public static void Initialize()
        {
            SkillInfo.Table[46].Callback = OnUse;
        }

        public static bool CheckOkayHolding(Item item) =>
            item is null or Spellbook or Runebook
            || Core.AOS && item is BaseWeapon weapon && weapon.Attributes.SpellChanneling != 0
            || Core.AOS && item is BaseArmor armor && armor.Attributes.SpellChanneling != 0;

        public static TimeSpan OnUse(Mobile m)
        {
            m.RevealingAction();

            if (m.Target != null)
            {
                m.SendLocalizedMessage(501845); // You are busy doing something else and cannot focus.

                return TimeSpan.FromSeconds(5.0);
            }

            if (!Core.AOS && m.Hits < m.HitsMax / 10) // Less than 10% health
            {
                m.SendLocalizedMessage(501849); // The mind is strong but the body is weak.

                return TimeSpan.FromSeconds(5.0);
            }

            if (m.Mana >= m.ManaMax)
            {
                m.SendLocalizedMessage(501846); // You are at peace.

                return TimeSpan.FromSeconds(Core.AOS ? 10.0 : 5.0);
            }

            // ClusterF: Armored Meditation — skill > 200 bypasses the armor block.
            // Success chance is penalised by armor weight, providing a harder challenge
            // that re-opens skill gains past 200. Heavier armor = lower chance = more
            // gain opportunities; no mage-armor property required.
            var armorOffset   = Core.AOS ? RegenRates.GetArmorOffset(m) : 0.0;
            var skillVal      = m.Skills.Meditation.Value;
            var armoredExpert = armorOffset > 0.0 && skillVal > 200.0;

            if (Core.AOS && armorOffset > 0 && !armoredExpert)
            {
                m.SendLocalizedMessage(500135); // Regenerative forces cannot penetrate your armor!

                return TimeSpan.FromSeconds(10.0);
            }

            var oneHanded = m.FindItemOnLayer(Layer.OneHanded);
            var twoHanded = m.FindItemOnLayer(Layer.TwoHanded);

            if (Core.AOS && m.Player)
            {
                if (!CheckOkayHolding(oneHanded))
                {
                    m.AddToBackpack(oneHanded);
                }

                if (!CheckOkayHolding(twoHanded))
                {
                    m.AddToBackpack(twoHanded);
                }
            }
            else if (!CheckOkayHolding(oneHanded) || !CheckOkayHolding(twoHanded))
            {
                m.SendLocalizedMessage(502626); // Your hands must be free to cast spells or meditate.

                return TimeSpan.FromSeconds(2.5);
            }

            var chance = (50.0 + (skillVal - (m.ManaMax - m.Mana)) * 2) / 100;

            // Armored meditation penalty: heavier armor = lower chance = more gain opportunity
            if (armoredExpert)
                chance *= Math.Max(0.1, 1.0 - armorOffset * 0.02);

            if (chance > Utility.RandomDouble())
            {
                // ClusterF: raise gain cap to individual skill cap (supports extended caps 100 → 500)
                m.CheckSkill(SkillName.Meditation, 0.0, m.Skills[SkillName.Meditation].Cap);

                if (armoredExpert)
                    m.SendMessage(0x59, "You force your concentration through the weight of your armor.");
                else
                    m.SendLocalizedMessage(501851); // You enter a meditative trance.

                m.Meditating = true;
                (m as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.ActiveMeditation, 1075657));

                if (m.Player || m.Body.IsHuman)
                {
                    m.PlaySound(0xF9);
                }
            }
            else
            {
                if (armoredExpert)
                    m.SendMessage(0x44, "Your armor disrupts your concentration.");
                else
                    m.SendLocalizedMessage(501850); // You cannot focus your concentration.
            }

            return TimeSpan.FromSeconds(10.0);
        }
    }
}

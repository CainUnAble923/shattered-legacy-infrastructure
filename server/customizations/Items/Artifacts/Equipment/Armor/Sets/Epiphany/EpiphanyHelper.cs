using System;
using System.Collections.Generic;
using System.Linq;
using Server.Misc;
using Server.Mobiles;

namespace Server.Mobiles
{
    // ServUO: Mobiles/Normal/DespiseCreature.cs:10 declares this enum beside the Despise creature (CC4). It is
    // carried here because the Epiphany armour is the only consumer in the tree today; when CC4 ports
    // DespiseCreature.cs, drop the enum from that file rather than redeclaring it.
    public enum Alignment
    {
        Neutral,
        Good,
        Evil
    }
}

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/SurgeShield.cs:8 declares this enum beside the Surge Shield, which
    // is in the upstream/carrier bucket (Attributes.Brittle) and not ported. Same rule: when SurgeShield.cs is
    // ported, it takes this declaration from here.
    public enum SurgeType
    {
        None,
        Hits,
        Mana,
        Stam
    }

    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Epiphany/EpiphanyHelper.cs (CC9 batch 5).
    //
    // The Epiphany armour's mechanic is a karma-scaled burst of hits/stamina/mana on taking damage. ServUO
    // drives it from two engine hooks the pieces do not own: OnHit is called from AOS.Damage (AOS.cs:304) and
    // OnKarmaChange from PlayerMobile (PlayerMobile.cs:5859). Neither call site exists in pinned ModernUO, so
    // on this shard the helper is ported whole and INERT, exactly like BestialSetHelper (D-14, Q-042): a full
    // Villainous Epiphany suit is armour with MageArmor and never bursts. Both hooks are one line each into
    // things we already own (AOS-damage-eater-hook.patch touches AOS.Damage; Mobile.OnKarmaChange is a virtual
    // ModernUO declares at Mobiles/Mobile.cs:6812 and PlayerMobile does not override), costed on the
    // "set-mechanic hooks" row. Until they land, `Hooked` is false and AddProperties emits nothing, per the
    // Aloron precedent: a tooltip promising a burst the shard does not deliver is worse than no tooltip.
    //
    // Karma scale: ServUO divides by its Titles.MaxKarma (32000); pinned ModernUO's is 15000 (Misc/Titles.cs:14).
    // Using the shard's own cap keeps "20 at max karma" true here, which is the behaviour a wiki describes.
    public interface IEpiphanyArmor
    {
        Alignment Alignment { get; }
        SurgeType Type { get; }
        int Frequency { get; }
        int Bonus { get; }
    }

    public static class EpiphanyHelper
    {
        // Flip to true when the AOS.Damage and OnKarmaChange hooks exist; nothing else needs to change.
        public const bool Hooked = false;

        public static Dictionary<Mobile, Dictionary<SurgeType, int>> Table { get; set; }

        public static readonly int MinTriggerDamage = 15; // ServUO: "TODO: Amount?"

        public static int GetFrequency(Mobile m, IEpiphanyArmor armor)
        {
            if (m == null)
            {
                return 1;
            }

            return Math.Max(
                1,
                Math.Min(
                    5,
                    m.Items.Count(i => i is IEpiphanyArmor e && e.Alignment == armor.Alignment && e.Type == armor.Type)
                )
            );
        }

        public static int GetBonus(Mobile m, IEpiphanyArmor armor)
        {
            if (m == null)
            {
                return 0;
            }

            switch (armor.Alignment)
            {
                default:
                    return 0;
                case Alignment.Good:
                    if (m.Karma <= 0)
                    {
                        return 0;
                    }

                    return Math.Min(20, m.Karma / (Titles.MaxKarma / 20));
                case Alignment.Evil:
                    if (m.Karma >= 0)
                    {
                        return 0;
                    }

                    return Math.Min(20, -m.Karma / (Titles.MaxKarma / 20));
            }
        }

        // ServUO calls this from AOS.Damage. No caller here.
        public static void OnHit(Mobile m, int damage)
        {
            if (damage > MinTriggerDamage)
            {
                CheckHit(m, damage, SurgeType.Hits);
                CheckHit(m, damage, SurgeType.Stam);
                CheckHit(m, damage, SurgeType.Mana);
            }
        }

        public static void CheckHit(Mobile m, int damage, SurgeType type)
        {
            var item = m.Items.OfType<IEpiphanyArmor>().FirstOrDefault(i => i.Type == type);

            if (item == null)
            {
                return;
            }

            Table ??= new Dictionary<Mobile, Dictionary<SurgeType, int>>();

            if (!Table.ContainsKey(m))
            {
                Table[m] = new Dictionary<SurgeType, int>();
            }

            if (!Table[m].ContainsKey(type))
            {
                Table[m][type] = damage;
            }
            else
            {
                damage += Table[m][type];
            }

            var freq = GetFrequency(m, item);
            var bonus = GetBonus(m, item);

            if (freq > 0 && bonus > 0 && damage > Utility.Random(10000 / freq))
            {
                Table[m].Remove(type);

                if (Table[m].Count == 0)
                {
                    Table.Remove(m);
                }

                // ServUO restores Hits on all three surge types (its Stam and Mana cases both write m.Hits).
                // Reproduced as written; it is what a wiki reader on ServUO gets.
                switch (type)
                {
                    case SurgeType.Hits:
                        m.Hits = Math.Min(m.HitsMax, m.Hits + bonus);
                        break;
                    case SurgeType.Stam:
                        m.Hits = Math.Min(m.HitsMax, m.Hits + bonus);
                        break;
                    default:
                    case SurgeType.Mana:
                        m.Hits = Math.Min(m.HitsMax, m.Hits + bonus);
                        break;
                }
            }
            else
            {
                Table[m][type] = damage;
            }
        }

        // ServUO calls this from PlayerMobile.OnKarmaChange. No caller here.
        public static void OnKarmaChange(Mobile m)
        {
            foreach (var item in m.Items.Where(i => i is IEpiphanyArmor))
            {
                item.InvalidateProperties();
            }
        }

        public static void AddProperties(IEpiphanyArmor item, IPropertyList list)
        {
            if (item == null || !Hooked)
            {
                return;
            }

            switch (item.Type)
            {
                case SurgeType.Hits:
                    list.Add(1150830 + (int)item.Alignment + 1); // Set Ability: good healing burst
                    break;
                case SurgeType.Stam: // ServUO: "This doesn't exist on EA, but put it in here anyways!"
                    list.Add(1149953, $"Set Ability\t{(item.Alignment == Alignment.Evil ? "evil stamina burst" : "good stamina burst")}");
                    break;
                default:
                case SurgeType.Mana:
                    list.Add(1150240 + (int)item.Alignment); // Set Ability: evil mana burst
                    break;
            }

            if (item is Item i)
            {
                list.Add(1150240, GetFrequency(i.Parent as Mobile, item).ToString()); // Set Bonus: Frequency ~1_val~
                list.Add(1150243, GetBonus(i.Parent as Mobile, item).ToString());     // Karma Bonus: Burst level ~1_val~
            }
        }
    }
}

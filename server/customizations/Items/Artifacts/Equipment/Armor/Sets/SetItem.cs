using System;
using System.Linq;
using Server.Items;

namespace Server
{
    public enum SetItem
    {
        None,
        Acolyte,
        Assassin,
        Darkwood,
        Grizzle,
        Hunter,
        Juggernaut,
        Mage,
        Marksman,
        Myrmidon,
        Necromancer,
        Paladin,
        Virtue,
        Luck,
        Knights,
        Scout,
        Sorcerer,
        Initiation,
        Fisherman,
        Luck2,
        Bestial,
        Virtuoso,
        Aloron,
        Darden
    }

    public interface ISetItem
    {
        SetItem SetID { get; }
        int Pieces { get; }
        bool BardMasteryBonus { get; }
        int SetHue { get; set; }
        bool IsSetItem { get; }
        bool SetEquipped { get; set; }
        bool LastEquipped { get; set; }
        AosAttributes SetAttributes { get; }
        AosSkillBonuses SetSkillBonuses { get; }

        int SetPhysicalBonus { get; }
        int SetFireBonus { get; }
        int SetColdBonus { get; }
        int SetPoisonBonus { get; }
        int SetEnergyBonus { get; }

        int SetResistBonus(ResistanceType type);
    }

    public static class SetHelper
    {
        public static void GetSetProperties(IPropertyList list, ISetItem setItem)
        {
            AosAttributes attrs;

            if (setItem is BaseWeapon weapon)
            {
                attrs = weapon.Attributes;
            }
            else if (setItem is BaseArmor armor)
            {
                attrs = armor.Attributes;
            }
            else if (setItem is BaseClothing clothing)
            {
                attrs = clothing.Attributes;
            }
            else if (setItem is BaseQuiver quiver)
            {
                attrs = quiver.Attributes;
            }
            else if (setItem is BaseJewel jewel)
            {
                attrs = jewel.Attributes;
            }
            else
            {
                attrs = new AosAttributes(setItem as Item);
            }

            var full = setItem.SetEquipped;
            var pieces = setItem.Pieces;
            var beserk = setItem.SetID == SetItem.Bestial;

            int prop;

            if (beserk)
            {
                list.Add(1151542, "5"); // Berserk ~1_VAL~ (total)
            }

            if (setItem.BardMasteryBonus)
            {
                list.Add(1151571); // Mastery Bonus Cooldown: 15 min.
            }

            if ((prop = setItem.SetAttributes.RegenHits) != 0)
            {
                // hit point regeneration ~1_val~ (total)
                list.Add(full ? 1080244 : 1154369, (full ? prop + attrs.RegenHits * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.RegenMana) != 0)
            {
                // mana regeneration ~1_val~ (total)
                list.Add(full ? 1080245 : 1080250, (full ? prop + attrs.RegenMana * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.DefendChance) != 0)
            {
                // defense chance increase ~1_val~% (total)
                list.Add(full ? 1073493 : 1060408, (full ? prop + attrs.DefendChance * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.AttackChance) != 0)
            {
                // hit chance increase ~1_val~% (total)
                list.Add(full ? 1073490 : 1154366, (full ? prop + attrs.AttackChance * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.BonusStr) != 0)
            {
                // strength bonus ~1_val~ (total)
                list.Add(full ? 1072514 : 1060485, (full ? prop + attrs.BonusStr * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.BonusDex) != 0)
            {
                // dexterity bonus ~1_val~ (total)
                list.Add(full ? 1072503 : 1080447, (full ? prop + attrs.BonusDex * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.BonusInt) != 0)
            {
                // intelligence bonus ~1_val~ (total)
                list.Add(full ? 1072381 : 1080439, (full ? prop + attrs.BonusInt * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.BonusHits) != 0)
            {
                // hit point increase ~1_val~ (total)
                list.Add(full ? 1080360 : 1080358, (full ? prop + attrs.BonusHits * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.BonusStam) != 0)
            {
                // stamina increase ~1_val~ (total)
                list.Add(full ? 1060484 : 1060484, (full ? prop + attrs.BonusStam * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.BonusMana) != 0)
            {
                // mana increase ~1_val~ (total)
                list.Add(full ? 1060439 : 1060439, (full ? prop + attrs.BonusMana * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.WeaponDamage) != 0)
            {
                // damage increase ~1_val~% (total)
                list.Add(full ? 1151216 : 1154367, (full ? prop + attrs.WeaponDamage * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.WeaponSpeed) != 0)
            {
                // swing speed increase ~1_val~% (total)
                list.Add(full ? 1074323 : 1154368, (full ? prop + attrs.WeaponSpeed * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.SpellDamage) != 0)
            {
                // spell damage increase ~1_val~% (total)
                list.Add(full ? 1072380 : 1060483, (full ? prop + attrs.SpellDamage * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.CastRecovery) != 0)
            {
                // faster cast recovery ~1_val~ (total)
                list.Add(full ? 1080242 : 1080248, (full ? prop + attrs.CastRecovery * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.CastSpeed) != 0)
            {
                // faster casting ~1_val~ (total)
                list.Add(full ? 1080243 : 1080247, (full ? prop + attrs.CastSpeed * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.LowerManaCost) != 0)
            {
                // lower mana cost ~1_val~% (total)
                list.Add(full ? 1073488 : 1060433, (full ? prop + attrs.LowerManaCost * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.LowerRegCost) != 0)
            {
                // lower reagent cost ~1_val~% (total)
                list.Add(full ? 1080441 : 1080440, (full ? prop + attrs.LowerRegCost * pieces : prop).ToString());
            }

            if ((prop = setItem.SetAttributes.ReflectPhysical) != 0)
            {
                // reflect physical damage ~1_val~% (total)
                list.Add(full ? 1072513 : 1060442, (prop + attrs.ReflectPhysical).ToString());
            }

            if ((prop = setItem.SetAttributes.Luck) != 0)
            {
                // luck ~1_val~% (total)
                list.Add(full ? 1073489 : 1080246, (full ? prop + attrs.Luck * pieces : prop).ToString());
            }

            if (!setItem.SetEquipped && setItem.SetAttributes.NightSight != 0)
            {
                list.Add(1060441); // night sight
            }

            if (setItem.SetSkillBonuses.Skill_1_Value != 0)
            {
                // ~1_skill~ ~2_val~ (total)
                list.Add(
                    1072502,
                    $"{AosSkillBonuses.GetLabel(setItem.SetSkillBonuses.Skill_1_Name):#}\t{setItem.SetSkillBonuses.Skill_1_Value}"
                );
            }
        }

        public static void RemoveSetBonus(Mobile from, SetItem setID, Item item)
        {
            var self = false;

            for (var i = 0; i < from.Items.Count; i++)
            {
                if (from.Items[i] == item)
                {
                    self = true;
                }

                Remove(from, setID, from.Items[i]);
            }

            if (!self)
            {
                Remove(from, setID, item);
            }

            from.UpdateResistances();
        }

        public static void Remove(Mobile from, SetItem setID, Item item)
        {
            if (item is ISetItem setItem)
            {
                if (setItem.IsSetItem && setItem.SetID == setID)
                {
                    if (setItem.LastEquipped)
                    {
                        if (from != null)
                        {
                            RemoveStatBonuses(from, item);
                        }

                        setItem.SetSkillBonuses.Remove();
                    }

                    if (setID != SetItem.Bestial)
                    {
                        var temp = item.Hue;
                        item.Hue = setItem.SetHue;
                        setItem.SetHue = temp;
                    }

                    setItem.SetEquipped = false;
                    setItem.LastEquipped = false;
                    item.InvalidateProperties();
                }
            }
        }

        public static void AddSetBonus(Mobile to, SetItem setID)
        {
            int temp;

            for (var i = 0; i < to.Items.Count; i++)
            {
                if (to.Items[i] is ISetItem setItem)
                {
                    if (setItem.IsSetItem && setItem.SetID == setID)
                    {
                        if (setItem.LastEquipped)
                        {
                            AddStatBonuses(
                                to,
                                to.Items[i],
                                setItem.SetAttributes.BonusStr,
                                setItem.SetAttributes.BonusDex,
                                setItem.SetAttributes.BonusInt
                            );

                            setItem.SetSkillBonuses.AddTo(to);
                        }

                        if (setID != SetItem.Bestial)
                        {
                            temp = to.Items[i].Hue;
                            to.Items[i].Hue = setItem.SetHue;
                            setItem.SetHue = temp;
                        }

                        setItem.SetEquipped = true;
                        Timer.DelayCall(item => item.InvalidateProperties(), to.Items[i]);
                    }
                }
            }

            to.UpdateResistances();

            Effects.PlaySound(to.Location, to.Map, 0x1F7);
            to.FixedParticles(0x376A, 9, 32, 5030, EffectLayer.Waist);
            to.SendLocalizedMessage(1072391); // The magic of your armor combines to assist you!
        }

        public static bool FullSetEquipped(Mobile from, SetItem setID, int pieces)
        {
            var equipped = 0;

            for (var i = 0; i < from.Items.Count && equipped < pieces; i++)
            {
                if (from.Items[i] is ISetItem setItem)
                {
                    if (setItem.IsSetItem && setItem.SetID == setID)
                    {
                        equipped += 1;
                    }
                }
            }

            return equipped == pieces;
        }

        public static void RemoveStatBonuses(Mobile from, Item item)
        {
            var modName = item.Serial.ToString();

            from.RemoveStatMod(modName + "SetStr");
            from.RemoveStatMod(modName + "SetDex");
            from.RemoveStatMod(modName + "SetInt");

            from.Delta(MobileDelta.Armor);
            from.CheckStatTimers();
        }

        public static void AddStatBonuses(Mobile to, Item item, int str, int dex, int intel)
        {
            if (str != 0 || dex != 0 || intel != 0)
            {
                var modName = item.Serial.ToString();

                if (str != 0)
                {
                    to.AddStatMod(new StatMod(StatType.Str, modName + "SetStr", str, TimeSpan.Zero));
                }

                if (dex != 0)
                {
                    to.AddStatMod(new StatMod(StatType.Dex, modName + "SetDex", dex, TimeSpan.Zero));
                }

                if (intel != 0)
                {
                    to.AddStatMod(new StatMod(StatType.Int, modName + "SetInt", intel, TimeSpan.Zero));
                }
            }

            to.CheckStatTimers();
        }

        public static bool SetCraftedWith(Mobile from, ISetItem setItem, CraftResource resource)
        {
            var count = 0;

            for (var i = 0; i < from.Items.Count && count < setItem.Pieces; i++)
            {
                if (from.Items[i] is BaseArmor armor)
                {
                    if (armor is ISetItem setArmor && setArmor.IsSetItem && setArmor.SetID == setItem.SetID &&
                        armor.Resource == resource)
                    {
                        count += 1;
                    }
                }
                else if (from.Items[i] is BaseWeapon weapon)
                {
                    if (weapon is ISetItem setWeapon && setWeapon.IsSetItem && setWeapon.SetID == setItem.SetID &&
                        weapon.Resource == resource)
                    {
                        count += 1;
                    }
                }
            }

            return count == setItem.Pieces;
        }

        public static bool ResistsBonusPerPiece(ISetItem item)
        {
            if (item.SetPhysicalBonus == 0 && item.SetFireBonus == 0 && item.SetColdBonus == 0 &&
                item.SetPoisonBonus == 0 && item.SetEnergyBonus == 0)
            {
                return true;
            }

            return item.SetID switch
            {
                SetItem.Virtue => true,
                SetItem.Aloron => true,
                SetItem.Darden => true,
                _              => false
            };
        }

        public static int GetSetTotalResist(Mobile m, ResistanceType resist)
        {
            var total = 0;

            foreach (var item in m.Items.Where(i => i is ISetItem { IsSetItem: true, SetEquipped: true }))
            {
                var sItem = item as ISetItem;

                switch (resist)
                {
                    case ResistanceType.Physical:
                        total += item.PhysicalResistance + sItem.SetPhysicalBonus;
                        break;
                    case ResistanceType.Fire:
                        total += item.FireResistance + sItem.SetFireBonus;
                        break;
                    case ResistanceType.Cold:
                        total += item.ColdResistance + sItem.SetColdBonus;
                        break;
                    case ResistanceType.Poison:
                        total += item.PoisonResistance + sItem.SetPoisonBonus;
                        break;
                    case ResistanceType.Energy:
                        total += item.EnergyResistance + sItem.SetEnergyBonus;
                        break;
                }
            }

            return total;
        }
    }
}

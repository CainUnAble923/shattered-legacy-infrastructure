using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Bestial/BeastialSetHelper.cs (CC9 batch 4). Filename kept
    // (ServUO misspells it); the class is BestialSetHelper as in ServUO. Namespace Server.Items rather than
    // ServUO's Server, because only the Bestial pieces call it.
    //
    // What is wired: OnAdded/OnRemoved (called by the three Bestial pieces), the hue handling and the timer.
    // What is NOT wired (D-14): ServUO calls OnDamage from AOS.Damage and OnHeal from every heal path
    // (SpellHelper.Heal, bandages, potions), and neither hook exists in pinned ModernUO. Both methods are kept
    // so the day those hooks land the berserk mechanic switches on unchanged; until then a full Bestial set
    // gives its set bonus and nothing else, and the pieces never change hue. Same interim shape as
    // SetSelfRepair on the S10 carriers (Q-038). Costed separately, not folded into a content batch.
    public static class BestialSetHelper
    {
        public static readonly int BerserkHue = 1255;

        private static Dictionary<Mobile, BerserkTimer> _table;

        // ServUO call site: SpellHelper.Heal / Bandage / BasePotion. No ModernUO caller today (D-14).
        public static void OnHeal(Mobile healed, Mobile healer, ref int toHeal)
        {
            if (_table == null || !_table.TryGetValue(healed, out var timer))
            {
                return;
            }

            var block = TotalPieces(healed) * timer.Level + 2;

            toHeal = Math.Max(1, toHeal - block);
            healed.SendLocalizedMessage(1151540, block.ToString()); // Your rage blocks ~1_VALUE~ points of healing.
        }

        // ServUO call site: AOS.Damage. No ModernUO caller today (D-14).
        public static void OnDamage(Mobile victim, Mobile attacker, ref int damage)
        {
            var equipped = TotalPieces(victim);

            if (equipped > 0 && victim.Hits - damage < victim.HitsMax / 2)
            {
                if (_table == null || !_table.TryGetValue(victim, out var timer))
                {
                    AddBerserk(victim);
                    return;
                }

                if (!timer.Running)
                {
                    return;
                }

                var absorb = equipped * timer.Level + 2;

                damage = Math.Max(1, damage - absorb);
                timer.DamageTaken += damage;
                victim.SendLocalizedMessage(1151539, absorb.ToString()); // In your rage, you shrug off ~1_VALUE~ points of damage.
            }
        }

        public static int GetTotalBerserk(Item item)
        {
            if (item?.RootParent is Mobile m && _table != null && _table.TryGetValue(m, out var timer))
            {
                return timer.Level;
            }

            return 1;
        }

        public static void OnAdded(Mobile m, Item item)
        {
            if (_table != null && _table.TryGetValue(m, out var timer) && timer.Running &&
                item is ISetItem { SetID: SetItem.Bestial })
            {
                item.Hue = BerserkHue + timer.Level;
            }
        }

        public static void OnRemoved(Mobile m, Item item)
        {
            if (TotalPieces(m) == 0 && _table != null && _table.TryGetValue(m, out var timer))
            {
                timer.EndBerserk();
            }

            if (item is ISetItem { SetID: SetItem.Bestial })
            {
                item.Hue = 2010;
            }
        }

        public static void DoHue(Mobile m, int hue)
        {
            foreach (var i in m.Items.Where(item => item is ISetItem { SetID: SetItem.Bestial } && item.Hue != hue))
            {
                i.Hue = hue;
            }

            m.HueMod = hue;
        }

        public static int TotalPieces(Mobile m) => m.Items.Count(i => i is ISetItem { SetID: SetItem.Bestial });

        public static void AddBerserk(Mobile m)
        {
            _table ??= new Dictionary<Mobile, BerserkTimer>();
            _table[m] = new BerserkTimer(m);
        }

        public static void RemoveBerserk(Mobile m)
        {
            if (_table != null && _table.Remove(m) && _table.Count == 0)
            {
                _table = null;
            }
        }

        public static bool IsBerserk(Mobile m) => _table != null && _table.ContainsKey(m);

        public class BerserkTimer : Timer
        {
            private int _damageTaken;

            public BerserkTimer(Mobile m) : base(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))
            {
                Mobile = m;
                StartHue = m.HueMod;

                DelayCall(
                    TimeSpan.FromSeconds(1),
                    () =>
                    {
                        LastDamage = Core.Now;
                        Start();
                    }
                );
            }

            public Mobile Mobile { get; set; }

            public int DamageTaken
            {
                get => _damageTaken;
                set
                {
                    var level = Level;
                    var old = _damageTaken;

                    _damageTaken = value;

                    if (old < _damageTaken)
                    {
                        LastDamage = Core.Now;
                    }

                    if (level < Level)
                    {
                        var hue = BerserkHue + Level;
                        DoHue(Mobile, hue);

                        if (level < 5)
                        {
                            Mobile.SendLocalizedMessage(1151533, "", hue); // Your rage grows!
                        }
                    }
                    else if (level > Level && level > 0)
                    {
                        var hue = BerserkHue + Level;
                        DoHue(Mobile, hue);

                        if (level > 1)
                        {
                            Mobile.SendLocalizedMessage(1151534, "", hue); // Your rage recedes.
                        }
                    }
                }
            }

            public int StartHue { get; set; }
            public DateTime LastDamage { get; set; }
            public int Level => Math.Min(5, Math.Max(1, _damageTaken / 50));

            protected override void OnTick()
            {
                if (LastDamage + TimeSpan.FromSeconds(10) < Core.Now || !Mobile.Alive)
                {
                    EndBerserk();
                }
                else if (LastDamage + TimeSpan.FromSeconds(3) < Core.Now && Level > 1)
                {
                    DamageTaken -= 50;
                }
                else if (Mobile.HueMod == StartHue || Mobile.HueMod == -1)
                {
                    DoHue(Mobile, BerserkHue);
                    Mobile.SendLocalizedMessage(1151532); // You enter a berserk rage!
                }
            }

            public void EndBerserk()
            {
                RemoveBerserk(Mobile);
                Mobile.HueMod = StartHue;
                Mobile.SendLocalizedMessage(1151535); // Your berserk rage has subsided.

                foreach (var item in Mobile.Items.Where(i => i is ISetItem { SetID: SetItem.Bestial }))
                {
                    item.Hue = 2010;
                }

                Stop();
            }
        }
    }
}

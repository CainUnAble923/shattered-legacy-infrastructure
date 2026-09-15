using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Engines.VeteranRewards;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/TenthAnniversarySculpture.cs (CC9 batch 5). Touching it once a day gives an
    // hour of luck scaled by the account's veteran reward level (200 + 50 per level, capped at 1000).
    //
    // INERT ON THIS SHARD (D-28): ServUO applies the bonus from PlayerMobile.Luck (PlayerMobile.cs:4395,
    // `+ TenthAnniversarySculpture.GetLuckBonus(this)`), and pinned ModernUO's PlayerMobile.Luck has no such
    // read. The shard already carries one luck hook on that exact line, S2's PlayerMobile-fountain-luck-bonus.patch,
    // so closing this is one more term on it; it is costed on the "set-mechanic hooks" row with the others rather
    // than folded into a content batch. Until then the sculpture keeps its tables, cooldowns and messages
    // (including "Your luck just improved!"), and GetLuckBonus is correct but uncalled. The per-sculpture cooldown
    // table is runtime state on ServUO too (its Serialize writes only the version), so nothing persists here either.
    [Flippable(0x3BB3, 0x3BB4)]
    [SerializationGenerator(0, false)]
    public partial class TenthAnniversarySculpture : Item
    {
        private static readonly Dictionary<Mobile, DateTime> _luckTable = new();
        private static readonly List<TenthAnniversarySculpture> _sculptures = new();
        private static DefragTimer _timer;
        private static readonly int MaxLuckBonus = 1000;

        private Dictionary<Mobile, DateTime> _rewardCooldown;

        public Dictionary<Mobile, DateTime> RewardCooldown => _rewardCooldown;

        [Constructible]
        public TenthAnniversarySculpture() : base(15283)
        {
            _rewardCooldown = new Dictionary<Mobile, DateTime>();
            AddSculpture(this);
        }

        public override int LabelNumber => 1079532; // 10th Anniversary Sculpture
        public override double DefaultWeight => 1.0;

        [AfterDeserialization(false)]
        private void AfterDeserialization()
        {
            _rewardCooldown = new Dictionary<Mobile, DateTime>();
            AddSculpture(this);
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(GetWorldLocation(), 2))
            {
                from.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            DefragTables();

            if (!IsCoolingDown(from))
            {
                _luckTable[from] = Core.Now + TimeSpan.FromMinutes(60);
                from.SendLocalizedMessage(1079551); // Your luck just improved!

                _rewardCooldown[from] = Core.Now + TimeSpan.FromHours(24);
                from.Delta(MobileDelta.Armor);
            }
        }

        public bool IsCoolingDown(Mobile from)
        {
            var doneMessage = false;

            if (_luckTable.ContainsKey(from))
            {
                from.SendLocalizedMessage(1079534); // You're still feeling lucky from the last time you touched the sculpture.
                doneMessage = true;
            }

            foreach (var sculpture in _sculptures)
            {
                if (sculpture.RewardCooldown?.ContainsKey(from) == true)
                {
                    if (!doneMessage)
                    {
                        var left = sculpture.RewardCooldown[from] - Core.Now;

                        if (left.TotalHours > 1)
                        {
                            from.SendLocalizedMessage(1079550, ((int)left.TotalHours).ToString()); // You can improve your fortunes again in about ~1_TIME~ hours.
                        }
                        else if (left.TotalMinutes > 1)
                        {
                            from.SendLocalizedMessage(1079548, ((int)left.TotalMinutes).ToString()); // You can improve your fortunes in about ~1_TIME~ minutes.
                        }
                        else
                        {
                            from.SendLocalizedMessage(1079547); // Your fortunes are about to improve.
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        public static void DefragTables()
        {
            foreach (var sculpture in _sculptures)
            {
                var list = new List<Mobile>(sculpture.RewardCooldown.Keys);

                foreach (var m in list)
                {
                    if (sculpture.RewardCooldown.TryGetValue(m, out var until) && until < Core.Now)
                    {
                        sculpture.RewardCooldown.Remove(m);
                    }
                }
            }

            var remove = new List<Mobile>();

            foreach (var kvp in _luckTable)
            {
                if (kvp.Value < Core.Now)
                {
                    remove.Add(kvp.Key);
                }
            }

            foreach (var m in remove)
            {
                _luckTable.Remove(m);

                if (m.NetState != null)
                {
                    m.SendLocalizedMessage(1079552); // Your luck just ran out.
                }
            }
        }

        // ServUO reads this from PlayerMobile.Luck. No caller here (D-28).
        public static int GetLuckBonus(Mobile from)
        {
            if (_luckTable.ContainsKey(from) && from.Account is Account account)
            {
                return Math.Min(MaxLuckBonus, 200 + RewardSystem.GetRewardLevel(account) * 50);
            }

            return 0;
        }

        public override void OnAfterDelete()
        {
            base.OnAfterDelete();

            RemoveSculpture(this);
            _rewardCooldown?.Clear();
        }

        public static void AddSculpture(TenthAnniversarySculpture sculpture)
        {
            if (!_sculptures.Contains(sculpture))
            {
                _sculptures.Add(sculpture);
                StartTimer();
            }
        }

        public static void RemoveSculpture(TenthAnniversarySculpture sculpture)
        {
            _sculptures.Remove(sculpture);

            if (_sculptures.Count == 0 && _timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }

        public static void StartTimer()
        {
            if (_timer?.Running == true)
            {
                return;
            }

            _timer = new DefragTimer();
            _timer.Start();
        }

        private class DefragTimer : Timer
        {
            public DefragTimer() : base(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1))
            {
            }

            protected override void OnTick() => DefragTables();
        }
    }
}

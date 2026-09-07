using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Network;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class FountainOfFortune : BaseAddon
    {
        private static readonly int LuckBonus = 400;

        private static readonly List<FountainOfFortune> m_Fountains = new();
        private static readonly Dictionary<Mobile, DateTime> m_LuckTable = new();
        private static readonly Dictionary<Mobile, DateTime> m_SpecialProtection = new();
        private static readonly Dictionary<Mobile, DateTime> m_BalmBoost = new();

        private static Timer m_Timer;

        public static Dictionary<Mobile, DateTime> SpecialProtection => m_SpecialProtection;
        public static Dictionary<Mobile, DateTime> BalmBoost => m_BalmBoost;

        // ServUO recreates both tables in Deserialize rather than persisting them, so a restart
        // clears every cooldown. Field initialisers reproduce that exactly on both paths.
        private Dictionary<Mobile, DateTime> m_ResCooldown = new();
        private Dictionary<Mobile, DateTime> m_RewardCooldown = new();

        public Dictionary<Mobile, DateTime> ResCooldown => m_ResCooldown;
        public Dictionary<Mobile, DateTime> RewardCooldown => m_RewardCooldown;

        [Constructible]
        public FountainOfFortune()
        {
            var itemID = 0x1731;

            AddComponent(new AddonComponent(itemID++), -2, +1, 0);
            AddComponent(new AddonComponent(itemID++), -1, +1, 0);
            AddComponent(new AddonComponent(itemID++), +0, +1, 0);
            AddComponent(new AddonComponent(itemID++), +1, +1, 0);

            AddComponent(new AddonComponent(itemID++), +1, +0, 0);
            AddComponent(new AddonComponent(itemID++), +1, -1, 0);
            AddComponent(new AddonComponent(itemID++), +1, -2, 0);

            AddComponent(new AddonComponent(itemID++), +0, -2, 0);
            AddComponent(new AddonComponent(itemID++), +0, -1, 0);
            AddComponent(new AddonComponent(itemID++), +0, +0, 0);

            AddComponent(new AddonComponent(itemID++), -1, +0, 0);
            AddComponent(new AddonComponent(itemID++), -2, +0, 0);

            AddComponent(new AddonComponent(itemID++), -2, -1, 0);
            AddComponent(new AddonComponent(itemID++), -1, -1, 0);

            AddComponent(new AddonComponent(itemID++), -1, -2, 0);

            // Pre-increment, not post: ServUO skips graphic 0x1740 for the last tile. Ported
            // unchanged. See notes/s2-fountain.md.
            AddComponent(new AddonComponent(++itemID), -2, -2, 0);

            Movable = false;

            AddFountain(this);
        }

        [AfterDeserialization(false)]
        private void AfterDeserialization()
        {
            AddFountain(this);
        }

        public bool OnTarget(Mobile from, Item coin)
        {
            DefragTables();

            if (IsCoolingDown(from))
            {
                from.SendLocalizedMessage(1113368); // You already made a wish today. Try again tomorrow!
                return false;
            }

            if (.20 >= Utility.RandomDouble())
            {
                Item item = null;

                switch (Utility.Random(4))
                {
                    case 0: item = new SolesOfProvidence(); break;
                    case 1: item = new GemologistsSatchel(); break;
                    case 2: item = new RelicFragment(5); break;
                    case 3: item = new EnchantedEssence(5); break;
                }

                if (from.Backpack == null || !from.Backpack.TryDropItem(from, item, false))
                {
                    item.MoveToWorld(from.Location, from.Map);
                }
            }
            else
            {
                // Ported unchanged: ServUO rolls Random(4) over a six-case switch, so cases 4 and
                // 5 (special protection, balm boost) are unreachable upstream too. Widening the
                // roll here would hand our players a buff that ServUO and OSI players do not get,
                // which is a fidelity deviation in the direction nobody checks a wiki for.
                // Recorded in notes/s2-fountain.md.
                switch (Utility.Random(4))
                {
                    case 0:
                        from.AddStatMod(new StatMod(StatType.Str, "FoF_Str", 10, TimeSpan.FromMinutes(60)));
                        from.SendLocalizedMessage(1113373); // You suddenly feel stronger!
                        break;
                    case 1:
                        from.AddStatMod(new StatMod(StatType.Dex, "FoF_Dex", 10, TimeSpan.FromMinutes(60)));
                        from.SendLocalizedMessage(1113374); // You suddenly feel more agile!
                        break;
                    case 2:
                        from.AddStatMod(new StatMod(StatType.Int, "FoF_Int", 10, TimeSpan.FromMinutes(60)));
                        from.SendLocalizedMessage(1113371); // You suddenly feel wiser!
                        break;
                    case 3:
                        m_LuckTable[from] = Core.Now + TimeSpan.FromMinutes(60);
                        from.SendLocalizedMessage(1079551); // Your luck just improved!
                        break;
                    case 4:
                        m_SpecialProtection[from] = Core.Now + TimeSpan.FromMinutes(60);
                        from.SendLocalizedMessage(1113375); // You suddenly feel less vulnerable!
                        break;
                    case 5:
                        m_BalmBoost[from] = Core.Now + TimeSpan.FromMinutes(60);
                        from.SendLocalizedMessage(1113372); // The duration of your balm has been increased by an hour!
                        break;
                }

                from.FixedParticles(0x373A, 10, 15, 5018, EffectLayer.Waist);
            }

            from.PlaySound(0x22);

            m_RewardCooldown[from] = Core.Now + TimeSpan.FromHours(24);

            if (coin.Amount <= 1)
            {
                coin.Delete();
            }
            else
            {
                coin.Amount--;
            }

            return false;
        }

        public bool IsCoolingDown(Mobile from)
        {
            foreach (var fountain in m_Fountains)
            {
                if (fountain.RewardCooldown != null && fountain.RewardCooldown.ContainsKey(from))
                {
                    return true;
                }
            }

            return false;
        }

        public static int GetLuckBonus(Mobile from) => m_LuckTable.ContainsKey(from) ? LuckBonus : 0;

        public static bool UnderProtection(Mobile m) => m_SpecialProtection.ContainsKey(m);

        public bool CanRes(Mobile m)
        {
            if (!m_ResCooldown.ContainsKey(m))
            {
                return true;
            }

            if (m_ResCooldown[m] < Core.Now)
            {
                m_ResCooldown.Remove(m);
                return true;
            }

            return false;
        }

        public override bool HandlesOnMovement => true;

        public override void OnMovement(Mobile m, Point3D oldLocation)
        {
            if (m.Player && CanRes(m) && !m.Alive && m.InRange(Location, 5))
            {
                m.SendGump(new FountainResurrectGump(this, m));
            }
        }

        public void Resurrect_Callback(Mobile m)
        {
            m_ResCooldown[m] = Core.Now + TimeSpan.FromMinutes(10);
        }

        // ServUO passes a ResurrectCallback to ResurrectGump. ModernUO's ResurrectGump has no
        // callback parameter, so the callback is expressed by subclassing it here rather than by
        // patching upstream: fewer moving parts on a pinned-commit bump, and no upstream logic
        // copied into our tree. The cooldown is set only when the player is actually raised,
        // which is what the ServUO callback did.
        private class FountainResurrectGump : ResurrectGump
        {
            private readonly FountainOfFortune m_Fountain;
            private readonly Mobile m_From;

            public FountainResurrectGump(FountainOfFortune fountain, Mobile from)
                : base(from, ResurrectMessage.Generic, false, 0.0)
            {
                m_Fountain = fountain;
                m_From = from;
            }

            public override void OnResponse(NetState state, in RelayInfo info)
            {
                var wasDead = !m_From.Alive;

                base.OnResponse(state, info);

                if (wasDead && m_From.Alive)
                {
                    m_Fountain.Resurrect_Callback(m_From);
                }
            }
        }

        public static void DefragTables()
        {
            foreach (var fountain in m_Fountains)
            {
                var list = new List<Mobile>(fountain.ResCooldown.Keys);
                var list2 = new List<Mobile>(fountain.RewardCooldown.Keys);

                foreach (var m in list)
                {
                    if (fountain.ResCooldown.ContainsKey(m) && fountain.ResCooldown[m] < Core.Now)
                    {
                        fountain.ResCooldown.Remove(m);
                    }
                }

                foreach (var m in list2)
                {
                    if (fountain.RewardCooldown.ContainsKey(m) && fountain.RewardCooldown[m] < Core.Now)
                    {
                        fountain.RewardCooldown.Remove(m);
                    }
                }

                list.Clear();
                list2.Clear();
            }

            var remove = new List<Mobile>();

            foreach (var kvp in m_LuckTable)
            {
                if (kvp.Value < Core.Now)
                {
                    remove.Add(kvp.Key);
                }
            }

            remove.ForEach(m =>
            {
                m_LuckTable.Remove(m);

                if (m.NetState != null)
                {
                    m.SendLocalizedMessage(1079552); // Your luck just ran out.
                }
            });

            remove.Clear();

            foreach (var kvp in m_SpecialProtection)
            {
                if (kvp.Value < Core.Now)
                {
                    remove.Add(kvp.Key);
                }
            }

            remove.ForEach(m => m_SpecialProtection.Remove(m));

            remove.Clear();

            foreach (var kvp in m_BalmBoost)
            {
                if (kvp.Value < Core.Now)
                {
                    remove.Add(kvp.Key);
                }
            }

            remove.ForEach(m => m_BalmBoost.Remove(m));

            remove.Clear();
        }

        public override void Delete()
        {
            base.Delete();

            RemoveFountain(this);

            m_ResCooldown?.Clear();
            m_RewardCooldown?.Clear();
        }

        public static void AddFountain(FountainOfFortune fountain)
        {
            if (!m_Fountains.Contains(fountain))
            {
                m_Fountains.Add(fountain);
                StartTimer();
            }
        }

        public static void RemoveFountain(FountainOfFortune fountain)
        {
            if (m_Fountains.Contains(fountain))
            {
                m_Fountains.Remove(fountain);
            }

            if (m_Fountains.Count == 0 && m_Timer != null)
            {
                m_Timer.Stop();
                m_Timer = null;
            }
        }

        public static void StartTimer()
        {
            if (m_Timer != null && m_Timer.Running)
            {
                return;
            }

            // ModernUO's Timer.DelayCall starts the timer itself; ServUO's explicit Start() would
            // be a double start here.
            m_Timer = Timer.DelayCall(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), DefragTables);
        }
    }
}

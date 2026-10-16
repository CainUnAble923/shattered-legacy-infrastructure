// cc-P70, F-33 Parts A, C and D: attaching modules, and what they do. Notes: shard-migration notes/cc-P70-mule-modules.md.
//
// The numbers a module changes go through PackMule.Compute (P69's one place: pack, resists, stamina, hits, slot cost,
// death drop), by the ModifyNumbers event below. Timed behavior runs on one tick every two seconds over the mules that
// carry modules (not the mule's own OnThink: a pet's AI stops when its sector goes idle, BaseCreature.cs:4186-4199).
// Every number is in MuleModuleData; what is stubbed says so on the module's tooltip.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Items;
using Server.Multis;
using Server.Regions;
using Server.Targeting;

namespace Server.Mobiles
{
    public static class MuleModules
    {
        public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(2);

        private static readonly HashSet<PackMule> _active = new();

        // Per-mule state that need not survive a restart (cooldowns, the tracked map, the last warning).
        public sealed class Runtime
        {
            public DateTime LastDamaged;
            public DateTime NextWarn, NextHeal, NextCure, NextAura, EmergencyReady, WardReady, NextSmelt, NextBoards;
            public DateTime NextPickup, NextHide, DeadSince, LeftBehindSince;
            public HashSet<Serial> Warned = new();
            public Item TrackedMap;
            public MuleForgeSpot Forge;
            public MuleAnvilSpot Anvil;
        }

        private static readonly ConditionalWeakTable<PackMule, Runtime> _runtime = new();

        public static Runtime State(PackMule m) => _runtime.GetOrCreateValue(m);

        private static bool _configured;

        /// <summary>Hooks the modules into the mule's numbers and speech. Idempotent: the test host calls it directly.</summary>
        public static void Configure()
        {
            if (_configured)
            {
                return;
            }

            _configured = true;
            PackMule.ModifyNumbers += Apply;
            EventSink.Speech += OnSpeech;
        }

        public static void Initialize() => Timer.StartTimer(TickInterval, TickInterval, TickAll);

        // --- what a mule carries -----------------------------------------------------------------------------------

        public static IEnumerable<MuleModule> Installed(PackMule m) =>
            m?.Modules == null ? Enumerable.Empty<MuleModule>() : m.Modules.OfType<MuleModule>().Where(x => !x.Deleted);

        /// <summary>The tier of this module on the mule, 0 if it has none.</summary>
        public static int TierOf(PackMule m, MuleModuleId id) =>
            Installed(m).Where(x => x.ModuleId == id).Select(x => x.Tier).DefaultIfEmpty(0).Max();

        public static bool HasModule(PackMule m, MuleModuleId id, int minTier = 1) => TierOf(m, id) >= minTier;

        /// <summary>A trait's boost to a module (MuleModuleData.TraitBoosts), by the mule's grade in that trait.</summary>
        public static int Boost(PackMule m, MuleModuleId id)
        {
            var total = 0;

            foreach (var b in MuleModuleData.TraitBoosts)
            {
                if (b.Module == id && m.GradeOf(b.Trait) is var grade and > 0)
                {
                    total += b.PerGrade[Math.Min(grade, b.PerGrade.Length) - 1];
                }
            }

            return total;
        }

        /// <summary>Celestial Tempered's boost to every module's number, in percent.</summary>
        public static int CelestialBoost(PackMule m) =>
            m.GradeOf(MuleTrait.CelestialTempered) is var g and > 0
                ? MuleModuleData.TraitBoosts.First(b => b.Trait == MuleTrait.CelestialTempered).PerGrade[Math.Min(g, 3) - 1]
                : 0;

        private static int Boosted(PackMule m, int value) => value * (100 + CelestialBoost(m)) / 100;

        public static PackMule MuleOf(Mobile owner, int range = -1)
        {
            if (owner is not PlayerMobile pm)
            {
                return null;
            }

            if (pm.Mount is PackMule ridden)
            {
                return ridden;
            }

            foreach (var f in pm.AllFollowers ?? new HashSet<Mobile>())
            {
                if (f is PackMule { Deleted: false, IsDeadPet: false } mule && mule.ControlMaster == pm && mule.Map == pm.Map &&
                    (range < 0 || mule.InRange(pm, range)))
                {
                    return mule;
                }
            }

            return null;
        }

        // --- attaching ------------------------------------------------------------------------------------------------

        /// <summary>Why this module cannot go on this mule now, or null.</summary>
        public static string RefuseAttach(Mobile from, PackMule mule, MuleModule module)
        {
            if (from != mule.ControlMaster)
            {
                return "Only the mule's owner can fit it with modules.";
            }

            if (mule.IsDeadPet || !mule.Alive)
            {
                return "The mule must be alive.";
            }

            if (module.Tier > mule.Tier)
            {
                return $"A tier {MuleData.GradeName(module.Tier)} module needs a tier {module.Tier} mule or better; yours is tier {mule.Tier}.";
            }

            if (Installed(mule).Any(x => x.GuildKey == module.GuildKey))
            {
                return $"The mule already carries a {module.Info?.GuildName} module; take it off first.";
            }

            if (Installed(mule).Count() >= mule.ModuleSlots)
            {
                return $"All {mule.ModuleSlots} of the mule's module slots are full.";
            }

            return null;
        }

        /// <summary>Attaches it, or refuses with the reason (the module stays with the player). True when attached.</summary>
        public static bool TryAttach(Mobile from, PackMule mule, MuleModule module)
        {
            var refusal = RefuseAttach(from, mule, module);

            if (refusal != null)
            {
                from.SendMessage(0x22, refusal);
                return false;
            }

            mule.Modules.Add(module);
            module.Internalize();
            mule.MarkDirty();
            mule.ApplyNumbers(false);
            Track(mule);
            OnMuleMoved(mule);
            from.SendMessage(0x44, $"You fit the {module.Name} to the mule.");
            mule.InvalidateProperties();
            return true;
        }

        /// <summary>Takes a module off and gives it to the owner (pack, or bank if the pack is full). Its tier is kept.</summary>
        public static bool Detach(Mobile from, PackMule mule, MuleModule module)
        {
            if (from != mule.ControlMaster || !mule.Modules.Remove(module))
            {
                return false;
            }

            mule.MarkDirty();

            if (from.Backpack?.TryDropItem(from, module, false) != true)
            {
                from.BankBox.DropItem(module);
                from.SendMessage(0x59, $"Your pack is full: the {module.Name} went to your bank.");
            }
            else
            {
                from.SendMessage(0x44, $"You take the {module.Name} off the mule.");
            }

            mule.ApplyNumbers(false);
            OnMuleMoved(mule);
            mule.InvalidateProperties();
            return true;
        }

        public static void Track(PackMule m)
        {
            if (Installed(m).Any())
            {
                _active.Add(m);
            }
        }

        public static void OnMuleDeleted(PackMule m)
        {
            _active.Remove(m);

            if (_runtime.TryGetValue(m, out var rt))
            {
                rt.Forge?.Delete();
                rt.Anvil?.Delete();
            }

            // The modules go back to the owner's bank; with no owner, they go with the mule.
            var owner = m.ControlMaster ?? m.LastOwner;

            foreach (var module in Installed(m).ToList())
            {
                if (owner is { Deleted: false } && owner.BankBox is { } bank)
                {
                    bank.DropItem(module);
                }
                else
                {
                    module.Delete();
                }
            }

            m.Modules?.Clear();
        }

        // --- numbers (PackMule.Compute) --------------------------------------------------------------------------------

        public static readonly Dictionary<string, int> SocketCaps = new()
        {
            ["physical"] = 15, ["fire"] = 15, ["cold"] = 15, ["poison"] = 15, ["energy"] = 15,
            ["hits"] = 25, ["carry"] = 300, ["hitsregen"] = 2, ["stamregen"] = 5
        };

        public static void Apply(PackMule m, MuleNumbers n)
        {
            foreach (var module in Installed(m))
            {
                var t = module.Tier;
                var v = Boosted(m, MuleModuleData.ValueAt(module.ModuleId, t));

                switch (module.ModuleId)
                {
                    case MuleModuleId.ReinforcedSaddlebags:
                        n.PackItems += v;
                        break;
                    case MuleModuleId.ClockworkLoadHarness:
                        n.PackWeight += n.PackWeight * (v + Boost(m, module.ModuleId)) / 100;
                        break;
                    case MuleModuleId.TrailwiseTack:
                        n.Stam += n.Stam * (v + Boost(m, module.ModuleId)) / 100;
                        n.StamRegen += 10;
                        if (t >= 3)
                        {
                            n.SlotCost = 0;
                        }

                        break;
                    case MuleModuleId.ExpeditionBarding:
                        n.DamageReduction += v + Boost(m, module.ModuleId);
                        if (t >= 2)
                        {
                            n.HitsMax += n.HitsMax * (t >= 3 ? 20 : 10) / 100;
                        }

                        break;
                    case MuleModuleId.ArcaneWardHarness:
                        n.MagicResist += v + Boost(m, module.ModuleId);
                        break;
                    case MuleModuleId.SoulTether:
                        if (t >= 2)
                        {
                            n.KeepPackOnDeath = true;
                        }

                        break;
                    case MuleModuleId.ImbuedBardingSocket:
                        foreach (var (kind, value) in module.Sockets)
                        {
                            switch (kind)
                            {
                                case "physical": n.Physical += value; break;
                                case "fire": n.Fire += value; break;
                                case "cold": n.Cold += value; break;
                                case "poison": n.Poison += value; break;
                                case "energy": n.Energy += value; break;
                                case "hits": n.HitsMax += value; break;
                                case "carry": n.PackWeight += value; break;
                                case "hitsregen": n.HitsRegen += value; break;
                                case "stamregen": n.StamRegen += value; break;
                            }
                        }

                        break;
                }
            }

            n.DamageReduction = Math.Min(n.DamageReduction, 50);
        }

        public static int StamRegenBonus(Mobile from) => from is PackMule m && Installed(m).Any() ? PackMule.Compute(m).StamRegen : 0;

        /// <summary>Survey Saddle Roll I: one more chunk of reveal while the owner's mule is within 12 tiles (or ridden).</summary>
        public static int ExploreBonus(PlayerMobile pm) => HasModule(MuleOf(pm, 12), MuleModuleId.SurveySaddleRoll) ? 1 : 0;

        // --- weight (PackMuleBackpack) ---------------------------------------------------------------------------------

        private static readonly Type[] Gems =
        {
            typeof(Amber), typeof(Amethyst), typeof(Citrine), typeof(Diamond), typeof(Emerald), typeof(Ruby), typeof(Sapphire),
            typeof(StarSapphire), typeof(Tourmaline), typeof(BlueDiamond), typeof(BrilliantAmber), typeof(DarkSapphire),
            typeof(EcruCitrine), typeof(FireRuby), typeof(PerfectEmerald), typeof(Turquoise), typeof(WhitePearl)
        };

        public static bool HasWeightModules(PackMule m) =>
            HasModule(m, MuleModuleId.OreHopper) || HasModule(m, MuleModuleId.TimberRack) || HasModule(m, MuleModuleId.SurveySaddleRoll);

        /// <summary>The share of an item's weight the pack counts: 1 = all of it.</summary>
        public static double WeightFactor(PackMule m, Item item)
        {
            if (item is TreasureMap or SOS or MessageInABottle)
            {
                return HasModule(m, MuleModuleId.SurveySaddleRoll) ? 0.0 : 1.0;
            }

            MuleModuleId id;

            if (item is BaseOre or BaseIngot or BaseGranite || Array.IndexOf(Gems, item.GetType()) >= 0)
            {
                id = MuleModuleId.OreHopper;
            }
            else if (item is Log or Board)
            {
                id = MuleModuleId.TimberRack;
            }
            else
            {
                return 1.0;
            }

            var tier = TierOf(m, id);

            if (tier == 0)
            {
                return 1.0;
            }

            var saved = Math.Min(90, Boosted(m, MuleModuleData.ValueAt(id, tier)) + Boost(m, id));
            return (100 - saved) / 100.0;
        }

        public static bool InsideSatchel(Item item, Container pack)
        {
            for (var p = item.Parent as Item; p != null && p != pack; p = p.Parent as Item)
            {
                if (p is CompactOreSatchel or ForestersLumberSatchel or HuntersSatchel)
                {
                    return true;
                }
            }

            return false;
        }

        // --- damage ---------------------------------------------------------------------------------------------------

        /// <summary>Every hit on the mule, once: Expedition Barding everywhere, Deepdelver in a dungeon.</summary>
        public static int ScaleIncoming(PackMule m, int amount)
        {
            if (amount <= 0)
            {
                return amount;
            }

            var n = PackMule.Compute(m);
            var scale = (100 - n.DamageReduction) / 100.0;

            if (m.Region?.IsPartOf<DungeonRegion>() == true)
            {
                scale *= n.DungeonDamageScale;
            }

            return Math.Max(scale < 1.0 ? 0 : amount, (int)Math.Round(amount * scale));
        }

        /// <summary>Arcane Ward Harness II: absorbs one damage spell, then recharges (60 s, Nightmare-Kin faster).</summary>
        public static bool WardAbsorb(PackMule m, ref int damage, DateTime? now = null)
        {
            var at = now ?? DateTime.UtcNow;
            var rt = State(m);

            if (damage <= 0 || TierOf(m, MuleModuleId.ArcaneWardHarness) < 2 || at < rt.WardReady)
            {
                return false;
            }

            damage = 0;
            rt.WardReady = at + TimeSpan.FromSeconds(60.0 * (100 - Boost(m, MuleModuleId.ArcaneWardHarness)) / 100);
            m.FixedParticles(0x375A, 10, 15, 5037, EffectLayer.Waist);
            m.ControlMaster?.SendMessage(0x59, "Your mule's ward swallows the spell.");
            return true;
        }

        public static void OnMuleDamaged(PackMule m, Mobile from, bool willKill, double? roll = null)
        {
            var rt = State(m);
            rt.LastDamaged = DateTime.UtcNow;

            if (willKill)
            {
                return;
            }

            // Shadow Cloak III: hides on its own when attacked (30-second cooldown).
            if (TierOf(m, MuleModuleId.ShadowCloak) >= 3 && !m.Hidden && DateTime.UtcNow >= rt.NextHide)
            {
                m.Hidden = true;
                rt.NextHide = DateTime.UtcNow + TimeSpan.FromSeconds(30);
            }

            // Minstrel's Bells II: a chance to calm whatever attacks it.
            if (TierOf(m, MuleModuleId.MinstrelsBells) >= 2 && from is BaseCreature { Controlled: false, Summoned: false } bc &&
                !bc.BardImmune && !bc.Uncalmable && (roll ?? Utility.RandomDouble()) * 100 < MuleModuleData.ValueAt(MuleModuleId.MinstrelsBells, 2))
            {
                bc.Pacify(m.ControlMaster ?? m, Core.Now + TimeSpan.FromSeconds(15));
            }
        }

        /// <summary>Shadow Cloak II: a hidden mule walking beside its hidden, unmounted owner stays hidden.</summary>
        public static bool StaysHiddenMoving(PackMule m, Direction d) =>
            TierOf(m, MuleModuleId.ShadowCloak) >= 2 && m.ControlMaster is PlayerMobile { Hidden: true, Mount: null } owner &&
            m.InRange(owner, 3) && (d & Direction.Running) == 0;

        /// <summary>Minstrel's Bells III: how much easier the owner's bard skills are near the mule (lowers the window).</summary>
        public static double BardSkillEase(Mobile from, SkillName skill) =>
            skill is SkillName.Peacemaking or SkillName.Provocation or SkillName.Discordance &&
            HasModule(MuleOf(from, 8), MuleModuleId.MinstrelsBells, 3)
                ? MuleModuleData.ValueAt(MuleModuleId.MinstrelsBells, 3)
                : 0;

        // --- the tick -------------------------------------------------------------------------------------------------

        private static void TickAll()
        {
            foreach (var m in _active.ToList())
            {
                if (m.Deleted || !Installed(m).Any())
                {
                    _active.Remove(m);
                    continue;
                }

                Tick(m, DateTime.UtcNow);
            }
        }

        /// <summary>One tick of a mule's timed modules (public for the tests).</summary>
        public static void Tick(PackMule m, DateTime now)
        {
            var rt = State(m);
            var owner = m.ControlMaster as PlayerMobile;

            OnMuleMoved(m);
            SoulTetherTick(m, rt, now);

            if (m.IsDeadPet || !m.Alive)
            {
                return;
            }

            var outOfCombat = m.Combatant == null && now - rt.LastDamaged >= TimeSpan.FromSeconds(10);

            // Imbued Barding Socket: hits regen out of combat.
            if (outOfCombat && PackMule.Compute(m).HitsRegen is var regen and > 0)
            {
                m.Hits = Math.Min(m.HitsMax, m.Hits + regen);
            }

            if (owner != null)
            {
                TrailwiseTick(m, owner, rt, now);
                ShadowTick(m, owner);
                SmelterTick(m, owner, rt, now);
                PickupTick(m, owner, rt, now);
                AuraTick(m, owner, rt, now);
            }

            MedicTick(m, rt, now, outOfCombat);
            WardRecallTick(m, owner);
            BoardsTick(m, rt, now);
            BellsTick(m);
        }

        // Trailwise Tack I: hostile warning; II: Homing.
        private static void TrailwiseTick(PackMule m, PlayerMobile owner, Runtime rt, DateTime now)
        {
            var tier = TierOf(m, MuleModuleId.TrailwiseTack);

            if (tier == 0)
            {
                return;
            }

            if (now >= rt.NextWarn && owner.Alive && owner.Map == m.Map)
            {
                rt.NextWarn = now + TimeSpan.FromSeconds(10);
                var range = 12 * (100 + Boost(m, MuleModuleId.TrailwiseTack)) / 100;
                var fresh = HostilesNear(owner, range).Where(h => rt.Warned.Add(h.Serial)).Take(3).ToList();

                if (fresh.Count > 0)
                {
                    owner.SendMessage(0x22, $"Your mule shies: {string.Join(", ", fresh.Select(h => h.Name))} nearby.");
                }
            }

            if (tier >= 2)
            {
                Homing(m, owner, rt, now);
            }
        }

        public static List<BaseCreature> HostilesNear(PlayerMobile owner, int range)
        {
            var list = new List<BaseCreature>();

            foreach (var mob in owner.GetMobilesInRange(range))
            {
                if (mob is BaseCreature { Controlled: false, Summoned: false, Deleted: false, Alive: true } bc &&
                    bc.FightMode is not (FightMode.None or FightMode.Aggressor) &&
                    (bc.Combatant == owner || Notoriety.Compute(owner, bc) == Notoriety.Murderer))
                {
                    list.Add(bc);
                }
            }

            return list;
        }

        /// <summary>Trailwise Tack II: left behind on the same map for 3 seconds (more than 12 tiles off), it comes to the
        /// owner. Not while ridden, stabled, dead, or told to stay.</summary>
        public static bool Homing(PackMule m, PlayerMobile owner, Runtime rt, DateTime now)
        {
            if (m.Rider != null || m.IsStabled || m.IsDeadPet || owner.Map == null || owner.Map == Map.Internal ||
                m.Map != owner.Map || m.ControlOrder is not (OrderType.Follow or OrderType.Come) || m.InRange(owner, 12))
            {
                rt.LeftBehindSince = DateTime.MinValue;
                return false;
            }

            if (rt.LeftBehindSince == DateTime.MinValue)
            {
                rt.LeftBehindSince = now;
                return false;
            }

            if (now - rt.LeftBehindSince < TimeSpan.FromSeconds(3))
            {
                return false;
            }

            rt.LeftBehindSince = DateTime.MinValue;
            m.MoveToWorld(owner.Location, owner.Map);
            m.PlaySound(0xA8);
            return true;
        }

        // "all come" from any distance (Homing): speech reaches pets only within 14-15 tiles (pinned OnSpeech.cs:139,
        // Mobile.cs:5544); the speech event fires before any range filter (Mobile.cs:5590).
        private static void OnSpeech(SpeechEventArgs e)
        {
            if (e.Mobile is not PlayerMobile pm || !e.HasKeyword(0x164)) // "all come"
            {
                return;
            }

            foreach (var f in (pm.AllFollowers ?? new HashSet<Mobile>()).ToList())
            {
                if (f is PackMule { IsDeadPet: false, IsStabled: false, Rider: null } m && m.Map == pm.Map &&
                    !m.InRange(pm, 14) && TierOf(m, MuleModuleId.TrailwiseTack) >= 2)
                {
                    m.MoveToWorld(pm.Location, pm.Map);
                    m.IssueOrder(OrderType.Come, pm);
                }
            }
        }

        // Shadow Cloak I: hides when the owner hides (polled; the owner's hiding raises no event).
        private static void ShadowTick(PackMule m, PlayerMobile owner)
        {
            if (TierOf(m, MuleModuleId.ShadowCloak) >= 1 && owner.Hidden && !m.Hidden && m.InRange(owner, 3) && m.Rider == null)
            {
                m.Hidden = true;
            }
        }

        // Field Medic Pack: I slow self-heal out of combat; II self-cure; III emergency heal.
        private static void MedicTick(PackMule m, Runtime rt, DateTime now, bool outOfCombat)
        {
            var tier = TierOf(m, MuleModuleId.FieldMedicPack);

            if (tier == 0)
            {
                return;
            }

            if (outOfCombat && now >= rt.NextHeal && m.Hits < m.HitsMax)
            {
                var pct = MuleModuleData.ValueAt(MuleModuleId.FieldMedicPack, tier);

                if (m.Region?.IsPartOf<DungeonRegion>() == true)
                {
                    pct = pct * (100 + Boost(m, MuleModuleId.FieldMedicPack)) / 100;
                }

                m.Hits = Math.Min(m.HitsMax, m.Hits + Math.Max(1, m.HitsMax * Boosted(m, pct) / 100));
                rt.NextHeal = now + TimeSpan.FromSeconds(10);
            }

            if (tier >= 2 && m.Poisoned && now >= rt.NextCure)
            {
                m.CurePoison(m);
                rt.NextCure = now + TimeSpan.FromSeconds(10);
            }

            if (tier >= 3 && m.Hits * 4 < m.HitsMax && now >= rt.EmergencyReady)
            {
                m.Hits = Math.Min(m.HitsMax, m.Hits + m.HitsMax / 2);
                m.FixedParticles(0x376A, 9, 32, 5030, EffectLayer.Waist);
                rt.EmergencyReady = now + TimeSpan.FromMinutes(5);
            }
        }

        // Field Medic Pack III: a slow healing aura on the owner and their other followers within 3 tiles.
        private static void AuraTick(PackMule m, PlayerMobile owner, Runtime rt, DateTime now)
        {
            if (TierOf(m, MuleModuleId.FieldMedicPack) < 3 || now < rt.NextAura)
            {
                return;
            }

            rt.NextAura = now + TimeSpan.FromSeconds(5);

            if (owner.Alive && owner.Hits < owner.HitsMax && owner.InRange(m, 3))
            {
                owner.Heal(2, m, false);
            }

            foreach (var f in owner.AllFollowers ?? new HashSet<Mobile>())
            {
                if (f != m && f.Alive && f.InRange(m, 3) && f.Hits < f.HitsMax)
                {
                    f.Heal(2, m, false);
                }
            }
        }

        // Arcane Ward Harness III: below 15% hits, the mule and its pack go to the owner's stable (the stable trainer's own
        // steps, AnimalTrainer.cs:253-267; the stable limit is respected).
        public static bool WardRecallTick(PackMule m, PlayerMobile owner)
        {
            if (TierOf(m, MuleModuleId.ArcaneWardHarness) < 3 || owner == null || m.IsStabled || m.Hits * 100 >= m.HitsMax * 15)
            {
                return false;
            }

            if (owner.Stabled != null && owner.Stabled.Count >= AnimalTrainer.GetMaxStabled(owner))
            {
                return false;
            }

            m.Rider = null;
            m.Combatant = null;
            m.ControlTarget = null;
            m.ControlOrder = OrderType.Stay;
            m.Internalize();
            m.SetControlMaster(null);
            m.IsStabled = true;
            m.StabledBy = owner;
            m.Loyalty = BaseCreature.MaxLoyalty;
            owner.AddStabled(m);
            owner.SendMessage(0x59, "Your mule's ward flares and it vanishes, pack and all, to your stable.");
            return true;
        }

        // Soul Tether I: a dead mule rises again on its own after 2 minutes (no healer, no fee).
        private static void SoulTetherTick(PackMule m, Runtime rt, DateTime now)
        {
            if (!m.IsDeadPet)
            {
                rt.DeadSince = DateTime.MinValue;
                return;
            }

            if (TierOf(m, MuleModuleId.SoulTether) == 0)
            {
                return;
            }

            if (rt.DeadSince == DateTime.MinValue)
            {
                rt.DeadSince = now;
            }
            else if (now - rt.DeadSince >= TimeSpan.FromMinutes(2))
            {
                m.ResurrectPet();
                rt.DeadSince = DateTime.MinValue;
                m.ControlMaster?.SendMessage(0x44, "Your mule's tether pulls it back to life.");
            }
        }

        // Field Smelter Rig I: smelts one ore pile in the pack a minute (Emberblood faster), with the owner within 2 tiles
        // of the unridden mule (pinned's smelt measures from the ore's location: Ore.cs:154).
        public static bool SmelterTick(PackMule m, PlayerMobile owner, Runtime rt, DateTime now)
        {
            if (!HasModule(m, MuleModuleId.FieldSmelterRig) || now < rt.NextSmelt || m.Rider != null || !owner.InRange(m, 2) ||
                m.Backpack?.FindItemByType<BaseOre>() is not { } ore)
            {
                return false;
            }

            rt.NextSmelt = now + TimeSpan.FromSeconds(60.0 * 100 / (100 + Boost(m, MuleModuleId.FieldSmelterRig)));
            new BaseOre.InternalTarget(ore) { IngotDestination = m.Backpack }.SmeltAt(owner, new StaticTarget(m.Location, 4017));
            return true;
        }

        // Timber Rack III: 10 carried logs become boards every 5 minutes.
        public static bool BoardsTick(PackMule m, Runtime rt, DateTime now)
        {
            if (TierOf(m, MuleModuleId.TimberRack) < 3 || now < rt.NextBoards || m.Backpack?.FindItemByType<Log>() is not { } log)
            {
                return false;
            }

            rt.NextBoards = now + TimeSpan.FromMinutes(5);
            var n = Math.Min(10, log.Amount);
            var resource = log.Resource;
            log.Consume(n);
            m.Backpack.DropItem(new Board(resource, n));
            return true;
        }

        // Salvage Bin II: picks up loose items near the owner that match its filter.
        public static readonly string[] PickupCategories = { "reagents", "gems", "gold", "ore and ingots", "hides and leather", "logs and boards", "bandages" };

        public static bool Matches(Item item, IReadOnlyList<string> filter) =>
            filter.Any(c => c switch
            {
                "reagents" => item is BaseReagent,
                "gems" => Array.IndexOf(Gems, item.GetType()) >= 0,
                "gold" => item is Gold,
                "ore and ingots" => item is BaseOre or BaseIngot,
                "hides and leather" => item is BaseHides or BaseLeather,
                "logs and boards" => item is Log or Board,
                "bandages" => item is Bandage,
                _ => false
            });

        public static int PickupTick(PackMule m, PlayerMobile owner, Runtime rt, DateTime now)
        {
            if (TierOf(m, MuleModuleId.SalvageBin) < 2 || now < rt.NextPickup || owner.Map == null || m.Backpack == null ||
                Installed(m).FirstOrDefault(x => x.ModuleId == MuleModuleId.SalvageBin) is not { } bin || bin.Filter.Count == 0)
            {
                return 0;
            }

            rt.NextPickup = now + TimeSpan.FromSeconds(5);
            var picked = 0;

            var near = new List<Item>();

            foreach (var i in owner.Map.GetItemsInRange(owner.Location, MuleModuleData.ValueAt(MuleModuleId.SalvageBin, 2)))
            {
                near.Add(i);
            }

            foreach (var item in near)
            {
                if (item.Parent == null && item.Movable && item.Visible && !item.IsLockedDown && !item.IsSecure && item.Spawner == null &&
                    !item.Nontransferable && item is not Corpse && Matches(item, bin.Filter) &&
                    BaseHouse.FindHouseAt(item) is null && m.Backpack.TryDropItem(owner, item, false))
                {
                    picked++;
                }
            }

            return picked;
        }

        // Minstrel's Bells I: hostiles fixed on the mule lose it now and then and wait before picking it again.
        private static void BellsTick(PackMule m)
        {
            if (!HasModule(m, MuleModuleId.MinstrelsBells) || m.Map == null)
            {
                return;
            }

            foreach (var mob in m.GetMobilesInRange(8))
            {
                if (mob is BaseCreature { Controlled: false } bc && bc.Combatant == m && Utility.RandomDouble() < 0.25)
                {
                    bc.Combatant = null;
                    bc.NextReacquireTime = Core.TickCount + 4000;
                }
            }
        }

        // Field Smelter Rig II and III: the mule counts as a forge, then also an anvil. Pinned looks for forge and anvil
        // items near the crafter (DefBlacksmithy.CheckAnvilAndForge, items only, never a mobile), so a hidden helper item
        // follows the mule. It serves anyone standing beside the mule, not only its owner (owner-only needs a pinned hook).
        public static void OnMuleMoved(PackMule m)
        {
            var rt = State(m);
            var tier = TierOf(m, MuleModuleId.FieldSmelterRig);
            var here = m.Map != null && m.Map != Map.Internal && m.Rider == null && !m.IsDeadPet && !m.Deleted;

            rt.Forge = Place(rt.Forge, here && tier >= 2, m, () => new MuleForgeSpot());
            rt.Anvil = Place(rt.Anvil, here && tier >= 3, m, () => new MuleAnvilSpot());
        }

        private static T Place<T>(T spot, bool wanted, PackMule m, Func<T> make) where T : Item
        {
            if (!wanted)
            {
                spot?.Delete();
                return null;
            }

            if (spot == null || spot.Deleted)
            {
                spot = make();
            }

            if (spot.Map != m.Map || spot.Location != m.Location)
            {
                spot.MoveToWorld(m.Location, m.Map);
            }

            return spot;
        }

        // --- Survey Saddle Roll II and III ------------------------------------------------------------------------------

        public static List<Item> TrackableMaps(PackMule m)
        {
            var list = new List<Item>();

            if (m.Backpack != null)
            {
                foreach (var i in m.Backpack.FindItems())
                {
                    if (i is TreasureMap { Completed: false } tm && tm.Decoder != null || i is SOS)
                    {
                        list.Add(i);
                    }
                }
            }

            return list;
        }

        public static Point2D? TargetOf(Item map) =>
            map switch
            {
                TreasureMap tm => tm.ChestLocation,
                SOS sos => new Point2D(sos.TargetLocation.X, sos.TargetLocation.Y),
                _ => null
            };

        /// <summary>Survey II: points the owner's quest arrow at a map (the nearest when none is given).</summary>
        public static Item Track(PackMule m, PlayerMobile owner, Item map = null)
        {
            if (TierOf(m, MuleModuleId.SurveySaddleRoll) < 2)
            {
                return null;
            }

            map ??= TrackableMaps(m).OrderBy(i => TargetOf(i) is { } p ? owner.GetDistanceToSqrt(new Point3D(p, 0)) : double.MaxValue)
                .FirstOrDefault();

            if (map == null || TargetOf(map) is not { } target)
            {
                return null;
            }

            State(m).TrackedMap = map;

            if (owner.NetState != null)
            {
                owner.QuestArrow = new QuestArrow(owner, m, target.X, target.Y);
            }

            return map;
        }

        /// <summary>Survey III: the mule digs the tracked treasure map's chest, when the owner stands within 6 tiles of the
        /// spot on its facet: no shovel, no Mining check; the map fills the chest and spawns the guardians as for any dig
        /// (TreasureMap.OnDug, cc-P127).</summary>
        public static TreasureMapChest Dig(PackMule m, PlayerMobile owner)
        {
            if (TierOf(m, MuleModuleId.SurveySaddleRoll) < 3 || State(m).TrackedMap is not TreasureMap { Completed: false, Deleted: false } tm ||
                tm.ChestMap != owner.Map || owner.GetDistanceToSqrt(new Point3D(tm.ChestLocation, owner.Z)) > 6)
            {
                return null;
            }

            var x = tm.ChestLocation.X;
            var y = tm.ChestLocation.Y;
            // cc-P127: the chest is raised empty (temporary) and the map fills it and spawns its guardians, as a
            // shovel dig does, so the revamp's quality, loot and guardian rules apply here too.
            var chest = new TreasureMapChest(owner, tm.Level, true);
            chest.MoveToWorld(new Point3D(x, y, tm.ChestMap.GetAverageZ(x, y)), tm.ChestMap);
            tm.OnDug(owner, chest);

            m.PlaySound(0x125);
            owner.QuestArrow = null;
            return chest;
        }

        // --- Trader's Ledger III and Salvage Bin I ----------------------------------------------------------------------

        /// <summary>Why the portable bank will not open here, or null. In a dungeon is DungeonRegion; in combat is any
        /// aggression in the last 30 seconds, monsters included.</summary>
        public static string RefuseBank(PlayerMobile owner, PackMule m)
        {
            if (TierOf(m, MuleModuleId.TradersLedger) < 3)
            {
                return "Your mule carries no Trader's Ledger III.";
            }

            if (!owner.Alive || m.IsDeadPet || !owner.InRange(m, 3) && owner.Mount != m)
            {
                return "Your mule must be beside you, and both of you alive.";
            }

            if (owner.Region?.IsPartOf<DungeonRegion>() == true)
            {
                return "The ledger will not open the bank in a dungeon.";
            }

            if (owner.Criminal)
            {
                return "The ledger will not open the bank for a criminal.";
            }

            var cutoff = Core.Now - TimeSpan.FromSeconds(30);

            if (owner.Combatant != null || owner.Aggressors.Any(a => a.LastCombatTime > cutoff) ||
                owner.Aggressed.Any(a => a.LastCombatTime > cutoff))
            {
                return "The ledger will not open the bank in the middle of a fight.";
            }

            return null;
        }

        /// <summary>Salvage Bin I: trashes an item for Clean Up Britannia points, as the trash bag does (TrashBag.cs:251-296).</summary>
        public static bool Trash(PlayerMobile owner, PackMule m, Item item)
        {
            if (!HasModule(m, MuleModuleId.SalvageBin) || item.Deleted ||
                !(item.IsChildOf(owner.Backpack) || item.IsChildOf(m.Backpack)) || !ClusterFCustodianSystem.IsEligible(item, false))
            {
                return false;
            }

            var points = ClusterFCustodianSystem.ComputePoints(item);
            item.Delete();
            ClusterFCustodianSystem.AwardPoints(owner, points);
            return true;
        }
    }

    /// <summary>The Field Smelter Rig's forge beside the mule: hidden, follows the mule, never saved for long.</summary>
    [Forge]
    [SerializationGenerator(0, false)]
    public partial class MuleForgeSpot : Item
    {
        public MuleForgeSpot() : base(0x0FB1)
        {
            Movable = false;
            Visible = false;
        }

        public override string DefaultName => "a field smelter";

        [AfterDeserialization]
        private void AfterDeserialization() => Timer.DelayCall(Delete); // the mule puts a new one down
    }

    [Anvil]
    [SerializationGenerator(0, false)]
    public partial class MuleAnvilSpot : Item
    {
        public MuleAnvilSpot() : base(0x0FAF)
        {
            Movable = false;
            Visible = false;
        }

        public override string DefaultName => "a field anvil";

        [AfterDeserialization]
        private void AfterDeserialization() => Timer.DelayCall(Delete);
    }

    /// <summary>F-33: from satchel tier 3, "Unload" sends a satchel's contents to the owner's mule within 12 tiles; the
    /// tier 5 ore satchel reaches the mule anywhere on the same map. Automatic sending is later (AutoUnload hook).</summary>
    public static class MuleSatchels
    {
        public const int NearRange = 12;

        /// <summary>Stock cliloc 1116072, "Unload" (no new client text needed).</summary>
        public const int UnloadCliloc = 1116072;

        /// <summary>Set later for automatic sending; called with the satchel after something is added to it.</summary>
        public static Action<PlayerMobile, Container> AutoUnload;

        public static int TierOf(Item satchel) =>
            satchel switch
            {
                MasterExpeditionSatchel => 5,
                DeepdelversSatchel => 4,
                SurveyorsSatchel => 3,
                ReinforcedOreSatchel => 2,
                CompactOreSatchel => 1,
                ArboristLumberSatchel => 3,
                SeasonedLumberSatchel => 2,
                ForestersLumberSatchel => 1,
                WardensFieldSatchel => 3,
                SeasonedHuntersSatchel => 2,
                HuntersSatchel => 1,
                _ => 0
            };

        public static PackMule ReachableMule(PlayerMobile pm, Item satchel)
        {
            var tier = TierOf(satchel);
            return tier < 3 ? null : MuleModules.MuleOf(pm, tier >= 5 ? -1 : NearRange);
        }

        public static void AddUnloadEntry(Container satchel, Mobile from, ref Server.Collections.PooledRefList<Server.ContextMenus.ContextMenuEntry> list)
        {
            if (from is PlayerMobile pm && from.Alive && TierOf(satchel) >= 3 && satchel.IsChildOf(from.Backpack))
            {
                list.Add(new UnloadEntry(satchel, ReachableMule(pm, satchel) != null));
            }
        }

        /// <summary>Moves what fits into the mule's pack; returns how many items moved.</summary>
        public static int Unload(PlayerMobile pm, Container satchel)
        {
            if (ReachableMule(pm, satchel) is not { Backpack: { } pack })
            {
                pm.SendMessage(0x22, "Your mule is not within reach.");
                return 0;
            }

            var moved = 0;

            foreach (var item in satchel.Items.ToList())
            {
                if (pack.TryDropItem(pm, item, false))
                {
                    moved++;
                }
            }

            pm.SendMessage(moved > 0 ? 0x44 : 0x22, moved > 0 ? $"{moved} item(s) sent to your mule." : "Nothing fit in your mule's pack.");
            return moved;
        }

        private sealed class UnloadEntry : Server.ContextMenus.ContextMenuEntry
        {
            private readonly Container _satchel;

            public UnloadEntry(Container satchel, bool enabled) : base(UnloadCliloc, 2)
            {
                _satchel = satchel;
                Enabled = enabled;
            }

            public override void OnClick(Mobile from, IEntity target)
            {
                if (from is PlayerMobile pm && !_satchel.Deleted)
                {
                    Unload(pm, _satchel);
                }
            }
        }
    }
}

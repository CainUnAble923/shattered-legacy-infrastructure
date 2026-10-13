using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Targeting;

namespace Server.Mobiles
{
    // -------------------------------------------------------------------------------------------------------------------
    // The Compact Mule (F-33): the Miners' Compact's rideable pack animal and the platform for the guilds' modules.
    //
    // cc-P69: five tiers (MuleData.Tiers: HP, pack, resists, module slots, the metal's hue), a bloodline (stat quality,
    // up to three graded traits, lineage), breeding that passes strong parents' traits on, and 1 follower slot. The base
    // mule comes from a League errand (MuleErrand); a tier-up path replaces the Civic Mule Exchange's sale (MuleUpgrades).
    // Every number the mule shows is computed in one place, Compute(), which the module prompt adds to (ModifyNumbers).
    //
    // Body 0x123 (291) is the pack horse; ItemID 0x3EA0 the horse shown under a rider. Notes:
    // shard-migration notes/cc-P69-mule-core.md (and cc-P47 sections 5 and 6 for the history).
    // -------------------------------------------------------------------------------------------------------------------

    /// <summary>What Compute() works out for a mule: everything a module may add to.</summary>
    public sealed class MuleNumbers
    {
        public int HitsMax, Str, Dex, Stam;
        public int PackWeight, PackItems;
        public int Physical, Fire, Cold, Poison, Energy;
        public double MagicResist, Tactics, Wrestling;
        public int DamageMin, DamageMax, VirtualArmor;

        /// <summary>Follower slots the mule costs. Trailwise Tack III (the modules prompt) sets it to 0.</summary>
        public int SlotCost = 1;

        /// <summary>Damage taken in a dungeon is scaled by this (Deepdelver lowers it).</summary>
        public double DungeonDamageScale = 1.0;

        /// <summary>The one check for the pack on death: false drops it to the corpse (Chase: "drop, until a gear
        /// upgrade"). Soul Tether II or a barding upgrade sets it true.</summary>
        public bool KeepPackOnDeath;

        // cc-P70: the modules' numbers (MuleModules.Apply).
        /// <summary>Percent less damage taken (Expedition Barding).</summary>
        public int DamageReduction;

        /// <summary>Stamina regen points, added in RegenRates (Trailwise Tack, the Imbued Barding Socket).</summary>
        public int StamRegen;

        /// <summary>Hits healed every module tick (two seconds) out of combat (the Imbued Barding Socket).</summary>
        public int HitsRegen;
    }

    [SerializationGenerator(1, false)]
    public partial class PackMule : BaseMount
    {
        // --- saved -------------------------------------------------------------------------------------------------

        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private bool _isMale;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private DateTime _nextBreedTime;

        [SerializableField(2, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _tier;

        [SerializableField(3, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _qualityHits;

        [SerializableField(4, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _qualityStr;

        [SerializableField(5, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _qualityStam;

        [SerializableField(6, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _qualityResist;

        [SerializableField(7, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _qualityPack;

        // id * 10 + grade (MuleGenes.EncodeTraits), at most three.
        [SerializableField(8, setter: "private")]
        private int[] _traits;

        [SerializableField(9)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private string _familyName;

        [SerializableField(10, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _generation;

        [SerializableField(11, setter: "private")]
        private uint _parent1;

        [SerializableField(12, setter: "private")]
        private uint _parent2;

        [SerializableField(13, setter: "private")]
        private int _inheritedTier5;

        // The modules the module prompt attaches; empty until then.
        [SerializableField(14, setter: "private")]
        private List<Item> _modules;

        // --- construction --------------------------------------------------------------------------------------------

        [Constructible]
        public PackMule() : this(1, null)
        {
        }

        [Constructible]
        public PackMule(int tier) : this(tier, null)
        {
        }

        public PackMule(int tier, MuleGenes genes) : base(0x123, 0x3EA0, AIType.AI_Animal, FightMode.Aggressor)
        {
            BaseSoundID = 0xA8;
            Fame = 0;
            Karma = 500;
            Tamable = true;
            MinTameSkill = 0.0; // deed-only: never in the wild
            SetMana(0);
            SetDamageType(ResistanceType.Physical, 100);

            _tier = Math.Clamp(tier, MuleData.MinTier, MuleData.MaxTier);
            _modules = new List<Item>();
            SetGenes(genes ?? MuleGenes.Fresh());
            _nextBreedTime = DateTime.MinValue; // ready to breed at once

            // Replace the default backpack with the mule's pack (its capacity follows the mule: PackMuleBackpack).
            Backpack?.Delete();
            AddItem(new PackMuleBackpack());

            ApplyNumbers(true);
        }

        // --- identity and display ------------------------------------------------------------------------------------

        public MuleTier TierInfo => MuleData.Tier(_tier);

        public override string CorpseName => "a mule corpse";
        public override string DefaultName => $"a {TierInfo.Name} mule";

        public override int Meat => 4;
        public override int Hides => 15;
        public override FoodType FavoriteFood => FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;

        public int ModuleSlots => TierInfo.ModuleSlots;

        public bool CanBreedNow => !IsDeadPet && DateTime.UtcNow >= _nextBreedTime;
        public string GenderLabel => _isMale ? "male" : "female";


        /// <summary>Tier-5 generations in a row up to and including this mule.</summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public int LineTier5Generations => _inheritedTier5 + (_tier == MuleData.MaxTier ? 1 : 0);

        [CommandProperty(AccessLevel.GameMaster)]
        public bool IsLegendaryLine => LineTier5Generations >= MuleData.LegendaryGenerations;

        public MuleGenes Genes =>
            new()
            {
                IsMale = _isMale,
                QualityHits = _qualityHits,
                QualityStr = _qualityStr,
                QualityStam = _qualityStam,
                QualityResist = _qualityResist,
                QualityPack = _qualityPack,
                Traits = MuleGenes.DecodeTraits(_traits),
                FamilyName = _familyName,
                Generation = _generation,
                Parent1 = _parent1,
                Parent2 = _parent2,
                InheritedTier5 = _inheritedTier5
            };

        public void SetGenes(MuleGenes g)
        {
            _isMale = g.IsMale;
            _qualityHits = Math.Clamp(g.QualityHits, 0, 100);
            _qualityStr = Math.Clamp(g.QualityStr, 0, 100);
            _qualityStam = Math.Clamp(g.QualityStam, 0, 100);
            _qualityResist = Math.Clamp(g.QualityResist, 0, 100);
            _qualityPack = Math.Clamp(g.QualityPack, 0, 100);
            _traits = g.EncodeTraits();
            _familyName = g.FamilyName;
            _generation = g.Generation;
            _parent1 = g.Parent1;
            _parent2 = g.Parent2;
            _inheritedTier5 = g.InheritedTier5;
            this.MarkDirty();
        }

        public int GradeOf(MuleTrait id) => Genes.GradeOf(id);

        [CommandProperty(AccessLevel.GameMaster)]
        public string TraitsText => Genes.TraitsText();

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add($"Tier {_tier}: {TierInfo.Name} Mule ({GenderLabel})");
            list.Add($"Quality: hits {_qualityHits}%, strength {_qualityStr}%, stamina {_qualityStam}%, resists {_qualityResist}%, pack {_qualityPack}%");
            list.Add($"Bloodline: {TraitsText}");

            if (!string.IsNullOrEmpty(_familyName) || _generation > 0)
            {
                list.Add($"{(_familyName ?? "No family")} line, generation {_generation}{(IsLegendaryLine ? ", Legendary" : "")}");
            }

            list.Add($"Module slots: {Modules.Count} / {ModuleSlots}");

            foreach (var item in Modules)
            {
                if (item is MuleModule module)
                {
                    list.Add($"Module: {module.Name}");
                }
            }
        }

        // --- the one place every number comes from -------------------------------------------------------------------

        /// <summary>The modules prompt adds to the numbers here (more weight, resists, slot cost 0, keep pack on death).</summary>
        public static event Action<PackMule, MuleNumbers> ModifyNumbers;

        private static int Place(int min, int max, int quality) => min + (max - min) * Math.Clamp(quality, 0, 100) / 100;

        private static int Resist(MuleTier t, double shape, int quality) =>
            (int)Math.Round(t.ResistMin + (t.ResistMax - t.ResistMin) * (shape / 2 + Math.Clamp(quality, 0, 100) / 200.0));

        /// <summary>Tier + quality + traits (+ modules, through ModifyNumbers).</summary>
        public static MuleNumbers Compute(PackMule m)
        {
            var t = m.TierInfo;
            var g = m.Genes;

            var n = new MuleNumbers
            {
                HitsMax = Place(t.HitsMin, t.HitsMax, g.QualityHits),
                Str = Place(t.StrMin, t.StrMax, g.QualityStr),
                Dex = Place(t.DexMin, t.DexMax, g.QualityStam),
                Stam = Place(t.StamMin, t.StamMax, g.QualityStam),
                PackWeight = Place(t.PackWeight * 9 / 10, t.PackWeight, g.QualityPack),
                PackItems = Place(t.PackItems * 9 / 10, t.PackItems, g.QualityPack),
                Physical = Resist(t, MuleData.PhysicalShape, g.QualityResist),
                Fire = Resist(t, MuleData.FireShape, g.QualityResist),
                Cold = Resist(t, MuleData.ColdShape, g.QualityResist),
                Poison = Resist(t, MuleData.PoisonShape, g.QualityResist),
                Energy = Resist(t, MuleData.EnergyShape, g.QualityResist),
                MagicResist = t.Skill,
                Tactics = t.Skill,
                Wrestling = t.Skill,
                DamageMin = t.DamageMin,
                DamageMax = t.DamageMax,
                VirtualArmor = t.VirtualArmor
            };

            foreach (var (id, grade) in g.Traits)
            {
                var v = MuleData.TraitValue(id, grade);

                switch (id)
                {
                    case MuleTrait.Burdened:
                        n.PackWeight += n.PackWeight * v / 100;
                        n.PackItems += n.PackItems * v / 100;
                        break;
                    case MuleTrait.Stoneback:
                        n.Physical += v;
                        break;
                    case MuleTrait.Emberblood:
                        n.Fire += v;
                        break;
                    case MuleTrait.Surefooted:
                        n.Stam += n.Stam * v / 100;
                        break;
                    case MuleTrait.Deepdelver:
                        n.DungeonDamageScale *= (100 - v) / 100.0;
                        break;
                    case MuleTrait.NightmareKin:
                        n.MagicResist += v;
                        break;
                    case MuleTrait.CelestialTempered:
                        n.Physical += v;
                        n.Fire += v;
                        n.Cold += v;
                        n.Poison += v;
                        n.Energy += v;
                        n.HitsMax += n.HitsMax * v / 100;
                        break;
                }
            }

            ModifyNumbers?.Invoke(m, n);

            n.Physical = Math.Min(n.Physical, MuleData.ResistCap);
            n.Fire = Math.Min(n.Fire, MuleData.ResistCap);
            n.Cold = Math.Min(n.Cold, MuleData.ResistCap);
            n.Poison = Math.Min(n.Poison, MuleData.ResistCap);
            n.Energy = Math.Min(n.Energy, MuleData.ResistCap);
            n.SlotCost = Math.Max(0, n.SlotCost);
            return n;
        }

        public MuleNumbers Numbers => Compute(this);

        public int PackWeightCap => Numbers.PackWeight;
        public int PackItemsCap => Numbers.PackItems;

        /// <summary>Sets body hue, stats, resists, skills and slot cost from Compute(). heal: also fills hits and stamina.</summary>
        public void ApplyNumbers(bool heal)
        {
            var n = Compute(this);

            Hue = TierInfo.Hue;

            RawStr = n.Str;
            RawDex = n.Dex;
            RawInt = 100;
            HitsMaxSeed = n.HitsMax;
            StamMaxSeed = n.Stam;

            SetDamage(n.DamageMin, n.DamageMax);
            SetResistance(ResistanceType.Physical, n.Physical);
            SetResistance(ResistanceType.Fire, n.Fire);
            SetResistance(ResistanceType.Cold, n.Cold);
            SetResistance(ResistanceType.Poison, n.Poison);
            SetResistance(ResistanceType.Energy, n.Energy);
            SetSkill(SkillName.MagicResist, n.MagicResist);
            SetSkill(SkillName.Tactics, n.Tactics);
            SetSkill(SkillName.Wrestling, n.Wrestling);
            VirtualArmor = n.VirtualArmor;

            SetSlotCost(n.SlotCost);

            if (heal)
            {
                Hits = HitsMax;
                Stam = StamMax;
            }
            else
            {
                Hits = Math.Min(Hits, HitsMax);
                Stam = Math.Min(Stam, StamMax);
            }

            InvalidateProperties();
        }

        /// <summary>Changes the follower slot cost and keeps the owner's count right (pinned has no hook on ControlSlots:
        /// remove the followers at the old cost, set it, add them at the new one; cc-P47 6).</summary>
        public void SetSlotCost(int cost)
        {
            if (ControlSlots == cost)
            {
                return;
            }

            var owner = Controlled ? ControlMaster : null;
            owner = owner is { Deleted: false } ? owner : null;

            if (owner != null)
            {
                RemoveFollowers();
            }

            ControlSlots = cost;

            if (owner != null)
            {
                AddFollowers();
            }
        }

        /// <summary>One tier up, keeping quality, traits, lineage, name, bond and pack. False at tier 5.</summary>
        public bool TierUp()
        {
            if (_tier >= MuleData.MaxTier)
            {
                return false;
            }

            Tier = _tier + 1;
            ApplyNumbers(true);
            return true;
        }

        /// <summary>Staff: sets the tier directly.</summary>
        public void StaffSetTier(int tier)
        {
            Tier = Math.Clamp(tier, MuleData.MinTier, MuleData.MaxTier);
            ApplyNumbers(true);
        }

        public void SetBreedCooldown(DateTime until)
        {
            NextBreedTime = until;
        }

        // Every kind of damage reaches Mobile.Damage (melee, spells, poison, AOS.Damage; pinned Mobile.cs:5956), so the
        // scaling is applied once, here: Deepdelver in a dungeon, and Expedition Barding (cc-P70). Before cc-P70 the
        // dungeon scale sat in the melee and spell hooks only.
        public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
        {
            base.Damage(MuleModules.ScaleIncoming(this, amount), from, informMount, ignoreEvilOmen);
        }

        // Arcane Ward Harness II: the ward absorbs one damage spell, then recharges.
        public override void AlterSpellDamageFrom(Mobile from, ref int damage)
        {
            base.AlterSpellDamageFrom(from, ref damage);
            MuleModules.WardAbsorb(this, ref damage);
        }

        public override void OnDamage(int amount, Mobile from, bool willKill)
        {
            base.OnDamage(amount, from, willKill);
            MuleModules.OnMuleDamaged(this, from, willKill);
        }

        // Shadow Cloak II: a cloaked mule walking beside its stealthing owner stays hidden (base Mobile.OnMove reveals).
        protected override bool OnMove(Direction d) =>
            Hidden && MuleModules.StaysHiddenMoving(this, d) || base.OnMove(d);

        // Soul Tether III: no skill loss on resurrection (the loss is taken by the caller after ResurrectPet returns).
        private double[] _skillsBeforeRes;

        /// <summary>The skills saved at the last resurrection with Soul Tether III (null otherwise).</summary>
        public double[] SkillsBeforeResurrect => _skillsBeforeRes;

        public override void OnBeforeResurrect()
        {
            base.OnBeforeResurrect();
            _skillsBeforeRes = MuleModules.TierOf(this, MuleModuleId.SoulTether) >= 3
                ? new double[Skills.Length]
                : null;

            if (_skillsBeforeRes != null)
            {
                for (var i = 0; i < Skills.Length; i++)
                {
                    _skillsBeforeRes[i] = Skills[i].Base;
                }
            }
        }

        public override void OnAfterResurrect()
        {
            base.OnAfterResurrect();

            if (_skillsBeforeRes is { } saved)
            {
                _skillsBeforeRes = null;
                Timer.DelayCall(() => RestoreSkills(saved));
            }
        }

        public void RestoreSkills(double[] saved)
        {
            for (var i = 0; i < Skills.Length && i < saved.Length; i++)
            {
                if (Skills[i].Base < saved[i])
                {
                    Skills[i].Base = saved[i];
                }
            }
        }

        protected override void OnLocationChange(Point3D oldLocation)
        {
            base.OnLocationChange(oldLocation);
            MuleModules.OnMuleMoved(this);
        }

        protected override void OnMapChange(Map oldMap)
        {
            base.OnMapChange(oldMap);
            MuleModules.OnMuleMoved(this);
        }

        public override void OnAfterDelete()
        {
            MuleModules.OnMuleDeleted(this);
            base.OnAfterDelete();
        }

        // --- migration from v0 (before cc-P69) ------------------------------------------------------------------------

        private bool _needsApply;

        // A v0 mule had today's stats, which are tier 3's range exactly (HP 250-310): it becomes tier 3. Its HP quality is
        // where its HP sat in that range; the other qualities are rolled; no traits, generation 0, no parents.
        private void MigrateFrom(V0Content content)
        {
            _isMale = content.IsMale;
            _nextBreedTime = content.NextBreedTime;

            var g = MuleGenes.Fresh();
            g.IsMale = content.IsMale;

            _tier = 3;
            _qualityHits = -1; // set from its hits once they are read (AfterDeserialization)
            _qualityStr = g.QualityStr;
            _qualityStam = g.QualityStam;
            _qualityResist = g.QualityResist;
            _qualityPack = g.QualityPack;
            _traits = Array.Empty<int>();
            _modules = new List<Item>();
            _needsApply = true;
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            _modules ??= new List<Item>();
            _traits ??= Array.Empty<int>();
            MuleModules.Track(this);

            if (_qualityHits < 0)
            {
                var t = MuleData.Tier(3);
                _qualityHits = Math.Clamp((HitsMax - t.HitsMin) * 100 / Math.Max(1, t.HitsMax - t.HitsMin), 0, 100);
            }

            // Any mule whose slot cost or numbers are out of date (every v0 mule: 2 slots then) is set right once the world
            // has loaded, owner online or not: Followers is saved on the owner and RemoveFollowers / AddFollowers work on it
            // either way. A stabled mule has no ControlMaster; its cost is simply set, and counts when it is claimed.
            if (_needsApply || ControlSlots != Compute(this).SlotCost)
            {
                Timer.DelayCall(() =>
                    {
                        if (!Deleted)
                        {
                            ApplyNumbers(false);
                        }
                    }
                );
            }
        }

        // --- double-click: mount, or open the pack while riding -------------------------------------------------------

        public override void OnDoubleClick(Mobile from)
        {
            if (Rider == from)
            {
                PackAnimal.TryPackOpen(this, from);
                return;
            }

            base.OnDoubleClick(from);
        }

        // --- drag-drop: a guild satchel's contents go into the pack ---------------------------------------------------

        public override bool OnDragDrop(Mobile from, Item item)
        {
            if (CheckFeed(from, item))
            {
                return true;
            }

            if (!PackAnimal.CheckAccess(this, from))
            {
                return base.OnDragDrop(from, item);
            }

            if (item is CompactOreSatchel or HuntersSatchel or ForestersLumberSatchel)
            {
                var satchel = (Container)item;
                var mulePack = Backpack;

                if (mulePack == null)
                {
                    return false;
                }

                var moved = 0;
                var skipped = 0;

                for (var i = satchel.Items.Count - 1; i >= 0; i--)
                {
                    var content = satchel.Items[i];

                    if (mulePack.CheckHold(from, content, false, true))
                    {
                        mulePack.DropItem(content);
                        moved++;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                if (moved > 0)
                {
                    from.SendMessage(0x44, $"{moved} item{(moved == 1 ? "" : "s")} transferred from your satchel to the mule's pack.");
                }
                else if (skipped > 0)
                {
                    from.SendMessage(0x22, "Nothing could fit in the mule's pack.");
                }
                else
                {
                    from.SendMessage(0x59, "That satchel is already empty.");
                }

                return false; // the satchel itself stays with the player
            }

            // cc-P70: a module dropped on the mule is attached (refused, it goes back to the player, never deleted).
            if (item is MuleModule module)
            {
                return MuleModules.TryAttach(from, this, module);
            }

            AddToBackpack(item);
            return true;
        }

        // --- context menu ---------------------------------------------------------------------------------------------

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);
            PackAnimal.GetContextMenuEntries(this, from, ref list);

            if (from is PlayerMobile pm && pm == ControlMaster && !IsDeadPet)
            {
                list.Add(new PackMuleBreedEntry(this));
                list.Add(new MuleOptionsEntry(this)); // cc-P70: the module actions
            }
        }

        // --- pack animal plumbing; the pack on death -------------------------------------------------------------------

        public override bool OnBeforeDeath()
        {
            Rider = null;

            if (!base.OnBeforeDeath())
            {
                return false;
            }

            if (!Compute(this).KeepPackOnDeath)
            {
                PackAnimal.CombineBackpacks(this);
            }

            return true;
        }

        public override DeathMoveResult GetInventoryMoveResultFor(Item item) =>
            Compute(this).KeepPackOnDeath ? DeathMoveResult.MoveToBackpack : DeathMoveResult.MoveToCorpse;

        public override bool IsSnoop(Mobile from) => !PackAnimal.CheckAccess(this, from) && base.IsSnoop(from);

        public override bool CheckNonlocalDrop(Mobile from, Item item, Item target) => PackAnimal.CheckAccess(this, from);

        public override bool CheckNonlocalLift(Mobile from, Item item) => PackAnimal.CheckAccess(this, from);

        // --- getting one: one summon path for every deed and the errand -------------------------------------------------

        /// <summary>
        /// A bonded mule at the player's feet (deeds and the League errand). Null, with the reason said, if the player
        /// is dead or has no free slot for it.
        /// </summary>
        public static PackMule SummonFor(PlayerMobile pm, int tier, MuleGenes genes, string arrival)
        {
            if (!pm.Alive)
            {
                pm.SendMessage(0x22, "You cannot summon a pack mule while dead.");
                return null;
            }

            var mule = new PackMule(tier, genes);

            if (pm.Followers + mule.ControlSlots > pm.FollowersMax)
            {
                mule.Delete();
                pm.SendMessage(0x22, "You have too many followers to summon your pack mule. Stable or dismiss a current follower first.");
                return null;
            }

            if (!mule.SetControlMaster(pm))
            {
                mule.Delete();
                return null;
            }

            mule.Owners.Add(pm);
            mule.IsBonded = true;
            mule.BondingBegin = DateTime.UtcNow;
            mule.OwnerAbandonTime = DateTime.MinValue;
            mule.ControlTarget = pm;
            mule.ControlOrder = OrderType.Follow;
            mule.MoveToWorld(pm.Location, pm.Map);

            pm.SendMessage(0x44, arrival);
            pm.PlaySound(0xA8);
            return mule;
        }

        // --- breeding context-menu entry -------------------------------------------------------------------------------

        internal sealed class PackMuleBreedEntry : ContextMenuEntry
        {
            private readonly PackMule _mule;

            // cc-P57 Part D: our own cliloc "Breed" (ShardClilocs), which the player package adds to the client.
            public const int Cliloc = ShardClilocs.Breed;

            public PackMuleBreedEntry(PackMule mule) : base(Cliloc, 2)
            {
                _mule = mule;
                Enabled = mule.CanBreedNow;
            }

            public override void OnClick(Mobile from, IEntity target)
            {
                if (target is not PackMule mule || mule.Deleted || mule.IsDeadPet)
                {
                    return;
                }

                if (!mule.CanBreedNow)
                {
                    var rem = mule._nextBreedTime - DateTime.UtcNow;
                    from.SendMessage(0x22, $"This mule cannot breed for another {(int)rem.TotalDays}d {rem.Hours}h.");
                    return;
                }

                from.SendMessage(0x59, $"This mule is {mule.GenderLabel}. Target the {(mule._isMale ? "female" : "male")} pack mule to breed with.");
                from.Target = new PackMuleBreedTarget(mule, (PlayerMobile)from);
            }
        }

        private sealed class PackMuleBreedTarget : Target
        {
            private readonly PackMule _initiator;
            private readonly PlayerMobile _owner;

            public PackMuleBreedTarget(PackMule initiator, PlayerMobile owner) : base(10, false, TargetFlags.None)
            {
                _initiator = initiator;
                _owner = owner;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is not PackMule mate)
                {
                    from.SendMessage(0x22, "That is not a pack mule.");
                    return;
                }

                var refusal = PackMuleBreedingSystem.CanBreed(_initiator, mate, _owner);

                if (refusal != null)
                {
                    from.SendMessage(0x22, refusal);
                    return;
                }

                PackMuleBreedingSystem.StartBreeding(_initiator, mate, _owner);
            }
        }
    }
}

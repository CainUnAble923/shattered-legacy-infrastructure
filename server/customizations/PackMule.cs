using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Mobiles
{
    // ─────────────────────────────────────────────────────────────────────────────
    // Pack Mule — ClusterF cross-guild prestige mount + pack animal
    //
    // Awarded via PackMuleDeed obtained from the Cross-Guild Exchange (all four
    // trade guild currencies).  Bred offspring can also be obtained through the
    // in-game breeding mechanic (two mules, one male + one female, 7-day cooldown,
    // 24-hour gestation).
    //
    // Key traits:
    //   • Ridable (extends BaseMount — Body 0x123, ItemID 0x3EA0 horse mount)
    //   • Nightmare / Drake tier stats (HP 250–310, high resists, 2 control slots)
    //   • PackMuleBackpack: 4,000 stone / 400 items
    //   • Drop a guild satchel onto the mule → its contents are transferred to the
    //     mule's pack, leaving the empty satchel in the player's possession
    //   • Right-click → "Breed" context entry when breeding is ready
    //
    // Hue 2500 (grey-brown) distinguishes it from a plain PackHorse.
    // Body 0x123 = 291 = the same pack-horse body; ItemID 0x3EA0 = standard horse
    // mount graphic shown while the player is riding.
    // ─────────────────────────────────────────────────────────────────────────────

    [SerializationGenerator(0, false)]
    public partial class PackMule : BaseMount
    {
        // ── Breeding constants ────────────────────────────────────────────────────

        private static readonly TimeSpan BreedingCooldown = TimeSpan.FromDays(7);

        // ── Serialized fields ─────────────────────────────────────────────────────

        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private bool _isMale;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private DateTime _nextBreedTime;

        // ── Constructor ───────────────────────────────────────────────────────────

        [Constructible]
        public PackMule() : base(0x123, 0x3EA0, AIType.AI_Animal, FightMode.Aggressor)
        {
            // Body  0x123 (291) = pack-horse body (same shape as PackHorse when dismounted)
            // Hue   2500       = grey-brown to distinguish from unmodified PackHorse
            // ItemID 0x3EA0    = horse mount graphic shown on the rider
            Hue         = 2500;
            BaseSoundID = 0xA8;

            // Nightmare / Drake tier — tough pack animal, not a combat machine
            SetStr(400, 450);
            SetDex(86, 105);
            SetInt(86, 125);

            SetHits(250, 310);
            SetStam(120, 150);
            SetMana(0);

            SetDamage(10, 14);

            SetDamageType(ResistanceType.Physical, 100);

            SetResistance(ResistanceType.Physical, 55, 65);
            SetResistance(ResistanceType.Fire,     20, 30);
            SetResistance(ResistanceType.Cold,     30, 40);
            SetResistance(ResistanceType.Poison,   30, 40);
            SetResistance(ResistanceType.Energy,   20, 30);

            SetSkill(SkillName.MagicResist, 80.1, 95.0);
            SetSkill(SkillName.Tactics,     80.1, 95.0);
            SetSkill(SkillName.Wrestling,   80.1, 95.0);

            Fame         = 0;
            Karma        = 500;
            VirtualArmor = 55;

            Tamable      = true;
            ControlSlots = 2;      // costs 2 slots like a Nightmare
            MinTameSkill = 0.0;    // deed-only — not tamable in the wild

            _isMale        = Utility.RandomBool();
            _nextBreedTime = DateTime.MinValue; // ready to breed immediately

            // Replace default backpack with the high-capacity mule pack.
            var existingPack = Backpack;
            existingPack?.Delete();

            var mulepack = new PackMuleBackpack();
            AddItem(mulepack);
        }

        // ── Identity ──────────────────────────────────────────────────────────────

        public override string CorpseName  => "a mule corpse";
        public override string DefaultName => "a pack mule";

        public override int      Meat         => 4;
        public override int      Hides        => 15;
        public override FoodType FavoriteFood => FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;

        // ── Breeding helpers ──────────────────────────────────────────────────────

        public bool   CanBreedNow  => !IsDeadPet && DateTime.UtcNow >= _nextBreedTime;
        public string GenderLabel  => _isMale ? "male" : "female";

        /// <summary>Called by <see cref="PackMuleBreedingSystem"/> to stamp the post-breed cooldown.</summary>
        public void SetBreedCooldown(DateTime until)
        {
            _nextBreedTime = until;
            this.MarkDirty();
        }

        // ── Double-click: mount or (if already riding) open pack ──────────────────

        public override void OnDoubleClick(Mobile from)
        {
            if (Rider == from)
            {
                // Rider opens the cargo pack while mounted.
                PackAnimal.TryPackOpen(this, from);
                return;
            }

            base.OnDoubleClick(from); // standard mount behaviour
        }

        // ── Drag-drop: guild satchel → dump contents into mule pack ──────────────

        public override bool OnDragDrop(Mobile from, Item item)
        {
            if (CheckFeed(from, item))
                return true;

            if (!PackAnimal.CheckAccess(this, from))
                return base.OnDragDrop(from, item);

            // Guild-satchel dump: if the player drops a guild satchel onto the
            // mule, transfer the satchel's contents without taking the satchel.
            if (item is CompactOreSatchel
             || item is HuntersSatchel
             || item is ForestersLumberSatchel)
            {
                var satchel  = (Container)item;
                var mulePack = Backpack;
                if (mulePack == null) return false;

                var moved   = 0;
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
                    from.SendMessage(0x44,
                        $"{moved} item{(moved == 1 ? "" : "s")} transferred from your satchel to the mule's pack.");
                else if (skipped > 0)
                    from.SendMessage(0x22, "Nothing could fit in the mule's pack.");
                else
                    from.SendMessage(0x59, "That satchel is already empty.");

                return false; // return false = don't actually move the satchel itself
            }

            AddToBackpack(item);
            return true;
        }

        // ── Context menu ──────────────────────────────────────────────────────────

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);

            // "Open Backpack" — standard pack animal entry
            PackAnimal.GetContextMenuEntries(this, from, ref list);

            // "Breed" — only shown to the owner when a cooldown isn't active
            if (from is PlayerMobile pm && pm == ControlMaster && !IsDeadPet)
            {
                list.Add(new PackMuleBreedEntry(this));
            }
        }

        // ── Pack animal plumbing ──────────────────────────────────────────────────

        public override bool OnBeforeDeath()
        {
            Rider = null; // force dismount before death processing
            if (!base.OnBeforeDeath()) return false;
            PackAnimal.CombineBackpacks(this);
            return true;
        }

        public override DeathMoveResult GetInventoryMoveResultFor(Item item) =>
            DeathMoveResult.MoveToCorpse;

        public override bool IsSnoop(Mobile from) =>
            !PackAnimal.CheckAccess(this, from) && base.IsSnoop(from);

        public override bool CheckNonlocalDrop(Mobile from, Item item, Item target) =>
            PackAnimal.CheckAccess(this, from);

        public override bool CheckNonlocalLift(Mobile from, Item item) =>
            PackAnimal.CheckAccess(this, from);

        // ── Breeding context-menu entry ───────────────────────────────────────────

        private sealed class PackMuleBreedEntry : ContextMenuEntry
        {
            private readonly PackMule _mule;

            // Cliloc 6131 = "Tame" (stock UO string — closest readable option for breed)
            // The breeding action is initiated here; the label is imperfect but visible.
            public PackMuleBreedEntry(PackMule mule) : base(6131, 2)
            {
                _mule   = mule;
                Enabled = mule.CanBreedNow;
            }

            public override void OnClick(Mobile from, IEntity target)
            {
                if (target is not PackMule mule || mule.Deleted || mule.IsDeadPet)
                    return;

                if (!mule.CanBreedNow)
                {
                    var rem = mule._nextBreedTime - DateTime.UtcNow;
                    from.SendMessage(0x22,
                        $"This mule cannot breed for another {(int)rem.TotalDays}d {rem.Hours}h.");
                    return;
                }

                from.SendMessage(0x59,
                    $"This mule is {mule.GenderLabel}. " +
                    $"Target the {(mule._isMale ? "female" : "male")} pack mule to breed with.");
                from.Target = new PackMuleBreedTarget(mule, (PlayerMobile)from);
            }
        }

        // ── Breeding target ───────────────────────────────────────────────────────

        private sealed class PackMuleBreedTarget : Target
        {
            private readonly PackMule    _initiator;
            private readonly PlayerMobile _owner;

            public PackMuleBreedTarget(PackMule initiator, PlayerMobile owner)
                : base(10, false, TargetFlags.None)
            {
                _initiator = initiator;
                _owner     = owner;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is not PackMule mate)
                {
                    from.SendMessage(0x22, "That is not a pack mule.");
                    return;
                }
                if (mate == _initiator)
                {
                    from.SendMessage(0x22, "A mule cannot breed with itself.");
                    return;
                }
                if (mate.ControlMaster != _owner)
                {
                    from.SendMessage(0x22, "You can only breed mules that you own.");
                    return;
                }
                if (mate._isMale == _initiator._isMale)
                {
                    from.SendMessage(0x22,
                        $"Both mules are {_initiator.GenderLabel} — you need one male and one female.");
                    return;
                }
                if (!mate.CanBreedNow)
                {
                    var rem = mate._nextBreedTime - DateTime.UtcNow;
                    from.SendMessage(0x22,
                        $"That mule cannot breed for another {(int)rem.TotalDays}d {rem.Hours}h.");
                    return;
                }
                if (mate.IsDeadPet)
                {
                    from.SendMessage(0x22, "That mule is dead.");
                    return;
                }

                // All checks passed — hand off to the breeding system.
                PackMuleBreedingSystem.StartBreeding(_initiator, mate, _owner);
            }
        }
    }
}

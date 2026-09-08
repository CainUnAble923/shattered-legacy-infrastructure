using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.ConPVP;
using Server.Engines.Harvest;
using Server.Network;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishBattleAxe.cs (CC9 batch 3). Based Off Battle Axe.
    // Derives from BaseSetWeapon, not BaseAxe: GargishPincer (Items/Artifacts/Equipment/Weapons/Pincer.cs) assigns SetSkillBonuses,
    // and a set carrier cannot be inserted above a type once an instance of it exists in a save
    // (AGENTS.md, "Give a base its final parent the first time you port it"). SetID is None, so the
    // carrier is inert here.
    // See shard-migration/notes/cc9-artifacts.md section 13.
    //
    // CC9 batch 4 correction: batch 3 re-parented this without reproducing BaseAxe's own members, so it
    // swung as a one-handed slashing sword (Slash1H, sword sounds) and could not chop wood. Stock
    // BaseAxe (pinned Items/Weapons/Axes/BaseAxe.cs) is reproduced below, statement for statement, the
    // way every other carrier child in batch 4 reproduces its stock parent. No instance existed in any
    // save when the two uses-remaining fields were added (notes/cc9-artifacts.md section 14).
    [Flippable(0x48B0, 0x48B1)]
    [SerializationGenerator(0, false)]
    public partial class GargishBattleAxe : BaseSetWeapon, IUsesRemaining
    {
        [SerializableField(0)]
        [InvalidateProperties]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private bool _showUsesRemaining;

        [SerializableField(1)]
        [InvalidateProperties]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _usesRemaining;

        [Constructible]
        public GargishBattleAxe() : base(0x48B0)
        {
            Layer = Layer.TwoHanded;
            _usesRemaining = 150;
        }

        public override double DefaultWeight => 4.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.BleedAttack;
        public override WeaponAbility SecondaryAbility => WeaponAbility.ConcussionBlow;

        public override int AosStrengthReq => 35;
        public override int AosMinDamage => 16;
        public override int AosMaxDamage => 19;
        public override int AosSpeed => 31;
        public override float MlSpeed => 3.50f;

        public override int OldStrengthReq => 40;
        public override int OldMinDamage => 6;
        public override int OldMaxDamage => 38;
        public override int OldSpeed => 30;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 70;

        // Stock BaseAxe members, reproduced because BaseSetWeapon derives from BaseMeleeWeapon directly.
        public override int DefHitSound => 0x232;
        public override int DefMissSound => 0x23A;

        public override SkillName DefSkill => SkillName.Swords;
        public override WeaponType DefType => WeaponType.Axe;
        public override WeaponAnimation DefAnimation => WeaponAnimation.Slash2H;

        public virtual HarvestSystem HarvestSystem => Lumberjacking.System;

        public virtual int GetUsesScalar()
        {
            if (Quality == WeaponQuality.Exceptional)
            {
                return 200;
            }

            return 100;
        }

        public override void UnscaleDurability()
        {
            base.UnscaleDurability();

            var scale = GetUsesScalar();

            UsesRemaining = (_usesRemaining * 100 + (scale - 1)) / scale;
            InvalidateProperties();
        }

        public override void ScaleDurability()
        {
            base.ScaleDurability();

            var scale = GetUsesScalar();

            UsesRemaining = (_usesRemaining * scale + 99) / 100;
            InvalidateProperties();
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (HarvestSystem == null || Deleted)
            {
                return;
            }

            if (!IsChildOf(from))
            {
                var loc = GetWorldLocation();

                if (!from.InLOS(loc) || !from.InRange(loc, 2))
                {
                    from.LocalOverheadMessage(MessageType.Regular, 0x3E9, 1019045); // I can't reach that
                    return;
                }
            }

            if (!IsAccessibleTo(from))
            {
                PublicOverheadMessage(MessageType.Regular, 0x3E9, 1061637); // You are not allowed to access this.
                return;
            }

            if (HarvestSystem is not Mining)
            {
                from.SendLocalizedMessage(1010018); // What do you want to use this item on?
            }

            HarvestSystem.BeginHarvesting(from, this);
        }

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);

            if (HarvestSystem != null)
            {
                BaseHarvestTool.AddContextMenuEntries(from, this, ref list, HarvestSystem);
            }
        }

        public override void OnHit(Mobile attacker, Mobile defender, double damageBonus = 1)
        {
            base.OnHit(attacker, defender, damageBonus);

            if (!Core.AOS && Core.UOR && (attacker.Player || attacker.Body.IsHuman) && Layer == Layer.TwoHanded &&
                attacker.Skills.Anatomy.Value >= 80 &&
                attacker.Skills.Anatomy.Value / 400.0 >= Utility.RandomDouble() &&
                DuelContext.AllowSpecialAbility(attacker, "Concussion Blow", false))
            {
                var mod = defender.GetStatMod("Concussion");

                if (mod == null)
                {
                    defender.SendMessage("You receive a concussion blow!");
                    defender.AddStatMod(
                        new StatMod(StatType.Int, "Concussion", -(defender.RawInt / 2), TimeSpan.FromSeconds(30.0))
                    );

                    attacker.SendMessage("You deliver a concussion blow!");
                    attacker.PlaySound(0x308);
                }
            }
        }
    }
}

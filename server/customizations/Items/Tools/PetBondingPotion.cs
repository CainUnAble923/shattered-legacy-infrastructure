// cc-P199: ServUO pub57 Scripts/Items/Tools/PetBondingPotion.cs. The pet bonding potion (1152921, art 0x0F04, hue 2629,
// weight 1, blessed). Double-clicked from the pack (else 1042001) it asks for a pet (1152922); on your own controlled pet
// that is not yet bonded, not summoned and not a talisman summon it bonds the pet at once (1049666) and is used up.
// Refusals, in ServUO's order: already bonded 1152925, not yours 1114368, summoned or a talisman summon 1152924, not a
// creature 1152924. BondingTarget keeps ServUO's name and stays in this file.
//
// Bonding is pinned's own BaseCreature.IsBonded (a saved field, Mobiles/BaseCreature.cs:690-696), set the way ServUO
// does (t.IsBonded = !t.IsBonded, which here is always true). BondingBegin is not touched, as in ServUO.
//
// One difference: ServUO also refuses an allured creature (t.Allured, PetBondingPotion.cs:75). Pinned's BaseCreature
// has no Allured (there is no Allure spell in pinned; AnimalTrainer.cs:232 has the check commented out), so the clause is
// dropped; summoned still refuses on that branch.
//
// Weight: ServUO sets Weight = 1.0 in the constructor (:14); this overrides DefaultWeight => 1.0. Same weight.

using ModernUO.Serialization;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class PetBondingPotion : Item
    {
        [Constructible]
        public PetBondingPotion() : base(0x0F04)
        {
            LootType = LootType.Blessed;
            Hue = 2629;
        }

        public override int LabelNumber => 1152921; // Pet Bonding Potion
        public override double DefaultWeight => 1.0;

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            }
            else
            {
                // Target the pet you wish to bond with. Press ESC to cancel. This item is consumed on successful use, so choose wisely!
                from.SendLocalizedMessage(1152922);
                from.Target = new BondingTarget(this);
            }
        }
    }

    public class BondingTarget : Target
    {
        private readonly PetBondingPotion _potion;

        public BondingTarget(PetBondingPotion potion) : base(1, false, TargetFlags.None) => _potion = potion;

        protected override void OnTarget(Mobile from, object target)
        {
            if (_potion == null || _potion.Deleted || !_potion.IsChildOf(from.Backpack))
            {
                return;
            }

            if (target is BaseCreature t)
            {
                if (t.IsBonded)
                {
                    from.SendLocalizedMessage(1152925); // That pet is already bonded to you.
                }
                else if (t.ControlMaster != from)
                {
                    from.SendLocalizedMessage(1114368); // This is not your pet!
                }
                else if (t.Summoned)
                {
                    from.SendLocalizedMessage(1152924); // That is not a valid pet.
                }
                else if (t is BaseTalismanSummon)
                {
                    from.SendLocalizedMessage(1152924); // That is not a valid pet.
                }
                else
                {
                    t.IsBonded = !t.IsBonded;
                    from.SendLocalizedMessage(1049666); // Your pet has bonded with you!
                    _potion.Delete();
                }
            }
            else
            {
                from.SendLocalizedMessage(1152924); // That is not a valid pet.
            }
        }
    }
}

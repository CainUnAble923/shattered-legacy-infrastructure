// cc-P480 Part 8: a vet who brings a dead bonded pet back, for new players (Chase 10-23: his starter pup died and a new
// character had no way back), the animal trainers' elixir of rebirth and the graduation box's blessed one. Notes:
// shard-migration notes/cc-P480-tutorial-first-play-fixes.md.
//
// cc-P666: every stock Veterinarian now brings pets back on ServUO's terms (Mobiles/NPCs/VetResurrection.cs). Tobin
// keeps his own offer, with the same fee, skill loss and line of sight. Departures, both for new players: free while
// the owner is Young, and a double-click asks as well as the step in; the player is not frozen while the offer is open.
// Off the isle, like ServUO, the pet's body must have faded first (its corpse gone) and it loses 0.2 of each skill.
//
// On the isle (any Fold map, P481 section 8 and F6): a Young owner's pet ghost that comes within 3 tiles of Tobin is
// brought back at once, free and with no skill loss, and the things on its corpse go back to it. Its body need not have
// faded. The pet still died, so the lesson means something. A pet that is not a Young owner's pays and loses as
// anywhere else. Notes: shard-migration notes/cc-P666-pet-protection-vet.md.
//
// The tutorial seeder places Tobin at the farm gate, and one in New Haven when it has no vet (TutorialCorePlacement).

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial
{
    public static class TutorialPetCare
    {
        /// <summary>How close to Tobin a Young owner's pet ghost comes back on the isle by itself.</summary>
        public const int IsleReviveRange = 3;

        /// <summary>The owner's dead bonded pets within 12 tiles and in sight (ServUO Veterinarian.GetDeadPets).</summary>
        public static BaseCreature[] DeadPetsOf(Mobile owner) => VetResurrection.DeadPets(owner);

        /// <summary>ServUO's fee, free while the owner is Young.</summary>
        public static int Fee(Mobile owner, BaseCreature pet) =>
            owner is PlayerMobile { Young: true } ? 0 : VetResurrection.Fee(pet);

        /// <summary>Whether the isle's rule covers this pet at this vet: on a Fold map, a Young owner's dead bonded pet.</summary>
        public static bool IsleCare(Mobile vet, BaseCreature pet) =>
            FoldMaps.IsFold(vet.Map) && pet.Map == vet.Map && pet.IsDeadBondedPet && pet.ControlMaster is PlayerMobile { Young: true };

        /// <summary>
        /// The isle's revival: the corpse's things go back to the pet (worn things back on it where the layer is free, the
        /// rest into its pack, or at its feet when it has none), the corpse goes, and the pet comes back with every skill.
        /// </summary>
        public static void ReviveWhole(BaseCreature pet)
        {
            if (pet.Corpse is { Deleted: false } corpse)
            {
                foreach (var item in new List<Item>(corpse.Items))
                {
                    if (item.Layer is > Layer.Invalid and <= Layer.LastUserValid and not Layer.Backpack &&
                        pet.FindItemOnLayer(item.Layer) == null)
                    {
                        pet.AddItem(item);
                    }
                    else
                    {
                        pet.AddToBackpack(item);
                    }
                }

                corpse.Delete();
            }

            pet.Corpse = null;
            pet.PlaySound(0x214);
            pet.ResurrectPet();
        }

        public const string IfYourPetDiesLine =
            "If your pet dies, wait for its body to disappear, then take its ghost to a vet or use an elixir of rebirth.";

        /// <summary>The farm's pet lesson line (P481 walk step 7), said when the farm lessons are done.</summary>
        public const string FarmPetLessonLine =
            "If your pet dies, bring its ghost to a vet. Here on the island, Tobin brings it back free and whole while " +
            "you are Young. Every town's vet can do it too, for a fee from your bank.";

        public const string IsleRevivedLine = "Your pet is alive again, with everything it carried.";
    }

    /// <summary>The vet's offer: one button per dead pet, with its fee.</summary>
    public class TutorialVetResurrectGump : Gump
    {
        private readonly TutorialVeterinarian _vet;
        private readonly BaseCreature[] _pets;

        public TutorialVetResurrectGump(TutorialVeterinarian vet, BaseCreature[] pets) : base(150, 80)
        {
            _vet = vet;
            _pets = pets;
            var h = 130 + pets.Length * 30;
            var allWhole = Array.TrueForAll(pets, p => TutorialPetCare.IsleCare(vet, p));
            TutorialStyle.Panel(this, 440, h);
            AddLabel(24, 18, TutorialStyle.HeadingHue, "Bring your pet back to life");
            AddHtml(24, 42, 392, 40, TutorialStyle.Html(allWhole
                ? "Choose the pet you want to bring back. On this island it keeps all its skills."
                : "Choose the pet you want to bring back. It loses 0.2 points in every skill."), false, false);
            for (var i = 0; i < pets.Length; i++)
            {
                var fee = TutorialPetCare.Fee(pets[i].ControlMaster ?? pets[i], pets[i]);
                AddButton(24, 88 + i * 30, 4005, 4007, 1 + i);
                AddLabel(60, 90 + i * 30, TutorialStyle.ActionHue,
                    $"Bring back {pets[i].Name} ({(fee == 0 ? "free while you are Young" : $"{fee} gold from your bank")})");
            }

            AddButton(24, h - 40, 4017, 4019, 0);
            AddLabel(60, h - 38, TutorialStyle.TextHue, "Not now");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var i = info.ButtonID - 1;
            if (sender.Mobile is not { } from || i < 0 || i >= _pets.Length || _vet.Deleted)
            {
                return;
            }

            _vet.Resurrect(from, _pets[i]);
        }
    }
}

namespace Server.Mobiles
{
    using Server.Tutorial;

    [SerializationGenerator(0, false)]
    public partial class TutorialVeterinarian : Veterinarian
    {
        [Constructible]
        public TutorialVeterinarian()
        {
        }

        // Veterinarian's own offer (VetResurrection.OnVetMovement) skips this type: Tobin answers here.
        public override void OnMovement(Mobile m, Point3D oldLocation)
        {
            base.OnMovement(m, oldLocation);
            if (m is BaseCreature pet && InRange(pet, TutorialPetCare.IsleReviveRange) && TutorialPetCare.IsleCare(this, pet))
            {
                ReviveWhole(pet);
            }
            else if (m is PlayerMobile { Alive: true } pm && InRange(pm, 3) && !Utility.InRange(Location, oldLocation, 3))
            {
                Offer(pm);
            }
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from is PlayerMobile { Alive: true } pm && InRange(pm, 4) && Offer(pm))
            {
                return;
            }

            base.OnDoubleClick(from);
        }

        /// <summary>
        /// Brings back at once the player's isle-covered pets standing within 3 tiles, then offers the rest, if any. True
        /// when it did either.
        /// </summary>
        public bool Offer(PlayerMobile pm)
        {
            var revived = false;
            var rest = new List<BaseCreature>();
            foreach (var pet in TutorialPetCare.DeadPetsOf(pm))
            {
                if (InRange(pet, TutorialPetCare.IsleReviveRange) && TutorialPetCare.IsleCare(this, pet))
                {
                    ReviveWhole(pet);
                    revived = true;
                }
                else
                {
                    rest.Add(pet);
                }
            }

            if (rest.Count == 0)
            {
                return revived;
            }

            SayTo(pm, "I can bring your pet back. Choose its name in the window.");
            pm.CloseGump<TutorialVetResurrectGump>();
            pm.SendGump(new TutorialVetResurrectGump(this, rest.ToArray()));
            return true;
        }

        private void ReviveWhole(BaseCreature pet)
        {
            TutorialPetCare.ReviveWhole(pet);
            if (pet.ControlMaster is { } owner && owner.Map == Map && owner.InRange(this, 12))
            {
                SayTo(owner, TutorialPetCare.IsleRevivedLine);
            }
        }

        /// <summary>ServUO VetResurrectGump's checks and effect, with the Young fee and the isle's rule.</summary>
        public bool Resurrect(Mobile from, BaseCreature pet)
        {
            // cc-P610 (P514's P475/P480 findings): checked again at the answer, not only at the offer. The offer stays open
            // while the owner walks off with the ghost, dies or changes facet; then nothing is done and nothing is paid.
            if (Deleted || from.Deleted || !from.Alive || pet.Deleted || from.Map != Map || pet.Map != Map || !from.InRange(this, 4))
            {
                from.SendMessage("Bring your pet's ghost to the vet in person to have it brought back.");
                return false;
            }

            var fee = TutorialPetCare.Fee(from, pet);
            if (!pet.IsDeadBondedPet)
            {
                from.SendMessage("Your pet is still alive."); // cc-P609: was cliloc 501041
            }
            else if (!from.InRange(pet, 12))
            {
                from.SendMessage("Bring your pet closer to you, then try again."); // cc-P609: was cliloc 500643
            }
            else if (pet.ControlMaster != from)
            {
                from.SendMessage("Only this pet's owner can ask the vet to bring it back."); // cc-P609: was cliloc 1113200
            }
            else if (TutorialPetCare.IsleCare(this, pet))
            {
                ReviveWhole(pet);
                return true;
            }
            else if (pet.Corpse is { Deleted: false })
            {
                from.SendMessage("Wait until your pet's body disappears, then ask the vet again."); // cc-P609: was cliloc 1113279
            }
            else if (fee > 0 && !Banker.Withdraw(from, fee))
            {
                from.SendMessage("You need more gold in your bank to pay the vet."); // cc-P609: was cliloc 1060020
            }
            else
            {
                pet.PlaySound(0x214);
                pet.ResurrectPet();
                for (var j = 0; j < pet.Skills.Length; ++j)
                {
                    pet.Skills[j].Base -= VetResurrection.SkillLoss;
                }

                if (pet.Map == Map.Internal)
                {
                    pet.MoveToWorld(from.Location, from.Map);
                }

                if (fee > 0)
                {
                    from.SendMessage($"You paid the vet {fee} gold from your bank."); // cc-P609: was cliloc 1060398
                }

                SayTo(from, "Your pet is alive again.");
                return true;
            }

            return false;
        }
    }

    /// <summary>The animal trainer's elixir of rebirth (AnimalTrainer-elixir-of-rebirth.patch). Price: notes cc-P480.</summary>
    public class SBPetRebirth : SBInfo
    {
        public const int Price = 500;

        public override IShopSellInfo SellInfo { get; } = new GenericSellInfo();

        public override List<GenericBuyInfo> BuyInfo { get; } = new()
        {
            new GenericBuyInfo(typeof(ElixirOfRebirth), Price, 10, 0x24E2, 0x48E)
        };
    }
}

namespace Server.Items
{
    using Server.Tutorial;

    /// <summary>The graduation box's elixir: blessed, with a line on what it is for.</summary>
    [SerializationGenerator(0, false)]
    public partial class StarterElixirOfRebirth : ElixirOfRebirth
    {
        [Constructible]
        public StarterElixirOfRebirth() => LootType = LootType.Blessed;

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Brings a bonded pet back after its body disappears");
        }
    }
}

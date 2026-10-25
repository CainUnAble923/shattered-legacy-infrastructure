// cc-P480 Part 8: a vet who brings a dead bonded pet back, for new players (Chase 10-23: his starter pup died and a new
// character had no way back), the animal trainers' elixir of rebirth and the graduation box's blessed one. Notes:
// shard-migration notes/cc-P480-tutorial-first-play-fixes.md.
//
// Pinned ModernUO's Veterinarian sells and trains only; it has no resurrection (no OnMovement offer, no gump). ServUO's
// does (Scripts/Mobiles/NPCs/Veterinarian.cs: GetDeadPets, GetResurrectionFee, VetResurrectGump), and TutorialVeterinarian
// is that behaviour on a subclass: a player stepping within 3 tiles with a dead bonded pet nearby is offered its
// resurrection for ServUO's fee, 100 + 1.1041^MinTameSkill gold from the bank (100 to 30,000), and the pet loses 0.2 of
// each skill. Like ServUO, the pet's body must have faded first (its corpse gone). Departures, both for new players: free
// while the owner is Young, and a double-click asks as well as the step in; the player is not frozen while the offer is
// open. The tutorial seeder places one at the farm gate and one in New Haven when it has no vet (TutorialCorePlacement).

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
        /// <summary>The owner's dead bonded pets within 12 tiles (ServUO Veterinarian.GetDeadPets, without the automaton).</summary>
        public static BaseCreature[] DeadPetsOf(Mobile owner)
        {
            var pets = new List<BaseCreature>();
            foreach (var m in owner.GetMobilesInRange(12))
            {
                if (m is BaseCreature { IsDeadBondedPet: true } bc && bc.ControlMaster == owner)
                {
                    pets.Add(bc);
                }
            }

            return pets.ToArray();
        }

        /// <summary>ServUO's fee, free while the owner is Young.</summary>
        public static int Fee(Mobile owner, BaseCreature pet) =>
            owner is PlayerMobile { Young: true } ? 0 : Math.Clamp((int)(100 + Math.Pow(1.1041, pet.MinTameSkill)), 100, 30000);

        public const string IfYourPetDiesLine =
            "If your pet dies, wait for its body to disappear, then take its ghost to a vet or use an elixir of rebirth.";
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
            TutorialStyle.Panel(this, 440, h);
            AddLabel(24, 18, TutorialStyle.HeadingHue, "Bring your pet back to life");
            AddHtml(24, 42, 392, 40, TutorialStyle.Html("Choose the pet you want to bring back. It loses 0.2 points in every skill."), false, false);
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

        public override void OnMovement(Mobile m, Point3D oldLocation)
        {
            base.OnMovement(m, oldLocation);
            if (m is PlayerMobile { Alive: true } pm && InRange(pm, 3) && !Utility.InRange(Location, oldLocation, 3))
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

        /// <summary>Offers the player's dead pets, if any. True when it did.</summary>
        public bool Offer(PlayerMobile pm)
        {
            var pets = TutorialPetCare.DeadPetsOf(pm);
            if (pets.Length == 0)
            {
                return false;
            }

            SayTo(pm, "I can bring your pet back. Choose its name in the window.");
            pm.CloseGump<TutorialVetResurrectGump>();
            pm.SendGump(new TutorialVetResurrectGump(this, pets));
            return true;
        }

        /// <summary>ServUO VetResurrectGump's checks and effect, with the Young fee.</summary>
        public bool Resurrect(Mobile from, BaseCreature pet)
        {
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
                    pet.Skills[j].Base -= 0.2;
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

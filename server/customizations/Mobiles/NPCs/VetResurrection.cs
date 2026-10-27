// cc-P666 (P481 F6, P480's "every vet" draft): every stock Veterinarian brings a player's dead bonded pet back, as ServUO's
// does (Scripts/Mobiles/NPCs/Veterinarian.cs). Pinned ModernUO's Veterinarian sells and trains only. The hook is one
// OnMovement override in Veterinarian-resurrect-pets.patch; alternatives in shard-migration notes/cc-P666-pet-protection-vet.md.
//
// ServUO's behaviour, kept: a player stepping within 3 tiles of the vet, in line of sight, with a dead bonded pet of
// theirs within 12 tiles and in line of sight, is frozen (up to a minute) and offered VetResurrectGump. The fee is
// 100 + 1.1041^MinTameSkill gold from the bank, from 100 to 30,000. The pet loses 0.2 of every skill. It is refused while
// the pet's corpse is still there.
//
// Differences from ServUO:
//   - Text: ServUO's 1113xxx clilocs are written out as plain text (its own English, from the comments in its file),
//     so the window reads the same on any client. The bank lines keep their clilocs.
//   - The answer is checked in person too (the vet still there, same facet, within 4 tiles), as the tutorial vet's is
//     (cc-P610): nothing is paid for a service not given. The player is frozen while the offer is open, so this changes
//     nothing in normal play.
//   - "Cancel" is button 2 (ServUO's -1), and the expiry timer is stopped on any answer (ServUO's could unfreeze a
//     player that something else froze later).
//   - The Kotl automaton is not listed: ServUO adds a broken automaton head's automaton at no fee, but our KotlAutomaton
//     dies into the stable (IsStabled) and the vet path would leave it there. Left for the automaton's repair hook.
//   - TutorialVeterinarian (Tutorial/TutorialPetCare.cs) keeps its own offer; this one skips it.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Network;
using Server.Tutorial;

namespace Server.Mobiles
{
    public static class VetResurrection
    {
        /// <summary>What every vet's resurrection takes from each of the pet's skills (ServUO).</summary>
        public const double SkillLoss = 0.2;

        public static readonly TimeSpan OfferTime = TimeSpan.FromMinutes(1.0);

        private static readonly Dictionary<Mobile, Timer> _expire = new();

        /// <summary>ServUO Veterinarian.GetDeadPets, without the automaton: within 12 tiles and in line of sight.</summary>
        public static BaseCreature[] DeadPets(Mobile from)
        {
            var pets = new List<BaseCreature>();
            foreach (var m in from.GetMobilesInRange(12))
            {
                if (m is BaseCreature { IsDeadBondedPet: true } bc && bc.ControlMaster == from && from.InLOS(bc))
                {
                    pets.Add(bc);
                }
            }

            return pets.ToArray();
        }

        /// <summary>ServUO Veterinarian.GetResurrectionFee.</summary>
        public static int Fee(BaseCreature pet) => Math.Clamp((int)(100 + Math.Pow(1.1041, pet.MinTameSkill)), 100, 30000);

        /// <summary>Called by every Veterinarian's OnMovement (Veterinarian-resurrect-pets.patch).</summary>
        public static void OnVetMovement(Veterinarian vet, Mobile m, Point3D oldLocation)
        {
            if (vet is TutorialVeterinarian || m is not PlayerMobile { Alive: true } || !vet.InRange(m, 3) ||
                vet.InRange(oldLocation, 3) || !vet.InLOS(m))
            {
                return;
            }

            var pets = DeadPets(m);
            if (pets.Length == 0)
            {
                return;
            }

            m.Frozen = true;
            if (_expire.Remove(m, out var old))
            {
                old.Stop();
            }

            _expire[m] = Timer.DelayCall(OfferTime, ResetExpire, m);
            m.CloseGump<VetResurrectGump>();
            m.SendGump(new VetResurrectGump(vet, pets));
        }

        /// <summary>Unfreezes the player and closes the offer (ServUO Veterinarian.ResetExpire).</summary>
        public static void ResetExpire(Mobile m)
        {
            m.Frozen = false;
            m.CloseGump<VetResurrectGump>();
            if (_expire.Remove(m, out var t))
            {
                t.Stop();
            }
        }

        /// <summary>ServUO VetResurrectGump's checks and effect for one pet. True when the pet came back.</summary>
        public static bool Resurrect(Veterinarian vet, Mobile from, BaseCreature pet)
        {
            if (vet.Deleted || from.Deleted || !from.Alive || pet.Deleted || from.Map != vet.Map || !from.InRange(vet, 4))
            {
                from.SendMessage("Bring your pet's ghost to the vet in person to have it brought back.");
                return false;
            }

            var fee = Fee(pet);
            if (!pet.IsDeadBondedPet)
            {
                from.SendLocalizedMessage(501041); // Target is not dead.
            }
            else if (!from.CanSee(pet) || !from.InLOS(pet))
            {
                from.SendLocalizedMessage(503376); // Target cannot be seen.
            }
            else if (!from.InRange(pet, 12))
            {
                from.SendLocalizedMessage(500643); // Target is too far away.
            }
            else if (pet.ControlMaster != from)
            {
                from.SendMessage("You must be the owner of that pet to have it resurrected."); // ServUO 1113200
            }
            else if (pet.Corpse is { Deleted: false })
            {
                from.SendMessage("That creature's spirit lacks cohesion. Try again in a few minutes."); // ServUO 1113279
            }
            else if (Banker.Withdraw(from, fee))
            {
                pet.PlaySound(0x214);
                pet.ResurrectPet();
                for (var j = 0; j < pet.Skills.Length; ++j)
                {
                    pet.Skills[j].Base -= SkillLoss;
                }

                if (pet.Map == Map.Internal)
                {
                    pet.MoveToWorld(from.Location, from.Map);
                }

                from.SendLocalizedMessage(1060398, fee.ToString()); // ~1_AMOUNT~ gold has been withdrawn from your bank box.
                from.SendLocalizedMessage(1060022, Banker.GetBalance(from).ToString(), 0x16); // You have ~1_AMOUNT~ gold in cash remaining in your bank box.
                return true;
            }
            else
            {
                from.SendLocalizedMessage(1060020); // Unfortunately, you do not have enough cash in your bank to cover the cost of the healing.
            }

            return false;
        }
    }

    /// <summary>ServUO's VetResurrectGump: one radio per dead pet with its fee; OK (1) or Cancel (2). Not closable.</summary>
    public class VetResurrectGump : Gump
    {
        public const int OkButton = 1;
        public const int CancelButton = 2;

        private readonly Veterinarian _vet;
        private readonly BaseCreature[] _pets;

        public VetResurrectGump(Veterinarian vet, BaseCreature[] pets) : base(150, 50)
        {
            _vet = vet;
            _pets = pets;

            AddPage(0);

            Closable = false;

            AddImage(0, 0, 0xE10);
            AddImageTiled(0, 14, 15, 380, 0xE13);
            AddImageTiled(380, 14, 14, 380, 0xE15);
            AddImage(0, 381, 0xE16);
            AddImageTiled(15, 381, 370, 16, 0xE17);
            AddImageTiled(15, 0, 370, 16, 0xE11);
            AddImage(380, 0, 0xE12);
            AddImage(380, 381, 0xE18);
            AddImageTiled(15, 15, 365, 370, 0xA40);

            // ServUO 1113193, 1113284, 1113286, as text.
            AddHtml(30, 20, 355, 35, "<BASEFONT COLOR=#FFFFFF>Ah, thine pet seems to be in dire condition! I can help thee, but must charge a small fee...</BASEFONT>");
            AddHtml(30, 72, 345, 40, "<BASEFONT COLOR=#8CB2D6>Please select the pet you wish to resurrect:</BASEFONT>");
            AddHtml(20, 280, 345, 40, "<BASEFONT COLOR=#8CB2D6><CENTER>Your pet will suffer 0.2 points of skill-loss if resurrected in this manner.</CENTER></BASEFONT>");
            AddImageTiled(95, 62, 200, 1, 0x23C5);
            AddImageTiled(15, 325, 365, 1, 0x2393);

            AddButton(110, 343, 0xF7, 0xF8, OkButton);
            AddButton(230, 343, 0xF2, 0xF1, CancelButton);

            AddImageTiled(15, 14, 365, 1, 0x2393);
            AddImageTiled(380, 14, 1, 370, 0x2391);
            AddImageTiled(15, 385, 365, 1, 0x2393);
            AddImageTiled(15, 14, 1, 370, 0x2391);
            AddImageTiled(0, 0, 395, 1, 0x23C5);
            AddImageTiled(394, 0, 1, 397, 0x23C3);
            AddImageTiled(0, 396, 395, 1, 0x23C5);
            AddImageTiled(0, 0, 1, 397, 0x23C3);

            for (int i = 0, yOffset = 0; i < pets.Length; i++, yOffset += 35)
            {
                AddRadio(30, 102 + yOffset, 0x25FF, 0x2602, i == 0, i);
                AddLabel(70, 107 + yOffset, 0x47E, $"{pets[i].Name}  {VetResurrection.Fee(pets[i])}");
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile is not { } from)
            {
                return;
            }

            VetResurrection.ResetExpire(from);

            if (info.ButtonID != OkButton)
            {
                // ServUO 1113197, as text.
                from.SendMessage("You decide against paying the Veterinarian, and the ghost of your pet looks at you sadly...");
                return;
            }

            for (var i = 0; i < _pets.Length; i++)
            {
                if (info.IsSwitched(i))
                {
                    VetResurrection.Resurrect(_vet, from, _pets[i]);
                    break;
                }
            }
        }
    }
}

using System;
using Server.Items;
using Server.Mobiles;

namespace Server;

// -----------------------------------------------------------------------------------------------------------------------
// Pack Mule breeding (F-33; cc-P69 Part D).
//
// Two mules one player owns, one male and one female, each ready (7-day cooldown), breed. The foal's genes are rolled at
// once (MuleGenes.Breed: strong parents pass on quality and traits, the breeder's Animal Lore 120-200 helps) into a
// BredPackMuleDeed, which waits on the internal map for the 24-hour gestation with its due time and owner saved, then goes
// to the owner's bank. Before cc-P69 the gestation was an unsaved timer and a restart lost the foal.
// -----------------------------------------------------------------------------------------------------------------------

public static class PackMuleBreedingSystem
{
    /// <summary>Why these two cannot breed now, or null if they can.</summary>
    public static string CanBreed(PackMule a, PackMule b, PlayerMobile owner)
    {
        if (a == b)
        {
            return "A mule cannot breed with itself.";
        }

        if (a.ControlMaster != owner || b.ControlMaster != owner)
        {
            return "You can only breed mules that you own.";
        }

        if (a.IsMale == b.IsMale)
        {
            return $"Both mules are {a.GenderLabel}: you need one male and one female.";
        }

        if (a.IsDeadPet || b.IsDeadPet)
        {
            return "That mule is dead.";
        }

        foreach (var m in new[] { a, b })
        {
            if (!m.CanBreedNow)
            {
                var rem = m.NextBreedTime - DateTime.UtcNow;
                return $"That mule cannot breed for another {(int)rem.TotalDays}d {rem.Hours}h.";
            }
        }

        return null;
    }

    /// <summary>
    /// Breeds the two (the caller has checked CanBreed): both get the cooldown, and the foal deed, its genes rolled now,
    /// starts its gestation. Returns the pending deed.
    /// </summary>
    public static BredPackMuleDeed StartBreeding(PackMule a, PackMule b, PlayerMobile owner)
    {
        var now = DateTime.UtcNow;
        var cooldownUntil = now + MuleBreeding.Cooldown;

        a.SetBreedCooldown(cooldownUntil);
        b.SetBreedCooldown(cooldownUntil);

        var genes = MuleGenes.Breed(a, b, owner.Skills.AnimalLore.Value);
        var deed = new BredPackMuleDeed(genes);
        deed.BeginGestation(owner, now + MuleBreeding.Gestation);

        owner.SendMessage(0x44,
            "The mules seem quite taken with each other. " +
            $"A foal deed will arrive in your bank box in {(int)MuleBreeding.Gestation.TotalHours} hours.");

        a.PlaySound(0xA8);
        b.PlaySound(0xA8);
        return deed;
    }
}

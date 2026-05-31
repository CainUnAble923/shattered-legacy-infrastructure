using System;
using Server.Items;
using Server.Mobiles;

namespace Server;

// ─────────────────────────────────────────────────────────────────────────────
// Pack Mule Breeding System
//
// Two Pack Mules owned by the same player (one male, one female) can breed
// once every 7 real days each.  After a 24-hour gestation the owner receives
// a BredPackMuleDeed in their bank box.
//
// Usage flow:
//   1. Player right-clicks a PackMule → breeding entry → targets the mate
//   2. PackMule.PackMuleBreedTarget calls PackMuleBreedingSystem.StartBreeding(...)
//   3. Both mules receive a 7-day cooldown; a one-shot Timer fires after 24 h
//   4. Timer delivers a BredPackMuleDeed to the owner's bank box
//
// Note on timer persistence:
//   The gestation timer is NOT serialized.  If the server restarts during the
//   24-hour window the deed will not be delivered.  This is accepted behaviour
//   for a controlled shard with infrequent restarts.  A fully-persistent
//   solution would require storing pending breeds in a TimerRegistry entry.
// ─────────────────────────────────────────────────────────────────────────────

public static class PackMuleBreedingSystem
{
    private static readonly TimeSpan BreedingCooldown = TimeSpan.FromDays(7);
    private static readonly TimeSpan GestationTime    = TimeSpan.FromHours(24);

    /// <summary>
    /// Initiates breeding between <paramref name="muleA"/> and <paramref name="muleB"/>.
    /// Both mules receive a 7-day cooldown.  After <see cref="GestationTime"/> the
    /// owner's bank box receives a <see cref="BredPackMuleDeed"/>.
    /// </summary>
    public static void StartBreeding(PackMule muleA, PackMule muleB, PlayerMobile owner)
    {
        var cooldownUntil = DateTime.UtcNow + BreedingCooldown;

        muleA.SetBreedCooldown(cooldownUntil);
        muleB.SetBreedCooldown(cooldownUntil);

        owner.SendMessage(0x44,
            "The mules seem quite taken with each other. " +
            $"A foal deed will arrive in your bank box in {(int)GestationTime.TotalHours} hours.");

        muleA.PlaySound(0xA8);
        muleB.PlaySound(0xA8);

        // Capture a weak reference so the mobile is not pinned in memory if the
        // owner logs out during the gestation window.
        var ownerRef = new WeakReference<PlayerMobile>(owner);
        Timer.StartTimer(GestationTime, () => DeliverFoal(ownerRef));
    }

    // ── Gestation callback ────────────────────────────────────────────────────

    private static void DeliverFoal(WeakReference<PlayerMobile> ownerRef)
    {
        if (!ownerRef.TryGetTarget(out var owner) || owner.Deleted)
            return;

        var bank = owner.BankBox;
        if (bank == null)
            return;

        var deed = new BredPackMuleDeed();
        bank.DropItem(deed);

        // Notify the player if they are currently online.
        if (owner.NetState != null)
        {
            owner.SendMessage(0x44,
                "A bred pack mule deed has been delivered to your bank box. " +
                "The lineage continues.");
            owner.PlaySound(0x3D);
        }
    }
}

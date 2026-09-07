// The test route's one correct way to advance time, and the proof that it is correct.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// THE TRAP THIS EXISTS FOR (Q-029, found by S6):
//
//   Timer.Slice turns the timer wheel. It does not move Core.Now or Core.TickCount.
//   Projects/Server/Timer/Timer.TimerWheel.cs:54 takes an ABSOLUTE tick count and turns the
//   wheel until it has caught up with it; nothing in it assigns to Core._now. The server's
//   main loop moves the clock first and slices second - Projects/Server/Main.cs:469-474:
//
//       _tickCount = GetTimestamp();
//       _now       = DateTime.UtcNow;
//       ...
//       Timer.Slice(_tickCount);
//
//   A test that slices without moving the clock therefore runs every scheduled callback
//   against a Core.Now that has not changed. S6's first damage-eater test drove AOS.Damage
//   and then sliced four seconds, and reported healed=40 expected=3: Mobile.Damage starts
//   Mobile.HitsTimer (Projects/Server/Mobiles/Mobile.cs:8910), which does Hits++ and re-arms
//   on every turn, so four seconds of wheel with a frozen clock regenerated the whole hit.
//   The test measured regeneration and would have passed or failed for reasons unrelated to
//   the thing under test.
//
//   Two further traps in the same call, neither of them obvious from the name:
//
//   - The argument is absolute, not a delta. Slicing to 4000 twice in a row turns the wheel
//     the first time and does nothing the second.
//   - Re-initialising the wheel empties every ring. Doing it mid-test silently drops timers
//     the test has already scheduled, and doing it with 0 while Core.TickCount is non-zero
//     leaves the wheel and the clock disagreeing for every later test in the collection.
//
// THE ROUTE'S ANSWER: Arm() once at the top of a timing-dependent test, then Advance().
// Advance moves Core.TickCount and Core.Now together in 8 ms steps - the wheel's own tick
// rate - slicing at each step, so a callback that falls due at t+3s sees Core.Now at t+3s.
//
// docker/uo/apply-patches.sh FAILS THE BUILD for any other test file under server/tests/
// that drives the timer wheel directly. This file is the single exemption, because
// SliceAloneDoesNotAdvanceTheClock below has to call the wrong thing to prove it is wrong.

using System;
using Server;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

/// <summary>
/// Advances the server clock and the timer wheel together in a test. Use this rather than
/// driving the wheel directly, which advances the wheel only.
/// </summary>
public static class ShardTestClock
{
    /// <summary>
    /// The clock the test host starts from. UOContentFixture never assigns Core._now, so it
    /// sits at DateTime.MinValue and any <c>Core.Now - TimeSpan</c> throws. Arm pins it here
    /// once, and it only ever moves forward afterwards.
    /// </summary>
    public static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Timer._tickRate. It is private upstream, so it is duplicated rather than read; a change
    // to it upstream costs accuracy in Advance, not correctness, because the wheel re-checks
    // the catch-up condition itself. AdvanceFiresATimerAtTheRightTime would notice a large one.
    private const long WheelTickMilliseconds = 8;

    private static bool _armed;

    /// <summary>
    /// Takes control of the clock for the current test: pins Core.Now to <see cref="Epoch"/> the
    /// first time it is called in a process, and re-synchronises the timer wheel with
    /// Core.TickCount, dropping timers left behind by an earlier test.
    /// Call it BEFORE scheduling anything the test intends to fire.
    /// </summary>
    public static void Arm()
    {
        if (Core._now < Epoch)
        {
            Core._now = Epoch;
        }

        // This re-seeds the wheel's last-turned mark and empties every ring. Seeding it with
        // Core.TickCount rather than 0 is the point: the wheel and the clock must agree, or the
        // first Advance either turns nothing or turns for the whole of the previous test's
        // elapsed time. UOContentFixture.Dispose re-initialises it the same way.
        Timer.Init(Core._tickCount);
        _armed = true;
    }

    /// <summary>
    /// Advances Core.TickCount, Core.Now and the timer wheel by <paramref name="by"/>, running
    /// every timer that falls due with the clock reading the time it is due at.
    /// </summary>
    public static void Advance(TimeSpan by)
    {
        if (!_armed)
        {
            throw new InvalidOperationException(
                "ShardTestClock.Arm() must be called before Advance(). Arm empties the timer " +
                "wheel, so calling it after scheduling would drop what this test scheduled."
            );
        }

        if (by < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(by), by, "The server clock only moves forward.");
        }

        var remaining = (long)by.TotalMilliseconds;

        while (remaining > 0)
        {
            var step = Math.Min(WheelTickMilliseconds, remaining);
            remaining -= step;

            Core._tickCount += step;
            Core._now = Core._now.AddMilliseconds(step);

            // Absolute, not a delta: the wheel turns until it has caught up with this value.
            Timer.Slice(Core._tickCount);
        }
    }

    /// <summary>Convenience overload for the common case.</summary>
    public static void AdvanceSeconds(double seconds) => Advance(TimeSpan.FromSeconds(seconds));
}

/// <summary>
/// Proves the clock helper does what its name says, and pins the upstream behaviour it exists
/// to work around. If a pinned-commit bump ever makes a bare slice advance Core.Now,
/// SliceAloneDoesNotAdvanceTheClock is what notices, and this file should then be revisited.
/// </summary>
[Collection("Sequential UOContent Tests")]
public class ShardTestClockVerification
{
    private readonly ITestOutputHelper _out;

    public ShardTestClockVerification(ITestOutputHelper output) => _out = output;

    [Fact]
    public void AdvanceMovesTheClockByExactlyWhatItWasAsked()
    {
        ShardTestClock.Arm();

        var before = Core.Now;
        var beforeTicks = Core.TickCount;

        ShardTestClock.Advance(TimeSpan.FromSeconds(10));

        _out.WriteLine($"clock {before:O} -> {Core.Now:O}; ticks {beforeTicks} -> {Core.TickCount}");

        Assert.Equal(before.AddSeconds(10), Core.Now);
        Assert.Equal(beforeTicks + 10_000, Core.TickCount);
        Assert.True(Core.Now >= ShardTestClock.Epoch, "Arm() left the clock at DateTime.MinValue");
    }

    [Fact]
    public void AdvanceFiresATimerAtTheRightTime()
    {
        ShardTestClock.Arm();

        var firedAt = DateTime.MinValue;
        var fired = 0;

        var scheduledAt = Core.Now;

        Timer.DelayCall(
            TimeSpan.FromSeconds(3),
            () =>
            {
                fired++;
                firedAt = Core.Now;
            }
        );

        ShardTestClock.Advance(TimeSpan.FromSeconds(2.5));
        Assert.Equal(0, fired);

        ShardTestClock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, fired);

        var lateBy = firedAt - scheduledAt.AddSeconds(3);
        _out.WriteLine($"fired {fired}x at {firedAt:O}, {lateBy.TotalMilliseconds} ms off the 3 s mark");

        // The wheel's resolution is 8 ms, so the callback lands within a tick of due - and,
        // critically, Core.Now reads the DUE time when it runs rather than the time the test
        // finished advancing. That is the whole difference from turning the wheel alone.
        Assert.InRange(lateBy, TimeSpan.FromMilliseconds(-16), TimeSpan.FromMilliseconds(16));

        // Nothing re-arms it.
        ShardTestClock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, fired);
    }

    [Fact]
    public void SliceAloneDoesNotAdvanceTheClock()
    {
        ShardTestClock.Arm();

        var fired = 0;
        var seenNow = DateTime.MinValue;

        Timer.DelayCall(TimeSpan.FromSeconds(1), () => { fired++; seenNow = Core.Now; });

        var before = Core.Now;

        // The wrong way, called here on purpose. apply-patches.sh fails the build for any other
        // test file that does this. The argument is absolute, so this reads as "the clock is now
        // four seconds later" - except that it is not, because nothing but the wheel is told.
        Timer.Slice(Core.TickCount + 4000);

        _out.WriteLine($"after slicing alone: fired={fired} Core.Now moved={Core.Now != before} " +
                       $"callback saw {seenNow:O}");

        Assert.Equal(1, fired);            // the wheel turned: the timer ran
        Assert.Equal(before, Core.Now);    // the clock did not move
        Assert.Equal(before, seenNow);     // so the callback ran a second into a frozen present

        // That slice left the wheel 4000 ms ahead of Core.TickCount. Re-arm so the desync does
        // not leak into the next test in this collection - which is exactly the failure mode
        // that makes a bare slice unsafe in a shared fixture.
        ShardTestClock.Arm();

        var check = 0;
        Timer.DelayCall(TimeSpan.FromSeconds(1), () => check++);
        ShardTestClock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, check);
    }
}

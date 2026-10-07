// GuardRevealProbe.cs
//
// cc-P66 Part A, a probe. cc-P54 (from cc-P20) read "guards cannot reveal hiders from Hiding 121": guards have Detect
// Hidden 100 (Mobiles/Guards/ArcherGuard.cs:64, WarriorGuard.cs:81) and the reveal contest is +/-10
// (Skills/DetectHidden.cs:197-198). But a guard's "Reveal!" is UseSkill(SkillName.DetectHidden) (ArcherGuard.cs:159,
// WarriorGuard.cs:170), whose handler only opens a target cursor (DetectHidden.cs:30-35), and nothing answers a guard's
// cursor: BaseGuard derives from Mobile, not BaseCreature, so no AI handles it, and Mobile.Target only sends it to a client.
// ServUO pub57 is the same. If that reading is right, guards reveal no hider at any Hiding and there is no threshold to
// clamp. This fact runs a warrior guard's own reveal against a hider with Hiding 0 and records what happens.

using Server;
using Server.Items;
using Server.Mobiles;
using Server.SkillHandlers;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuardRevealProbe
{
    private readonly ITestOutputHelper _out;

    public GuardRevealProbe(ITestOutputHelper output) => _out = output;

    [Fact]
    public void AGuardsRevealRevealsNoHiderAtAnyHiding()
    {
        var at = new Point3D(2200, 2200, 0);
        var hider = new PlayerMobile { Player = true, Race = Race.Human };
        hider.AddItem(new Backpack());
        hider.MoveToWorld(at, Map.Trammel);
        hider.Skills.Hiding.Base = 0.0;
        hider.Hidden = true;

        var guard = new WarriorGuard(hider);
        guard.MoveToWorld(new Point3D(at.X + 1, at.Y, at.Z), Map.Trammel);

        try
        {
            // The real server registers the skill's handler at start (DetectHidden.cs:25-28); the test host does not, and
            // without it UseSkill refuses before reaching it, which would prove nothing.
            DetectHidden.Initialize();

            Assert.Equal(100.0, guard.Skills.DetectHidden.Base);
            var used = guard.UseSkill(SkillName.DetectHidden);
            _out.WriteLine(
                $"guard UseSkill(DetectHidden) returned {used}; guard target {guard.Target?.GetType().FullName ?? "none"}; " +
                $"hider (Hiding 0) hidden {hider.Hidden}"
            );

            // The skill ran and opened its cursor (DetectHidden.cs:33), and nothing answered it.
            Assert.True(used);
            Assert.Equal("Server.SkillHandlers.DetectHidden+InternalTarget", guard.Target?.GetType().FullName);
            Assert.True(hider.Hidden, "a guard revealed a hider: cc-P54's guard threshold is real and needs its clamp");
        }
        finally
        {
            guard.Delete();
            hider.Delete();
        }
    }
}

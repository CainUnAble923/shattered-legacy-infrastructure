// Shattered Legacy's own resources in the Test Kit. One line per resource; TESTKIT.md beside this file says how.
//
// Registered in Configure, the tree's convention for filling a registry before the world loads (pinned
// SkillsInfo.Configure fills SkillInfo.Table, SkillsInfo.cs:135; CommandSystem.Register is called from Configure in
// AccountHandler.cs:65 and ClusterFSkillGain.cs:32). Registering does nothing on its own: TestCenterKit only places
// entries when TestCenter.Enabled, so these lines are inert on the live shard.
//
// Amounts follow pinned's raw materials bag (TestCenter.cs:329-353): 5000 of each ingot and board. Ore and logs are
// the raw form, so 1000 each is plenty to test smelting and cutting.

using Server.Items;

namespace Server.Misc;

public static class TestCenterKitEntries
{
    public static void Configure()
    {
        // ClusterFExtendedOres.cs: tiers 10-17
        TestCenterKit.Register(() => new PlatinumOre(), 1000);
        TestCenterKit.Register(() => new ToxicOre(), 1000);
        TestCenterKit.Register(() => new BlazeOre(), 1000);
        TestCenterKit.Register(() => new FrostOre(), 1000);
        TestCenterKit.Register(() => new ObsidianOre(), 1000);
        TestCenterKit.Register(() => new MythrilOre(), 1000);
        TestCenterKit.Register(() => new AdamantiumOre(), 1000);
        TestCenterKit.Register(() => new CelestialOre(), 1000);

        TestCenterKit.Register(() => new PlatinumIngot(), 5000);
        TestCenterKit.Register(() => new ToxicIngot(), 5000);
        TestCenterKit.Register(() => new BlazeIngot(), 5000);
        TestCenterKit.Register(() => new FrostIngot(), 5000);
        TestCenterKit.Register(() => new ObsidianIngot(), 5000);
        TestCenterKit.Register(() => new MythrilIngot(), 5000);
        TestCenterKit.Register(() => new AdamantiumIngot(), 5000);
        TestCenterKit.Register(() => new CelestialIngot(), 5000);

        // ClusterFExtendedLumber.cs
        TestCenterKit.Register(() => new IronwoodLog(), 1000);
        TestCenterKit.Register(() => new GhostwoodLog(), 1000);
        TestCenterKit.Register(() => new EmberbarkLog(), 1000);
        TestCenterKit.Register(() => new FrostbarkLog(), 1000);
        TestCenterKit.Register(() => new ShadowbarkLog(), 1000);
        TestCenterKit.Register(() => new RunewoodLog(), 1000);
        TestCenterKit.Register(() => new VoidwoodLog(), 1000);
        TestCenterKit.Register(() => new StarwoodLog(), 1000);

        TestCenterKit.Register(() => new IronwoodBoard(), 5000);
        TestCenterKit.Register(() => new GhostwoodBoard(), 5000);
        TestCenterKit.Register(() => new EmberbarkBoard(), 5000);
        TestCenterKit.Register(() => new FrostbarkBoard(), 5000);
        TestCenterKit.Register(() => new ShadowbarkBoard(), 5000);
        TestCenterKit.Register(() => new RunewoodBoard(), 5000);
        TestCenterKit.Register(() => new VoidwoodBoard(), 5000);
        TestCenterKit.Register(() => new StarwoodBoard(), 5000);
    }
}

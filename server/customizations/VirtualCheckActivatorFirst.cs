// cc-P265: the server stopped at start with "NullReferenceException: GumpActivator" (VirtualCheck.Configure) once this
// branch added one more Configure method (GuildHallDecorSeeder). Why: pinned ModernUO runs every Configure in the order
// of a List.Sort by CallPriority (AssemblyHandler.Invoke, CallPriorityComparer), and nearly all share the default 50, so
// their order is whatever the unstable sort leaves. VirtualCheck.Configure (Server.dll) throws when the edit gump is on
// (virtualChecks.useEditGump, default Core.TOL: on for our expansion) and VirtualCheckGump.Configure (UOContent), which
// sets the activator, has not run yet. Adding or removing any Configure anywhere can flip the two; it did here, and the
// test gate cannot see it (the tests never start the server through AssemblyHandler).
//
// This sets the activator first, at a priority below the default, so the two never depend on the sort again.
// VirtualCheckGump.Configure only assigns the activator, so running it twice is harmless.
// Pinned by VirtualCheckActivatorVerification.

using Server.Gumps;

namespace Server;

public static class VirtualCheckActivatorFirst
{
    [CallPriority(0)]
    public static void Configure() => VirtualCheckGump.Configure();
}

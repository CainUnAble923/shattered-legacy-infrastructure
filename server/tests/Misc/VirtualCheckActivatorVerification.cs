// VirtualCheckActivatorVerification.cs
//
// cc-P265: VirtualCheckActivatorFirst.Configure (which sets VirtualCheck.GumpActivator) sorts before
// VirtualCheck.Configure (which throws at server start when the activator is unset), whatever the unstable sort does
// with the default-priority Configure methods. Pinned's own comparer decides the order, as AssemblyHandler.Invoke does.

using System.Reflection;
using Server;
using Server.Items;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class VirtualCheckActivatorVerification
{
    [Fact]
    public void TheActivatorIsSetBeforeVirtualCheckConfigureRuns()
    {
        var ours = typeof(VirtualCheckActivatorFirst).GetMethod("Configure", BindingFlags.Public | BindingFlags.Static);
        var theirs = typeof(VirtualCheck).GetMethod("Configure", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(ours);
        Assert.NotNull(theirs);

        var comparer = new CallPriorityComparer();
        Assert.True(comparer.Compare(ours, theirs) < 0, "VirtualCheckActivatorFirst.Configure must sort strictly first");
        Assert.True(comparer.Compare(theirs, ours) > 0, "and VirtualCheck.Configure strictly after it");
    }
}

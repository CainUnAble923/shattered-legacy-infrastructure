// Loud startup error if the Test Center is on for a shard listening on the live port.
//
// Live and test run the same image and differ only by configuration (docker/uo/docker-compose.yml vs
// docker-compose.test.yml), so the one thing that could turn the Test Center on for live is a hand edit of live's
// modernuo.json. This line is what that mistake looks like in the log. It logs and does not refuse to start; the
// reasoning is in shard-migration/notes/cc-P9-test-center.md section 4.
//
// Runs in Initialize, after every Configure: TestCenter.Enabled is set in TestCenter.Configure, which has the default
// priority, so reading it from another Configure would depend on sort order (the defect Part E of that note fixes).

using System.Collections.Generic;
using System.Net;
using Server.Logging;

namespace Server.Misc;

public static class TestCenterLiveGuard
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(TestCenterLiveGuard));

    // docker/uo/docker-compose.yml publishes 2593 and live's modernuo.json "listeners" is 0.0.0.0:2593.
    public const int LivePort = 2593;

    public static void Initialize()
    {
        if (IsTestCenterOnLivePort(TestCenter.Enabled, ServerConfiguration.Listeners))
        {
            logger.Error(
                "TEST CENTER IS ON AND THIS SHARD LISTENS ON THE LIVE PORT {Port}. Any player can set skills and stats " +
                "and new characters get a stocked bank. Set testCenter.enable to False in this shard's modernuo.json " +
                "and restart.",
                LivePort
            );
        }
    }

    public static bool IsTestCenterOnLivePort(bool testCenterEnabled, IEnumerable<IPEndPoint> listeners)
    {
        if (!testCenterEnabled || listeners == null)
        {
            return false;
        }

        foreach (var listener in listeners)
        {
            if (listener?.Port == LivePort)
            {
                return true;
            }
        }

        return false;
    }
}

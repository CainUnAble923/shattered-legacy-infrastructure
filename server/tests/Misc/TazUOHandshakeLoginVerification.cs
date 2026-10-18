// TazUOHandshakeLoginVerification.cs
//
// cc-P165: P147's play tests failed because no client ever received the 0xCE id 1 handshake. It was sent from
// EventSink.Connected, which ModernUO raises inside PlayCharacter right after state.BlockAllPackets = true, so
// NetState.Send dropped it. P103's and P147's facts attached the NetState with nothing blocked, so the handshake
// landed in their buffer and the bug never showed. These facts log in the way PlayCharacter, its LoginTimer and
// DoLogin do (IncomingAccountPackets.cs in ModernUO d4531cd9: PlayCharacter :289-295, LoginTimer :637-638, the
// login event at the end of DoLogin :341-344). Notes: shard-migration notes/cc-P165-achievements-button-fix.md.
//
// Facts:
//   1. A client that logs in the real way gets the handshake exactly once, after packets are unblocked.
//   2. Nothing is sent while packets are blocked (attaching the NetState alone sends no handshake).
//   3. clusterf.tazuo.enhancedPackets False: no handshake at login.

using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.TazUO;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TazUOHandshakeLoginVerification
{
    private static readonly byte[] Handshake = [0xCE, 0x00, 0x0B, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x04];

    private sealed class Login : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Login()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Eve" };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(new Point3D(1720, 1720, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
        }

        // PlayCharacter: block every packet, then attach (this raises EventSink.Connected).
        public void SelectCharacter()
        {
            Ns.BlockAllPackets = true;
            Ns.Mobile = Pm;
            Pm.NetState = Ns;
        }

        // LoginTimer, once the client version is in: unblock; DoLogin's last step: the login event.
        public void EnterWorld()
        {
            Ns.BlockAllPackets = false;
            PlayerMobile.PlayerLoginEvent(Pm);
        }

        public int Handshakes()
        {
            var sent = Ns.SendBuffer.GetReadSpan();
            var n = 0;
            for (var i = 0; i + Handshake.Length <= sent.Length; i++)
            {
                if (sent.Slice(i, Handshake.Length).SequenceEqual(Handshake))
                {
                    n++;
                }
            }

            return n;
        }

        public void Dispose() => Pm.Delete();
    }

    [Fact]
    public void AClientThatLogsInTheRealWayGetsTheHandshakeOnce()
    {
        using var l = new Login();
        l.SelectCharacter();
        l.EnterWorld();
        Assert.Equal(1, l.Handshakes());
    }

    [Fact]
    public void NothingIsSentWhilePacketsAreBlocked()
    {
        using var l = new Login();
        l.SelectCharacter();
        Assert.Equal(0, l.Handshakes());
        Assert.Equal(0, l.Ns.SendBuffer.GetReadSpan().Length);
    }

    [Fact]
    public void WithTheHandshakeTurnedOffNoneIsSentAtLogin()
    {
        var was = TazUOClientCommand.HandshakeEnabled;
        TazUOClientCommand.HandshakeEnabled = false;
        try
        {
            using var l = new Login();
            l.SelectCharacter();
            l.EnterWorld();
            Assert.Equal(0, l.Handshakes());
        }
        finally
        {
            TazUOClientCommand.HandshakeEnabled = was;
        }
    }
}

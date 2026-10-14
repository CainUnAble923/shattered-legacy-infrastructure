// TazUOClientCommandVerification.cs
//
// cc-P103: the server half of our TazUO patch's ClientCommand (0xCE id 4). Notes in shard-migration
// notes/cc-P103-tazuo-client-command.md.
//
// Facts:
//   1. The handshake the server sends at connect is 0xCE id 1, version 0, asking for id 4.
//   2. A client that answered id 4 (our patched TazUO) is sent the command; a stock TazUO (answered id 1 only) and a
//      client that answered nothing (EA's, ClassicUO) are sent nothing.
//   3. A command off the whitelist, or one that is not plain ASCII, is refused before sending, even to our client.
//   4. The tutorial stations that name the World Map, the Journal or the options window offer a button for each, only to
//      our client, and the button sends that command.

using System;
using System.Buffers;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.TazUO;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TazUOClientCommandVerification
{
    private sealed class Client : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Client()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Taz" };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(new Point3D(1720, 1720, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public int Sent => Ns.SendBuffer.GetReadSpan().Length;

        public byte[] SentSince(int from) => Ns.SendBuffer.GetReadSpan()[from..].ToArray();

        // What the client sends back: [0xCE][u16 length][u16 id]; the handler gets the bytes after the length.
        public void Answers(ushort id)
        {
            var body = new byte[] { (byte)(id >> 8), (byte)id };
            TazUOClientCommand.OnEnhancedPacket(Ns, new SpanReader(body));
        }

        public void Dispose() => Pm.Delete();
    }

    private static byte[] Expected(string command)
    {
        var length = 7 + command.Length + 1;
        var b = new byte[length];
        b[0] = 0xCE;
        b[1] = (byte)(length >> 8);
        b[2] = (byte)length;
        b[3] = 0x00;
        b[4] = 0x04;   // id 4, ClientCommand
        b[5] = 0x00;
        b[6] = 0x00;   // version 0
        for (var i = 0; i < command.Length; i++)
        {
            b[7 + i] = (byte)command[i];
        }

        return b;     // the last byte is the terminating 0
    }

    [Fact]
    public void TheHandshakeIsEnhancedPacket1AskingForClientCommand()
    {
        using var c = new Client();
        var from = c.Sent;
        TazUOClientCommand.SendHandshake(c.Ns);
        Assert.Equal(new byte[] { 0xCE, 0x00, 0x0B, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x04 }, c.SentSince(from));
    }

    [Fact]
    public void OurPatchedClientIsSentTheCommand()
    {
        using var c = new Client();
        c.Answers(1);
        c.Answers(4);
        Assert.True(TazUOClientCommand.CanRunCommands(c.Ns));
        var from = c.Sent;
        Assert.Equal(ClientCommandResult.Sent, TazUOClientCommand.Send(c.Pm, "worldmap"));
        Assert.Equal(Expected("worldmap"), c.SentSince(from));
    }

    [Fact]
    public void AStockTazUOAndAnUnknownClientAreSentNothing()
    {
        using var stock = new Client();
        stock.Answers(1);              // a TazUO without our patch answers the enable, never id 4
        Assert.True(TazUOClientCommand.IsTazUO(stock.Ns));
        Assert.False(TazUOClientCommand.CanRunCommands(stock.Ns));
        var from = stock.Sent;
        Assert.Equal(ClientCommandResult.NotCapable, TazUOClientCommand.Send(stock.Pm, "journal"));
        Assert.Equal(from, stock.Sent);

        using var other = new Client();   // EA's client or ClassicUO: answers nothing
        Assert.False(TazUOClientCommand.IsTazUO(other.Ns));
        from = other.Sent;
        Assert.Equal(ClientCommandResult.NotCapable, TazUOClientCommand.Send(other.Pm, "journal"));
        Assert.Equal(from, other.Sent);

        Assert.Equal(ClientCommandResult.NotCapable, TazUOClientCommand.Send(null, "journal"));
    }

    [Theory]
    [InlineData("cast Fireball")]
    [InlineData("organizer")]
    [InlineData("playlscript evil.py")]
    [InlineData("")]
    [InlineData("worldmapé")]
    [InlineData("journal\n-cast Kill")]
    public void ACommandOffTheWhitelistIsRefusedBeforeSending(string command)
    {
        using var c = new Client();
        c.Answers(4);
        var from = c.Sent;
        Assert.Equal(ClientCommandResult.Refused, TazUOClientCommand.Send(c.Pm, command));
        Assert.Equal(from, c.Sent);
    }

    [Fact]
    public void TheWhitelistIsThePatchesSeven()
    {
        Assert.Equal(new[] { "optlink", "paperdoll", "marktile", "radius", "worldmap", "journal", "options" },
            TazUOClientCommand.Whitelist);
        Assert.True(TazUOClientCommand.IsAllowed("radius 5 33"));
        Assert.True(TazUOClientCommand.IsAllowed("OptLink"));
    }

    [Theory]
    [InlineData("core.travel", "worldmap")]
    [InlineData("travel.wing", "worldmap")]
    [InlineData("core.chat", "journal")]
    [InlineData("core.movement", "options")]
    public void StationsOfferTheirWindowOnlyToOurClient(string key, string command)
    {
        var station = TutorialStations.Find(key)!;
        Assert.Equal(new[] { command }, new TutorialStationGump(station, true).Offered);
        Assert.Empty(new TutorialStationGump(station, false).Offered);
        Assert.Empty(new TutorialStationGump(station).Offered);
    }

    [Fact]
    public void AStationWithoutThoseWindowsOffersNothing()
    {
        Assert.Empty(new TutorialStationGump(TutorialStations.Find("core.paperdoll")!, true).Offered);
        Assert.Empty(new TutorialStationGump(TutorialStations.Find("core.league")!, true).Offered);
    }

    [Fact]
    public void TheStationButtonSendsTheCommand()
    {
        using var c = new Client();
        c.Answers(1);
        c.Answers(4);
        var gump = new TutorialStationGump(TutorialStations.Find("core.travel")!, true);
        var from = c.Sent;
        gump.OnResponse(c.Ns, new RelayInfo(TutorialStationGump.BtnWindowBase, default, default, default, default));
        var sent = c.SentSince(from);
        var expected = Expected("worldmap");
        Assert.True(sent.Length >= expected.Length, "nothing was sent");
        Assert.Equal(expected, sent[..expected.Length]);
    }
}

// TazUOShardButtonVerification.cs
//
// cc-P147: the Achievements button in our TazUO (paperdoll and top bar) sends 0xCE id 4 with the command
// "achievements"; the server answers by sending AchievementsGump, exactly as [achievements does. Notes in shard-migration
// notes/cc-P147-achievements-button.md; the client half is client/patches/0003-shard-buttons-achievements.patch.
//
// Facts:
//   1. Our client (it acknowledged id 4) pressing the button gets the achievements page, through the real packet path.
//   2. A stock TazUO, a client that answered nothing, and a client whose first id 4 is a button press (no
//      acknowledgement before it) get nothing; the press does not count as the acknowledgement.
//   3. A command that is not a shard button gets nothing, even from our client.
//   4. The bare id 4 is still the acknowledgement; the list is the patch's; only a listed name can be registered.

using System;
using System.Buffers;
using System.Text;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.TazUO;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TazUOShardButtonVerification
{
    private sealed class Client : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Client()
        {
            if (ClusterFAchievementSystem.Definitions.Count == 0)
            {
                ClusterFAchievementSystem.Configure();
            }

            ClusterFAchievementSystem.RegisterShardButton();
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Taz" };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(new Point3D(1720, 1720, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        // What the client sends: [0xCE][u16 length][u16 id][payload]; the handler gets the bytes after the length.
        public void Answers(ushort id) => TazUOClientCommand.OnEnhancedPacket(Ns, new SpanReader([(byte)(id >> 8), (byte)id]));

        public void Presses(string command)
        {
            var text = Encoding.ASCII.GetBytes(command);
            var body = new byte[2 + text.Length + 1];
            body[1] = 0x04;
            text.CopyTo(body, 2);
            TazUOClientCommand.OnEnhancedPacket(Ns, new SpanReader(body));
        }

        public bool HasPage => Pm.GetGumps().Find<AchievementsGump>() != null;

        public void Dispose() => Pm.Delete();
    }

    [Fact]
    public void OurClientsButtonOpensTheAchievementsPage()
    {
        using var c = new Client();
        c.Answers(1);
        c.Answers(4);
        Assert.False(c.HasPage);
        c.Presses("achievements");
        Assert.True(c.HasPage, "the page [achievements opens");
        Assert.Equal(ShardButtonResult.Opened, TazUOClientCommand.HandleShardButton(c.Ns, "achievements"));
    }

    [Fact]
    public void AClientThatNeverAcknowledgedGetsNothing()
    {
        using var stock = new Client();
        stock.Answers(1);                     // a TazUO without our patch
        stock.Presses("achievements");
        Assert.False(stock.HasPage);
        Assert.Equal(ShardButtonResult.NotCapable, TazUOClientCommand.HandleShardButton(stock.Ns, "achievements"));

        using var other = new Client();       // answered nothing at all
        other.Presses("achievements");
        Assert.False(other.HasPage);
        Assert.False(TazUOClientCommand.CanRunCommands(other.Ns), "a press is not the acknowledgement");
        other.Presses("achievements");
        Assert.False(other.HasPage);
    }

    [Theory]
    [InlineData("worldmap")]
    [InlineData("Achievements")]
    [InlineData("achievements now")]
    [InlineData("cast Fireball")]
    [InlineData("")]
    public void ACommandThatIsNotAShardButtonGetsNothing(string command)
    {
        using var c = new Client();
        c.Answers(4);
        c.Presses(command);
        Assert.False(c.HasPage);
        Assert.Equal(ShardButtonResult.Refused, TazUOClientCommand.HandleShardButton(c.Ns, command));
    }

    [Fact]
    public void TheBareId4IsStillTheAcknowledgementAndTheListIsThePatches()
    {
        using var c = new Client();
        c.Answers(4);
        Assert.True(TazUOClientCommand.CanRunCommands(c.Ns));
        Assert.Equal(new[] { "achievements" }, TazUOClientCommand.ShardButtons);
        Assert.Throws<ArgumentException>(() => TazUOClientCommand.RegisterShardButton("worldmap", _ => { }));
    }
}

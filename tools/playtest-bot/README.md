# The overnight play-test bot (cc-P91)

A headless UO client that logs into a **scratch copy of the TEST world** with its own accounts and characters, runs
every open play test a bot can honestly settle, soaks the shard for a while, and leaves a report and an email for
Chase in the morning. **It never marks a result**: it produces evidence, and Chase marks on the play-test page.

```
powershell -NoProfile -ExecutionPolicy Bypass -File D:\ShatteredLegacy\tools\playtest-bot\Run-OvernightPlaytests.ps1
    [-Commit <sha>] [-SkipBuild] [-SkipSoak] [-Only T-041,P64-1] [-SoakMinutes 120] [-Runs 3] [-NoEmail] [-KeepUp]
```

Results: `D:\UO\shard-migration\testing\bot-runs\<yyyy-MM-dd-HHmm>\` (report.md, results.json, email.txt, the
transcripts; the shard's logs stay out of git).

## Safety (cc-P91 section 1)

- **Never the live shard (2593, sl-modernuo) or the real test shard (2594, sl-modernuo-test).** The script records
  both containers' state, start time and image before and after, and the report says whether either changed. It
  never starts, stops, builds or writes into either.
- **Scratch only:**
  - compose project `sl-uo-bot`, container `sl-modernuo-bot`, image `sl-modernuo:bot` (never `latest`);
  - world and Configuration copy in `D:\UO\bot-shard` (outside both repos);
  - published on `127.0.0.1:2595` only, listening on `0.0.0.0:2595` inside, and advertising `127.0.0.1`.
  - `Assert-Rule1` reads the compose file (comments left out) and refuses anything else.
- **The bot refuses any other target** (`uobot/safety.py`):
  - the host must be `127.0.0.1` and the port `2595`, and the 0x8C relay must say the same;
  - anything else disconnects and aborts.
  - `tests/test_bot.py` proves it, without ever dialling a real port, even when `safety.py` is broken on purpose.
- **Its accounts:**
  - The world copy's accounts are wiped by **TestAccountWipe** (cc-P73: `SL_SHARD_ROLE=test` in the compose file,
    `clusterf.testShard.allowAccountWipe` True in the copy, no 2593 listener). The request file is in the console's
    own format (`New-WipeRequestText`, `New-WipePasswordHash`).
  - The wipe leaves one admin, `slbot-admin`.
  - Players `slbot-player`, `slbot-player2` to `4` are auto-created at first login.
  - Their random passwords are only in `secrets.local.json` (git-ignored), and are never printed.
- **Email:** only the morning report, through the autopilot's own mail settings, read only (`Send-BotReport.ps1`).

## Layout

| File | What |
|---|---|
| `Run-OvernightPlaytests.ps1` | The one entry point: rule 1, build, world, wipe, start, checks, soak, down, report, email |
| `docker-compose.bot.yml` | The scratch shard |
| `Send-BotReport.ps1` | Emails `email.txt` |
| `uobot/client.py` | The client: login, characters, actions, and the model of what the server said |
| `uobot/packets.py` | Every packet built or read, with its pinned citation |
| `uobot/huffman.py` | The game connection's decompression (pinned's table) |
| `uobot/cliloc.py` | Cliloc.enu, BWT decompression ported from pinned, cached in `D:\UO\bot-shard\cache` |
| `uobot/checks.py` | The checks: one per play-test id |
| `uobot/run.py` | Runs the checks N times each; PASS 3/3, FAIL 0/3, else FLAKY; ERROR when it cannot be set up |
| `uobot/soak.py` | The soak: walker, miner, fighter; memory and loud log lines every minute |
| `uobot/report.py` | report.md and email.txt |
| `tests/test_bot.py` | `python -m unittest discover -s tests` |

## The login, as the bot does it

Every step is verified against pinned ModernUO (`D:\UO\ModernUO-pinned\Projects`, at d4531cd94):

1. Connect to 127.0.0.1:2595. Send **0xEF** (21 bytes: seed, then client version 7, 0, 117, 0), then **0x80**
   (account login, 62 bytes).
2. Read **0xA8** (the server list); send **0xA0** with the first entry's index.
3. Read **0x8C** (the relay: address, port, auth id). **Checked against 127.0.0.1:2595**, else abort.
4. Reconnect to the relay. Send the auth id as a raw 4-byte seed, then **0x91** (game login, 65 bytes).
5. Everything the server sends from here on is Huffman-compressed, one packet per terminal code
   (`IncomingAccountPackets.cs:482`, `NetState.cs:658-660`).
6. Read **0xB9** and **0xA9** (characters and cities).
7. Send **0xF8** to create a character, or **0x5D** to play one.
8. The server sends **0xBD**; the bot answers with **0xBD** "7.0.117.0". A 7.0.117.0 client gets every protocol flag
   (`Server/Client/ClientVersion.cs:26-43`, `ProtocolChanges.Version70610`). The server's own client data is
   7.0.114.40, and Configuration-test sets `clientVerification.minRequired` to 7.0.117.0.
9. `LoginTimer` runs `DoLogin` (`IncomingAccountPackets.cs:299-345`), ending with **0x55** (login complete).

The packet layout is checked live as well: on the game connection every compressed chunk is exactly one packet, so
`packets.split_chunk` compares each chunk's length with the table below and logs any disagreement as `FRAMING`.

## Packet citations

`S` = `Server/Network/Packets/`, `U` = `UOContent/`, `ACC` = `U/Network/Packets/IncomingAccountPackets.cs`. Big-endian
unless said (`Server/Buffers/SpanWriter.cs:182-260`).

### Sent by the bot

| Id | Len | Layout | Read by (pinned) |
|---|---|---|---|
| EF | 21 | int32 seed; int32 major, minor, revision, patch | ACC:522 (registered :86) |
| 80 | 62 | Latin1 user[30]; Latin1 pass[30]; 1 byte | ACC:550 |
| A0 | 3 | int16 server index | ACC:493 |
| 91 | 65 | int32 authId (= the raw seed sent on this connection); user[30]; pass[30] | ACC:433 |
| F8 | 106 | skip 9; name[30]; skip 2; int32 flags; skip 8; byte profession; skip 15; byte genderRace (2/3 human, 4/5 elf, 6/7 gargoyle); str, dex, int; 4 (skill, value) pairs; u16 hue; i16 hair, hairHue, beard, beardHue; byte; byte city; skip 8; i16 shirtHue, pantsHue | ACC:90-139, race codes :142-150 |
| 5D | 73 | 0xEDEDEDED; name[30]; 2; int32 flags; 24; int32 slot; 4 bytes IP | ACC:248 |
| 83 | 39 | password[30] (not read); int32 index; IP | ACC:217 |
| BD | var | ASCII version, null-terminated | ACC:225 (registered :84) |
| 02 | 7 | byte direction (0x80 run); byte sequence; u32 fastwalk key (ignored) | U/Network/Packets/IncomingMovementPackets.cs:83-94 |
| AD | var | byte type; i16 hue; i16 font; ASCII lang[4]; BigUnicode text, null-terminated (no keywords: the 0xC0 bit is never set) | U/Network/Packets/IncomingMessagePackets.cs:58 |
| 06 | 5 | u32 serial | U/Network/Packets/IncomingEntityPackets.cs:61 |
| 09 | 5 | u32 serial | U/Network/Packets/IncomingEntityPackets.cs:105 |
| 6C | 19 | byte type; int32 targetId; byte flags; u32 serial; i16 x; i16 y; byte; sbyte z; u16 graphic | U/Network/Packets/IncomingTargetingPackets.cs:28 |
| B1 | var | u32 serial; int32 typeId; int32 button; int32 switchCount, switches; int32 textCount; per entry u16 id, u16 length, UTF-16BE | U/Gumps/Base/GumpSystem.IncomingPackets.cs:31 |
| 34 | 10 | int32 0xEDEDEDED; byte type (4 status, 5 skills); u32 serial | U/Network/Packets/IncomingPlayerPackets.cs:389 |
| 73 | 2 | byte, echoed | U/Network/Packets/IncomingPlayerPackets.cs:379 |
| 07 | 7 | u32 serial; u16 amount | U/Network/Packets/IncomingItemPackets.cs:35 |
| 08 | 15 | u32 serial; i16 x, y; sbyte z; byte grid; u32 container | U/Network/Packets/IncomingItemPackets.cs:69; U/Network/ContainerGridPacketHandler.cs |
| 13 | 10 | u32 item (skipped); byte layer (skipped); u32 mobile | U/Network/Packets/IncomingItemPackets.cs:44 |
| 05 | 5 | u32 serial | U/Network/Packets/IncomingPlayerPackets.cs:69 |

### Read by the bot

| Id | Len | What the bot reads | Written by (pinned) |
|---|---|---|---|
| A8 | var | servers: u16 index, Latin1 name[32], ... | S/OutgoingAccountPackets.cs:411 |
| 82 | 2 | reject reason | S/OutgoingAccountPackets.cs:402 |
| 8C | 11 | 4 address bytes (in a.b.c.d order), u16 port, int32 authId | S/OutgoingAccountPackets.cs:447 |
| A9 | var | slots (name[30] + 30); cities (new format: idx, city[32], building[32], 6 int32) | S/OutgoingAccountPackets.cs:293 |
| 86, 85, 53 | var, 2, 2 | character list update, delete result, popup | S/OutgoingAccountPackets.cs:242, 121, 131 |
| 1B | 37 | serial, body, x, y, z, direction | S/OutgoingAccountPackets.cs:190 |
| 55 | 1 | login complete | S/OutgoingAccountPackets.cs:231 |
| 1C, AE | var | header (serial, graphic, type, hue, font), name[30], text (Latin1 / BigUnicode) | S/OutgoingMessagePackets.cs:189 |
| C1 | var | header, int32 cliloc, name[30], arguments LittleUnicode | S/OutgoingMessagePackets.cs:60 |
| CC | var | header, int32 cliloc, byte affix type, name[30], affix Latin1, arguments BigUnicode | S/OutgoingMessagePackets.cs:120 |
| DD | var | serial, typeId, x, y; zlib layout; line count; zlib text (u16 count + UTF-16BE per line). No 0xB0 is ever sent | U/Gumps/Base/OutgoingGumpPackets.cs:31-38 |
| 6C | 19 | target type, targetId | S/OutgoingTargetPackets.cs:57 |
| 11 | var | status v6 for 7.0.117.0 (self): hits, str, dex, int, stam, mana, gold, weight, ... | S/OutgoingMobilePackets.cs:450, 497 |
| 3A | var | skills: full list (id+1, value*10, base, lock, cap) and single updates (0xDF) | S/OutgoingPlayerPackets.cs:119, 154 |
| 3C, 25, 24 | var, 21, 9 | container contents, an item into a container, opened container | S/OutgoingContainerPackets.cs:169, 127, 108 |
| F3 | 26 | world entity (High Seas form) | S/OutgoingEntityPackets.cs:88 |
| 78, 20, 77 | var, 19, 17 | mobile incoming (with equipment), update, moving | S/OutgoingMobilePackets.cs:601, 578, 104 |
| 22, 21 | 3, 8 | move ack, move reject | S/OutgoingMovementPackets.cs:42, 45; sequence rule Server/Network/MovementThrottle.cs:264-269 |
| 2E, 1D | 15, 5 | equip update, remove entity | S/OutgoingEquipmentPackets.cs:86; S/OutgoingEntityPackets.cs:63 |
| D6 | var | properties: int32 cliloc, u16 length, LittleUnicode | Server/PropertyList/ObjectPropertyList.cs:77, 121, 144 |
| BD | var | the server's version request; the bot answers | S/OutgoingAccountPackets.cs:112 |
| F4 | var | **ours, not pinned's**: ClusterF exploration (fog of war) state for the ClusterF client (`server/customizations/ClusterFExplorationManager.cs:121-134`). The bot ignores it |

Everything else is counted (`seen_ids`) and otherwise ignored.

## What the bot cannot do

- **No keyword speech.** NPCs that act on keywords (vendors' "buy", bankers' "balance", guildmasters, guards) ignore it.
  Commands and plain speech work.
- **Nothing visual:** art, hues as seen, paperdolls, animation, gump layout and colour. Those halves are left for
  Chase in each BOT-PARTIAL row.
- **Fresh characters are rationed:**
  - a player character stays in the world for 5 minutes after its client leaves, outside an inn
    (`Server/Regions/Region.cs:200`), and blocks its account until then;
  - so fresh characters rotate over three accounts (`slbot-player2` to `4`), each resting 5.5 minutes;
  - a fifth account per address is refused (`accountHandler.maxAccountsPerIP` 5).

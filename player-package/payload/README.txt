SHATTERED LEGACY - how to play
==============================

1. Unzip this whole folder anywhere you like (Desktop, Documents, a USB stick).
   Keep everything together; do not move files out of it.

2. Double-click  "Play Shattered Legacy.bat"

   The first time only, it looks for Ultima Online on your computer. If it is not
   there it offers to download EA's free installer (Ultima Online is free). Say yes,
   then read and accept EA's license yourself. EA's launcher then downloads the game
   (about 1.8 GB). Leave it open and wait: this window shows the progress and
   starts the game by itself when EA's download is done. Windows asks for
   permission for EA's installer and launcher; those requests come from EA.

3. The game window opens. At the login screen, type ANY account name and password
   you like. Your first login creates your account, so write them down.
   Next time, log in with the same ones.

That is all. From now on, step 2 is all you need.

  For now this is the TEST shard: the real one opens later as a fresh world.
  Anything you do on the test shard can be wiped.

  The first start after an update can show "Preparing Shattered Legacy art and
  map" with a progress bar for a few seconds. If it takes longer than a minute,
  the game starts anyway with the art you had, and the new art is used from the
  next start.


Updates
-------
  When a newer version of this package is out, "Play Shattered Legacy.bat" says so
  and asks "Update now? [Y/n]". Press Enter to update: it downloads the new version,
  keeps your account, settings, Ultima Online folder and gump positions, and starts
  the game. Type n to skip it this time; it asks again next time. If the update
  server cannot be reached, the game just starts as usual.

  Shattered Legacy's Legion scripts (SL_Mining, SL_Lumberjack and the rest, in
  TazUO's script manager) update by themselves each time you play, without asking.
  If you changed one of them, your copy is kept in app\tazuo\LegionScripts\backup\
  first. Scripts of your own are never touched. "Update Scripts.bat" gets the newest
  ones straight away.


The TEST shard
--------------
  "Play Shattered Legacy.bat" and "Play TEST Shard.bat" both connect to the test
  world, where Chase tries out new things. Nothing you do there is kept for good:
  characters, items and accounts can be wiped at any time.


If Windows or your antivirus complains
--------------------------------------
This package is not "signed" (that costs money every year), so Windows does not
recognise it. That is expected and does not mean anything is wrong.

  * A blue "Windows protected your PC" box: click "More info", then "Run anyway".
  * "Open File - Security Warning": click "Run".
  * Antivirus deleted or quarantined a file: restore it, or unzip the package again.
    The game client is TazUO, a free open-source Ultima Online client.

If you are unsure about anything, ask Chase before clicking through it.


If something goes wrong
-----------------------
  * "Cannot reach Shattered Legacy": the server may be restarting. Wait a few
    minutes and try again. If it keeps happening, tell Chase.
  * "Couldn't connect to Ultima Online. Please try again in a few moments." right
    after typing a NEW account name: the server has a limit on new accounts.
    Tell Chase; waiting will not fix it.
  * "EA's launcher closed before Ultima Online was ready": check the internet
    connection and that the drive has about 3 GB free, then double-click
    "Play Shattered Legacy.bat" again. It starts EA's launcher and waits again.
  * The game does not start, or something looks wrong: send Chase the newest files
    in app\logs\ (launch-... is what the window said; overrides-... is how long
    each part of preparing the art took).
  * In a hurry: app\Play-Skip-Art.bat starts the game with the art as it is,
    without checking or preparing anything.
  * Anything else: take a photo or screenshot of the window and send it to Chase.


For the house
-------------
  app\Play-Lan.bat    the test shard, straight to its address in the house
                      (192.168.1.58:2594)
  app\Play-Test.bat   same as "Play TEST Shard.bat" (see "The TEST shard" above)
  app\Play-Skip-Art.bat
                      starts without preparing the art (see "If something goes wrong")
  app\Setup.bat "D:\somewhere\Ultima Online Classic"
                      use an Ultima Online folder the package did not find itself
  app\Play-Test.bat -Account gargoyle
                      the TEST shard with its own saved login, one per name (a-z,
                      0-9 and -, up to 24). The first time, log in with Save Account
                      ticked; after that it logs straight in as your last character.
                      Good for a desktop shortcut per test account. TEST shard only.


What is in here
---------------
  app\tazuo\   TazUO, the game client (BSD 2-Clause licence, app\tazuo\LICENSE-TazUO.txt,
               source at https://github.com/PlayTazUO/TazUO)
               with Fiddle-Me-This interface art by NewYears1978 (CC0,
               https://github.com/NewYears1978/Fiddle-Me-This)
               and Microsoft's Visual C++ runtime file vcruntime140.dll, which
               TazUO's zlib.dll needs. It is Microsoft's, shipped unmodified under
               the Visual Studio redistribution terms
               (https://aka.ms/vs/18/redistribution).
               The custom health and mana gumps are off until you turn them on
               from the TazUO top bar (XmlGumps menu).
               Optional looks for your backpack, bags, metal chests, crates and
               spell scrolls (Options, Interface, Appearance; only you see them)
               by Carl Hamilton, Safera, Castiel, Erachiro and CorvaeOboro.
               Optional redrawn world art (Options, Interface, Appearance, "Redrawn
               world art (Zigholtul)"; off until you tick it; only you see it):
               fuller trees, crisper furniture, chests and crystals, from
               Zigholtul's UO Art Static Replacements for TazUO, in
               app\tazuo\ExternalImages\sl-redrawn-art\. They are redraws of
               Ultima Online's own world art (some made with AI, a few taken from
               EA's Enhanced Client) and ship only inside this package.
  app\music\   Shattered Legacy's own music for chosen places and fights, when any
               ships: remixes of the Ultima Online music by Zigholtul (UO Classic
               Music, SOS Mix) and ParanoiaPhD (IceYouMusic remixes), credited by
               track in app\music\shard-music.txt. The stock music stays as it is;
               type [ShardMusic off in game to keep only the stock music.
  app\map\     the land of Trammel and Felucca: "Siege of Sosaria Trammel Map 3.0"
               by Zigholtul (https://www.servuo.dev/archive/siege-of-sosaria-trammel-map.2450/).
               Shattered Legacy's own changes (app\trammel.mapops) are put on a copy of
               it at your first start. It is the author's work, built on Ultima Online's
               land, and ships only inside this package.
  app\         the scripts that set it up and start it
  No EA game file is included. Ultima Online itself comes from EA's own free
  installer at https://uo.com/client-download/

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


Updates
-------
  When a newer version of this package is out, "Play Shattered Legacy.bat" says so
  and asks "Update now? [Y/n]". Press Enter to update: it downloads the new version,
  keeps your account, settings, Ultima Online folder and gump positions, and starts
  the game. Type n to skip it this time; it asks again next time. If the update
  server cannot be reached, the game just starts as usual.


The TEST shard
--------------
  "Play TEST Shard.bat" connects to a separate, throwaway test world where Chase
  tries out new things before they reach the real one. It only works while Chase
  has it running. Nothing you do there is kept: characters, items and accounts can
  be wiped at any time, so use a different account name from your real one.


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
  * Anything else: take a photo or screenshot of the window and send it to Chase.


For the house
-------------
  app\Play-Lan.bat    connects straight to the server's address in the house
  app\Play-Test.bat   same as "Play TEST Shard.bat" (see "The TEST shard" above)
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
               The custom health and mana gumps are off until you turn them on
               from the TazUO top bar (XmlGumps menu).
  app\         the scripts that set it up and start it
  Nothing from EA is included. Ultima Online itself comes from EA's own free
  installer at https://uo.com/client-download/

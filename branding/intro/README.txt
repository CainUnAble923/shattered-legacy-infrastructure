Shattered Legacy intro, prepared 2026-10-01

intro-original.mp4   Chase's original: 800x800, 6.05 s, 30 fps, with sound, ends on white.
intro-web.mp4        Website. Cut at 5.10 s, before the fade to white, so it ends on the crest
                     in the fire. No audio track (browsers only autoplay muted video). About 1 MB.
intro-sound.mp4      Same cut with sound, short audio fade at the end. Opener for shard videos.
intro-poster.jpg     The final frame (5.06 s). Poster image while the video loads, and the
                     fallback for anyone who has reduced motion turned on.
crest-800/512/256/128.png   Same frame as square stills: Discord icon, login art, favicon source.

Website snippet (plays once, muted, and stops on the last frame; anyone with reduced motion
turned on sees the still poster instead):

  <video id="sl-intro" src="/media/intro-web.mp4" poster="/media/intro-poster.jpg"
         muted playsinline preload="auto" width="800" height="800"
         style="display:block;width:min(480px,90vw);height:auto;margin:0 auto"
         aria-label="Shattered Legacy crest"></video>
  <script>
    if (!matchMedia('(prefers-reduced-motion: reduce)').matches)
      document.getElementById('sl-intro').play().catch(function () {});
  </script>

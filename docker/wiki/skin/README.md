# Shattered Legacy wiki skin

Reskins the stock DokuWiki template to the Shattered Legacy design system (Night theme).
Nothing in the template or plugins is modified, so a DokuWiki upgrade does not undo it.

| File | Goes to (inside sl-wiki) | What |
|---|---|---|
| conf/userstyle.css | /storage/conf/userstyle.css | type, headings, tables, buttons, code |
| conf/tpl/dokuwiki/style.ini | /storage/conf/tpl/dokuwiki/style.ini | the template's color placeholders |
| conf/footer.html | /storage/conf/footer.html | the non-affiliation line on every page |
| media/wiki/logo.svg, favicon.ico, favicon.svg | /storage/data/media/wiki/ | the sword-and-ring mark |

The media files are also committed under docker/wiki/content/media/wiki so the content copy to
Haven carries them.

Install: run D:\UO\Install-WikiSkin.ps1 from the Windows box. It copies this folder to Haven,
backs up whatever it replaces, installs, and clears the CSS cache. Then set the wiki title and
tagline in Admin > Configuration Settings.

The footer also hides DokuWiki's badge row (#dokuwiki__footer .buttons). Delete that rule in
userstyle.css to bring the badges back.

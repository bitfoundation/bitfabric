# Inter

The app's typeface. Its look follows grok.com, whose own family (Universal Sans) is proprietary; Inter is the first fallback grok.com's own font stack names, and it is released under the SIL Open Font License.

The files are self-hosted, not loaded from a font CDN, so the app renders the same offline (MAUI, Windows, the PWA) and under the strict Content Security Policy of production.

- `inter-latin.woff2`: Basic Latin and Latin-1 (U+0000 to U+00FF plus the common punctuation), one variable file for weights 100 to 900. Every culture the app ships uses it for Latin text, digits included.
- `inter-latin-ext.woff2`: Latin Extended (U+0100 to U+02BA and the other extended blocks), for the accented letters Latin-1 lacks. A browser downloads it only for a page that renders one of those characters.
- `OFL.txt`: the SIL Open Font License that Inter is released under.

Both files are the `latin` and `latin-ext` subsets Google Fonts serves for Inter v4 (`fonts.gstatic.com/s/inter/v20`). `Styles/_brand.scss` declares them with the same `unicode-range` values.

Persian and Arabic text keeps coming from Vazirmatn (`../vazirmatn`), and Hindi and Chinese from the system fonts `Styles/_brand.scss` names for them.

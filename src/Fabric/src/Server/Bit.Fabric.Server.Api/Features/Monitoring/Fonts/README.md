# The SLA report's fonts

The PDF of a project's SLA report (`bitfabric-internal/specs/monitoring.md`, SLA reports) is drawn on the server with SkiaSharp, whose Linux build here (`SkiaSharp.NativeAssets.Linux.NoDependencies`) finds no fonts on the server's image. So the report brings the ones the app's pages use, embedded in the assembly (`SlaFonts.<file name>`, read by `Services/SlaPdfText.cs`):

- `Inter-Regular.ttf`, `Inter-SemiBold.ttf`: Inter 4.1, from its release's `extras/ttf`, for Latin text and digits. `Inter-OFL.txt` is its license.
- `Vazirmatn-Regular.ttf`, `Vazirmatn-SemiBold.ttf`: Vazirmatn 33.003, from its release's `fonts/ttf`, for Arabic.
- `Vazirmatn-FD-Regular.ttf`, `Vazirmatn-FD-SemiBold.ttf`: the same release's Farsi-digits cut (`misc/Farsi-Digits`), for Persian, so `1` is drawn as ۱ as on the app's pages. `Vazirmatn-OFL.txt` is the license of both.

- `NotoSansDevanagari-Regular.ttf`, `NotoSansDevanagari-SemiBold.ttf`: Noto Sans Devanagari, from `notofonts/notofonts.github.io`'s `fonts/NotoSansDevanagari/hinted/ttf`, for Hindi. `NotoSansDevanagari-OFL.txt` is its license.
- `NotoSansSC-Regular.otf`: Noto Sans SC, the Simplified Chinese subset from `notofonts/noto-cjk`'s `Sans/SubsetOTF/SC`, for Chinese: 8 MB, so one weight, emboldened for bold, and drawn as outlines so a PDF carries only the characters it draws rather than the whole font. `NotoSansSC-OFL.txt` is its license.

So every language the app speaks prints without the server's fonts (`SlaPdfTextTests` checks each of them). Any other script comes from the server's own fonts when it has any, drawn as outlines too.

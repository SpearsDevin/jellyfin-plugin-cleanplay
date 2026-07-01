# CleanPlay — content filtering for Jellyfin

VidAngel-style content filtering for Jellyfin 10.11+. CleanPlay lets you skip profanity, nudity, violence, or any other content during playback, using Jellyfin's built-in **media segments** system — no client modifications required.

## How it works

CleanPlay stores *filter segments* (time ranges) per movie/episode and publishes them to Jellyfin as media segments. Any client that supports media segment skipping (Jellyfin Web, Android TV / Google TV 0.18+, and others) will automatically skip those ranges during playback.

Filters come from three sources:

1. **Automatic profanity detection** — CleanPlay scans your media's external subtitle files (`.srt`, `.vtt`, `.ass`/`.ssa`) against a configurable word list and creates skip segments for every line containing profanity. Runs per-item from the editor, or nightly across your whole library.
2. **Manual segment editor** — search any movie or episode from the plugin settings page and tag scenes (nudity, violence, etc.) with start/end times.
3. **EDL import** — paste MPlayer-style EDL files (`start end action`, one per line), e.g. from community filter lists.

> **Honest limitation:** unlike VidAngel, there is no curated human-tagged database behind this — nudity/violence filters are only as good as the segments you (or an imported EDL) define. Also, official Jellyfin clients currently support *skipping* segments but not *muting*, so profanity is skipped (typically 1–3 seconds) rather than muted.

## Installation

1. In Jellyfin: **Dashboard → Plugins → Repositories → Add**, and use this URL:

   ```
   https://raw.githubusercontent.com/OWNER/jellyfin-plugin-cleanplay/main/manifest.json
   ```

2. Go to **Catalog**, install **CleanPlay**, and restart Jellyfin.

## Setup (important!)

Clients decide what to do with segments, so each user must enable skipping once per client. CleanPlay emits segments typed **Commercial** by default (configurable).

- **Jellyfin Web:** click your user icon → **Settings → Playback**, and under media segment actions set **Commercial** to **Skip** (or *Ask to Skip*).
- **Google TV / Android TV app:** **Settings → Playback**, set the media segment action for **Commercial** to skip automatically.

Then in **Dashboard → Plugins → CleanPlay**:

- Adjust the profanity word list (one word per line; `damn*` matches `damnit`).
- Use the **Filter Editor** to search a title, scan its subtitles, add manual scene skips, or import an EDL.
- Changes apply to playback immediately — no library scan needed.

The scheduled task **CleanPlay subtitle profanity scan** (Dashboard → Scheduled Tasks) processes your entire library daily; disable it in plugin settings if you prefer per-title scanning only.

## Notes & tips

- Profanity scanning requires an **external** subtitle file next to your media. If you only have embedded subs, extract them once (e.g. `ffmpeg -i movie.mkv -map 0:s:0 movie.srt`).
- Subtitle timing = skip timing. Well-synced subtitles give clean skips; padding is configurable in settings.
- Category → segment type mapping is configurable if you want, say, violence skips controlled separately (e.g. mapped to `Preview`) so a client can treat them differently.
- Filter data is stored server-side in `<jellyfin data dir>/data/cleanplay/`, one JSON file per item — easy to back up or share.

## Building from source

```bash
dotnet publish Jellyfin.Plugin.CleanPlay/Jellyfin.Plugin.CleanPlay.csproj -c Release -o publish
```

Copy `publish/Jellyfin.Plugin.CleanPlay.dll` into a `CleanPlay` folder inside your Jellyfin `plugins` directory and restart.

## Releasing (maintainers)

Push a tag like `v1.0.0`. GitHub Actions builds the plugin, attaches `cleanplay_<version>.zip` to a GitHub release, and updates `manifest.json` on `main` so Jellyfin servers see the update.

## License

MIT

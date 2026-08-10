# Site media

`demo.mp4` here is the walkthrough on the landing page: 1112×720, 15 fps, no
audio track, 1 min 32 s, **2.4 MB**. It came from a 45.9 MB screen capture —
the whole difference was the capture running at **120 fps**, which a screen
recording never needs. Re-encoding at 15 fps cut it about tenfold; resolution
was left untouched so the UI text stays sharp.

It is trimmed from 21.5 s to 113.5 s of the original: the opening carried the
Windows desktop and a macOS *"Record Screen" is accessing your screen*
notification (which sits in the top-right until 21 s), and the tail caught the
switch back to the host Mac desktop at about 115 s. Both ends were located by
sampling frames rather than guessed — if you re-cut this, check the top-right
corner across the whole clip before publishing.

The page loads this file and nothing else. There is deliberately **no
off-site fallback**: an external copy would be an unversioned public URL of
footage that was trimmed here for privacy reasons, and it would quietly serve
the untrimmed cut if this file ever moved. If `demo.mp4` is missing or will not
decode, the page falls back to its built-in schematic animation and still reads
as finished.

## Shrinking a screen recording

Screen captures compress far better than camera footage — large flat areas,
most pixels identical frame to frame. A 45 MB capture should land near 2–5 MB
with no visible loss.

Start here:

```
ffmpeg -i raw.mp4 -an -vcodec libx264 -crf 28 -preset slow \
       -movflags +faststart -vf "scale=1280:-2" demo.mp4
```

- `-an` — drop the audio track entirely; the hero plays muted.
- `-crf 28` — quality knob, higher is smaller. Screen text stays crisp to about
  30; try `-crf 32` if it is still heavy.
- `-vf "scale=1280:-2"` — 1280 px wide is plenty; the page never renders it
  larger. `-2` keeps the aspect ratio and an even height (H.264 requires it).
- `-movflags +faststart` — moves the index to the front so playback can begin
  before the download finishes.

Two things that cut more than any encoder setting:

- **Trim to the essential window.** A hero loop wants 20–40 seconds: the edit,
  the save, the switch, the refresh. Add `-ss 00:00:05 -t 35` *before* `-i`.
- **Drop the frame rate.** A screen recording loses nothing at 15 fps:
  add `-r 15`. This alone often halves the file.

Everything together, for a 30-second excerpt:

```
ffmpeg -ss 00:00:05 -t 30 -i raw.mp4 -an -r 15 \
       -vcodec libx264 -crf 30 -preset slow \
       -movflags +faststart -vf "scale=1280:-2" demo.mp4
```

No ffmpeg to hand? HandBrake (GUI, macOS/Windows) with the *Web > Gmail Small
30fps* preset, audio track removed, gets close.

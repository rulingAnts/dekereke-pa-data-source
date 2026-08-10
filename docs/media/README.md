# Site media

The landing page takes the first of these it can find:

1. **`demo.mp4` in this folder** — a small re-encoded copy. Cheap enough to
   autoplay as a silent loop, so the hero feels live. This is the one to aim
   for.
2. **The full recording on the project CDN** (URL in the script at the bottom
   of `../index.html`). Never fetched until the visitor clicks Play, because
   handing tens of megabytes to someone on a field connection uninvited is not
   acceptable.
3. **The schematic animation** — if neither loads, the page falls back to it
   and still reads as finished.

Adding `demo.mp4` here is therefore an upgrade, not a requirement, and needs no
other edit: the page probes for it on load.

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

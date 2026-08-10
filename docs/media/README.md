# Site media

## `demo.mp4` — the screen recording on the landing page

Drop a file named exactly `demo.mp4` in this folder and the landing page
switches from the schematic animation to the real recording automatically.
Nothing else needs editing; if the file is absent or fails to decode, the
schematic stays.

What works best:

| | |
|---|---|
| Format | MP4, H.264 video, **no audio track** (it plays muted and looping) |
| Length | Under 45 s — longer files still play, but the page will not autoplay them |
| Size | Keep under ~10 MB so the page stays quick over a field connection |
| Shape | Crop tight to the two windows; the page renders it full width |

What it should show, in one take: an edit in Dekereke, a save, the switch to
Phonology Assistant, and the row updating by itself. That single loop is the
whole product.

`ffmpeg` recipe for stripping audio and shrinking a raw capture:

```
ffmpeg -i raw-capture.mp4 -an -vcodec libx264 -crf 26 -preset slow \
       -movflags +faststart -vf "scale=1280:-2" demo.mp4
```

(`-an` drops audio, `+faststart` lets it begin playing before it finishes
downloading.)

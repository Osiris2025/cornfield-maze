# The moon's provenance — public domain, and the only reason we can ship it

**What this is:** the photographic source behind the game's moon (`Assets/Resources/Sky/T_Moon_Full.png`),
built by `scripts/moon_build.py`.

## The file

| | |
|---|---|
| Local file | `source-art/moon/moon_nasa_phase_full.jpg` |
| sha256 | `f6766945087efb8f0f91ac0ec8e74a194ff9b7b65414f2d0859669367992a172` |
| Size / format | 1024 × 1024 JPEG, 189,098 bytes |
| Original url | https://svs.gsfc.nasa.gov/vis/a000000/a005000/a005048/phase_full.1571_print.jpg |
| Retrieved | 2026-09-25 |
| Publisher | NASA's Scientific Visualization Studio (NASA SVS) |
| Item | *Moon Phase and Libration, 2023* — ID 5048, full-phase frame `phase_full.1571_print.jpg` |
| Required credit | **NASA's Scientific Visualization Studio** |
| Licence | **Public domain.** NASA imagery is generally not copyrighted; SVS asks for the credit line above. No attribution obligation attaches to the file, and no share-alike. |

## Why this source and not the first one I found

The obvious web hit for "full moon photo" is Wikimedia's `FullMoon2010.jpg` — a genuinely
beautiful Moon (a Celestron 9.25, 20 stacked frames). Its licence is **CC BY-SA 3.0**:
attribution *and* share-alike. Carrying that inside a shipped game is an obligation this
project should not take on, so it was rejected and the NASA frame used instead. The same
check is why the file above records a licence rather than a URL alone.

## What it is used for, and the one thing that must change if it is replaced

`DuskSky.SkyAtlas` used to synthesise the moon from a perlin patch with a soft limb; it read
as a blob because it *was* one — no maria, no craters. Todd, 2026-09-24: *"that picture of the
'moon' doesnt show a halloween moon, it shows wha appears to me to be a blob."*

The generator bakes real albedo (maria, Tycho's rays, crater speckle) into the RGB, the disc
mask into alpha, and an amber glow into the margin. **The wiring must honour this geometry:** the
disc's diameter is `0.62` of the texture width, so a quad meant to subtend angle `A` is sized
`A / 0.62`. If `DISC_FRACTION` changes in `scripts/moon_build.py`, the wiring changes with it —
the generator prints both numbers on every run.

## If the source is ever swapped

Any replacement must (a) be public domain or explicitly licensed for commercial use, (b) record
its licence, credit line and sha256 in this file, and (c) survive the generator's own check: the
lit region must be disc-like (fill 0.70–1.01) or `moon_build.py` refuses to build.

# COOKIE BRIEF — the player character (M0)

**For: Dough (@corn-art). Binding for the M0 cookie work.** Facts below are lifted from the locked docs — `docs/DECISIONS.md` §M0 and `docs/ASSETS-INVENTED.md`. Do not re-derive numbers; if something here looks wrong, escalate to Harrow, do not adjust it locally.

## 1. What it is

The player is a **gingerbread cookie**. The shipped silhouette is the supplied FBX's: `Assets/GingerbreadMan/gb_man.fbx` — **WE DO NOT RE-MODEL IT.** Reference frames (rendered in Blender, independent of Unity): `artifacts/review/cookie/ASSET-PREVIEW-front.png`, `ASSET-PREVIEW-side.png`, `ASSET-PREVIEW-back.png`, `ASSET-PREVIEW-head.png`.

## 2. The rejected thing we are replacing

~40 Unity primitives (`CreatePrimitive` cubes/spheres) in `Assets/Scripts/FarmWalkerController.cs` → `GingerbreadMesh.Build()`. Todd, verbatim: *"we are very unhappy with the gingerbread man."*

## 3. Measured facts of the supplied asset (locked)

- **25 bones.**
- **1,796 verts / 3,238 triangles.**
- **4 meshes, split by ROLE:** `gb_man_body` (2016 tris), `gb_man_decoration` (742 tris — **THE ICING**), `gb_man_eyes` (288), `gb_man_mouth` (192).
- **1 material**, `gb_man_texture` (colour + normal map).
- **ZERO animation clips.**
- **Bounds: 5.376 tall x 4.75 arm span.**
- Per-mesh table, texture files and provenance: `docs/ASSETS-INVENTED.md`.

## 4. UNPROVEN — it must load and render correctly IN UNITY

Blender is not Unity; the previews prove nothing about the Unity import. The **trap** to watch: a failed/white material, or a missing-texture quad. That is the failure this pass exists to rule out.

## 5. It must be ANIMATED IN CODE

The file carries no clips, so walk, idle and eat are authored in code **against the new bones**, replacing the old `AnimateModel` joint walk. Keep the **FarmRig** contract — `Hips, Torso, ArmL/R, ForeL/R, LegL/R, ShinL/R` — or update every caller in the same commit.

## 6. Scale

Bounds 5.376 tall vs the player capsule `height 1.80f` → **scale ≈ 0.335**, so it is not a 5-metre cookie. Camera values to re-check in `FarmWalkerController.cs`: **`CameraDistance 5.2f` / `CameraHeight 2.1f`**. It must read at **third-person distance** — the judge frame is a **plain launch of the Mac build at iPhone aspect (2556x1179, 2.168:1)**.

## 7. The dissolve

Rain soften/dissolve must wash the **ICING FIRST**, and the asset makes that directly possible because the icing is its **own mesh**, `gb_man_decoration` — drive the wash off the decoration mesh's material. **Do not merge the meshes.** Existing code path: `FarmWalkerController.ApplyDissolve(amount)` + the `List<bool> icingFlags` from `GingerbreadMesh.Build`; `DissolveRainSeconds = 210f`.

## 8. Tone — LOCKED 2026-09-17: this is a SCARY game

Todd, verbatim, on the supplied cookie's hollow ring eyes and big red ring mouth: *"that is by design. this will be a scary gamer"* — read as **scary game**. Source: `docs/DECISIONS.md` §Tone.

- The wide-eyed, hollow, **ring-eyes-and-red-ring-mouth face is DELIBERATE. Do not "fix" it.** Rounding the rings into friendly dot-eyes, shrinking the mouth into a smile, or adding cartoon charm has misread the product and is a defect.
- **The register is horror, not cosy.** The existing shipped material is the reference standard: anxious underscore, chase stabs, horror bed, the Crumb Beast, the dissolve with icing washing off first, near-night sky, lightning before thunder.
- **The cookie is the victim, not a mascot.** Cute-and-scary is allowed; cute-and-safe is not.
- Warm baked dough and icing may stay — a soft thing in a bad place. Do not sand off the menace to make him likeable.
- A bot that believes a change makes the game **less scary** escalates to Harrow with options laid out, rather than deciding.

## 9. The gate

M0 is a **HARD GATE**: no other milestone closes until Todd has looked at a frame from the **plain launch of the Mac build** at iPhone aspect (2556x1179, 2.168:1) and said so. Harness-posed frames are not evidence. **A green verify does not close it.**

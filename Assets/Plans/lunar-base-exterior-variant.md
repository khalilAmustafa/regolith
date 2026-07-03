# Project Overview
- **Game Title:** Regolith (lunar survival / base-restoration)
- **High-Level Concept:** A third-person astronaut explores a lunar base exterior, restores power (solar), oxygen (O2 extractor), and material (regolith) to open/restore a buried airlock base entrance.
- **Players:** Single player
- **Inspiration / Reference Games:** Lunar survival/base sims (Deliver Us The Moon, Lunar outpost builders)
- **Tone / Art Direction:** Matte, dusty, realistic lunar regolith — low contrast, neutral grey, not glossy/blue/white
- **Target Platform:** StandaloneWindows64
- **Screen Orientation / Resolution:** Landscape (PC)
- **Render Pipeline:** URP (URP-HighFidelity)

## What this task is (and is NOT)
This task creates a **safe duplicate terrain variant** and reshapes/retextures **only that duplicate** into a **mostly flat dusty lunar worksite with a shallow embedded base-entrance**.

**NOT a crater level.** Explicitly avoid: centered crater, deep bowl, central mountain, canyon, sharp cliff walls, tire tracks, trench road, all-over noisy rock, floating/hole-dropped airlock.

# Game Mechanics
## Core Gameplay Loop
Walk the open lunar yard → locate the embedded airlock entrance set into a shallow excavation wall → service the three objective pads (solar / O2 / regolith) → open/restore the base. The terrain must *read* this loop at a glance.

## Controls and Input Methods
No control changes. Existing third-person controller (Legacy Input Manager). Terrain must be walkable: gentle slopes only on the playable plain, shelf, and path (no steps/cliffs blocking the route).

# UI
No UI changes in this task.

# Key Asset & Context

## Source & safety boundaries
- **Source terrain (DO NOT EDIT):** GameObject `Terrain`, TerrainData `Assets/New Terrain 1.asset` (size 60×18×60, pos `(-29.67, -5.98, -31.70)`, 3 layers: TL_Moon_Regolith_Grey, TL_Moon_Dust, TL_Moon_Shadow).
- **New duplicate (the ONLY edit target):**
  - GameObject: `Terrain_ReferenceFaithful_Test`
  - Position: source pos **+200 on X** → `(170.33, -5.98, -31.70)`
  - TerrainData copy: `Assets/_Generated/TerrainVariants/TD_ReferenceFaithful_Test.asset`
- **Leftover from previous attempt:** `Terrain_Variant_AirlockTest` (+40 X) and `Assets/_Environment/TerrainVariant_AirlockTest.asset`. Recommendation: **delete this old variant** (it is the disliked crater version) for a clean comparison. See Open Decision #3.

## Existing assets to reuse
- **Heightmaps (flat/rolling only):** `Assets/All In One - Heightmaps/Heightmaps/HillsDry/T_HeightMap4k (1).png` (primary candidate for subtle background rolling), `FlatIslands/T_HeightMap4k.png`, `Desert/T_HeightMap4k.png`.
  - **Rejected (cause craters/cliffs/mountains):** `Moon`, `Mars`, `Canyons*`, `DryCanyons`, `*Mountains`, `Volcano*`, `Valley*`.
- **Dust textures:** `Assets/_Materials/Moon/Textures/Moon01/moon_01_diff_4k.jpg` (base dust), `moon_01_nor_gl_4k.exr` (normal), `Assets/_Materials/Moon/Textures/LunarRock/lunar-rock-albedo.png` (+ `lunar-rock-normal-u.png`) for rock accent.

## New terrain layers to create (under `Assets/_Generated/TerrainMaterials/ReferenceFaithful/`)
1. **TL_Reference_Dust_Base** — matte neutral grey dust. Source: `moon_01_diff_4k.jpg`; metallic 0; smoothness 0.05; normalScale ~0.18 (normal map `moon_01_nor_gl_4k`); tileSize 5×5; diffuseRemapMin/Max tuned to neutral grey, reduced contrast.
2. **TL_Reference_Dust_Dark** — darker powdery dust for the entrance cut/dips. Source: `moon_01_diff_4k.jpg` with `diffuseRemapMax ≈ 0.6` (darken); metallic 0; smoothness 0.04; normalScale low; tileSize 6×6.
3. **TL_Reference_Rock_Accent** — darker grey rock for steep edges & far ridges only. Source: `lunar-rock-albedo.png` (+ normal); metallic 0; smoothness 0.09; normalScale ~0.45; tileSize 4×4.

## Coordinate plan (terrain-local meters, 0..60 on X/Z)
- **Facing convention:** airlock at the **back-side wall**, door faces the **open foreground (+X side / front)**.
- **Foreground plain:** front 50–60% of terrain → broad flat work yard.
- **Shallow entrance cut:** back-side, soft oval ~30 m wide × 18 m deep, floor lowered **0.9 m** below plain, smooth falloff.
- **Embedded airlock wall:** back/side edge of the cut rises **~+1.5 m** above plain behind the door footprint; door face left clear/visible.
- **Shelf:** flat 10×8 m directly in front of airlock (at cut-floor level), edges smoothed.
- **Approach path:** 8 m wide, gentle slope plain→shelf, center lowered only 0.15 m (no trench, no tracks).
- **Pads:** Solar 16 m (front-left, flat, slightly raised/exposed); O2 9 m (near shelf, flat); Regolith 12 m (near O2, slightly rough but walkable).
- **Background ridges:** far edge band, low rolling **1.5–4 m**, framing only.

## Height/relief strategy (gameplay-friendly, low relief)
- Rebuild the variant heightfield **procedurally from a flat base** (do NOT inherit the original basin shape), so there is no crater/bowl.
- Optionally lower the variant TerrainData vertical size from 18 → **10** for finer low-relief control (variant only; see Open Decision #2).
- Blend `HillsDry` heightmap at **very low influence (≤8%)**, masked to the **far background band only**, for natural rolling character. Procedural control handles the plain, cut, shelf, path, pads.
- Heightmaps sampled non-destructively via a RenderTexture readback (`Graphics.Blit` → `ReadPixels`) so no PNG import settings are modified.

# Implementation Steps

### Step 1 — Create safe duplicate (heights + TerrainData copy)
- **Description:** Duplicate source `Terrain` GameObject → rename `Terrain_ReferenceFaithful_Test`, offset +200 X. `AssetDatabase.CopyAsset("Assets/New Terrain 1.asset", "Assets/_Generated/TerrainVariants/TD_ReferenceFaithful_Test.asset")` (create folders). Assign the copied TerrainData to the duplicate's Terrain + TerrainCollider. Verify the duplicate's TerrainData path != original.
- **Assigned role:** main agent
- **Dependencies:** None
- **Parallelizable:** No

### Step 2 — Procedural low-relief base shaping
- **Description:** On the variant TerrainData only: set vertical size (per Decision #2), write a new heightfield = flat plain + subtle lunar bumps (small-amplitude Perlin), with extreme roughness removed on the playable area. Establish base plain level and far-background rolling band.
- **Assigned role:** main agent
- **Dependencies:** Step 1
- **Parallelizable:** No

### Step 3 — Carve shallow entrance, embedded wall, shelf, path, pads
- **Description:** Apply masked height operations per the Coordinate plan: shallow oval cut (0.9 m), raised embedded back wall (+1.5 m) behind door footprint with door face clear, flat 10×8 shelf, 8 m smooth path (center −0.15 m), Solar/O2/Regolith pads, then localized smoothing of all functional flats. Keep door accessible and not buried.
- **Assigned role:** main agent
- **Dependencies:** Step 2
- **Parallelizable:** No

### Step 4 — Light background heightmap blend (optional, ≤8%)
- **Description:** Sample `HillsDry` heightmap via RenderTexture readback; blend at ≤8% masked to the far background band for natural ridge variation. Log the heightmap used (or log "none" if Decision #1 = procedural-only).
- **Assigned role:** main agent
- **Dependencies:** Step 3
- **Parallelizable:** No

### Step 5 — Create dust-focused terrain layers
- **Description:** Create the 3 `TL_Reference_*` terrainlayer assets under `Assets/_Generated/TerrainMaterials/ReferenceFaithful/` reusing existing dust/rock textures with matte settings (per Key Asset & Context). (If Decision #1b = AI generation, generate a tileable regolith texture first and use it for the base layer.)
- **Assigned role:** main agent
- **Dependencies:** Step 1
- **Parallelizable:** Yes (independent of Steps 2–4)

### Step 6 — Assign layers & paint splatmaps
- **Description:** Set variant TerrainData.terrainLayers to the 3 new layers. Compute alphamaps from masks: Dust_Base 75–90% everywhere; Dust_Dark inside the entrance cut & soft dips; Rock_Accent only on steep slopes (by computed gradient) and far ridges. Do not paint the whole terrain rocky.
- **Assigned role:** main agent
- **Dependencies:** Step 3, Step 5
- **Parallelizable:** No

### Step 7 — Refresh collider, verify, capture
- **Description:** Reassign TerrainCollider.terrainData, SyncHeightmap. Numerically verify: original heightmap byte-identical (unchanged), variant has unique TD, plain is flat, cut is shallow (0.5–1.2 m), no crater, walkable slopes. Render a hillshade/relief preview + multi-angle scene capture, validate against the visual target, fix if needed (max 3 iterations). Clean up any scratch preview files.
- **Assigned role:** main agent
- **Dependencies:** Step 6
- **Parallelizable:** No

# Verification & Testing
1. **Original untouched:** compare `New Terrain 1.asset` heights before/after — must be identical; confirm its GameObject/TD unreferenced by edits.
2. **Unique TerrainData:** variant TD path = `Assets/_Generated/TerrainVariants/TD_ReferenceFaithful_Test.asset` ≠ original.
3. **No gameplay/player/camera/script/UI/mission/O2/inventory/animation changes** (none touched).
4. **Not a crater:** sample center vs edges — center must NOT be a deep low point; playable plain variance < ~0.4 m.
5. **Shallow readable entrance:** cut depth within 0.5–1.2 m, smooth edges.
6. **Airlock zone:** embedded wall behind footprint, flat shelf in front, door face clear (not in middle of a hole).
7. **Walkability:** max slope on plain/shelf/path below controller threshold (no cliffs/steps).
8. **Texture:** dust-dominant (Dust_Base ≥75%), rock accent only on steep edges/ridges.
9. **Logs include:** terrain duplicated, TerrainData created, heightmap(s) used, terrain layers created, and a summary of changes.

# Open Decisions (please confirm at approval)
1. **Terrain texture source:** (a) **Reuse existing** moon dust/rock textures with matte/neutral tuning — *recommended, no dependency*; or (b) **AI-generate** a new tileable regolith texture for the base layer (uses asset generation).
2. **Variant vertical scale:** (a) keep size.y = 18 (matches original); or (b) **lower to 10** for finer low-relief control — *recommended for a gameplay-friendly flat look* (variant only, original unaffected).
3. **Old leftover variant:** (a) **delete** `Terrain_Variant_AirlockTest` + its TerrainData for a clean comparison — *recommended*; or (b) keep it.

*Defaults if you don't specify: 1=(a) reuse existing, 2=(b) lower to 10, 3=(a) delete old variant.*

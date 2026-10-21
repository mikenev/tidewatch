# Tidewatch: sprite and sound list (M1 handoff for Art Dev and Audio Dev)

All assets are original. Drop files at the paths below with these exact names; the game loads them by name
(`Resources.Load`) and falls back to placeholder shapes/silence when a file is missing, so you can deliver in any order.
An editor import script (`Assets/Editor/TidewatchImport.cs`, M1) applies the settings below automatically to anything in
these folders, so no manual Inspector work is needed.

## World scale
- Camera: fixed orthographic, top-down, whole map on one screen. View = **19.2 x 10.8 world units = 1920 x 1080 px**.
- **100 pixels per unit** (Unity's default; matches Broomstick Bash's 1 unit = 1 m scale. Broomstick Bash itself has no
  2D sprites, its art is 3D FBX, so there is no other PPU to inherit).
- Author at 1x (the sizes below). Flat, bright, outlined style, ~3 px dark outline. Transparent PNG, sRGB.
- Ships, boats and turret barrels are drawn **top-down, bow pointing right (+X)**; the game rotates them along the lane.
- Shared sprites are recolored per island by tint, so draw them in neutral/light base colours where a tint should show
  (sails, flags, hull trim).

## Sprite import settings (all sprites)
Texture Type Sprite (2D and UI) · Sprite Mode Single (sheets: Multiple, sliced by grid, frames left to right) ·
Pixels Per Unit 100 · Mesh Type Full Rect · Pivot Center (unless noted) · Filter Bilinear · Wrap Clamp ·
Generate Mip Maps off · Alpha Is Transparency on · Max Size 2048 · Compression Normal Quality (WebGL default format).

## Sprites, `Assets/Resources/Sprites/`
| File | Size (px) | Notes |
|---|---|---|
| `coralcay_bg.png` | 1920 x 1080 | Coral Cay terrain, opaque: turquoise sea, white sand, palms. Keep the lane and slots readable (M1 layout PNG to follow on the PR) |
| `city_harbor.png` | 320 x 320 | Walled port town at the lane's end (left side), shared, tinted per island |
| `slot_land.png` | 128 x 128 | Empty build plot on land |
| `slot_buoy.png` | 96 x 96 | Empty mooring buoy in the water |
| `tower_cannon_1.png`, `tower_cannon_2.png` | 128 x 128 | Cannon Battery base, level 1 and upgraded |
| `tower_cannon_barrel.png` | 96 x 48 | Rotating barrel, pivot at the left-centre (0, 0.5) |
| `ship_gunboat_1.png`, `ship_gunboat_2.png` | 160 x 96 | Defensive Gunboat, level 1 and upgraded |
| `enemy_sloop.png` | 160 x 96 | Standard pirate ship |
| `enemy_dinghy.png` | 96 x 56 | Raider Dinghy, small and fast |
| `cannonball.png` | 24 x 24 | Projectile |
| `fx_splash.png` | 384 x 96 | 4 frames of 96 x 96 (miss / ship sinking splash) |
| `fx_hit.png` | 384 x 96 | 4 frames of 96 x 96 (hit puff) |
| `ui_heart.png`, `ui_gold.png` | 64 x 64 | HUD icons |
| `ui_button.png` | 192 x 96 | Button background, 9-slice border 24 px on all sides |
| `ui_panel.png` | 256 x 256 | Menu panel, 9-slice border 32 px |
| `ui_icon_cannon.png`, `ui_icon_gunboat.png` | 128 x 128 | Build menu icons |
| `ui_icon_upgrade.png`, `ui_icon_sell.png`, `ui_icon_pause.png`, `ui_icon_speed.png`, `ui_icon_wave.png` | 96 x 96 | Buttons |
| `ui_win.png`, `ui_lose.png` | 1024 x 256 | Victory / defeat banners |

Later milestones (same rules, same folder; not needed for M1): `tower_mortar_{1,2,3}`, `tower_chain_{1,2,3}` (128 x 128),
`ship_nettender_{1,2,3}` (160 x 96), `enemy_brig` (192 x 112), `enemy_fireraft` (112 x 80), `enemy_galleon` (288 x 160),
level-3 variants of the cannon and gunboat, `twinreefs_bg`, `stormisle_bg` (1920 x 1080), `fx_rain`, `fx_lightning`.

## Audio, `Assets/Resources/Audio/`
Same convention as Broomstick Bash: **.ogg Vorbis, 44.1 kHz**, peaks at or below -1.5 dBFS.
- SFX: mono. Import: Load Type **Decompress On Load**, Compression **Vorbis, quality 0.4**, Preload on.
- Music: stereo. Import: Load Type **Streaming**, Compression **Vorbis, quality 0.5**, Load In Background on.
- Loops (music beds) must loop seamlessly; looping is set on the AudioSource, not in the importer. Everything else is a one-shot.

| File | Length | Notes |
|---|---|---|
| `Music/theme_shanty_loop.ogg` | 30-60 s loop | Sea-shanty-style main theme (title, build phase) |
| `Music/battle_loop.ogg` | 30-60 s loop | During waves |
| `Music/victory_sting.ogg`, `Music/defeat_sting.ogg` | 2-4 s | Win / lose screens |
| `SFX/cannon_fire.ogg` | < 0.6 s | Cannon Battery shot |
| `SFX/gunboat_fire.ogg` | < 0.4 s | Gunboat broadside (fires often; keep it light) |
| `SFX/ball_hit.ogg` | < 0.4 s | Cannonball hits a ship (wood crack) |
| `SFX/ship_sink.ogg` | < 1.2 s | Ship sunk (gold earned) |
| `SFX/harbor_breach.ogg` | < 1 s | Enemy reaches the harbor, a heart is lost |
| `SFX/coin.ogg` | < 0.4 s | Gold gained (wave bonus, early call) |
| `SFX/build.ogg` | < 0.8 s | Defense built |
| `SFX/upgrade.ogg` | < 0.8 s | Defense upgraded |
| `SFX/sell.ogg` | < 0.6 s | Defense sold |
| `SFX/wave_horn.ogg` | 1-2 s | Wave starts |
| `SFX/ui_click.ogg` | < 0.15 s | Any button |
| `SFX/ui_error.ogg` | < 0.3 s | Not enough gold |

## M2 additions (optional; the game falls back until these exist)
| File | Size / length | Notes |
|---|---|---|
| `Sprites/tower_chain_barrel.png` | 80 x 32 px | Chain-Shot Post rotating barrel, pivot at left-centre (0, 0.5) like the cannon barrel |
| `Audio/SFX/mortar_fire.ogg`, `mortar_impact.ogg`, `chainshot_fire.ogg`, `net_cast.ogg`, `fireraft_explode.ogg`, `galleon_horn.ogg`, `ship_move.ogg` | delivered (PR #2) | Wired in `GameAudio`; each falls back to an M1 clip if missing |

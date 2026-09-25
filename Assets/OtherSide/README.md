# The Other Side — Unity asset pack

This pack contains every visual and audio asset from the three.js prototype, exported for Unity. The prototype never loaded any files: it built all of these things in code at runtime (boxes, cylinders, spheres, canvas textures and synthesized audio). This pack rebuilds each of them with the same dimensions, colours and materials, then exports them as real files you can import into Unity.

## What's inside

| Folder | Contents |
|---|---|
| `Models/FBX/` | 64 models as FBX. Unity imports these natively. |
| `Models/GLB/` | The same 64 models as glTF binary. Use these with **glTFast** (`com.unity.cloud.gltfast`) if you want the emissive, metal and transparent material settings to come through exactly. |
| `Textures/` | Exit sign, code-panel screen, cracked-wall texture, the four glow icons (world size and 256 px UI size), and a soft particle dot. |
| `Audio/` | The 12 prototype sound effects as 16-bit 44.1 kHz WAV, normalised to −1 dBFS. |
| `Fonts/` | Chakra Petch and Barlow, the HUD fonts, with their SIL OFL licences. |
| `Data/` | `world.json` plus `maps/side_a.txt` and `maps/side_b.txt`. These are the exact files the server loads. |
| `Reference/` | A contact sheet of all models, plus a top-down blueprint of each side with rooms, objects and spawn points. |
| `manifest.json` | A machine-readable list of every asset. For each one it gives the object type, child node names, what to animate, collider size, lights and triangle count. |
| `palette.json` | Every material: base colour, metallic, roughness, emission, opacity, and which Unity shader to use. |

The whole pack is about 60k triangles in total. Every asset is a placeholder-quality blockout, so you can later swap in art-team meshes without changing any logic, as long as the node names stay the same.

## Conventions (all assets follow these)

- **Scale:** 1 unit = 1 tile = 1 m. The FBX files are exported with *Apply Scale: FBX All*, so the import Scale Factor stays at 1. All transforms are scale (1,1,1), because scales are baked into the geometry.
- **Pivot:** the centre of the tile, at floor level (y = 0). Floor tiles have their top surface at y = 0 and extend 0.1 below. Walls are 1.35 m high.
- **Facing:** the front of every model points toward **+Z**. Wall-mounted items (switch, light switch, valve, code panel, cabinet, pipes, vending machine, bookshelf, couch) have their back at −Z, which is where the wall goes.
- **Placing objects from the map:** I recommend `unityPos = (x + 0.5, 0, -(y + 0.5))`. That puts map north at the top of the screen, the same as in the prototype. With that mapping, rotate wall-mounted items to face away from the wall:

| Wall is on this side | Rotation Y |
|---|---|
| North (y − 1) | 180 |
| South (y + 1) | 0 |
| West (x − 1) | 90 |
| East (x + 1) | −90 |

- **Doors and laser runs:** these are authored along local X. Leave the rotation at 0 when the walls are at x ± 1. Otherwise rotate them 90°.
- **Mirroring:** Unity's importer mirrors X when converting from right-handed to left-handed coordinates. Only tiny asymmetric details are affected (the key's teeth and the bomb's fuse lean), so this is harmless.

## Mapping design-doc object types to prefabs

| `type` in world.json | Prefab | Animated child | How to drive it from state |
|---|---|---|---|
| `button_door`, `code_door`, `key_door`, `exit_door` | `Door_Button` / `Door_Code` / `Door_Key` / `Door_Exit` | `LeafL`, `LeafR`, `Lamp` | When open: LeafL x −0.25 → −0.71 and LeafR x +0.25 → +0.71, smoothed at k = 5/s. Swap Lamp between `M_Lamp_Red` and `M_Lamp_Green`. The collider is on only while closed. |
| `lasers` | `Laser_Post` at each cell edge, plus `Laser_Beams_1m` per cell | `Beams` | While on: beams visible, collider on, red point light. Flicker `M_LaserBeam` opacity. |
| `fire` | `Fire_Tile` per cell | `Flames/Flame_*`, pivot at the flame base | Flicker scale.y. To extinguish, scale Flames to 0 over about 0.7 s and emit `FX_SteamPuff`. Scorch stays. |
| `button`, `final_button` | `Button` / `FinalButton` | `Cap` | Tint the Cap with the glow colour. When pressed: Cap y −0.08 and switch to `M_ButtonCap_Pressed`. This is one-shot. |
| `switch` | `LeverSwitch` | `Lever`, pivot at the hinge | rotation.x goes from −0.6 rad (off) to +0.6 rad (on). A switch with `latch: true` stops accepting input once on. |
| `light_switch` | `LightSwitch` | `Lever` | Same as `switch`. The room's `lights` key controls darkness. |
| `valve` | `Valve` | `Wheel` | Spin about local Z. This is a latch. |
| `keypad` | `Keypad` | `Screen` | Swap `M_Keypad_Locked` to `M_Keypad_Unlocked`. This opens your 4-digit UI and sends `interact{action:"submit", value}`. |
| `code_panel` | `CodePanel` | `CodeText_Anchor` (empty) | Put a TextMeshPro object here using Chakra Petch Bold in `#8ef0b0`. Its text is `object.display` from the `world` message. |
| `bomb`, `key` | `Pickup_Bomb` / `Pickup_Key` | `Bomb` / `Key` | Bob and spin while at `"home"`. When the value changes, rise and shrink, then hide. |
| `bombable_wall` | `BombableWall` | `Intact`, `Rubble` | **Disable Rubble in the prefab.** When broken: Intact off, Rubble on, collider off. |
| unknown | (use a magenta cube) | | Keep the doc's rule that a broken map shows up visibly instead of disappearing. |

Other assets:

- **Glow rings:** `GlowRing_A`, `GlowRing_B`, `GlowRing_Both` and `GlowRing_Info` go under every object, based on the colour the server derives. Pair each ring with its `Icon_*` sprite floating 1.55 m above the object. The colours are A `#ff9f43`, B `#3ecfd8`, both `#c49bff` and info `#8ef0b0`, and the icons are ▲ ● ◆ ✱ so colour-blind players can tell them apart.
- **Environment:** `Wall_Full` is for `#` cells that touch floor, and `Wall_Low` is for solid interior cells. `Floor_<Theme>_A` and `Floor_<Theme>_B` form a checkerboard: use A where (x+y) is odd and B where it's even. `Floor_Grout` goes underneath. `Water_Tile` fills a room while its `flooded` key is true.
- **Characters:** `Player_A` (orange) and `Player_B` (cyan) have a `Lamp_Anchor` for a point light (range 7.5). `Boss_Drone` has a `Hover` child at 0.75 m holding the `Eye` and `Halo`.
- **Decor:** themed blocking props. `manifest.json` lists which room themes use each one. `Decor_CafeTable` is walkable around and should have no blocking collider.

## Import steps

1. Copy the `OtherSide` folder into `Assets/`.
2. Choose how to import the models:
   - **FBX (no packages needed):** select all the FBX files and, in the Model tab, tick **Bake Axis Conversion**. This stops Unity from adding a −90° X rotation to each root. Then use Materials → *Extract Materials*. Then fix emission and transparency using `palette.json`. The FBX format drops most emissive and alpha settings.
   - **GLB (recommended if you use URP):** install glTFast from Package Manager → *Add by name* → `com.unity.cloud.gltfast`, and import `Models/GLB`. Emissive, metal/roughness and alpha settings all come across correctly.
3. For materials whose `shader` in `palette.json` says *Unlit / Additive* (lasers, fire, glow rings, player ring), switch to *URP → Particles → Unlit* with Blend Mode set to Additive. No importer creates additive materials automatically.
4. Import `MaterialLibrary` once. It holds a swatch of every material, including state variants such as `M_Lamp_Green`, `M_Keypad_Unlocked` and `M_ButtonCap_Pressed` that no model uses by default.
5. Import the fonts through TextMeshPro → Font Asset Creator.
6. For the audio files, use *Decompress On Load* for short clips and 3D spatial blend. The prototype panned each sound to the player who caused it.

## Camera and lighting values from the prototype

- **Camera:** top-down perspective, placed 22 m above and 8 m behind the player. Walk speed is 4.4 tiles/s.
- **Vision radius:** 8 tiles normally, 2.4 in dark rooms. While the drone debuff is active: 3.2 tiles and 0.55× speed for 15 s.
- **Rendering:** ACES tone mapping at exposure 1.0, with soft shadows.
- **Fog of war:** a radial mask around the player that is multiplied into every material. Recreate it as a URP full-screen pass or a shader-graph sub-graph that reads the player position, the dark-room rectangles and the fire light holes.

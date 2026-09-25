# The Other Side — Unity client

**Unity 6000.6.3f1** (everyone must use exactly this version).

The client renders its own side of the floor from the server's `world` message and redraws
objects from `patch` messages. It bundles no map data. Movement is never synced.

## Run

1. Open `Assets/Scenes/Main.unity` and press Play (any scene works; `GameManager` bootstraps itself).
2. **Create a room** and read the code on the waiting screen to your partner, who types it in and hits **Join room**. The server is `wss://phoenix-zv1i.onrender.com/ws`;
   to use a local one, change `GameManager.DefaultServerUrl` (e.g. `ws://localhost:8080/ws`).
3. **Offline test** loads `Assets/Resources/FakeWorld.json` and runs a tiny fake server
   (rules / inventory / codes / `exit_open`) so the client can be tested without Go.
4. **Tutorial** plays `Assets/Resources/TutorialWorld.json` offline (maps in
   `Resources/Maps/tutorial_a.txt` / `tutorial_b.txt`; client only, the server never loads it):
   one small room per mechanic on each side. Tips beside what's in your current room and its
   doorways say what each thing does and how to get past it (changing with its state), hints
   on the controls show until you've used them, and a banner gives the goal. Online games also
   start with the object tips on; **Tips** in the HUD turns them on or off in any game.

Two clients side by side: build a standalone player and run it next to the editor
(`runInBackground` is on).

## Controls

| Key | Action |
| --- | --- |
| WASD / arrows | Move |
| E / Space | Interact with the highlighted object |
| Digits, Enter, Esc | Keypad |
| F1 | Debug overlay: state keys + last messages (offline: click a bool to flip it) |
| F2 | Toggle the vision mask |
| F3 | Level editor (offline only), see below |

## Layout

| Path | What |
| --- | --- |
| `Scripts/Net/Protocol.cs` | The wire contract (envelope, message DTOs, `world` schema) |
| `Scripts/Net/GameConnection.cs` | NativeWebSocket, `join`, reconnect with token for 2 min |
| `Scripts/Net/FakeServer.cs` | Offline stand-in for the server |
| `Scripts/Core/WorldState.cs` | State mirror + key index (key → objects that read it) |
| `Scripts/Core/World.cs` | Builds Tilemap walls/floor, rooms, objects from `world` |
| `Scripts/Objects/*` | One class per object type, each with `Apply(value)` |
| `Scripts/Player/*` | Rigidbody2D controller, room tracking, camera + vision mask |
| `Scripts/UI/*` | IMGUI screens and debug overlay |
| `Resources/Tutorial.json` | Tutorial tip text (`TutorialTips`): per object type and state, control hints, goal |

## Level editor

**Level editor** on the join screen (or F3 during an offline game) opens an in-game editor for
the offline level. Every edit rebuilds the world live, so you can walk around and play-test as
you go (no clipping while the editor is open; scroll to zoom).

- **Tools:** Select (edit or drag objects and rooms), Wall / Floor / Erase (paint), Spawn,
  Object (place any type), Room (drag a rectangle; set theme, dark, flooded), Link.
- **Link** connects a trigger to what it controls, writing the rule for you: button → door,
  switch → lasers (toggle), button → fire (put out), valve → flooded room, light switch → dark
  room, keypad → code door, keypad → code panel (requires that code). Key doors, bombable walls
  and latch buttons get their rules when placed; `exit_open` = all latch buttons pressed.
- Right-click deletes, Delete removes the selection, Ctrl+Z undoes. W±/H± resize the map.
- **Save** writes `Assets/Resources/Levels/<name>.json` (FakeWorld.json format; a build saves
  to `persistentDataPath`). **Load** opens any saved level or the built-in FakeWorld.

Levels are one side, for offline play; the live server still uses its own `world.json`.

## Art

The game draws the 3D models from the Other Side asset pack (`Assets/OtherSide`, see its
README). Gameplay and physics stay 2D on the XY plane; `Scripts/Core/Art.cs` lays the Y-up
models onto that plane, and the camera is the prototype's tilted perspective (22 m up, 8 m back).

- `Assets/Resources/ArtCatalog.asset` is what the game loads at runtime. It is generated: after
  changing `palette.json` or a model, run **Tools → Other Side → Build Assets**.
- Floors use the room's `theme` (guessed from the room id when the server omits it), walls are
  `Wall_Full`/`Wall_Low`, and each large room gets a themed prop in its free corners.
- Each object class picks its model (`ModelName`) and animates it from its key: door leaves and
  lamp, lever angle, button cap, valve wheel, laser beams, fire flames, pickups bobbing.
- Without the catalog the client falls back to the procedural sprites and the orthographic camera.

To replace an object's look with your own prefab instead, create
`Assets/Resources/ObjectCatalog.asset` (Create → Escape Office → Object Catalog) and map a
`type` to a prefab. Assign the prefab's `body` SpriteRenderer, or give it an Animator with an
`Active` bool that follows the object's key. Sounds: the pack's `SFX_*` clips play for the
server's cue names; drop clips in `Assets/Resources/Sfx/<effect>` (e.g. `door_clunk.wav`) to
override one.

## Git

One owner for `Main.unity`; everything else is code or prefabs. Install Git LFS
(`brew install git-lfs && git lfs install`) before adding any art or audio.

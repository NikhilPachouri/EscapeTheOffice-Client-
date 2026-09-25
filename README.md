# The Other Side — Unity client

**Unity 6000.6.3f1** (everyone must use exactly this version).

The client renders its own side of the floor from the server's `world` message and redraws
objects from `patch` messages. It bundles no map data. Movement is never synced.

## Run

1. Open `Assets/Scenes/Main.unity` and press Play (any scene works; `GameManager` bootstraps itself).
2. Enter a room code and **Join**. The server defaults to `wss://phoenix-zv1i.onrender.com/ws`;
   the field on the join screen can point at a local server instead (e.g. `ws://localhost:8080/ws`).
3. **Offline test** loads `Assets/Resources/FakeWorld.json` and runs a tiny fake server
   (rules / inventory / codes / `exit_open`) so the client can be tested without Go.

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

## Art

Everything is procedural placeholder art. To replace an object's look, create
`Assets/Resources/ObjectCatalog.asset` (Create → Escape Office → Object Catalog) and map a
`type` to a prefab. Assign the prefab's `body` SpriteRenderer, or give it an Animator with an
`Active` bool that follows the object's key. Sounds: drop clips in `Assets/Resources/Sfx/<effect>`
(e.g. `door_clunk.wav`) to replace the synthesized ones.

## Git

One owner for `Main.unity`; everything else is code or prefabs. Install Git LFS
(`brew install git-lfs && git lfs install`) before adding any art or audio.

# Scripts

Runner game code. Each folder is its own namespace under `ButchersGames`.

| Folder | Namespace | Contents |
|---|---|---|
| `Core/` | `ButchersGames.Core` | `GameManager` (game flow and `GameState`), `SceneSingleton<T>`, `GameTags` |
| `Core/Utils/` | `ButchersGames.Core` | `PathUtility` (waypoint path math), `RoundRobinPool<T>`, `ColliderExtensions.IsPlayer` |
| `Player/` | `ButchersGames.Player` | `PlayerController` (path following), `SteeringInput` (swipe/keyboard), `PlayerAppearance`, `PlayerAnimation` |
| `Camera/` | `ButchersGames.Cameras` | `CameraFollow` |
| `Gameplay/Collectibles/` | `ButchersGames.Gameplay.Collectibles` | `Collectible` base class, `MoneyPickup`, `BottlePickup` |
| `Gameplay/Economy/` | `ButchersGames.Gameplay.Economy` | `ScoreManager`, `WealthConfig`/`WealthStatus`/`WealthTracker`, `RewardCalculator` |
| `Gameplay/Checkpoints/` | `ButchersGames.Gameplay.Checkpoints` | `SaveZone`, `CheckpointManager`, `ProgressSave`, `FlagRaiser`, `MaterialSwapTarget` |
| `Gameplay/Finish/` | `ButchersGames.Gameplay.Finish` | `DoorsController`, `Door`, `FinishController`, `FinishCelebration` |
| `Levels/` | `ButchersGames.Levels` | `LevelManager`, `Level`, `LevelsList` (+ inspector in `Levels/Editor`) |
| `Feedback/` | `ButchersGames.Feedback` | `PickupFeedback`, `FloatingText`, `AudioManager` |
| `UI/` | `ButchersGames.UI` | `UIManager`, `Views/` (screens and HUD), `Widgets/` (`Billboard`, `CountUpTween`) |
| `Editor/` | `ButchersGames.EditorTools` | `Tools/Runner/*` menu: `Setup/` builds the scene, `Fixes/` repairs tags, layers and sprites |

## How the parts talk

- Pickups raise the static `Collectible.OnCollected`. `ScoreManager` counts money, `PickupFeedback` plays effects.
  Neither knows about the other.
- `WealthTracker` turns money into a `WealthStatus`. `PlayerAppearance`, `StatusBarView` and the doors react to it.
- `PlayerController.OnFinished` (end of path) or `DoorsController` (stop in front of a closed door) ends the level.
  `GameManager` then shows the result and reloads the scene for the next level.
- Scene references left empty in the inspector are found automatically in `Start`.

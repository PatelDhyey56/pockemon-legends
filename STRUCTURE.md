# Project Structure

```
Pokemon/
├── AGENTS.md                          # Agent instructions
├── project.md                         # Game design doc (match-3 PvP)
│
├── Assets/
│   ├── Scenes/
│   │   ├── SplashScene.unity          # Entry point — Firebase, AdMob init
│   │   ├── MenuScene.unity            # Main menu — banner ad, settings, IAP
│   │   ├── WebScene.unity             # In-app WebView (privacy/license)
│   │   ├── pokemon.unity              # Empty — for battle scene
│   │   └── demo.unity                 # Empty — for prototyping
│   │
│   ├── Scripts/                      # All game code (Assembly-CSharp, no asmdef)
│   │   ├── Manager/
│   │   │   ├── SplashManager.cs      # Startup: Firebase → AdMob → MenuScene
│   │   │   ├── FirebaseManager.cs    # Firebase Analytics init + log events
│   │   │   └── PurchaseController.cs # IAP bridge, static Action events
│   │   │
│   │   ├── View/
│   │   │   ├── BaseView/View.cs      # Canvas/CanvasGroup show/hide base class
│   │   │   ├── BackKeyHandler/       # Android back-button stack (View stack)
│   │   │   └── BasicView/            # 7 concrete views:
│   │   │       ├── MenuView.cs       #   Main menu
│   │   │       ├── SettingView.cs    #   Settings
│   │   │       ├── LoadingView.cs    #   Loading overlay
│   │   │       ├── MessageView.cs    #   Modal popup
│   │   │       ├── ExitView.cs       #   Quit confirmation
│   │   │       ├── AdFreeView.cs     #   Remove Ads IAP popup
│   │   │       └── RateAppPopUpView.cs # Rate prompt
│   │   │
│   │   ├── WebView/
│   │   │   ├── WebView.cs            # WebView scene controller
│   │   │   └── WebViewUI.cs          # WebView UI + back handling
│   │   │
│   │   ├── ScriptableObjects/
│   │   │   └── GameSettings/GameSettings.cs  # Debug/Release toggle + config
│   │   │
│   │   └── Utils/
│   │       ├── Constants.cs          # Scene names, ad keys, app URLs
│   │       ├── LogHelper.cs          # Toggleable debug logging
│   │       ├── PreferenceHelper.cs   # PlayerPrefs keys
│   │       ├── GamePlayerPrefs.cs    # Typed PlayerPrefs wrapper
│   │       ├── AppSharing.cs         # Native share dialog
│   │       ├── AspectRatio.cs        # Canvas Scaler (1080×1920)
│   │       ├── ATTPopupManager.cs    # iOS ATT permission request
│   │       ├── GameCompleteCounter.cs # Fibonacci rate-prompt trigger
│   │       ├── Extensions.cs         # Utility extensions
│   │       ├── TakeScreenshot.cs     # Screenshot helper
│   │       ├── SampleWebView.cs      # WebView usage example
│   │       └── Animations/           # DOTween wrappers:
│   │           ├── FadeAnimation.cs
│   │           ├── PopUpAnimation.cs
│   │           ├── RotateAnimation.cs
│   │           └── ShakeEffect.cs
│   │
│   ├── AdsManager/                   # Embedded pkg: com.gamepad.admob v1.2.5
│   │   └── Runtime/
│   │       ├── AdsManager.asmdef     # GamePadAssambly
│   │       ├── AdMobManager.cs       # Singleton: Banner, Interstitial, Rewarded, App Open
│   │       ├── ScriptableObjects/AdData.cs  # Ad unit IDs per platform
│   │       └── Utils/               # AdsDebug, AdmobExtension, GamePlayerPrefs
│   │
│   ├── PurchaseManager/              # Embedded pkg: com.gamepad.purchase-manager v1.2.9
│   │   └── Runtime/
│   │       ├── Purchasemanager.asmdef # Purchasemanager (references GamePadAssambly)
│   │       ├── PurchaseManager.cs    # Singleton: Unity IAP wrapper
│   │       ├── ScriptableObjects/InAppData.cs  # Product definitions
│   │       └── Utils/IAPDebug.cs
│   │
│   ├── Resources/                    # ScriptableObject assets (loaded via Resources.Load)
│   │   ├── GameSettings.asset        # Debug/Release mode, app URLs
│   │   ├── AdData.asset              # Ad unit IDs
│   │   ├── InAppData.asset           # IAP product IDs
│   │   ├── DOTweenSettings.asset
│   │   └── BillingMode.json          # IAP billing mode
│   │
│   ├── Prefabs/
│   │   ├── Managers/AdmobManager.prefab
│   │   └── View/
│   │       ├── BackKey/BackKeyHandler.prefab
│   │       └── CommanView/           # Prefabs for all 7 views
│   │
│   ├── Plugins/
│   │   ├── DOTween/                  # Tweening library
│   │   ├── WebViewObject.cs          # gree/unity-webview plugin
│   │   └── Android/                  # AndroidManifest, Firebase, WebView AAR
│   │
│   ├── Sprites/                      # UI sprites (buttons, icons, popups)
│   └── TextMesh Pro/                 # TMP essentials (fonts, shaders, settings)
│
├── GooglePackages/                   # Local tarballs for Firebase
│   ├── com.google.external-dependency-manager-1.2.186.tgz
│   ├── com.google.firebase.analytics-13.2.0.tgz
│   └── com.google.firebase.app-13.2.0.tgz
│
├── Packages/manifest.json            # Unity package dependencies
├── ProjectSettings/ProjectVersion.txt # Editor: 6000.0.69f1 (Unity 6)
├── .gitignore
└── GooglePackages/
```

## Key: Where to Add Game Code

| What you add | Where to put it |
|-------------|----------------|
| Battle manager, turn controller | `Assets/Scripts/Manager/` |
| Pokemon data (ScriptableObjects) | `Assets/Scripts/ScriptableObjects/` + `Assets/Resources/` |
| Board puzzle logic | `Assets/Scripts/` (new folder like `Board/`) |
| Battle UI overlays | `Assets/Scripts/View/BasicView/` + `Assets/Prefabs/View/CommanView/` |
| Ability / evolution systems | `Assets/Scripts/` (new folder like `Battle/`) |
| Store & progression | `Assets/Scripts/` (new folder like `Progression/`) |
| Battle scene | `Assets/Scenes/` (use `pokemon.unity` or new scene) |

## AI Build Steps

Execute in order. Each step depends on the previous.

### Phase 1 — Puzzle Board

1. **Grid model** — 8×8 array of GemType. Logic-only, no Mono.
2. **BoardManager** — MonoBehaviour singleton: init grid, detect matches (3+), cascade/refill, expose events for match-found, cascade-done.
3. **BoardView** — MonoBehaviour: renders grid as UI Image grid, animates swaps/cascades with DOTween, reads from BoardManager.
4. **SwapInput** — MonoBehaviour on board: tap two adjacent cells → validate → execute swap → trigger match detection.

### Phase 2 — Data Foundation

5. **Pokemon ScriptableObject** — Create `PokemonData` SO (name, element, baseHP, role, sprite, ability refs). Save in `Resources/Pokemon/`.
6. **Ability ScriptableObject** — Create `AbilityData` SO (name, element, energyCost, damage/heal, type: Basic/Active/Ultimate). Save in `Resources/Abilities/`.
7. **PlayerState model** — Plain C# class: currentHP, energyPerElement, charry, team roster.
8. **GemType enum** — Fire, Water, Nature, Electric, Psychic, Healing, Charry.

### Phase 3 — Battle System

9. **BattleManager** — MonoBehaviour singleton: owns turn loop, references both PlayerStates + BoardManager + two Pokemon on field. Flow: PlayerTurn → 2 moves → EnemyTurn (AI).
10. **EnergySystem** — After each match, award energy matching the Pokemon's element. Track per-Pokemon energy.
11. **AISystem** — Simple opponent: pick best available match matching their Pokemon's element. If none, random.
12. **TurnUI** — Display whose turn, move count remaining (2/2), End Turn button.

### Phase 4 — Abilities & Evolution

13. **AbilitySystem** — When Pokemon has enough energy, button lights up. On click: consume energy, apply effect (damage/heal/buff), animate.
14. **EvolutionSystem** — Track Charry count. When threshold met, flash UI → replace current Pokemon with evolved stats/abilities.
15. **BattleStateUI** — HP bars per Pokemon, energy gauges, Charry counter, evolution button.

### Phase 5 — Team Selection

16. **TeamSelectView** — New View subclass: show owned Pokemon, pick 2, confirm to start battle.
17. **PokemonCollection** — PlayerPrefs-persisted owned list. Starting team of 2 given on first launch.

### Phase 6 — Progression

18. **RewardSystem** — Post-battle: coin payout, XP, evolution mats. Save via PlayerPrefs + PreferenceHelper.
19. **StoreView** — New View subclass: buy Pokemon packs / coins / premium gems. Hook into PurchaseController events.
20. **LevelSystem** — XP → level up → stat increases per Pokemon.

### Phase 7 — Scene Wiring

21. **pokemon.unity** — Wire BattleManager, BoardView, TurnUI, BattleStateUI into scene. Use existing Canvas + BackKeyHandler.
22. **MenuScene** — Add "Battle" button → fade load into pokemon.unity. Add "Collection" / "Store" buttons to navigate between views.
23. **SplashManager** — After AdMob init, load MenuScene as today. No changes needed unless adding blocking assets.

### Conventions to Follow

- Add new views as `View` subclasses, register in `BackKeyHandler`.
- Place new ScriptableObjects in `Assets/Resources/`, load with `Resources.Load<>()`.
- Use `static Action` events for cross-system communication (like `PurchaseController`).
- Use DOTween for all animations.
- All UI text via TextMesh Pro.
- New managers go in `Assets/Scripts/Manager/` or a new subfolder with equivalent asmdef-less setup.
- Add new scenes to `File → Build Settings`.

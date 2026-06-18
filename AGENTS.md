# Pokemon Legends — Agent Guide

## Project State

**Early-stage app shell.** The `project.md` describes a match-3 PvP battle game, but no battle/puzzle/progression code exists yet. Implemented: splash, menu, settings, ads (AdMob), IAP (Unity Purchasing), Firebase analytics, iOS ATT, in-app WebView, rate prompt. Three utility scenes (`pokemon.unity`, `demo.unity`) are empty.

## Key Structure

| Path | Purpose |
|------|---------|
| `Assets/Scripts/Manager/` | App entrypoints (`SplashManager`, `FirebaseManager`, `PurchaseController`) |
| `Assets/Scripts/View/BaseView/` | `View` base class: Canvas/CanvasGroup show/hide lifecycle |
| `Assets/Scripts/View/BackKeyHandler/` | Android back-button stack (`PushView`/`PopView`) |
| `Assets/Scripts/View/BasicView/` | 7 concrete views: `MenuView`, `SettingView`, `LoadingView`, `MessageView`, `ExitView`, `AdFreeView`, `RateAppPopUpView` |
| `Assets/Scripts/Utils/` | Helpers (`Constants`, `LogHelper`, `PreferenceHelper`, `AppSharing`, `AspectRatio`, `ATTPopupManager`, `GameCompleteCounter`) + `Animations/` |
| `Assets/AdsManager/` | Embedded package `com.gamepad.admob` v1.2.5 — `AdMobManager` singleton |
| `Assets/PurchaseManager/` | Embedded package `com.gamepad.purchase-manager` v1.2.9 — `PurchaseManager` singleton |
| `Assets/Resources/` | ScriptableObject configs: `GameSettings.asset`, `AdData.asset`, `InAppData.asset`, `DOTweenSettings.asset` |
| `GooglePackages/` | Local tarballs: External Dependency Manager, Firebase Analytics, Firebase App |
| `ProjectSettings/ProjectVersion.txt` | Unity `6000.0.69f1` (Unity 6) |

## Architecture Notes

- **No assembly definition for game code.** `Assets/Scripts/` compiles into default `Assembly-CSharp.dll`. Embedded packages each have their own asmdef (`GamePadAssambly`, `Purchasemanager`).
- **No DI.** All managers are manual singletons (`_instance` + `GetInstance()` + `DontDestroyOnLoad`).
- **No state machine.** Scene loads are direct (`SceneManager.LoadScene`).
- **View stack** for Android back-navigation via `BackKeyHandler` (MonoBehaviour-stack pattern).
- **Event bus via static C# actions.** `PurchaseController` exposes `static Action` events; views subscribe in `OnEnable`/`OnDisable`.
- **ScriptableObjects** loaded via `Resources.Load<>()`.
- **DOTween** for all tweening (in `Assets/Plugins/`).
- **TextMesh Pro** for all UI text.

## Startup Flow

```
SplashScene → SplashManager.Start()
  → FirebaseManager.CheckFireBaseDependency()
  → AdMobManager.SetAdmobAdsID()
  → SceneManager.LoadScene("MenuScene")
```

## Key Packages (manifest.json)

- `com.google.ads.mobile` 10.5.0 (AdMob)
- `com.unity.purchasing` 5.0.1
- `com.google.firebase.analytics` 13.2.0 (local tarball)
- `com.google.firebase.app` 13.2.0 (local tarball)
- `com.coplaydev.unity-mcp` (MCP tooling)
- `com.unity.test-framework` 1.6.0 (no tests written)

## Commands

- **No test, lint, or build scripts** in this repo. Unity is the sole tool.
- Debug/Release toggled via `GameSettings` inspector (`UPDATE MODE` button) — controls `LogHelper.canPrintLog` and `PlayerSettings.productName`.
- Platform targets: Android + iOS.

## Conventions

- Global namespace for all game code (embedded packages use `AdsManager` / `IAPPurchasing` namespaces).
- Views are singletons with `GetInstance()`, parented under a Canvas.
- Heavy `#if UNITY_ANDROID / UNITY_IOS / UNITY_EDITOR` usage.
- Google External Dependency Manager handles Firebase/AdMob native dependency resolution.

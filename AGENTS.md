# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## What this is

A **Unity 2D single-player strategy horse-betting game** (Unity `6000.4.11f1`, URP). The design spec is `specs/賽馬玩法企畫書_Kiro初稿v1.1.docx` (a PRD originally written for React/TypeScript — this project re-implements it in Unity). The docx is UTF-8 but mojibakes in a Windows console; extract `word/document.xml` `<w:t>` text to a file and read that.

All game code lives under `Assets/HorseRacing/`. The rest of `Assets/` is leftover Unity template (SampleScene, TutorialInfo) and is slated for removal.

## Working with Unity (this repo is driven via Unity MCP)

The Unity Editor must be open (via Unity Hub) for MCP tools to work; an MCP HTTP server runs on port 8080 and the Editor registers a bridge with it. There is **no CLI build pipeline**.

- **Compile after editing scripts:** `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true)`, then `read_console(types=["error"])`. Use `scope="all"` — `scope="scripts"` does **not** import newly written files, producing phantom `CS0246` errors.
- **Run tests:** `run_tests(mode="EditMode", assembly_names=["HorseRacing.Tests"])` then poll `get_test_job`. Or in the Editor: Window ▸ General ▸ Test Runner ▸ EditMode. Single test: pass `test_names=["HorseRacing.Tests.OddsSystemTests.ComputeOdds_OddsWorsenAcrossRounds"]`.
- **Run the game:** load `Assets/HorseRacing/Scenes/Game.unity`, `manage_editor(action="play")`. `GameManager` starts at the main menu.
- **`execute_code`** runs as a *method body* — no top-level `using` directives (use fully-qualified names). Do not call `CompilationPipeline.RequestScriptCompilation()` from it (it throws); use `refresh_unity` instead.
- **Screenshots:** capture to file (`manage_camera(action="screenshot")`) and open the saved PNG with `Read`. Do **not** use `include_image=true` — the large inline payload crashes this Editor's MCP transport.

## Architecture

Three assemblies enforce a one-way dependency: **`HorseRacing.Core` ← `HorseRacing.UI`**, with `HorseRacing.Tests` referencing Core.

```
Scripts/Config/   ScriptableObject definitions (all tunable numbers)
Scripts/Domain/   Plain serializable models (Horse, Bet, RaceResult, PlayerState, enums)
Scripts/Systems/  Pure C# game logic — NO MonoBehaviour
Scripts/Flow/     GameManager (MonoBehaviour FSM) + IRandom
Scripts/UI/       Programmatic uGUI/TMP controllers
Data/             .asset instances of the configs (GameConfigDatabase is the master)
```

Key design rules that span multiple files:

- **Config-driven, never hardcoded (PRD §14).** Every gameplay number (odds, payouts, the 8×3 track-preference table, message-card text, event chances, analyst accuracy, card prices) lives in a ScriptableObject under `Data/`, aggregated by `GameConfigDatabase.asset`. `GameManager.config` points at it. To add a tunable: define the SO in `Scripts/Config/`, create its `.asset`, and wire it into `GameConfigDatabase`. Verify "config changes behavior with zero code edits."

- **Systems are pure & deterministic.** Every class in `Scripts/Systems/` is static/plain C#, takes an `IRandom` (`Scripts/Flow/IRandom.cs`), and is MonoBehaviour-free so EditMode tests can inject a seeded/fake RNG (`Tests/TestHelpers.cs` has `FakeRandom` with identity shuffle + `ConfigFactory` for in-memory configs). Keep new logic here, not in MonoBehaviours.

- **`GameManager` is the state machine** (`Scripts/Flow/GameManager.cs`) implementing the PRD §2 loop: `MainMenu → Betting (3 rounds) → Racing → Settlement → Shop → next round`. It owns `PlayerState` and a per-round `RoundContext`, calls the systems in order, and **raises `OnStateChanged`/`OnNotice` events**. It must **not** reference any UI type — that's what keeps Core independent of UI. UI reads state by subscribing.

- **UI is built entirely in code** (`Scripts/UI/GameUI.cs` via `UIFactory.cs`); no prefabs/scene-wired widgets. `GameUI` builds one Canvas with five panels and toggles them by `GameManager.Phase`. `RaceView.cs` animates the 8 horses left→right so arrival order matches `RaceResult` standings. Chinese renders via a dynamic TMP font asset (`Art/Fonts/MSJhengHei SDF.asset`, built from `msjh.ttc`) registered as a fallback on LiberationSans. Input System (new) is active, so the scene's EventSystem uses `InputSystemUIInputModule`.

Domain rules worth knowing: hidden bonus is a unique permutation of `0..7` across 8 horses; ranking is by `FinalSpeed = Base+Hidden+Track+Σ(stage events)` with **tie-break = lower horse number wins**; the **Win** bet pays the horse's dynamic per-round odds (`OddsSystem`), while the other five bet types pay fixed `BettingConfig` multipliers; protection cards are consumed on use (PRD §11).

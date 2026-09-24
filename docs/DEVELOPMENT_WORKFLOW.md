# Development Workflow

## Synchronizing the Unity project

Unity Hub opens a local folder. It does not automatically pull commits from GitHub.

Before opening Unity, update the clone from a terminal in the project folder:

```bash
git status --short
git pull --ff-only
```

If `git status` shows local changes, commit them on a branch or stash them before pulling. Avoid pulling while Unity is importing or saving assets. After a pull, return to Unity and allow it to import changed assets and restore packages.

When an automated change is made directly in the same local folder that Unity Hub opens, no pull is required. Unity detects the changed files locally. A different clone or computer still needs `git pull --ff-only`.

## Authoring boundaries

- Use Unity scenes and prefabs for visual composition, object placement, references, anchors, and designer-tuned values.
- Use C# for reusable gameplay rules, state, validation, responsive behavior, and systems shared across scenes.
- Use ScriptableObject assets for catalogs and data that designers should tune without editing code.
- Avoid hand-editing Unity YAML unless a change is mechanical, narrowly validated, and cannot be made safely through the Editor.

The current runtime Store layout is an intermediate compatibility layer around the existing thesis scene. Its product data and visual layout should move to ScriptableObject assets and a dedicated Store prefab before the catalog grows.

## Quantum Stability smoke test

After the project compiles and passes **Tools > QuantumQuarry > Validate Project**:

1. Touch an enemy and verify stability drops by one, knockback applies, and rapid contact does not cause repeated damage for one second.
2. Touch spikes and verify Stability drops by two while enemy contact still removes one.
3. Enter water and verify movement becomes buoyant; keep the player's head above water and verify Breath does not drain.
4. Submerge fully and verify the Breath countdown appears. After it expires, verify Stability drops by `0.5` every `1.25` seconds and surfacing resets Breath.
5. Enter the red liquid in Level 6 and verify lava removes the current life immediately.
6. At one Stability, collect a coin and verify `Coins x2` appears, the pressure HUD shows the combined multiplier, and carried rewards increase by the exact previewed value.
7. Lose a life and verify Stability restores to three, banked coins persist, unbanked ore is lost, and remaining lives decrease by one.
8. Enter and leave the Store and verify half-point Stability values persist with the rest of the run state.
9. Verify the medium-sized `Lives`, `Stability`, and `Banked` labels do not overlap the matching `PAUSE` control at the reference resolution and at 16:9 window sizes.

## Quarry Pressure validation

Status verified on 2026-09-24 with Unity 2022.3.12f1 and .NET SDK 8.0.425:

- All 889 rule/artwork assertions, 34 session/scene-classification assertions, and 35 C# 9 source syntax checks pass in both normal and runtime-validation configurations.
- Unity package restoration and API compilation pass, including Cinemachine 2.9.7.
- Custom checkpoint/vent sprites and prefabs have been generated through Unity; strict custom-prefab validation and project validation pass.
- A Windows x64 build of all 11 enabled scenes succeeds.
- Level 4 now has an optional upper-left bank, an exit-approach vent, and 1550 total base ore across 15 pickups.
- The isolated Windows-player suite passes 188 checks and captures 20 screenshots across all 11 scenes. It checks real coin/checkpoint collisions, distinct vent OFF/SAFE states, warning/damage/pause/invisibility, tier resets, armor retention and contact-damage rounding, death/reset losses, Store round trips, persistent-object suspension and level switching, and Pressure HUD bounds at 800x600, 1280x720, and 1920x1080.
- A controlled fixture clones a real Level 4 enemy and uses Ground-layer colliders to verify blocked sight, Alert/Chase/Search transitions, pressure-scaled detection and chase speed at all three tiers, invisibility loss/reacquisition, and banking restoring calm perception.
- Manual end-to-end route completion without upgrades, encounter tuning, knockback feel, and the remaining interactive checklist are still required. Controlled tests reposition the player and seed carried ore; they do not prove that the complete route is naturally playable.

Back up your PlayerPrefs before testing run resets.

From the project root:

```bash
dotnet run --project Tests/PressureRules/PressureRules.csproj
```

Expected: 889 rule/artwork assertions, 34 session/scene-classification assertions using Unity test doubles, and 35 Unity source files passing C# 9 syntax checks in normal and validation configurations. No external test packages are required. This is not a Unity API compilation check.

### Windows validation

Save your scene changes and close this project's Unity Editor before batch validation. Unity Hub can stay open. From PowerShell in the project root:

```powershell
.\Tests\ValidateUnity.ps1 -GeneratePressurePrefabs -BuildWindowsPlayer
```

The script waits for each Unity process, checks its actual exit code and success message, and stops on failure. Logs are written to `Logs\Validation`; the player is written to `Builds\Windows\QuantumQuarry.exe`. Both directories are ignored by Git. Without the switches, it runs only strict prefab and project validation. Set `-UnityEditorPath` if the required editor is installed elsewhere. Existing generated assets are preserved by the prefab builder.

For a user-local .NET installation, use the installed SDK explicitly if `dotnet` on PATH still resolves to a runtime-only system installation:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" run --project Tests\PressureRules\PressureRules.csproj
```

Cinemachine is required by the serialized camera prefab even though gameplay scripts do not name its types. Do not remove the dependency to silence a package-network error. On a managed PC, keep HTTPS verification enabled and do not change proxy or certificate configuration without IT approval. The Windows verification above used the official Cinemachine archive downloaded with Windows' existing HTTPS trust, checked against the official registry checksum, and cached locally; repository package pins and security settings were unchanged. A different machine or a cleared cache still requires package restoration.

### Isolated runtime validation

With this project's Editor closed, run:

```powershell
.\Tests\ValidateRuntime.ps1
```

This builds and launches a separate Windows player with the `QUARRY_VALIDATION` compile define and product name `QuantumQuarry Validation`. Only that disposable PlayerPrefs namespace is cleared and seeded; the campaign save is not used. The builder restores the original product name in a `finally` block. The test runner is excluded from normal player builds.

The player opens a window, changes resolution for captures, writes `report.json` and PNGs to a unique directory under `Logs\RuntimeValidation`, and exits with a failure code on errors or timeout. Leave it running without keyboard/mouse interaction until it exits. `-OutputDirectory` selects a new report directory; an existing report is never accepted as a fresh result. `-SceneSurvey` produces a Level 4 overview instead of running acceptance tests, and the report identifies that distinct mode.

### Level 4 pilot

- The bank is in the optional upper-left alcove near `(-12, 8.516)`. It banks ore only and is not a respawn checkpoint.
- The vent is near `(27, 5.516)` before the exit. Its raised label distinguishes `OFF` (disarmed) from `SAFE` (armed idle), then `WARNING` and `DANGER`, supplementing its color/pulse cues. See the [player-facing vent guide](../README.md#trying-the-pressure-vent).
- Thirteen 100-ore pickups follow existing solid platforms; a further 100-ore pickup sits on the exit-ladder approach. Together with the existing 150-ore coin, the scene contains 1550 base ore, enough to reach tier 2 if the player carries it rather than banking. The previous scene had only one actual 150-ore pickup; repeated prefab GUID references were not additional coins.
- Coins belong to `ScenePersist`, so Store visits and death do not respawn collected ore. Its children are suspended outside their owning gameplay scene, then restored on return. Switching to another level replaces the old persistent group; manual reset restores that level's pickups.
- **Tools > QuantumQuarry > Pressure > Create Level 4 Pilot** recreates missing pilot objects using Unity prefab APIs and checks floor/clearance before saving. It refuses to operate on an unsaved Level 4 scene and preserves existing placements. Repeated authoring must not duplicate objects.
- **Update Prefab Label Sorting** explicitly matches the generated bank/vent labels to their artwork's sorting layer and raises them above a standing player. Unlike **Create Custom Prefabs**, this command intentionally saves those two existing prefabs.

### Remaining human route check

Automated trigger and AI fixtures do not move through the complete platform route using normal controls. Before closing the Pressure milestone:

1. Use a backed-up/disposable save with no armor or queued power-ups, reset Level 4, and complete it using normal movement, jumping, and climbing. Do not teleport, seed ore, or enable ghost movement.
2. Attempt the carry route: collect at least 1500 base ore without banking, dying, or entering the Store. Confirm each required pickup is reachable, the approach leaves room to wait, and the vent can be crossed during `SAFE` without taking unavoidable damage.
3. Attempt the banking detour: collect ore and touch the upper-left bank. Check that the secured reward and cleared Pressure are understandable without reading code. The bank is not a respawn checkpoint.
4. Compare the choices honestly: the level only has 1550 base ore, so banking even one 100-ore pickup prevents reaching tier 2 with the remaining ore. Also, the pause-menu Store banks without walking to the physical bank. Record whether the bank detour offers any useful reason to take it; do not assume it does.
5. Record deaths, unclear jumps, waiting time, earned rewards, and whether warnings can be read while moving. Tune the layout/economy only after that evidence, then rerun the automated checks.

### Create and place custom elements

1. Open the project in Unity and resolve any compilation errors before proceeding.
2. Run **Tools > QuantumQuarry > Pressure > Create Custom Prefabs**. This generates two original 16x16 pixel-art PNGs in `Assets/Sprites/QuarryPressure`, plus `Assets/Prefabs/OreBankCheckpoint.prefab` and `Assets/Prefabs/PressurePulseVent.prefab`. Unity creates their metadata. Existing files are preserved.
3. Open a gameplay level, center the Scene view on safe ground, then use **Place Banking Checkpoint** or **Place Pulse Vent** in the same menu. Placement is snapped to the Scene view center, supports Undo, and does not save the scene. Inspect positioning and trigger bounds manually before saving.
4. Place a checkpoint on an accessible route and a vent where the player has room to avoid it. The checkpoint banks ore only; it does not move the respawn point or restore Stability.
5. Run **Validate Custom Prefabs**, then **Tools > QuantumQuarry > Validate Project**. The strict prefab check requires both generated assets, assigned artwork, enabled triggers, player-layer contact, and correctly sorted labels. The normal project validator also requires the Level 4 bank/vent and at least 1500 active base ore.
6. Commit generated PNGs, prefabs, changed scenes, and all corresponding `.meta` files together after the smoke test passes. Do not commit `Library`, test `bin`, or `obj` output.

Batch equivalents after Unity is available:

```bash
Unity -batchmode -quit -projectPath "$PWD" \
	-executeMethod QuarryPressureAuthoring.CreatePrefabs -logFile -
Unity -batchmode -quit -projectPath "$PWD" \
	-executeMethod QuantumQuarryProjectValidator.ValidatePressurePrefabsBatch -logFile -
Unity -batchmode -quit -projectPath "$PWD" \
	-executeMethod QuantumQuarryProjectValidator.ValidateBatch -logFile -
```

### Play Mode acceptance

1. Start with an existing save: old `Coins` remain banked and spendable; unlocked levels remain unlocked. Collecting new ore changes carried rewards, not banked money.
2. Reach 500, 1500, and 3000 base ore. Expect pressure tiers 1, 2, and 3, a visible tier notice, and next-pickup multipliers x1.25, x1.5, and x1.75. The crossing pickup uses the previously displayed multiplier. At critical Stability, verify x2.5, x3, and x3.5.
3. Compare each coin's preview, pickup feedback, and carried balance delta. Integer rewards round down. Bonuses must not accelerate the base-ore pressure thresholds.
4. Verify increasing enemy perception, seeded swift-pursuit speed changes, terrain line of sight, and invisibility. Seed 7319 is the default; the persisted `PressureSeed` integer selects modifiers. Repeating the same seed and tier must select the same modifier.
5. Below tier 2, the vent says `OFF` and contact is harmless. At tier 2 or 3, it says `SAFE` for 3.5 seconds, warns yellow with `WARNING` for 1.5 seconds, then turns red with `DANGER` and damages for 1 second. The six-second cycle runs regardless of player proximity. Its trigger remains present but cannot deal damage outside the active phase. Verify armor rounding, hit invulnerability, knockback, and invisibility still apply: with the current half-point damage units, neither existing armor tier reduces the vent's one-point hit. Banking disables vents; tier changes restart their safe phase. Pause must freeze the cycle.
6. Enter a checkpoint with multiple player colliders: deposit exactly once, show `Banked +N`, clear pressure, and retain armor/unlocks. Re-enter with no ore: no duplicate money or feedback. Checkpoint banking occurs on entry, not continuously while standing inside.
7. Enter the Store, buy something, and return: ore is banked exactly once and pressure stays zero. Completing a level banks ore; victory includes it in the final total.
8. Lose a life or reset the level: only unbanked ore is lost. Finish a game over and start a new run: no carried ore or pressure leaks into the new run. Persisted level unlocks survive.
9. Check 800x600, 1280x720, and 1920x1080: pressure text remains inside its bottom strip, notices fit, coin previews are legible, and HUD elements do not overlap lives, breath, power-up timers, or pause controls.

Do not mark the milestone playable until every check passes. If generation fails, preserve the Editor log and fix the reported importer/component error; do not hand-patch prefab YAML. Undo removes unsaved placements. Generated assets can be removed through the Editor only after their scene instances are removed. Reverting gameplay requires reverting the pressure commits together because they share the carried-currency contract.

## Unity upgrade procedure

The project currently targets Unity `2022.3.12f1`. Unity 6.3 LTS is the recommended stable migration target as of August 2026, but the upgrade must be isolated from feature work.

1. Verify the current `main` branch in Unity `2022.3.12f1`: compile, run the project validator, smoke-test all 11 scenes, and create a desktop build.
2. Create a dedicated upgrade branch and tag the validated 2022 baseline.
3. Install the latest Unity 6.3 LTS patch and the same required build modules through Unity Hub.
4. Open the branch in Unity 6.3 LTS, allow the API updater to run, and enter Safe Mode if compilation fails.
5. Review and upgrade packages deliberately, especially Cinemachine, Input System, TextMesh Pro/UI, 2D packages, and Test Framework. Commit package and lock-file changes together.
6. Replace obsolete APIs such as `FindObjectOfType` and `FindObjectsOfType`, then resolve all compiler errors and actionable warnings.
7. Run `Tools > QuantumQuarry > Validate Project`, inspect every scene and prefab, and test input, physics, moving platforms, enemy AI, Store, power-ups, UI scaling, and save migration.
8. Build and run the target desktop player. Compare gameplay and performance with the tagged 2022 baseline before merging.

Do not open the only working copy in a newer Unity version and then try to return it to 2022. Unity upgrades serialized project files and downgrade compatibility is not guaranteed.
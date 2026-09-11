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

Status verified on 2026-09-11: standalone .NET tests pass; Unity Editor import, prefab generation, placement, and Play Mode are pending. Use Unity 2022.3.12f1 and .NET 8. Back up your PlayerPrefs before testing run resets.

From the project root:

```bash
dotnet run --project Tests/PressureRules/PressureRules.csproj
```

Expected: 888 rule/artwork assertions, 28 session assertions using Unity test doubles, and 33 Unity source files passing C# 9 syntax checks. No external test packages are required. This is not a Unity API compilation check.

### Create and place custom elements

1. Open the project in Unity and resolve any compilation errors before proceeding.
2. Run **Tools > QuantumQuarry > Pressure > Create Custom Prefabs**. This generates two original 16x16 pixel-art PNGs in `Assets/Sprites/QuarryPressure`, plus `Assets/Prefabs/OreBankCheckpoint.prefab` and `Assets/Prefabs/PressurePulseVent.prefab`. Unity creates their metadata. Existing files are preserved.
3. Open a gameplay level, center the Scene view on safe ground, then use **Place Banking Checkpoint** or **Place Pulse Vent** in the same menu. Placement is snapped to the Scene view center, supports Undo, and does not save the scene. Inspect positioning and trigger bounds manually before saving.
4. Place a checkpoint on an accessible route and a vent where the player has room to avoid it. The checkpoint banks ore only; it does not move the respawn point or restore Stability.
5. Run **Validate Custom Prefabs**, then **Tools > QuantumQuarry > Validate Project**. The strict prefab check requires both generated assets, assigned artwork, enabled triggers, and player-layer contact. The normal project validator checks pressure rules and checks generated assets when present.
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
5. At tier 2 or 3, a vent idles for 3.5 seconds, warns yellow for 1.5 seconds, then turns red and damages for 1 second. Its trigger remains present but cannot deal damage outside the active phase. Verify armor, hit invulnerability, knockback, and invisibility still apply. Banking disables vents; tier changes restart their safe phase. Pause must freeze the cycle.
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
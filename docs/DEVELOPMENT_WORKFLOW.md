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
6. At one Stability, collect a coin and verify the HUD shows `Coins x2`, the pickup value is doubled, and the stored score receives that exact value.
7. Lose a life and verify Stability restores to three while coins and remaining lives persist.
8. Enter and leave the Store and verify half-point Stability values persist with the rest of the run state.
9. Verify the medium-sized `Lives`, `Stability`, and `Coins` labels do not overlap the matching `PAUSE` control at the reference resolution and at 16:9 window sizes.

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
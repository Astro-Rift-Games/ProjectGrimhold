# Main menu release version

`ApplicationVersionLabel` displays `v` followed by `Application.version` whenever enabled.
It owns presentation only: no authentication, networking, session state or external service.
The component and serialized menu wiring are implemented. After the Editor restart,
compilation, live structural validation and all four focused PlayMode cases passed.
Both complete Unity suites failed; visual/input acceptance remains pending.

## Existing menu wiring and remaining checks

Use the project's Unity version from `ProjectSettings/ProjectVersion.txt` (currently
`6000.5.1f1`). Inspect the scene through Unity MCP or the Editor before saving anything.

1. Open `Assets/Scenes/MainMenu.unity` and select its `MainMenu Canvas` instance.
   The source is `Assets/Prefabs/UI/MainMenu Canvas.prefab` (GUID
   `29f439518d3e565498687c9928661656`). Its root Canvas is enabled, Screen Space Overlay,
   with a 1920 x 1080 reference resolution.
2. Inspect its active direct child named `Application Version`, **not** inside the
   Menu, Login, Lobby or Character Creation panels. `MainMenuController` toggles those
   panels; the direct Canvas child stays independent while MainMenu remains loaded.
   This is scene-local persistence, not `DontDestroyOnLoad` or a new persistent system.
3. Its `TextMeshProUGUI` is assigned to `ApplicationVersionLabel`'s **Version Text**.
   It uses the existing alagard SDF font at size 22, lower-right anchor/pivot (1, 0),
   offset (-24, 18), size 280 x 32 and alpha 0.72. **Raycast Target** is disabled.
   No Button or input handler was added, and saved text is empty rather than hardcoded.
4. Keep the label enabled and the text nontransparent. Do not put it under panel
   transitions or an initially hidden CanvasGroup. Save only the related prefab/scene
   changes, preserving their `.meta` GUIDs; inspect the diff for unrelated serialization.
5. Verify in Play Mode that the text is exactly `v` + `Application.version` and remains
   visible through login, character creation and lobby/menu panel changes. Check buttons,
   keyboard/gamepad navigation and multiple supported aspect ratios for obstruction.

Source inspection identifies the root Canvas GameObject fileID `8936263877693565454`
and RectTransform fileID `2348136094593496772`; these identify the existing source, not
an instruction to hand-edit YAML. Only the related Canvas prefab changed (+152 lines);
the MainMenu scene file and existing meta GUIDs are unchanged.

## Candidate version configuration

Run the following entry points only in a disposable CI candidate checkout, with a
licensed Unity Editor matching the project. `$Unity` is that Editor executable and
`$Project` is the absolute Unity project directory. The example `0.3.2` is illustrative;
the release orchestrator supplies its calculated stable version without the `v` prefix.

```powershell
& $Unity -batchmode -nographics -projectPath $Project `
  -executeMethod ReleaseValidation.SetVersion -releaseVersion 0.3.2 -logFile set-version.log
& $Unity -batchmode -nographics -projectPath $Project `
  -executeMethod ReleaseValidation.Validate -releaseVersion 0.3.2 -logFile validate-version.log
```

`SetVersion` changes only `PlayerSettings.bundleVersion` through Unity's API and saves
assets. It does not modify the developer's configuration unless explicitly executed
against that checkout. Neither entry point creates a build, tag, release or commit.
Both exit with code 1 on invalid input/failure and code 0 on successful completion.
The caller must check each exit code before proceeding.

For a merge that must not create a release, skip `SetVersion` and pass the exact
pre-merge version to the same validator:

```powershell
& $Unity -batchmode -nographics -projectPath $Project `
  -executeMethod ReleaseValidation.Validate -baselineVersion 1.0 -logFile validate-version.log
```

The baseline must come from the orchestrator's captured pre-merge `main`, not from the
candidate being validated. It deliberately accepts legacy values such as current `1.0`:
no-release integrations must preserve that value, not normalize or increment it.
Release versions require strict stable `X.Y.Z`. The release orchestrator separately
compares the expected version with the Git tag and changelog metadata.

## What validation proves

| Check | Responsibility |
| --- | --- |
| Configured version equals supplied release/baseline | `ReleaseValidation.Validate` |
| MainMenu exists and is enabled in Build Settings | `ReleaseValidation.Validate` |
| Exactly one active label with its own enabled, non-raycasting TMP text | `ReleaseValidation.Validate` |
| Label is a direct root Canvas child, with font and nontransparent color | `ReleaseValidation.Validate` |
| `Application.version` text, re-enable lifecycle, missing-reference error, preserved raycast configuration | Four `ApplicationVersionLabelTests` in the PlayMode runner |
| Compilation and automated suites | Separate mandatory Unity CLI/Test Runner steps |
| Readability, persistent visibility, clipping and input interaction | Editor/Play Mode checks above |

The validator opens MainMenu as a preview scene and closes it without saving. It
does not prove visual quality, execute the automated tests or substitute for compilation.
Do not treat a version/wiring success as proof that all mandatory Unity checks passed.

## Validation history and current boundary

On 2026-10-20, live Unity 6000.5.1f1 and project identity were verified through MCP.
`ValidateMainMenu` failed before wiring and passed after the prefab was saved.
Current `Application.version` and `bundleVersion` remain the legacy `1.0`; no version
was changed outside publication, and no executable was built.

Two initial EditMode jobs discovered no tests. A temporary Editor test placement then
ran four failing cases: ordinary MonoBehaviour activation callbacks require PlayMode.
The tests now reside in `Assets/Tests/PlayMode/ApplicationVersionLabelTests.cs`, with
their original GUID preserved. Two PlayMode jobs failed initialization with zero cases.
The workspace `Logs/Editor.log` and Bee response file retained the removed Editor test
path (`CS2001`), despite correct AssetDatabase GUID resolution. One official
`CleanBuildCache` compilation request still failed. At that point proof awaited an
Editor restart rather than another generic cache recovery; empty jobs were not passes.

A preview callback returned `v1.0` and preserved non-raycasting configuration, but its
blank image is not visual QA. Unrelated TMP font serialization was restored exactly
to baseline and independently hash-checked on disk; Editor reload was unavailable
because MCP disconnected.

After the user restarted Unity and MCP, compilation succeeded, the Console initially
had zero errors, `ValidateMainMenu` passed again and all four focused PlayMode tests
passed (0 failures/skips). The restored font was loaded and not dirty.

The full EditMode suite completed 4177/4177 cases in 194.420 seconds and failed; the
full PlayMode suite completed 768/768 in 144.139 seconds and failed. MCP omitted the
aggregate result and capped each failure list at 25 diagnostics, so exact passed,
failed and skipped counts are unavailable. No baseline comparison established
causality to this change. No fixes or retries were performed. MainMenu and the font
remained clean, with no extra tracked serialized noise. Mandatory suite failures,
visual/input acceptance and operational runner proof still block publication.

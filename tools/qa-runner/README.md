# QA-001 / QA-002 / QA003 — Local observation, Windows demo, visual readiness

The default QA-001 mode is a Python standard-library-only runner that launches
one build executable, observes its process for a configured duration, cleans up
that process, and saves a local JSON report. No packages, installation step,
Win32 calls, Unity Editor, or game-code changes are needed for the default mode.
QA-002 optionally adds one global key press/release and a PNG evidence capture
on Windows. Python 3.14 / Windows x64 is the tested environment.

Default QA-001 checks **process lifetime only**. Optional QA003 can gate QA-002
input on a same-scale visual menu template; it does not establish functional
readiness, responsiveness, input delivery/reaction, networking, or crash cause.
Even a successful demo does **not** prove the game received the key or reacted.
There is no OCR, AI, OpenCV, Sheets/backend integration, or functional game-test
framework.

## Run on Windows

From the repository root, use PowerShell and quote paths containing spaces:

```powershell
python tools/qa-runner/runner.py --exe "C:\Builds\Grimhold\Project Grimhold.exe" --duration 30
```

If you use the Windows Python launcher, `py -3` can replace `python`:

```powershell
py -3 tools/qa-runner/runner.py --exe "C:\Builds\Grimhold\Project Grimhold.exe" --duration 30 --results-dir "C:\QA Reports\Grimhold"
```

- `--exe` is required. It is resolved to an absolute path and passed as a single
  argument to `Popen` with `shell=False`; spaces and Unicode are supported.
- `--duration` is required: a strictly positive, finite number of seconds
  (fractional values such as `0.5` are accepted; zero, negatives, NaN and infinity
  are rejected).
- `--results-dir` is optional. The default is `tools/qa-runner/results/`, regardless
  of the current directory. A custom relative directory is resolved from the
  caller's current directory. **Use arbitrary custom result locations outside
  the repository or explicitly covered by an existing ignore rule.** The runner
  does not change repository ignore rules for custom locations.
- The process working directory is the executable's parent so a Unity build can
  find its adjacent data. Keep the executable and all required build files
  together. No build is bundled with this tool.
- The CLI accepts no user-supplied game arguments or test-only command options.
  The runner itself always appends `-logFile <results>/qa-<run_id>-player.log`
  (absolute path, still one `Popen` argument list, `shell=False`) when launching
  the real build; see [Player log](#player-log). Output streams are inherited; the
  runner does not capture them. Without `--demo-key`, it never imports PyAutoGUI
  or sends input/captures images.

## Observation, outcome, and cleanup

The observation interval starts immediately after `Popen` returns. Elapsed time
uses `time.monotonic()`, not the wall clock. The process is polled approximately
once every 0.1 seconds (the final sleep is limited by the remaining interval).

- `success`: the process was still running at the observation deadline and its
  owned-process cleanup finished without errors.
- `failure`: any detected exit during observation, including return code **0**,
  a launch/observation error, or any cleanup error. An exit detected on the final
  poll is conservatively a failure; polling does not provide an exact exit time.
- `interrupted`: `KeyboardInterrupt` (usually Ctrl+C), including during cleanup.
  Cleanup is still attempted and its errors are included in the report.

Cleanup runs in `finally` after observation errors and interruptions. It operates
**only on the directly launched `Popen` object**: check whether it exited,
otherwise `terminate()` and `wait(timeout=2)`. If that fails or times out, attempt
`kill()` and another `wait(timeout=2)`. There is no unbounded wait. A recovered
terminate timeout/error still makes the result a failure. If kill/wait fails,
the process may remain running; the report says so through its cleanup error.
The optional return code is the final known code, so successful cleanup may leave
an OS-specific terminated-process code even though observation succeeded.

**Root-only ownership:** the runner never searches by image name or PID, kills
other processes, or walks process trees. Descendants started by a build/launcher
are not supervised or stopped and may survive. Select the actual game executable,
not a launcher that exits after spawning it. There is no Windows Job Object or
process-tree framework in QA-001.

## Reports and exit codes

A fresh `qa-<UUID>.json` is exclusively created for every run, preventing existing
report overwrite. Before launch, the runner creates the directory and writes and
flushes an initial failure report marked "Run incomplete". Known directory,
permission, or write errors therefore prevent launch. Final results replace only
this run's reserved report after cleanup.

Each final report contains:

| Field | Meaning |
| --- | --- |
| `date_utc` | Run creation timestamp in UTC ISO 8601, ending in `Z` |
| `executable` | Resolved absolute executable path |
| `configured_duration_seconds` | Positive finite requested observation interval |
| `observed_duration_seconds` | Last measured monotonic elapsed time; excludes cleanup |
| `result` | `success`, `failure`, or `interrupted` |
| `error` | Present on failure/interruption, including cleanup errors |
| `pid` | Present if a process was launched |
| `returncode` | Present if the owned process's exit code is known |
| `functional_validation` | `not_performed`, except `performed` in `--flow` mode (E4) |
| `gameplay_validated` | Always `false` |
| `actions` | Ordered demo steps/cleanup; empty in default mode |
| `evidence` | Accepted saved PNG records; empty in default mode |
| `readiness` | Independent `not_checked` (unrequested), `blocked`, or `ready` visual gate |
| `schema_version` | Report schema version; currently `1` |
| `run_id` | 32-hex uuid of the run; equals the `<UUID>` in `qa-<UUID>.json` |
| `player_log` | Evidence status of the per-run Unity Player log (see below) |
| `phases` | `launch`, `observe`, `cleanup` entries: `status` (`completed`, `error`, `interrupted`, `skipped`) plus `started_at_seconds`/`ended_at_seconds` |
| `first_failure` | `null` for a healthy run, else `{"stage", "error"}` for the earliest failing stage |
| `flow` | Only in `--flow` mode: the E4 feature result and assertions (see E4) |

Report schema v1 is this table. Reports without `schema_version` are legacy v0:
they are not migrated or reinterpreted, and no code reads old reports. Every v0
field above keeps its meaning in v1.

`phases` times are `time.monotonic()` seconds relative to the launch start (the
instant just before `Popen`), or `null` when not reached/unknown. A skipped phase
(for example `observe` after a launch error) has `null` times. In demo mode each
`actions` entry also carries `started_at_seconds`/`ended_at_seconds` on the same
origin (`null` for actions never attempted). `first_failure.stage` is an action
name when an action failed (it outranks the enclosing `observe` phase), otherwise
the failing phase: an early process exit is `observe`, an interruption is the
phase where it happened, and cleanup errors count only if nothing failed earlier.

### Player log

For the real executable the runner passes Unity's `-logFile <pathname>` Player
command-line argument (Unity 6000.0 documentation) with
`<results>/qa-<run_id>-player.log`. This file lives in the ignored results
directory, **may contain sensitive data (local paths, user names, machine
details) and must be reviewed before sharing**. Log content is never copied into
the report. After owned-process cleanup the runner records only
`player_log`: `requested`, `path`, `status`, `size_bytes`, `sha256` and `error`.

| `player_log.status` | Meaning |
| --- | --- |
| `not_requested` | Internal test child launched with `command`; no `-logFile` |
| `pending` | Only in the initial preflight report |
| `present` | File exists; `size_bytes` and `sha256` recorded |
| `missing` | File was not created |
| `error` | The file could not be read; `error` has the message |

A `missing` or `error` log is evidence status only: it never changes `result`.
The report never includes environment variables or child output.

Live check (2026-10-12): one attended default-mode run (15s, no input or
capture) against a local Windows build reported `schema_version` 1, a `run_id`
matching the filename, three `completed` phases, `first_failure: null` and a
`present` Player log (1955 bytes; recorded SHA256 matched the file). The log was
created at the requested path and reports engine `6000.5.1f1`; the root PID was
gone. Because the runner terminates the build, the log ends at that point and
may be incomplete. Crash, interruption, unavailable disk and synthetic-secret
cases are covered by unit tests only.

JSON is UTF-8 and written with `allow_nan=False`. The observed interval can be
slightly longer than requested due to scheduling. If observation itself raises,
it records the last completed time measurement (zero if none), not a fabricated
elapsed interval.

| CLI exit code | Meaning |
| --- | --- |
| `0` | Successful observation, cleanup, and final report save |
| `1` | Failed run, cleanup error, or report/path IO error |
| `2` | Invalid/missing CLI arguments (`argparse`) |
| `130` | Interrupted run |

`--flow` adds exit codes `3`, `4` and `5` (see E4). The CLI prints the report path and result; errors go to stderr. Storage can become
unwritable after preflight. Final report IO errors are reported to stderr with a
nonzero exit code **after owned-process cleanup**; no success is claimed. An
unexpected shutdown, disk failure, or interruption during report IO may leave an
initial/incomplete or partially written report. Preflight is not a guarantee of
future disk availability or crash-safe persistence.

## QA-002: install optional demo dependencies locally

From repository root in PowerShell (never install globally):

```powershell
python -m venv tools/qa-runner/.venv
New-Item -ItemType Directory -Force tools/qa-runner/.venv/.tmp | Out-Null
$env:TMP = (Resolve-Path tools/qa-runner/.venv/.tmp).Path
$env:TEMP = $env:TMP
& tools/qa-runner/.venv/Scripts/python.exe -m pip install --no-cache-dir -r tools/qa-runner/requirements.txt
& tools/qa-runner/.venv/Scripts/python.exe -m pip show PyAutoGUI Pillow
```

Run these in a dedicated shell; the temporary directory settings are local to
that shell. Both the environment and installer temporary files stay under the
ignored `.venv/`. Direct dependencies verified here are PyAutoGUI **0.9.54** and
Pillow **12.1.0**. Resolved transitive packages: PyScreeze 1.0.1, PyGetWindow 0.0.9,
PyMsgBox 2.0.1, PyTweening 1.2.0, MouseInfo 0.1.3, PyRect 0.2.0 and pyperclip 1.11.0.
Only the two direct dependencies are pinned; transitive versions may vary on a
future installation. Dependency installation and metadata inspection were
performed; PyAutoGUI was **not imported** during verification. Its import can
query the desktop, so it is loaded only when an explicit demo is executed.
Default QA-001 continues to work without these packages or Windows.

## QA-002: single-key demo (manual opt-in only)

**Do not run this on an unattended/shared desktop.** Until the isolated worker
stage (E2 in `docs/qa-runner/implementation-plan.md`), live runs are attended runs
on the local desktop, each explicitly authorized, with an authorized local build
and no sensitive windows visible; after E2, use the isolated Windows worker. The
following is a usage example, **not a demo performed or authorized by unit
verification**:

```powershell
& tools/qa-runner/.venv/Scripts/python.exe tools/qa-runner/runner.py --exe "C:\Builds\Grimhold\Project Grimhold.exe" --duration 30 --demo-key w --window-wait 10 --key-hold 0.2 --capture-delay 0.2
```

- Allowlisted single keys: `w`, `a`, `s`, `d`, `q`, `e`, `space`, `up`, `down`,
  `left`, `right`. No chords, modifiers, text, mouse actions, or arbitrary scripts.
  Key names are lowercase and case-sensitive.
- Demo timings are positive finite seconds. Window/manual focus wait: maximum
  60, default 10; key hold: maximum 10, default 0.2; capture delay: maximum 30,
  default 0.2. Timing options require `--demo-key`.
- Duration must exceed hold plus capture delay. Window wait reserves that budget
  and is bounded by both its own timeout and the **original** observation deadline
  starting after launch. The demo does not restart or extend observation; remaining
  time is ordinary process observation. Scheduling or synchronous GUI/PNG IO can
  overrun a deadline; subsequent safety checks fail the run, rather than silently
  adding time. These synchronous calls cannot be forcibly interrupted by a Python
  polling timeout. Cleanup time is excluded as in QA-001.
- Manually focus the launched build. The runner never activates/raises windows
  or steals focus. It enumerates visible, non-minimized, unowned root HWNDs for
  **only the launched root PID**, never titles. Absent windows/wrong focus time
  out; multiple candidates fail rather than choosing one. Child-process windows
  and owned dialogs are unsupported.
- `--acquire-focus` (requires `--demo-key`; off by default) replaces the manual
  focus step. Without it the runner never calls `SetForegroundWindow`. With it, once
  exactly one owned visible root HWND exists for the launched PID and it is not
  the exact foreground window, the runner calls `SetForegroundWindow(hwnd)` on
  **that HWND only**, every 0.5 s, at most 4 attempts, all inside the existing
  window-wait deadline. Success is the unchanged exact-foreground guard, not the
  call's return value. Ambiguous or absent windows fail before any call. If focus is
  not obtained, the run fails with an error starting `foreground lock:`, exactly
  like a window-wait failure: no input, readiness `blocked` if requested, cleanup.
  - **Measured limitation (2026-10-13):** Windows' foreground lock prevents a
    process without foreground rights from taking focus. Launched from a
    non-foreground process, the build window never received focus and
    `SetForegroundWindow` returned 0 (also with `AttachThreadInput`). Launched from
    a foreground terminal, the window received focus by itself in ~1 s (2/2).
  - **Launch requirement:** start the runner from the foreground interactive
    context (a focused terminal), not from a background or scheduled process. The
    flag cannot bypass the lock; it only requests focus and reports the outcome.
  - **Excluded techniques:** `AttachThreadInput`, synthetic key events
    (`keybd_event`/`SendInput`), `ShowWindow`/minimize tricks,
    `AllowSetForegroundWindow`, `SystemParametersInfo`, and touching any window
    other than the owned HWND.
- HWND/PID validity, sole-root ownership, process liveness, unsafe held modifiers,
  and exact foreground HWND are rechecked before every normal key event
  (including release) and around capture, and approximately every 20ms while
  holding/waiting. A physically held demo key or Shift/Ctrl/Alt/Windows modifier
  causes an abort before injection. **Do not use the keyboard or hold the same key
  during the demo.** Physical-state checks are snapshots, not an isolation lock.
- One keyDown, a bounded hold, one normal keyUp, a bounded capture delay, then
  `PyAutoGUI.screenshot(region=...)`. The PID-validated **whole window rectangle**
  (including frame, not a client-area-only image) is measured by `GetWindowRect`
  under a temporary per-monitor-aware V2 thread DPI context. This context is
  restored. Only a rectangle wholly inside the primary monitor is supported;
  unavailable DPI APIs, minimized/partially off-screen/secondary-monitor windows
  fail controlled rather than choosing a crop. Multi-monitor/mixed-DPI behavior
  remains pending live verification. For the manual demo, use a normal,
  non-maximized window wholly inside the primary monitor. Maximized windows can
  have negative frame coordinates in `GetWindowRect`; the primary-monitor guard
  intentionally rejects those rectangles.

### Residual risks and emergency cleanup

PyAutoGUI delivers **global input**, not PID-targeted input. The foreground can
change between the last guard and injection. This approved residual focus race
cannot be eliminated by these checks. There is no absolute keyboard isolation.
The screenshot is a **visible desktop region**, not exclusive content owned by
that HWND. Overlays, notifications, occlusion, and other applications can appear
inside it even while the HWND stays focused. QA-002 alone performs no pixel interpretation. QA003's optional template match
cannot distinguish an occluding imitation from genuine game pixels; neither
mode establishes a functional/gameplay conclusion.

PyAutoGUI's corner failsafe remains enabled, including checks during the hold
and before capture. Move the pointer to a primary-screen failsafe corner or use
Ctrl+C to abort. Both mechanisms still attempt key cleanup and then root-process
cleanup. No mouse movement is sent by the runner.

The held-key marker is set **before attempting keyDown**, because injection may
precede an exception. `finally` releases all previously attempted runner-owned
keys **before process termination**. Before each emergency release it inspects
ownership/focus, but it attempts the global keyUp even if the process exited,
focus was lost, or the HWND is invalid. The action records
`unsafe_global_release` and the residual risk of reaching another app or releasing
a key the human now holds. If a corner failsafe blocks cleanup keyUp, only a
retry of that held key's **release** temporarily bypasses `FAILSAFE`; the original
flag is restored in `finally`. No new keyDown ever bypasses it.

Cleanup retries at most three times for the one demo key, including repeated
KeyboardInterrupts. A marker is cleared only after keyUp returns successfully.
Errors (even recovered cleanup errors) prevent clean success; interruptions give
`interrupted`. `held_keys_unreleased` explicitly records any remaining marker.
OS termination, endless interrupts, a blocked GUI call or release failure can
still leave a key/process active; human intervention may be necessary.

### Demo report and evidence semantics

The QA-001 JSON reservation/timestamp/lifecycle remains unchanged. Demo reports
also set `demo_requested: true`, add `window` metadata (HWND, PID, physical-pixel
coordinates, primary-only limitation), and list these ordered action steps:
`setup`, `window_wait`, `key_down`, `key_hold`, `key_up`, `capture_delay`, `capture`.
Demo reports also carry a `focus` object (additive; `schema_version` stays 1):
`policy` (`"manual"` or `"acquire"`), `auto_foreground` (exact focus was present
before any `SetForegroundWindow` call), `set_foreground_attempts` (int),
`set_foreground_results` (list of the calls' boolean results), `acquired` (exact
foreground was reached under `acquire`) and `error` (`foreground lock: ...` or
null). For `manual`, attempts are `0`, results `[]` and `acquired` is `false`.
Additional `key_release_cleanup` actions are appended when needed. Setup/window/
input/capture failures and interruptions are persisted after cleanup, provided
report storage remains writable.

Each step has `requested`, `attempted`, `executed`, `status` and, on error, `error`.
`status` is `skipped` until reached, `attempted` while running, `executed` only on
successful return, or `error` when it raises. Here `attempted` means **step
processing began**, not that an input was sent. `global_input_attempted` is set
only immediately before an actual key API call and `global_input_completed` only
after it returns. These fields never establish game delivery. Capture records
`capture_attempted` only before the screenshot call; `executed`/accepted evidence
requires the actual saved PNG to pass Pillow format, integrity, decoded-data and
region-dimension validation.

Each PNG has a unique `qa-<run UUID>-evidence-<UUID>.png` beside its report.
`evidence` records its absolute path, saved status, format, HWND/PID, region,
coordinate space, unperformed content validation and overlay risk. Ownership,
focus, liveness and geometry changes during screenshot discard the image before
saving. If save/validation or a post-save safety check fails, the PNG is **not**
accepted evidence: the failed action records `discarded_evidence_path`. A partial
or discarded file may remain for diagnosis and must not be treated as success.
PNG/report/temp files, `results/`, `.test-output-*/`, caches and `.venv/` are
locally ignored. Custom result directories must be outside the repository or
already ignored; the runner does not change ignore rules for arbitrary paths.

## QA003: opt-in visual Game Ready gate

This usage example is **not** authorization to run a live demo. Use only a
separately authorized attended run under the QA-002 desktop policy above and
manually focus the owned build:

```powershell
& tools/qa-runner/.venv/Scripts/python.exe tools/qa-runner/runner.py --exe "C:\Builds\Grimhold\Project Grimhold.exe" --duration 30 --demo-key w --ready-reference tools/qa-runner/references/main-menu-iniciar.png --ready-timeout 10 --ready-interval 0.2
```

There is **no default reference** and no automatic readiness activation. The
`readiness` action is inserted strictly after `window_wait` and before `key_down`.
Only consecutive stable matches authorize the existing single-key demo; blocked
readiness sends **no down or up**, including cleanup. QA-001 and QA-002 without
`--ready-reference` retain their original action sequences. All reports retain
`gameplay_validated: false` and `functional_validation: not_performed`.

- All readiness options require both `--demo-key` and `--ready-reference`.
  `--ready-timeout`: finite 0.05..60 seconds, default 10; `--ready-interval`:
  finite 0.02..5 seconds, default 0.2; `--ready-tolerance`: finite 1..20,
  default 12; `--ready-count`: integer 2..10, default 3.
- The effective readiness deadline is the minimum of its requested timeout and
  the **original** observation deadline minus hold and capture-delay budget.
  Window wait and analysis consume that original budget; timeout defaults are
  not summed or required to fit the duration. Poll sleeps are guarded in 20ms
  slices. Analysis checks safety/deadline before/after conversion, each row and
  each candidate. Synchronous capture, Pillow operations, disk IO and Win32
  calls cannot be forcibly interrupted; detected overruns block rather than
  restart observation or send input.
- QA003 reuses QA-002's guarded screenshot/save boundary but measures only the
  Win32 **client area** using `GetClientRect`/`ClientToScreen` in physical pixels.
  Ownership, liveness, modifiers, exact focus and geometry are checked around
  screenshot and matching. Unsafe acquisitions are discarded. QA-002's final
  post-key evidence remains a compatible whole-window capture.
- No resize, multiscale search or DPI guessing: mismatched scale fails closed.
  Client frames must fit the reference and be at most **1920x1080**. References
  must be opaque PNG, at most 1 MiB, 16..256 by 8..128, at most 32768 pixels.
  Missing, corrupt, blank, low-contrast or low-detail references block input.
- The Pillow-only detector thresholds luminance at the reference contrast
  midpoint and searches its most transition-rich binary row using byte search.
  At most **4096 candidates** receive full-resolution ImageChops verification.
  References require contrast >=80, >=32 foreground pixels, 3..50% foreground,
  and >=8 transitions in at least three rows. Verification requires per-channel
  mean RGB error both overall **and foreground-only** <= tolerance, peak error
  <=3 times tolerance, and binary disagreement <=2% of foreground (minimum one
  pixel). `score` is the larger RGB mean error (**lower is better**), not a
  confidence percentage. Small color/antialias differences are tolerated only
  while the anchor's threshold pattern stays intact. Flat background similarity
  alone cannot pass. Multiple valid locations are conservatively no match.
- The default three matches must be at the exact same client-relative pixel
  location and screen rectangle. No match, flicker, movement or safely changed
  geometry resets the streak; focus/ownership loss immediately blocks.
- The independent `readiness` object includes requested parameters/reference
  path, SHA256 after successful reference load, attempts, last score/location,
  candidate count/ambiguity, streak, time-to-ready (from start of the gate),
  error and diagnostic evidence path/error. Launch/setup/dependency/window wait
  failures are `blocked`, never `not_checked`. Overall `result` still describes
  supervision/demo/cleanup, not visual or gameplay success.
- On failure, save only the **last previously guard-validated client frame**,
  with a unique PNG filename and QA-002's real PNG integrity/dimension verifier.
  No fresh capture is attempted after focus loss, exit or timeout. Evidence is
  labeled `purpose: readiness_diagnostic`, `stale: true`, and
  `role: previous_guarded_frame_not_current_authority`, with capture/save
  monotonic timestamps. It does not claim current ownership or readiness.
  Saving after the readiness timeout is allowed only before the global deadline;
  save/verification overruns or failures leave discarded-path/error metadata,
  not accepted evidence. With no safe frame, evidence is explicitly unavailable.
  READY does not save an additional confirming frame; QA-002 still captures
  its usual post-input evidence.

### Versioned reference provenance

`references/main-menu-iniciar.png` is an **88x30 RGB PNG** (1888 bytes), cropped
at `(128, 253, 216, 283)` (left/top inclusive, right/bottom exclusive) from the
976x579 QA-002 menu capture
`qa-7cd50939ba2a440ea0a47c9817fa342f-evidence-d7b58fc46eac47c5aa8b04e07a312103.png`.
Source report: `qa-7cd50939ba2a440ea0a47c9817fa342f.json`, dated
`2026-10-12T05:48:23.754531Z`. The crop contains only INICIAR text and nearby
button interior, no borders, desktop or window frame. Full captures remain
local/ignored; only this exact reference PNG is excepted from the PNG ignore
rule. SHA256: `93540e972bb47d2c0736d139762683344f7b9dbbb55675c40a5a0a45bbbe8ee3`.
Never version full desktop/window screenshots. Offline matching is not live-build
validation.

An offline 20-iteration benchmark (no Win32 guards, no capture/IO) matched the
960x540 menu interior at `(120, 222)` with RGB error 0 (median 7.46ms), rejected
the prior blank interior (7.31ms) and OPCIONES/CREDITOS/SALIR crops, and matched a
1920x1080 synthetic translated fixture (23.94ms). These are local detector-only
measurements, not a live capture/polling performance guarantee.

## E4: login-gate flow

`--flow login-gate` is the first feature check of the runner: starting from the
main menu it presses **Enter**, expects the login panel, presses **Escape**, and
expects the main menu again. It is not a gameplay or multiplayer test and covers
only this one UI transition. It reuses the QA-002 safety model (owned HWND/PID,
exact foreground focus, modifier guard, bounded guarded waits, key release before
process termination) and the QA003 visual detector; it adds no dependency.

**Do not run it on an unattended/shared desktop**: like the demo it injects global
keys. It is an attended run on the local desktop with an authorized local build.

### Game contract

Introduced by commit `bd94ea44` (`feat(ui): add keyboard access to main menu and
login panel`):

- **INICIAR** is the default selection whenever the main menu is shown, so Enter
  activates it through the standard UI Submit and the login panel opens.
- **Escape** (UI/Cancel) acts as **Back** on the login panel and returns to the
  main menu.
- The game writes two ordinary Player log lines, matched as the **exact complete
  line** (not a substring):
  - `[MainMenuController] Login panel shown.`
  - `[MainMenuController] Login panel closed.`

### Oracle

Two independent signals must **agree** for each step; neither alone passes:

1. **Visual**: the versioned `references/main-menu-iniciar.png` (INICIAR button)
   must be stably present/absent in guarded client-area frames (default 3
   consistent frames).
2. **Player log**: the expected line must be appended **after** the key press.
   Only bytes written after a per-step mark are read, a trailing unterminated
   line is ignored, and log text is never copied into the report.

Flow: **precondition** (INICIAR stably present, up to 20s) -> **A2** press Enter,
INICIAR absent and `Login panel shown.` -> **A3** press Escape (only if A2
passed), INICIAR present again and `Login panel closed.`. Each step waits up to
10s for the visual and up to 2s for the log line.

### Assertion and feature result

| Assertion status | Visual | Log |
| --- | --- | --- |
| `PASS` | observed | observed |
| `FAIL` | contradicted | contradicted or unavailable |
| `FAIL` | unavailable | contradicted |
| `INCONCLUSIVE` | any other combination (the signals disagree or are both missing) | |

A log that is missing, unreadable or shrank is `unavailable`, never a pass. After
any non-`PASS` assertion no further key is sent.

`flow.feature_result` is `PASS` only if both A2 and A3 pass; otherwise the first
non-`PASS` assertion status (`FAIL` or `INCONCLUSIVE`); `BLOCKED` when the flow
could not produce a verdict (precondition not reached, setup/window/focus failure,
unusable evidence, guard failure, deadline, interruption, no Player log) with the
cause in `flow.blocked_reason`. `BLOCKED` is never a pass or a fail of the feature.

### Report and evidence

Additive to schema v1 (the version stays `1`): `flow` holds `name`, `control`,
`feature_result`, `blocked_reason`, `precondition` and `assertions`
(`id`, `step`, `key`, `sources`, `visual`, `log`, `status`, `evidence_path`, times).
It is present only in flow mode. In flow mode `functional_validation` is
`performed` and `gameplay_validated` stays `false`. `result` keeps its supervision
meaning (process observed for the whole `--duration`, clean cleanup) and is
**independent** of `feature_result`. Requested `actions` are `setup`,
`window_wait`, `flow` plus one `flow_key_press` per key actually pressed.

If the flow does not complete, the first earlier failure (for example
`window_wait`) stays `first_failure`. Otherwise a non-`PASS` feature result becomes
`first_failure` with `stage` `flow:A2`, `flow:A3` or `flow:precondition` and the
assertion status or `blocked_reason` as `error`.

Evidence files, all in the results directory (ignored by git, **may show other
desktop content or sensitive data; review before sharing**): `qa-<run_id>.json`,
`qa-<run_id>-player.log`, and one client-area PNG for the precondition and one for
each executed assertion (`flow.*.evidence_path`, also listed in `evidence`).

### Key delivery: real scan codes

Flow keys (`enter`, `escape`, `q`) are injected by `win_keyboard.ScanCodeKeyboard`
with `keybd_event(vk, MapVirtualKeyW(vk, MAPVK_VK_TO_VSC), flags, 0)`, not through
PyAutoGUI. PyAutoGUI sends `keybd_event(vk, 0, ...)` (scan code 0) and Unity's Input
System identifies keys by physical scan code, so the Player ignores those events.
Measured live on 2026-10-13 with the same build, changing only the scan code:

| Mode | Result |
| --- | --- |
| scan code 0 (PyAutoGUI) | `{"mode":"scan0","shown":false,"closed":false}` |
| real scan codes | `{"mode":"scancode","enter_scan":28,"shown":true,"escape_scan":1,"closed":true}` |

Scan codes used: Enter `0x1C`, Escape `0x01`, Q `0x10`. The runner fails closed
(raises, sends nothing) if a scan code resolves to 0. Each `flow_key_press` action
and each `key_release_cleanup` for a flow key records the `scan_code` used, and a
flow key is always released through the same scan-code path it was pressed with.
This module is used only to press flow keys after focus was acquired; focus is never
obtained by injection.

**Known limitation (follow-up):** the QA-002 demo keys (`w`, `a`, `s`, `d`, `q`,
`e`, `space`, arrows) still use PyAutoGUI scan-code-0 events and were therefore
likely never received by the Player. That behavior is unchanged in this task.

### Control and launch requirement

`--flow-control wrong-key` is an **explicit negative control**: it presses `Q`
instead of Enter in A2. A working oracle must then report A2 `FAIL`,
`feature_result` `FAIL` and send no Escape. Run it to prove the check can fail;
it is accepted only with `--flow`.

The flow needs the real executable (the Player log is requested via `-logFile`).
Start the runner from the **foreground interactive context** (an attended
terminal in the logged-in desktop session): the flow always acquires focus, and
if Windows refuses to give the owned window the foreground the run fails with
`foreground lock` and the feature is `BLOCKED` without any key sent.

### CLI and exit codes

`--flow` is mutually exclusive with `--demo-key`, `--key-hold`, `--capture-delay`,
`--ready-*` and `--acquire-focus` (parser error, exit `2`). Allowed with it:
`--duration` (required; the global deadline, which must cover up to 20s
precondition plus both steps, so use at least 90), `--window-wait` (default 10),
`--results-dir` and `--flow-control wrong-key`. The summary line is
`QA runner: <result>; feature: <feature_result>; report: <path>`.

| Flow exit code | Meaning |
| --- | --- |
| `0` | `result` `success` and `feature_result` `PASS` |
| `1` | `result` `failure` (any feature result), or report/path IO error |
| `2` | Invalid CLI arguments |
| `3` | `result` `success` but `feature_result` `FAIL` |
| `4` | `result` `success` but `feature_result` `INCONCLUSIVE` |
| `5` | `result` `success` but `feature_result` `BLOCKED` |
| `130` | Interrupted |

A wrong-key control that behaves correctly therefore exits `3`.

Healthy run:

```powershell
& tools/qa-runner/.venv/Scripts/python.exe tools/qa-runner/runner.py --exe "C:\Builds\Grimhold\Project Grimhold.exe" --duration 90 --flow login-gate --window-wait 15
```

Negative control:

```powershell
& tools/qa-runner/.venv/Scripts/python.exe tools/qa-runner/runner.py --exe "C:\Builds\Grimhold\Project Grimhold.exe" --duration 90 --flow login-gate --window-wait 15 --flow-control wrong-key
```

Unit tests use fakes only and prove no live behavior.

### E4 live validation (2026-10-13)

Autonomous runs, each launched once from an attended foreground PowerShell with no
further interaction, against a local Windows build that includes commit `bd94ea44`
(main menu keyboard access and log lines), primary monitor 1920x1080 at 100%,
client area 960x540, `--duration 40 --window-wait 15`.

| Run | Focus | Precondition | A2 (Enter) | A3 (Escape) | Feature | Exit |
| --- | --- | --- | --- | --- | --- | --- |
| L1-1 | auto | observed at `(120, 222)` | PASS (visual + log observed, 1.5s) | PASS (1.4s) | PASS | 0 |
| L1-2 | acquired with 1 `SetForegroundWindow` (`[true]`) | observed | PASS (1.5s) | PASS (1.5s) | PASS | 0 |
| L1-3 | auto | observed | PASS (1.4s) | PASS (1.4s) | PASS | 0 |
| L2 wrong-key | auto | observed | FAIL (`Q` sent; visual and log contradicted after 12.3s) | not run, no Escape sent | FAIL | 3 |

Keys were sent with scan codes Enter 28, Escape 1 and Q 16. Every run left the
launched root PID gone and a `present` Player log. The A2 evidence frame of L1-1
shows the INICIAR SESION panel with empty fields and no main menu.

An earlier attempt with PyAutoGUI scan-code-0 key events failed A2 in three healthy
runs (INICIAR stayed stable, no log line): the oracle reported the missing
reaction correctly, and the cause was fixed as described in *Key delivery*.

Not validated live: a build without the game change (old-build case, which must
never PASS; covered by unit tests only), focus loss or a held modifier during the
flow, window move/resize, descendant processes, and runs started from a
non-foreground context other than the earlier `foreground lock` check (4 refused
`SetForegroundWindow` attempts, no key sent, feature `BLOCKED`).

## Verify

From the repository root:

```powershell
python -m unittest discover -s tools/qa-runner/tests -v
& tools/qa-runner/.venv/Scripts/python.exe -m unittest discover -s tools/qa-runner/tests -v
```

Tests use deterministic clocks and fake `Popen` handles for lifecycle, ownership,
interruption, timeout, validation, and report IO behavior. They also launch short
standard-library Python children to exercise real early exits and cleanup. Test
report directories are confined to ignored `.test-output-*` directories under
`tools/qa-runner/`; they are removed by the test fixtures. No game is launched by
this suite, and passing it is not game or Unity validation. QA-002 tests inject narrow
Win32, foreground, PyAutoGUI, process and clock mocks. Synthetic PNG fixtures
exercise real Pillow saving/validation without a desktop capture. No test imports
PyAutoGUI, sends physical/global input, or takes a screenshot. QA003 tests use
synthetic frames incorporating the versioned crop, deterministic perturbations,
negative patterns, bounds/deadlines, stability, diagnostics and mocked client
geometry; they never depend on ignored local captures. Actual PNG decoding
tests skip when Pillow is unavailable; use the local virtualenv for full coverage.

When a build is available, the remaining manual check is to run the CLI against
that build (including a path with spaces/Unicode), inspect the report, verify the
root process is gone after the duration/Ctrl+C, and separately check for any
surviving descendants. Unity, multiplayer, and functional gameplay validation
remain outside QA-001/QA-002. This implementation's verification did not launch
a game build or perform a live GUI demo. These are tooling checks only, not
functional game tests; no gameplay/multiplayer validation is claimed.

### QA003 live validation (2026-10-12)

Attended runs on the local desktop with `--demo-key q` against a local Windows
build (`Project Grimhold.exe`, build commit not recorded), primary monitor
1920x1080 at 100% scale (96 DPI), windowed 960x540 client area. Reports and PNGs
remain in the ignored `results/` directory. The negative reference was a
synthetic, valid, high-detail PNG generated outside the repository.

| Run | Setup | Observed |
|---|---|---|
| R1 ready | Versioned reference, timeout 20s | `ready` after 7 attempts (5.9s); early non-matching loading frames, then 3 stable exact matches (score 0.0) at client `(120, 222)`; key down/up only after readiness; root PID gone. |
| R2 timeout | Versioned reference, timeout 0.3s | `blocked` by readiness timeout after 1 attempt; no key event; stale diagnostic frame saved (960x540 client). |
| R3 absent | Negative reference, timeout 8s | `blocked` by timeout after 11 attempts with 0 candidates while the real menu was visible; no key event. |
| R4 Alt+Tab | Negative reference, Alt+Tab during readiness | `blocked` by `unsafe physically held modifier: alt` before any focus change could be observed; no key event. Alt+Tab cannot isolate the focus guard. |
| R4b focus | Negative reference, mouse click on another window | `blocked` by `owned window does not have exact foreground focus`; no key event. |
| R5 deadline | Negative reference, duration 8s, timeout 60s | `blocked` at 7.61s (deadline minus hold and capture-delay budget); diagnostic saved before the global deadline; no key event. |

Every run ended with the launched root PID gone; descendants were not checked.

Findings:

- **Captures can contain other desktop content.** R2's diagnostic frame was
  taken during the window-open animation and shows the terminal behind the
  build. Treat every PNG as potentially sensitive and review it before sharing.
- **Live readiness cadence is about 1.4 attempts per second**, not the requested
  0.2s interval (R3: 11 attempts in 8s), while the offline detector benchmark
  matches in milliseconds. The likely cause, not measured, is the per-row and
  per-candidate safety checks (Win32 ownership/focus queries). This is slower,
  not unsafe; `--ready-count` therefore costs roughly 0.7s per frame.
- Timeout and deadline blocks report the same error text; the report does not
  state which bound fired.

Still pending: multi-monitor/mixed-DPI and scale other than 100%, corner
failsafe/Ctrl+C during a live run, focus loss while a key is held, surviving
descendant processes, a Unicode executable path against a live build (the
validated path contained spaces), and the E2 isolated worker.

# Session Log — HVACrate 2.0

Chronological history of working sessions. Every session ends with a
short entry here (see instruction in CLAUDE.md — this is done
automatically by Claude Code at the end of a session, no explicit
request from the user needed).

---

## 2026-08-03 — Session 0 (planning, outside Claude Code)

**Context:** this session took place in a chat interface (not Claude
Code), to discuss the idea and do initial analysis — the actual C#/.NET
10 project code has not been started locally yet.

**Done:**

- Analyzed a real example: `floor1.dwg` / `.dxf` and a
  manually filled reference Excel file for the same project.
- Established the DXF structure: layer `_A [walls]` contains wall
  geometry (LINE/ARC/POLYLINE2D) and INSERT blocks for windows/doors;
  separate INSERT blocks `W Marker`/`D Marker` carry ATTRIB attributes
  with width (cm) and height (m) of the corresponding opening.
- Found that each floor has its own named Layout in the DXF, but the
  geometry of all floors lives together in one shared Model Space —
  decided that the application input will be one DXF file per floor
  (see decisions.md), rather than automatic geometric separation by
  viewport.
- Wrote a Python prototype of the logic (for quick idea validation).
- Wrote a C# skeleton (netDxf + ClosedXML) porting the same logic —
  not yet tested locally.
- Decided on the technology stack: C# / .NET 10, WPF, single-file
  `.exe` published via GitHub Releases, linked from the user's existing
  static website.
- Created: `CLAUDE.md`, `docs/decisions.md`, `docs/plan.md`,
  `docs/session-log.md`, initial project structure.
- Renamed the project to **HVACrate 2.0** and translated all project
  documentation to English. Added a CI pipeline (GitHub Actions) for
  pushes and pull requests targeting `main`.

**Open for the next session (actual work inside Claude Code):**

- Initialize the .NET 10 solution per the structure in README/CLAUDE.md
- First real run of the extraction against `floor1.dxf`
  (Floor I) and comparison against the reference Excel values
- Confirm the ATTRIB tag mapping for W/D Marker (width vs height)
- Floor area (Af) / volume (V) — still unresolved

---

## 2026-08-04 — Session 1 (first real runs, inside Claude Code)

**Context:** first session with actual code execution against real DXF
samples. Started with the original `Program.cs` (wall-layer scan +
top-level marker scan) and ended with a materially different extraction
strategy after it failed on every real sample tried.

**Done:**

- Ran the original extraction against `samples/Brizstroy_Misari.dxf`
  (h=2.89, north=0°) — console output only, no Excel write. Found the
  file is not actually a pre-split single floor (contradicts the
  decisions.md assumption): wall geometry for multiple floors lives in
  one shared layer/Model Space, so wall totals summed several floors
  together, and 0 window/door markers were found (they turned out to be
  nested inside anonymous/dynamic AutoCAD blocks, not top-level
  INSERTs).
- User supplied `samples/floor1.dxf` (single floor only). Ran against
  it: wall totals came out all-zero. Root-caused: walls in this file
  are not plain LINE/LWPOLYLINE on the wall layer — each wall run is
  its own uniquely-named block (`Wall_N_2`) inserted once with an
  identity transform; no `W Marker`/`D Marker` instances existed
  anywhere in this particular sample.
- User proposed manually tracing the floor's exterior envelope as a
  closed polyline on a new layer, `OVK`, and deriving wall
  length/direction and window/door direction from it instead of from
  wall geometry directly.
- User supplied `samples/floor2.dxf` (second floor, with `OVK` layer
  drawn and real windows/doors). Investigated the file structure and
  found: `W Marker`/`D Marker` blocks are nested one level deep inside
  the specific `Wall_N_2` block containing the opening; their ATTRIB
  tags are `AC_MarkerText_2` = width and `AC_MarkerText_3` = height,
  both in centimeters (confirmed against several real markers, e.g.
  90×210, 150×170, 160×250).
- Implemented the `OVK`-based approach in `src/HVACrate2.Core/Program.cs`:
  wall length/direction and floor area/volume from the `OVK` polygon;
  window/door direction from the nearest `OVK` edge, keeping only
  markers within 0.5m of the boundary (filters out interior doors).
- While implementing, found and fixed a real, pre-existing bug: the
  original direction formula could not distinguish a north-facing wall
  from a south-facing one (or east from west) because it discarded
  line direction before classifying. Fixed using the `OVK` polygon's
  winding order to get the true outward normal per edge. Verified on
  `floor2.dxf`: opposite parallel sides now come out equal length
  (С=Ю=15.80m), as they should for this floor's shape.
- Final console result for `floor2.dxf` (h=2.89 default, north=0°
  default — not explicitly confirmed for this floor):
  С=15.80m, И=12.55m, Ю=15.80m, З=12.50m, Af=181.82m², V=525.45m³;
  openings: 1.5×1.7→С=1, 0.9×2.1→С=2, 1.6×2.5→И=2, 0.9×2.5→И=2,
  1.58×2.43→Ю=1, 1.6×2.46→Ю=2.
- Excel writing was intentionally skipped all session (no `.xlsx`
  sample available, and user asked to see console results first) —
  `WriteToExcel` code exists but has not been run against a real file.
- Logged the strategy change and bug fix in `docs/decisions.md`, and
  updated `docs/plan.md` accordingly.
- Noticed at session end that `samples/Brizstroy_Misari.dxf` and
  `samples/floor1.dxf` are no longer present on disk (only
  `floor2.dxf` remains) — not deleted by any command run this session;
  flagged to the user, cause unconfirmed.

**Open for the next session:**

- `OVK` approach has been validated on exactly one real sample
  (`floor2.dxf`). Needs testing against a non-rectangular floor plan, a
  rotated building (non-zero north angle), and a marker equidistant
  between two `OVK` edges at a corner (tie-break untested).
- Run `WriteToExcel` against a real `.xlsx` template for the first time.
- `AC_WIDO_ID` marker attribute meaning still unconfirmed.
- Decide whether `OvkLayer` should be user-configurable per project
  (like `WallLayer` was), and whether `WallLayer`/old wall-scan code
  paths should be removed now that they're unused.
- Confirm whether `Brizstroy_Misari.dxf`/`floor1.dxf` need to be
  restored to `samples/`.

---

## 2026-08-04 — Session 2 (branch `phase1-excel-writing`)

**Done:**

- Started new branch `phase1-excel-writing` off `main` to close out the
  remaining Phase 1 item: run `WriteToExcel` against a real `.xlsx`.
- User pointed to their real working file,
  `output/Топлотехника V6.0.16.xls` — legacy binary `.xls`, not
  `.xlsx`. Converted to `.xlsx` via Excel COM automation (PowerShell),
  since ClosedXML cannot read the old binary format. Confirmed the
  sheet name (`Изчисления`) matches what `WriteToExcel` expects.
- Found the real file already holds live project data (not a blank
  template), so ran the write test against a **scratch copy**, never
  the original, to avoid destroying real work.
- Found and fixed two bugs blocking the test (see decisions.md for
  full detail):
  1. `HVACrate2.Core.csproj` missing `<OutputType>Exe</OutputType>` —
     `dotnet run` couldn't execute at all.
  2. Opening-row finder in `WriteToExcel` checked column `A` for
     "next free row," but the real template pre-fills `A` with row
     index numbers regardless of whether the row has real data —
     would have written into row 93, colliding with the next table's
     header. Fixed to check column `B` instead.
- Re-ran the full pipeline (`floor2.dxf` → scratch `.xlsx`) after the
  fixes: console output matched Session 1's values exactly, and the
  written cells landed correctly — wall block at row 31 (`C31`..`L31`),
  opening rows starting at row 57 (widths/heights/direction counts all
  correct).
- Reverted `ProjectConfig.cs` test-path edits back to their original
  placeholder defaults before finishing (only `Program.cs` and
  `HVACrate2.Core.csproj` have real, intentional changes on this
  branch).
- Left `output/Топлотехника V6.0.16.xls` and the converted
  `output/Toplotehnika_V6.0.16.xlsx` untracked (real client data, not
  committed).

**Open for the next session:**

- Phase 1 is now functionally complete end-to-end (DXF read → OVK
  extraction → Excel write), pending real-world validation:
  - Reference-value validation against a real floor with known correct
    answers is still open — `floor2.dxf` has no independent reference
    values (see Phase 1 checklist in plan.md).
  - `output/` is not covered by `.gitignore` — currently relying on
    the user not staging it; consider adding an explicit rule if this
    keeps coming up.
- Everything else carried over from Session 1 (OVK edge cases,
  `AC_WIDO_ID`, `OvkLayer` configurability, missing sample files) is
  still open — see above.

---

## 2026-08-04 — Session 3 (same branch, `phase1-excel-writing`) —

real Floor I validation, exterior corner count

**Done:**

- User replaced `samples/floor2.dxf` with the real
  `samples/floor1.dxf` — the actual file behind CLAUDE.md's original
  Floor I reference values.
- Ran the full pipeline against it: area matched exactly (110.90m² vs.
  110.9 reference), С/Ю wall lengths matched exactly (9.70m), window #1
  matched (1.5×1.7 → С=1). И/З came out 12.50m vs. reference 12.55m —
  user reviewed and accepted this 0.05m gap, not investigated further.
- Worked out, with the user, what "exterior corner count" (n) actually
  means: of the OVK boundary's 8 total edges/vertices, 6 are "outer"
  (face true open exterior) and 2 are "inner" (the two short walls of
  a small notch on the south side, facing each other rather than open
  air). Implemented as convex-vs-reflex vertex classification
  (`CountConvexCorners`, cross-product sign vs. polygon winding).
  Validated: 6 convex + 2 reflex = 8, matching the user's n=6 exactly.
  This closes the previously open Phase 2 "exterior corners" item.
- Found the "Geometric characteristics" Excel block (row 7: Af, h, V,
  P, n) was never actually being written — only ever printed to the
  console. Confirmed the real column layout directly from the user's
  template (it didn't match CLAUDE.md's placeholder note) and wired it
  into `WriteToExcel`. Corrected CLAUDE.md's Excel mapping section
  accordingly.
- Verified the write against a scratch copy of the real template:
  `C7=110.9, E7=2.89, F7=321, G7=44.4, K7=6` — all correct.
- Committed the earlier Excel-write bug fixes (missing `OutputType`,
  opening-row-finder column bug) from Session 2.

**Open for the next session:**

- Columns `H` (Aок, opening area), `I` (Аерк, envelope area), `J`
  (Lерк) in the geometric-characteristics block are still not written
  — no calculation exists for them yet.
- The 0.05m И/З gap against the original reference was accepted by the
  user but not root-caused — could be a minor OVK-tracing precision
  issue in the DXF itself, not a code bug (area matches exactly, which
  wouldn't be true if the underlying geometry itself were wrong).
- Everything else carried over from Sessions 1–2 (OVK edge cases on
  other floor shapes, rotated/non-zero-north floors, `AC_WIDO_ID`,
  `OvkLayer` configurability, `output/` gitignore) is still open.
- Pushed branch `phase1-excel-writing` to `origin`
  (two commits: Excel-write bug fixes, exterior-corner-count +
  geometric-characteristics block). PR not yet opened — no `gh` CLI
  available in this environment; user to open it manually via
  <https://github.com/ivanzlatinov1/HVACrate-2.0/pull/new/phase1-excel-writing>

---

## 2026-08-05 — Session 4 (branch `phase2-cleanup`, merged to `main`)

**Done:**

- Closed out four open Phase 2 items per user decision: H/I/J columns
  left permanently blank, `OvkLayer` name finalized as hardcoded
  `"OVK"`/`"ovk"` (not configurable), `AC_WIDO_ID` marker attribute
  confirmed unused, and the blank Excel template
  (`output/Топлотехника V6.0.16.xlsx`) tracked in git (was previously
  assumed to be live client data and left untracked — that assumption
  was wrong for this file).
- Logged all four as final decisions in `docs/decisions.md`; updated
  `docs/plan.md` checklist.
- Branch merged to `main` (via GitHub, outside this session).

**Open for the next session:** start Phase 3 (WPF UI).

---

## 2026-08-05 — Session 5 (branches `phase3-wpf` +
`phase3-projects-menu`, both merged to `main`)

**Context:** first UI-building session. Started Phase 3 from scratch —
a blank `MainWindow.xaml` and no pages existed yet.

**Done:**

- Refactored `HVACrate2.Core.Program.Main`'s console-only logic into a
  reusable public API (`FloorProcessor.ProcessFloor` /
  `WriteFloorToExcel` / `ProcessAndWriteFloors`, plus `FloorInput`/
  `FloorResult` models) so the app can drive it for an arbitrary number
  of floors. Re-verified against `samples/floor1.dxf` after the
  refactor — output unchanged.
- Built the navigation shell (`Frame`-based) and every page: Start menu
  (title, Start/Instructions buttons), a Projects menu (list of
  in-memory projects — name, floor count, creation date, Open/Delete,
  "+ New Project"), the Work/Floors page (dynamic per-floor DXF
  picker + height + north-direction dropdown, Extract & Fill Excel,
  Download filled Excel), and an Instructions page stub.
- Excel template is bundled into the app's build output
  (`Assets/Template.xlsx`, linked from the tracked
  `output/Топлотехника V6.0.16.xlsx`) rather than picked per run;
  "Extract & Fill" writes to a scratch temp file, "Download" lets the
  user Save As — the bundled template itself is never touched.
- Added, at user request mid-session: a light/dark theme toggle
  (`ThemeManager` swapping merged `ResourceDictionary` themes at
  runtime, all themed brushes moved to `DynamicResource`, including a
  from-scratch themed `TextBox`/`ComboBox` template), bright gradient
  page backgrounds per theme, and a glowing/shadowed page title.
- Compass direction control went through three iterations: image-based
  (user-supplied `compass.png` / `compass-dark.png`, theme-swapped) →
  user rejected the image approach entirely → replaced with a
  hand-drawn vector `CompassControl` (fixed N/E/S/W + intercardinal
  dial, animated needle with shortest-path rotation). The PNG assets
  were deleted, not kept as a fallback.
- **Bug found and fixed:** Start → Create project → Back threw a
  `XamlParseException` (`Run.Text` binding on `CreatedAt` defaulted to
  `TwoWay` against a read-only property). Root-caused by adding a
  temporary crash logger, then confirmed fixed via a scripted UI
  Automation repro of the exact reported flow against the real built
  `.exe`, before and after the fix — not just code review. Kept a
  permanent friendly-error `DispatcherUnhandledException` handler
  (logs to `%TEMP%\hvacrate-crash.log`) as a safety net going forward.
- Both feature branches merged to `main` this session (`phase3-wpf`,
  and `phase3-projects-menu` merged into `phase3-wpf` first per user
  request to keep the Projects-menu work isolated).

**Open for the next session:**

- 2D preview of recognized walls/windows, and a results-review table
  before the final Excel write — neither built yet.
- Instructions page is still a stub — blocked on the user recording a
  screen-capture video of the (now more stable) Work page; written
  step-by-step instructions also still needed.
- OVK edge-case testing (non-rectangular plan, rotated/non-zero-north
  building, corner tie-break) — still deferred from Session 3/4.
- Everything else carried over (see decisions.md, 2026-08-05 Phase 3
  entry) is still open.

---

## 2026-08-05 — Session 6 (branch `phase3-instructions-page`, merged
to `main`)

**Done:**

- Built out the Instructions page (was a stub): two parts per the
  user's spec, each with a video (new `Controls/VideoPlayerControl` —
  `MediaElement` wrapper with Play/Pause/Restart) followed by the
  numbered written steps, verbatim — (1) exporting the `.dxf` from
  AutoCAD via the `OVK` layer + `WBLOCK`, (2) using HVACrate 2.0 to
  extract the Excel result.
- Discussed video hosting options with the user (bundled in repo/exe,
  YouTube, GitHub Releases) and landed on **GitHub Releases** — same
  place the app's `.exe` is already published, and `MediaElement` can
  stream the URL directly with no new dependency. User uploaded both
  videos to the repo's `Pre-Release` release.
  `VideoPlayerControl`/`InstructionsPage` resolve local-first
  (`videos/` next to the repo root) and fall back to the hosted URL
  otherwise; added a `MediaFailed` handler for a friendly message
  instead of a blank player on a bad connection. Verified both the
  local and remote paths against the real built `.exe` (temporarily
  hid the local `videos/` folder to force and confirm the remote
  streaming/playback path).
- Per user request, stopped tracking `samples/` in git (kept on disk,
  local-only) — same treatment as `videos/`. `samples/floor1.dxf`
  removed from the index via `git rm --cached`; `.gitignore` updated
  for both directories.
- Branch merged to `main` and pushed to `origin`.

**Open for the next session:**

- 2D preview of recognized walls/windows, and a results-review table
  before the final Excel write — neither built yet.
- OVK edge-case testing (non-rectangular plan, rotated/non-zero-north
  building, corner tie-break) — still deferred from Session 3/4.
- Phase 4 (packaging/distribution) not started — single-file publish,
  clean-machine test, GitHub Releases upload, static-site link.
- Everything else carried over (see decisions.md, 2026-08-05
  Instructions page entry) is still open.

---

## 2026-08-07 — Session 7 (branches `feature/apartment-appliance-calcs`
and `feature/extract-loading-spinner`, both merged to `main`)

**Done:**

- Added an "Apartments" input per floor row on the Work page (next to
  height and up-direction). `FloorProcessor.ProcessAndWriteFloors` now
  sums apartment count and floor area across all floors in the project
  and writes the building-wide "electric consumers"/lamp block in the
  Excel template exactly once (that section exists only once in the
  template, unlike the per-floor blocks): `D317`/`D321`/`D332` =
  apartments, `D331`/`D333` = 2×apartments, `D336` = 5×apartments,
  `D348` = apartments (occupant count), `D291` = lamps =
  `ceil(7 × totalFloorArea / 20)` (changed to always-round-up after a
  user follow-up the same session; started as plain rounding). This
  overwrites six cells that had pre-existing chained formulas in the
  blank template — done deliberately, since the old `D336` formula
  (`=D333*3`) would have given 6×apartments, not the 5× the user
  wanted. Verified with a throwaway console harness against
  `samples/floor2.dxf` (two synthetic floors, 3+2 apartments) — all
  six cells plus D291/D348 matched spec exactly. See decisions.md,
  2026-08-07.
- Added a loading spinner (`Controls/LoadingSpinner`, same
  rotate-storyboard pattern as the existing compass needle) shown on
  the Work page while "Extract & Fill Excel" runs; the extraction
  pipeline now runs via `Task.Run` instead of blocking the UI thread,
  and the Extract button disables for the duration.
- Two small fixes in the same branch, per user follow-up: the
  "Download filled Excel" dialog's default filename now matches the
  real template name (`Топлотехника V6.0.16.xlsx`, was just
  `Топлотехника.xlsx`); the Projects page's "New project name..."
  textbox placeholder is now horizontally centered instead of
  left-aligned (shared `TextBox` template in `App.xaml`).
- Verified both branches via `dotnet build` and by launching the real
  `.exe` and driving it with `System.Windows.Automation` to reach the
  Work/Projects pages and confirm no crash. Did not automate the
  native `OpenFileDialog` to get a live screenshot of the spinner
  mid-spin — judged not worth the added risk of scripting native
  dialogs on the real (non-sandboxed) desktop for this change.
- Both branches merged to `main` and pushed to `origin`.

**Open for the next session:**

- 2D preview of recognized walls/windows, and a results-review table
  before the final Excel write — neither built yet.
- OVK edge-case testing (non-rectangular plan, rotated/non-zero-north
  building, corner tie-break) — still deferred from Session 3/4.
- Phase 4 (packaging/distribution) not started.
- Everything else carried over (see decisions.md, 2026-08-07 entries)
  is still open.

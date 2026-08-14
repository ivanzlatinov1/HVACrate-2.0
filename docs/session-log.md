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

---

## 2026-08-09 — Session 9 (branch `fix/ovk-logic`, merged to `main`)

**Context:** user supplied a new real sample, `samples/example.dxf`
(an Archicad export), and reported the app produced wrong results
against it — tiny/wrong area, only 2 of 8 wall directions populated,
zero windows/doors. This turned into the OVK edge-case testing deferred
since Session 3/4: this file is genuinely non-rectangular and needed a
non-zero north angle (45°), and its marker-placement convention is
different enough from floor1-4 that it broke assumptions baked into
Phase 1's original opening-extraction logic.

**Done:**

- **OVK boundary selection fixed:** `example.dxf` reuses the `OVK`
  layer for 55+ polylines (room outlines, annotation frames), not just
  the building envelope. `OvkVertices` picked the first one found
  (`FirstOrDefault`); now picks the one with the largest enclosed area,
  since the true envelope dominates any room/annotation polygon by a
  wide margin (47x in this file). No-op for floor1-4 (always had
  exactly one OVK-layer polyline).
- **Cross-floor opening rows merged instead of duplicated,** per user
  request: the openings table previously grew a fresh set of rows per
  floor even when the same window/door size repeated across floors.
  `ProcessAndWriteFloors` now sums counts into one table before writing.
- **Opening exterior/interior classification redesigned from scratch.**
  The original approach (marker within a fixed distance of the OVK
  boundary) could not work: `example.dxf`'s markers sit 1.2-2.5m from
  OVK (an Archicad annotation-leader convention) while floor1-4's sit
  ~0.25m out, and floor1's own genuinely-interior doors span a
  continuous 1.15-4.44m with no gap anywhere a threshold could exploit
  — proven by directly measuring real marker-to-boundary distances
  before writing any code. Two more approaches (network-wide hop
  search, marker-position-based direction with a raised tolerance)
  were tried and rejected with concrete before/after evidence at each
  step. Landed on `WallTopology.cs`: determine each opening's actual
  host wall from geometry (structural block-nesting for floor1-4;
  real window/door body object located by proximity for Archicad-style
  files, verified to land within 0.01m of true wall vertices), then
  classify by tracing only near-straight wall continuations toward
  OVK — a real corner (~90°) stops the trace, which is what correctly
  excludes an interior partition that merely touches its host exterior
  wall.
- **Coordinate scale auto-detected (cm vs mm).** `example.dxf`'s raw
  coordinates turned out to be millimeters, not centimeters like every
  other sample — confirmed by the user's real reference numbers being
  off by exactly 100x (area) and 10x (length). `$INSUNITS` cannot tell
  the two conventions apart (checked directly: both declare the
  identical value). `DetectCoordinateDivisor` tries centimeters first,
  falls back to millimeters only if that yields an implausible floor
  area.
- **Opening direction assignment fixed for the corner case,** after two
  more rejected attempts each with real regressions found and reverted
  (direction from the OVK touch point directly: relocated the same
  ambiguity to a shared corner vertex; direction from the traced wall
  run's own edge: broken by jamb-tick segments coincidentally aligning
  with the wrong facade). Landed on reusing the tight-tolerance
  OVK-edge match already computed per wall-graph node, chosen by
  plurality vote across every traced path (not summed path length,
  which a single long spurious detour was found to dominate).
- **Validated against a real reference table** the user supplied
  (window/door counts by size and direction, all 4 floors of a real
  project merged): 16 of 16 rows match exactly, including the
  previously-wrong corner case, with no regressions.
- **Two flagged discrepancies investigated to concrete conclusions**
  (not left as "probably fine," per explicit instruction): a
  0.4×2.46m window and a 90×210cm door height. Both checked
  exhaustively — real paired body-object geometry, duplicate/reference
  counts, hidden/disabled flags, alternate attributes — and found
  fully consistent with the DXF's own data in every case. If either is
  still wrong, the discrepancy is in the source drawing, not in
  extraction.
- **Sample file drift discovered as a side effect of debugging:**
  `samples/floor1.dxf` and `samples/floor2.dxf` on disk no longer
  match the versions last committed (before `samples/` was gitignored)
  and originally validated — confirmed by re-running against the
  committed byte-for-byte versions, which still reproduce every
  historical number exactly. Not caused by this session's changes;
  flagged as open since it affects reproducibility of old reference
  numbers.
- Extensive back-and-forth with the user rejecting several proposed
  fixes at each stage (tolerance tuning, network-wide search, several
  direction-assignment heuristics) before landing on the wall-topology
  design — each rejection is logged in decisions.md with the specific
  before/after evidence, not just the final approach, since the ruled-
  out paths are exactly what the next session needs to avoid re-trying.

**Open for the next session:**

- Exterior/interior classification is still imprecise for some small
  doors: `floor2.dxf` and `floor3.dxf` (same wall-block layout)
  classify their w=90cm doors differently from each other, and neither
  matches the reference table. Root cause not found.
- Coordinate-unit auto-detection and wall-layer-name detection
  (`WallTopology.IsWallLayer`) are both heuristics validated against
  only the conventions seen so far — see plan.md for specifics.
- The sample-file-drift finding above is unresolved (no fix attempted;
  flagged for the user's awareness).
- 2D preview of recognized walls/windows, and a results-review table
  before the final Excel write — still not built (carried over from
  every prior session).
- Phase 4 (packaging/distribution) — still not started.

---

## 2026-08-09 — Session 10 (branch
`fix/exterior-opening-classification-host-wall-topology`, merged to `main`)

**Context:** closed out Session 9's open item — floor2.dxf/floor3.dxf's
w=90cm doors classifying inconsistently and matching no reference
table. User supplied a full 17-row, 4-floor reference table this
session, which is what made rigorous validation (and root-causing)
possible.

**Done:**

- Investigated the exact structural difference between a genuine
  exterior door and the misclassified ones, using purpose-built
  read-only diagnostics (chain-following through collinear wall
  blocks, per-block OVK-overlap checks, full decision-path traces
  against the real pipeline internals) rather than reasoning from
  assumption at any step.
- Root cause found: the CAD exporter splits one physical exterior wall
  into many separate `Wall_N_2` blocks (one per opening) with no
  explicit link between them anywhere in the DXF.
- Two intermediate fixes were tried, validated against the full
  reference table, found to regress other openings, and reverted or
  redesigned in the same session — both are logged in decisions.md
  with the concrete evidence that ruled each one out (a notch-edge
  exclusion rule that also excluded genuine windows; a whole-run
  node-union approach that fixed the original bug but introduced 8 new
  false-positive/wrong-direction mismatches).
- Shipped fix: `WallTopology.BuildWallRun` reconstructs the physical
  wall run (nested convention only) and is used *only* to decide
  exterior/interior; path-finding and direction assignment stay scoped
  to the opening's own host block. Validated: all 17 reference rows
  match exactly (53/53 openings, 0 false positives/negatives/direction
  errors/duplicates). Floor areas/wall lengths/corner counts and the
  Archicad (`example.dxf`) branch confirmed unchanged.
- Cleaned up all throwaway diagnostic code (`DebugTrace.cs` and extra
  `Program.cs` command branches) before committing — final diff touches
  only `FloorProcessor.cs`/`WallTopology.cs`.
- Branch pushed and merged to `main` via GitHub.

**Open for the next session:**

- Sample-file drift (`floor1.dxf`/`floor2.dxf` on disk ≠ originally
  validated versions) — still unresolved, unrelated to this session's
  fix (see plan.md/decisions.md, 2026-08-09).
- Coordinate-unit auto-detection (cm vs mm) and wall-layer-name
  detection (`WallTopology.IsWallLayer`, English "wall" substring) —
  both still untested against conventions beyond the ones already seen.
- 2D preview / results-review table before the Excel write — still not
  built.
- Phase 4 (packaging/distribution) — still not started.

---

## 2026-08-09 — Session 11 (branch `feature/preview-results-before-excel`)

**Context:** closed the sample-drift, coordinate-unit, and
wall-layer-naming items by explicit user decision (drift accepted as
noise, the other two confirmed as user-side export concerns, not code
issues) — then, mid-session, the user identified and fixed the actual
cause of the `floor1.dxf` drift (a bad `OVK` layer in that specific
file) and asked for it to be re-verified now that it's fixed. Also
built the last two open Phase 3 items: a 2D preview and a results
table before the Excel write.

**Done:**

- Entered plan mode for the 2D preview / results table feature (a
  genuine multi-file UI feature) before writing code; plan approved
  by the user before implementation started.
- `FloorProcessor.ProcessAndWriteFloors` split into `ProcessFloors`
  (compute, no Excel) and `WriteFloorsToExcel` (write already-computed
  results) — verified byte-identical output to the pre-refactor
  baseline across floor1-4 and `example.dxf` before building anything
  on top of it.
- `FloorResult`/`Opening` extended with `OvkVerticesM` and per-opening
  world position — both were already computed internally and
  discarded; no extraction/classification logic changed.
- New `FloorPreviewControl` (2D canvas: OVK boundary, opening markers,
  a north arrow computed from the same bearing formula the classifier
  itself uses) and `PreviewPage` (the control plus a results table),
  inserted between "Extract" and the Excel write. `WorkPage`'s button
  renamed "Extract & Preview"; the actual write + download moved to
  the preview page's "Confirm & Write Excel" step.
- Verified live against the real `.exe`, not just built: scripted the
  full user flow with `System.Windows.Automation` (project creation,
  the native DXF file-picker dialog via its window handle, form
  fields, Extract, Preview, Confirm & Write, Download, Back) and
  confirmed the preview showed the exact known-correct extracted
  values, the write succeeded, and Back preserved the user's
  already-entered floor data instead of resetting it.
- Committed the preview feature, then re-validated `floor1.dxf` after
  the user supplied a corrected copy (fixed `OVK` layer): the file now
  reproduces the original 2026-08-04 reference exactly (Af=110.90m²,
  С=9.70/И=12.50/Ю=9.70/З=12.50, n=6) — confirms the earlier "drift"
  was a bad sample file, not a code bug. Full solution build
  (including the test project scaffold) clean.

- User confirmed `samples/floor2.dxf`'s current on-disk copy is the
  correct file (no export bug) — both floor1's and floor2's drift
  items are now closed.

**Open for the next session:**

- Coordinate-unit auto-detection and wall-layer-name detection remain
  closed per explicit user decision (not being revisited).
- Phase 4 (packaging/distribution) — still not started.
- `tests/HVACrate2.Core.Tests` is still an empty scaffold (one
  placeholder `Assert.That(true)` test) — pre-existing, not addressed
  this session.

---

## 2026-08-10 — Session 12 (branch `feature/floor-heating`)

**Context:** two housekeeping items closed first, then a substantial
new feature — Floor Heating — started from scratch at the user's
request. Not in the original CLAUDE.md scope.

**Done:**

- Closed both discrepancies flagged 2026-08-09 as investigated-but-
  unresolved: the 0.4×2.46m window (user checked the real CAD file,
  confirmed real — their own earlier "does not exist" report was a
  mistake) and the 90×210cm door height (user confirmed the source
  drawing/reference was wrong, the DXF's own 210cm data stands). Both
  closed in `plan.md`/`decisions.md`, no code changes.
- Entered plan mode for the new Floor Heating feature (multi-file,
  architectural); ran an `AskUserQuestion` round first to settle three
  open design forks (project-selection flow, whether the heating
  floor/room list ties to the DXF floors, delta/Qпт input granularity)
  before writing any code. Plan approved, then implemented:
  - `ProjectStore.CurrentProject` + Start page restructured to 4
    buttons (Project Management, Energy Efficiency, Floor Heating,
    Instructions), the latter two gated on a selected project.
  - `FloorHeatingCalculator` (`HVACrate2.Core/FloorHeating/`) —
    Rог/Rод/Ro/r_пд/Qc from the user's reference-sheet screenshots, all
    physical constants hardcoded, only deltas + Qпт taken as input, per
    room. Verified against the sheets' own worked example (Ro=1.4881)
    before wiring up any UI.
  - `FloorHeatingPage` (dynamic floor/room entry, mirroring `WorkPage`'s
    existing dynamic-row pattern one level deeper) + inline results,
    first pass.
  - **Caught mid-session, corrected the same session:** user pointed
    out the work had been done directly on `main` instead of a branch
    — moved everything to `feature/floor-heating` immediately (no
    commits existed yet on `main`, so this was a clean `git checkout
    -b`, nothing to salvage).
  - User supplied a real reference-table screenshot for a room results
    table (Помещение/Qпт/r пд/Qc/m/Qдол) and the m/Qдол formulas
    (`m = 3600·Qc/41870`, `Qдол = Qc − Qпт`). Added both to the
    calculator and a per-floor `DataGrid` results table — verified
    against the screenshot's own numbers (m=176.64, Qдол≈194.4 for the
    first row) before considering it done.
  - Per explicit user follow-up: split the results table onto a new
    `HeatingResultsPage` (was inline on the data-entry page — user
    wanted the same shape as the existing `WorkPage`/`PreviewPage`
    split), and moved the floor/room data from a page-local field onto
    `ProjectRecord.HeatingFloors` so it survives leaving and
    re-entering Floor Heating for the same project (the first version
    lost everything on navigating away — user caught this immediately
    on trying it).
  - `dotnet build` clean after every step; app launched and left
    running for the user to click through directly (no UI-automation
    tooling used this session — build success and a crash-free launch
    were the only automated checks).

**Open for the next session:**

- **Floor Heating is intentionally incomplete, blocked on the user
  supplying more information, not a bug:** a second table "for the
  serpentines for each room" was requested but its formulas/columns
  were never given; whether the feature needs any Excel output at all
  (vs. staying in-app-only) is undecided. See plan.md Phase 6.
- Everything else carried over from prior sessions (Phase 4 packaging,
  Phase 5 polish, empty test scaffold) — untouched this session, not
  being worked on per explicit user instruction ("Phase 4 and 5 will
  wait for now, as well as the tests").
- `feature/floor-heating` not yet merged to `main`.

---

## 2026-08-10 — Session 13 (branch `feature/language-toggle`, merged
into `feature/floor-heating`)

**Context:** continuation of the same day's work. User confirmed Floor
Heating (Session 12) needs more information before it can be finished,
so that was left as-is, docs updated, and committed on
`feature/floor-heating`. New task started from there: a persistent
language toggle (English/Bulgarian) across every page.

**Done:**

- Committed Session 12's Floor Heating work (first slice) with docs on
  `feature/floor-heating`.
- Branched `feature/language-toggle` off `feature/floor-heating` per
  explicit user correction — this session started the branch
  proactively from the start, unlike Session 12 where the branch was
  created only after the user pointed out the work had begun on `main`.
- Sized the localization scope by grepping every hardcoded UI string
  across `HVACrate2.App` before planning (~80 XAML + ~30 code-behind
  occurrences), then entered plan mode and ran an `AskUserQuestion`
  round to settle two forks: formula/domain notation (Rог, Rод, Qпт,
  room labels, compass-direction letters) stays fixed in both
  languages — recommended and confirmed; the Instructions page's full
  step-by-step prose is in scope for this pass, not deferred — user
  chose to include it now.
- Implemented `Shared/LocalizationManager.cs` + `Shared/Loc.cs` +
  `Shared/Strings.En.xaml`/`Strings.Bg.xaml`, reusing
  `ThemeManager.cs`'s existing resource-dictionary-swap pattern
  exactly rather than inventing a new mechanism. Added a second
  `ToggleButton` next to the existing theme toggle in `MainWindow.xaml`.
- Localized every page's UI chrome: Start, Project Management, Work +
  Preview (Energy Efficiency flow), Floor Heating + its results page,
  Instructions (full prose + both videos' controls), and the crash
  dialog — see decisions.md for the full file-by-file breakdown.
- Found and solved a real technical snag along the way: `StringFormat`
  bindings (`"Floor {0}"`, used on four pages) can't be
  `DynamicResource`-bound since `StringFormat` is parse-time, not a
  runtime dependency property. Fixed by adding a computed `FloorLabel`
  property to each affected view model instead, mirroring the
  `RoomLabel` pattern already used for `HeatingRoomViewModel`.
- Unified `FloorHeatingPage`'s seven near-duplicate per-field
  validation messages into one parameterized resource key.
- `dotnet build` clean after every stage; re-grepped every
  `Text=`/`Content=` in the app afterward to confirm only the
  intentionally-fixed strings remained. App launched and left running
  for the user to click through both languages directly.
- User approved after trying it live ("Very good, i like it").
  Committed on `feature/language-toggle`, then merged into
  `feature/floor-heating` (the branch point, since that branch is
  itself still unmerged into `main`) per explicit user instruction.

**Open for the next session:**

- Floor Heating's own open items (Session 12) are unchanged — still
  blocked on the user for the serpentine table and the Excel-output
  question.
- `feature/floor-heating` (now carrying both Session 12 and 13's work)
  still not merged to `main`.
- Everything else carried over from prior sessions (Phase 4 packaging,
  Phase 5 polish, empty test scaffold) untouched.

---

## 2026-08-14 — Session 14 (branch `feature/floor-heating`)

**Context:** first session working from a fresh checkout — started by
converting the user's freshly-uploaded `Топлотехника V6.0.16.xls` to
`.xlsx` (the tracked template was missing on disk) so `dotnet build`
would succeed. Then two fixes requested: the Work page's height field
rejecting decimals, and windows/doors not being extracted at all for
three new real sample floors the user uploaded.

**Done:**

- **Locale bug found and fixed.** Confirmed the user's machine is
  `bg-BG`; `double.TryParse` without an explicit culture rejects `.`-
  decimal input entirely under that locale (verified directly via a
  throwaway PowerShell check). Fixed in all three places this pattern
  existed: `FloorRowViewModel.TryGetHeightM`, `HeatingRoomViewModel`'s
  delta/Qпт parsing, and `FloorProcessor`'s DXF marker-attribute parsing
  — all now normalize `,`→`.` then parse with
  `CultureInfo.InvariantCulture` explicitly.
- **Investigated the "forgets windows/doors" report** by running a
  throwaway diagnostic harness against the user's 3 new sample floors —
  confirmed 0 openings extracted from all three, even after the locale
  fix, and traced it to a third DXF export convention (no `W Marker`/
  `D Marker` blocks at all; bare `MText`+`Line` leader annotations; pure
  Bulgarian wall-layer names) that the existing name-gated extraction
  code had no path for.
- User explicitly rejected patching in a fourth hardcoded convention and
  gave a detailed, specific redesign brief: detect openings from
  geometry/topology, treat names as hints only, never silently return
  zero, and prove name-independence with renamed-layer tests. Entered
  plan mode; the user corrected one design detail mid-plan ("an opening
  is a perpendicular line to the OVK layer with two numbers next to the
  line," replacing an early "gap in the wall run" idea) before approving.
- **Implemented the full pipeline** (`src/HVACrate2.Core/Openings/`, ~10
  new files) — see decisions.md for the complete architecture. Deleted
  `WallTopology.cs` entirely (confirmed via grep it had no other callers
  once the old extraction method was replaced).
- **First implementation had a real bug, caught by testing against the
  real files, not assumed correct:** an "exterior classifier" that
  snapped each candidate to the nearest wall-like point within an 8m
  search radius accepted almost everything in a compact floor plan
  (124 openings, many with implausible ~0.3-0.5m widths). Root-caused,
  reverted to a direct anchor-to-`OVK`-boundary distance check with a
  per-strategy tolerance, and tightened the leader-line strategy's
  label-pairing (labels must cluster near each other, not just near the
  line) — brought results down to plausible door/window sizes.
- Extended `tests/HVACrate2.Core.Tests`: real-sample regression tests
  (skip, don't fail, if `samples/` isn't present — it's gitignored) plus
  synthetic in-memory `DxfDocument`s with deliberately nonsense layer/
  block names, proving the two implemented strategies don't depend on
  recognizing any specific name. Added
  `FloorProcessor.ProcessFloorFromDocument` as the public test entry
  point (split from `ProcessFloor`, which still just loads a file). All
  7 tests pass (`dotnet run --project tests/HVACrate2.Core.Tests` — plain
  `dotnet test` no longer works on this SDK).
- Full solution `dotnet build` clean. App launched and left running for
  the user to click through the real Work → Extract & Preview flow
  directly.

**Continued the same session** once the user supplied their own
manually-extracted reference file (`Топлотехника V6.0.16.xls`, a live
client file — converted to `.xlsx` via Excel COM automation for reading,
never modified, not committed anywhere) for the same building:

- Compared the 46/77/76-per-floor result against the reference's merged
  108-opening table — confirmed real over-counting (199 raw openings,
  ~1.84x too many), matching the user's own direct report.
- Found and fixed two concrete bugs by tracing evidence, not guessing:
  (1) one physical opening detected multiple times from its own frame/
  jamb geometry, not deduplicated because the hits landed farther apart
  than the position-based merge tolerance — fixed by deduplicating on
  label-entity identity instead of position; (2) a handful of false
  positives from furniture/dimension/installation annotation layers
  coincidentally matching the geometric pattern — fixed with a small,
  evidence-driven negative name-hint list. Brought the total to 106,
  9/14 sizes matching the reference exactly.
- User then flagged one specific remaining false positive
  (`0.8×2.0m`) with a screenshot of the actual drawing, confirming it's
  a real interior door (room → balcony) and asking for a real fix, not
  just a note. Root-caused: exterior classification only checked
  distance to the OVK curve, not whether the opening's own host wall was
  itself part of the boundary. Added a wall-*backing* check
  (`WallGeometryClassifier.CollectWallLikeSegments`, requiring both
  endpoints of a segment near OVK) plus a margin-guarded override for
  walls explicitly labeled interior — found and fixed a real bug in the
  process (the wall-name hint didn't distinguish "интериор" from
  "екстериор" in Bulgarian, since both contain "стен"). Iterated three
  times against the real comparison after each change (a bare "closer
  than" override and an over-tight 0.25m threshold each looked like
  progress in isolation but regressed other correct matches when
  checked) before landing on the version that fixed the flagged case
  without regressing others: 104/108, 10/14 exact.
- Fixed a separately-reported UI bug: the 2D preview's north-arrow label
  could land outside the canvas's clipped bounds depending on the
  floor's north angle, leaving a stray line fragment (user circled a
  screenshot). Root cause was a fixed anchor position never checked
  against every possible angle; moved it to guarantee clearance.
- Full solution `dotnet build` clean and all 7 tests passing after every
  change in this continuation, not just at the end. App relaunched with
  each new build so the user could verify live.

**Open for the next session:**

- Remaining ~4-point gap (104 vs. 108) across a few sizes with 1-3
  fewer/extra counts per direction — smaller than what's been fixed so
  far, not yet individually root-caused the way the two bigger rounds
  above were.
- `BlockAttributeStrategy`'s looser (2.5m) exterior tolerance for
  detached annotation blocks is a known, explicitly-flagged trade-off
  versus the old full topology-hop-tracing — only synthetic-tested, not
  re-validated against the original floor1-4/`example.dxf` edge case
  that motivated the old approach, since those sample files no longer
  exist on disk.
- `feature/floor-heating` still not merged to `main`; Floor Heating's own
  open items (Session 12) unchanged.
- Everything else carried over from prior sessions (Phase 4 packaging,
  Phase 5 polish) untouched.

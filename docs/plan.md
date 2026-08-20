# Plan / Roadmap — HVACrate 2.0

## Phase 1 — Core extraction logic (library, no UI)

- [x] Project structure (.NET 10 solution, WPF + core library)
- [x] DXF reading (netDxf) — layers, INSERT + ATTRIB (nested, not just
      top-level — see [decisions.md](decisions.md))
- [x] Wall length calculation by 8 directions (relative to a given
      north) — **via the `OVK` boundary layer**, not direct wall-layer
      scanning (see decisions.md, 2026-08-04). Direction formula bug
      (couldn't tell north from south) also fixed same session.
- [x] Opening extraction (W/D Marker) — width/height/direction — via
      nested-marker scan + OVK-touch filter, tested on `floor2.dxf`
- [x] Grouping openings by (width, height) → count by direction
- [x] Writing to the Excel template (ClosedXML) — wall block + opening
      block. Tested against a real `.xlsx` (converted from the user's
      real `.xls` template) on a scratch copy — see decisions.md,
      2026-08-04 (row-finder bug fix).
- [x] **Validation:** re-ran against the real Floor I file
      (`samples/floor1.dxf`, replacing the earlier placeholder). Area,
      С/Ю wall lengths, window #1 (1.5×1.7), and exterior corner count
      (n=6) all matched the original reference exactly. И/З lengths
      came out 12.50m vs. the reference's 12.55m (0.05m each) — user
      confirmed this small a gap is not a concern. See decisions.md,
      2026-08-04 (exterior vs. interior corners).

## Phase 2 — Open issues from Phase 1

- [x] Floor area (Af) and volume (V) — resolved via `OVK` boundary
      shoelace formula (see decisions.md, 2026-08-04)
- [x] Confirm which marker ATTRIB tag is width / which is height —
      confirmed on real data (`floor2.dxf`, multiple markers):
      `AC_MarkerText_2` = width, `AC_MarkerText_3` = height, both cm
- [ ] Decide whether/how to include ARC segments in length-by-direction
      — likely moot now that wall length comes from the `OVK` polyline
      rather than raw wall geometry; revisit only if `OVK` tracing
      itself ever needs to follow a curved facade
- [x] Number of "exterior corners" — implemented via convex/reflex
      vertex classification (`CountConvexCorners`, sign of the
      cross product of adjacent edges vs. the polygon's overall
      winding). Validated on `floor1.dxf`: 6 convex (exterior) + 2
      reflex (interior/notch) = 8 total OVK vertices, matching the
      user's reference n=6. Written to Excel column `K`, row
      `FloorRow`. See decisions.md, 2026-08-04.
- [x] `AC_WIDO_ID` marker attribute — final decision: unused, ignored.
      See decisions.md, 2026-08-05.
- [x] `OVK` boundary approach validated against a non-rectangular,
      rotated (north=45°) real file (`samples/example.dxf`) and the
      marker-at-a-corner tie-break case, 2026-08-09. Two real bugs
      found along the way and fixed: the boundary polyline was picked
      via `FirstOrDefault` (wrong on files that reuse the `OVK` layer
      for room outlines too — see decisions.md), and the corner
      tie-break originally picked the wrong facade for one opening
      (now resolved deterministically — see decisions.md).
- [x] `OvkLayer` name — final decision: hardcoded `"OVK"`/`"ovk"`, not
      configurable. See decisions.md, 2026-08-05.
- [x] Columns H (Аок)/I (Аерк)/J (Lерк) — final decision: left blank,
      no calculation. See decisions.md, 2026-08-05.
- [x] Opening exterior/interior classification — marker-to-`OVK`
      straight-line distance replaced with wall-topology tracing
      (host wall determined from geometry, classified by whether that
      specific wall reaches `OVK`). See decisions.md, 2026-08-09, for
      the full investigation and why every distance-threshold
      approach tried first failed.
- [x] Exterior/interior classification imprecision for small doors
      (`floor2.dxf`/`floor3.dxf` w=90cm doors disagreeing with each
      other and with the reference table) — root cause found and
      fixed: the CAD exporter splits one physical exterior wall into
      many separate `Wall_N_2` blocks with no explicit link between
      them. `WallTopology.BuildWallRun` reconstructs the physical wall
      run (nested convention only) for the exterior/interior decision;
      path-finding and direction stay scoped to the opening's own host
      block. Validated against the full 4-floor reference table: all
      17 rows match exactly (53/53 openings, 0 false positives, 0
      false negatives, 0 direction errors, 0 duplicates). See
      decisions.md, 2026-08-10.
- [x] **Resolved — real root cause found, not just accepted:**
      `samples/floor1.dxf` drift (found 2026-08-09) traced to the
      sample file itself having an incorrectly-drawn `OVK` layer. User
      re-exported a corrected `floor1.dxf`; re-run reproduces the
      original 2026-08-04 reference numbers exactly (Af=110.90m²,
      С=9.70/И=12.50/Ю=9.70/З=12.50, n=6). Confirms the extraction code
      was never at fault. `samples/floor2.dxf`'s current on-disk copy
      confirmed by the user to be the correct file — no fix needed,
      its drift note is closed too. See decisions.md, 2026-08-10.
- [x] **Final decision:** coordinate-unit auto-detection (centimeters
      vs millimeters) stays as-is. User confirmed the unit is a choice
      made when exporting the `.dxf` from AutoCAD — a wrong result from
      a third, untested convention is a user-side export issue, not
      something the app needs to guard against further.
- [x] ~~**Final decision:** wall-layer detection
      (`WallTopology.IsWallLayer`, English "wall" substring match)
      stays as-is. User confirmed every wall-layer naming convention in
      real projects is English — no need to support non-English layer
      names.~~ **Superseded 2026-08-14** — real Bulgarian-only wall
      layers (`Стени - екстериор`/`Стени - интериор`) turned up in
      `floor1-3.dxf`. `WallTopology.cs` (and its English-only substring
      check) was deleted entirely as part of the Phase 8 opening-
      extraction rebuild — wall-like geometry is now identified by
      proximity to the `OVK` boundary, not by layer name in any
      language. See decisions.md, 2026-08-14.
- [x] **Resolved, not a code bug:** the 0.4m×2.46m window in
      `floor2.dxf`/`floor3.dxf` — user checked the real CAD file and
      confirmed the window is real; the earlier "does not exist" report
      was the user's own mistake. See decisions.md, 2026-08-10.
- [x] **Resolved, not a code bug:** the 90×210cm door height in
      `floor3.dxf` (reference implied 208cm) — confirmed a bug in the
      source drawing/reference, not in extraction; the DXF's own
      `AC_MarkerText_3` data (210cm) stands. See decisions.md, 2026-08-10.

## Phase 3 — WPF UI

- [x] Start menu page — title, "Start" (-> Projects) and "Instructions"
      buttons
- [x] Projects menu — sits between Start and the Floors page. Lists
      projects (name, floor count, creation date) with Open/Delete;
      "+ New Project" creates and opens one. **In-memory only, per user
      instruction — not persisted across app restarts.** See
      decisions.md, 2026-08-05 (Phase 3 UI session).
- [x] DXF file picker — per floor, via `OpenFileDialog`
- [x] Manual input form: floor height (per floor) and north/"up"
      direction (dropdown, full compass names, drives an animated
      custom vector compass control). Wall-layer-name input dropped —
      moot now that `OvkLayer` is a hardcoded final value (see
      decisions.md, 2026-08-05, Session 4).
- [x] Support repeating the process for multiple floors in one project
      — dynamic add/remove floor rows on the Work page
- [x] "Extract & Preview" button — runs `FloorProcessor.ProcessFloors`
      (compute only) and navigates to a review page; "Confirm & Write
      Excel" + "Download filled Excel" moved there (see the 2D preview
      item below). No separate template *picker* — the blank template
      is bundled into the app's own output folder
      (`Assets/Template.xlsx`, linked from
      `output/Топлотехника V6.0.16.xlsx`) rather than chosen per run.
- [x] Light/dark theme toggle (persistent, top-right of the window),
      gradient page backgrounds, themed inputs — not in the original
      Phase 3 scope, added per user request this session.
- [x] 2D preview of the recognized walls/windows (canvas) + results
      table before writing — built together as one review step
      (`PreviewPage` + `FloorPreviewControl`), inserted between
      "Extract" and the Excel write. Per floor: a drawn OVK boundary +
      opening markers (with a north arrow using the same bearing
      convention as classification) alongside area/volume/perimeter/
      corners/wall-lengths/openings. `FloorProcessor.ProcessAndWriteFloors`
      split into `ProcessFloors` (compute) + `WriteFloorsToExcel`
      (write pre-computed results) so the preview doesn't re-parse the
      DXFs before writing. Verified live end-to-end (not just built) —
      see decisions.md, 2026-08-10.
- [x] Instructions page — two parts, each with a video + numbered
      written steps: (1) exporting the `.dxf` from AutoCAD (`OVK`
      layer, `WBLOCK`), (2) using HVACrate 2.0 to extract the Excel
      result. Videos resolve local-first (`videos/`, gitignored) and
      fall back to hosted GitHub Releases URLs otherwise — see
      decisions.md, 2026-08-05 (Instructions page session).
- [x] Per-floor "Apartments" input (next to height/direction on the
      Work page) — drives the building-wide "electric consumers"/lamp
      block in the Excel template (stoves, fridges, TVs, laundries,
      PCs, others, lamps, occupant count). See decisions.md,
      2026-08-07.
- [x] Loading indicator on "Extract & Fill Excel" — custom animated
      spinner control, extraction now runs off the UI thread. See
      decisions.md, 2026-08-07.

## Phase 4 — Packaging and distribution

- [x] `dotnet publish` single-file self-contained build for win-x64 —
      automated in `.github/workflows/release.yml`, triggered by
      pushing a `v*` tag. `HVACrate2.App.csproj` now sets
      `AssemblyName=HVACrate2` and `Version=1.0.0` so the published
      exe is `HVACrate2.exe`. See decisions.md, 2026-08-20 (Release
      automation session).
- [ ] Test the `.exe` on a "clean" machine (no .NET installed) —
      `--self-contained true` bundles the runtime, which addresses the
      underlying concern architecturally, but a literal fresh-VM test
      hasn't been done and needs the user (no such environment
      available to Claude Code in this session).
- [x] Upload to GitHub Releases — automated by the same workflow
      (`softprops/action-gh-release@v2`, tag-triggered). No manual
      upload or `gh` CLI needed for any future release; just push a
      version tag.
- [ ] Link to the `.exe` from the existing static website — outside
      this repo, the user's own action. Stable URL to use (always
      resolves to the newest release, never needs updating):
      `https://github.com/ivanzlatinov1/HVACrate-2.0/releases/latest/download/HVACrate2-win-x64.zip`.
      The download is a zip, not a bare `.exe` — WPF's native interop
      DLLs get folded into the exe via
      `IncludeNativeLibrariesForSelfExtract`, but the bundled
      `Assets/Template.xlsx` is a loose file the app reads from disk
      next to the exe, so it can't be embedded the same way. See
      decisions.md, 2026-08-20 (Release automation session).

## Phase 5 — Polish (later, not urgent)

- [ ] Configurable wall-layer selection per project (different firms
      use different layer naming conventions)
- [ ] Better error handling for missing/malformed DXF data
- [ ] End-user documentation (short usage guide)

## Phase 6 — Floor Heating (new feature, in progress, branch
`feature/floor-heating`)

A second, independent calculation track alongside the Energy Efficiency
(DXF→Excel) flow — per-room floor heating heat-flow. Not in the original
CLAUDE.md scope; started 2026-08-10 at explicit user request, from
formula/constant reference sheets the user supplied as screenshots (not
yet added as files anywhere in the repo).

- [x] Start page restructured to 4 entry points (Project Management,
      Energy Efficiency, Floor Heating, Instructions), gated by a
      selected "current project" — `ProjectStore.CurrentProject`,
      set by Project Management's Open/Create, consumed by Start to
      enable/disable the two project-scoped buttons. See decisions.md,
      2026-08-10 (Floor Heating session).
- [x] `FloorHeatingCalculator` (`HVACrate2.Core/FloorHeating/`) —
      Rог, Rод, Ro, r_пд, Qc, m (kg/h), Qдол, all constants hardcoded,
      deltas + Qпт taken per room. Verified against the user's own
      worked example numbers (Ro=1.4881) and a real reference table
      row (m=176.64, Qдол≈194.4).
- [x] `FloorHeatingPage` (data entry: dynamic floor/room lists, 6
      deltas + Qпт per room) split from `HeatingResultsPage` (per-floor
      results table: Помещение/Qпт/r пд/Qc/m/Qдол) — Calculate
      validates then navigates between them.
- [x] Floor/room data persisted on `ProjectRecord.HeatingFloors` (not
      just the page instance) — re-entering Floor Heating for the same
      project keeps previously entered data. Still lost on app restart,
      same as every other in-memory project field.
- [ ] **Blocked on the user, not a code gap:** the feature is missing
      the information needed to finish it — a second table ("for the
      serpentines for each room") was requested but its formulas/
      columns were never supplied, and it's unclear whether floor
      heating needs any Excel output or stays in-app-only. Explicitly
      deferred to a future session per the user.

## Phase 7 — Language toggle (English / Bulgarian), branch
`feature/language-toggle` off `feature/floor-heating`

- [x] Persistent language toggle next to the existing theme toggle in
      `MainWindow.xaml` — switches every page's UI chrome between
      English and Bulgarian and back, live, without navigating away.
      `Shared/LocalizationManager.cs` mirrors `ThemeManager.cs`'s
      resource-dictionary-swap pattern exactly (`Strings.En.xaml` /
      `Strings.Bg.xaml`); `Shared/Loc.cs` covers the code-behind cases
      `DynamicResource` can't reach (dynamic strings, `MessageBox`
      content, `ToString()` overrides). See decisions.md, 2026-08-10
      (Language toggle session).
- [x] Every page's UI chrome localized: Start, Project Management,
      Energy Efficiency (Work + Preview), Floor Heating + its results
      page, Instructions (including the full step-by-step prose, not
      just short labels — explicit user decision), the video player
      controls, and the crash dialog.
- [x] **Final decision — stays fixed in both languages, not
      translated:** formula/domain notation (Rог, Rод, r_пд, Qc, Qпт,
      Qдол, δ-symbols), room labels ("пом.01"), the wall/opening
      compass-direction letters already produced by `HVACrate2.Core`
      (С/И/Ю/З/etc.), and the compass dial's N/E/S/W-style labels in
      `CompassControl.xaml`. Only the Work page's direction *dropdown*
      (`CompassDirectionOption`) translates — a UI input control, not
      calculation output.

## Phase 8 — Name-agnostic opening extraction, branch
`feature/floor-heating` (2026-08-14 session)

- [x] Locale bug fixed: height field (and the floor-heating delta/Qпт
      fields, and DXF marker-attribute parsing) rejected decimal input
      under a non-`.`-decimal OS locale (e.g. `bg-BG`) because
      `double.TryParse` used the current culture instead of an explicit
      invariant one. See decisions.md, 2026-08-14.
- [x] Opening (window/door) extraction rebuilt from scratch as a
      name-agnostic geometry/topology pipeline
      (`src/HVACrate2.Core/Openings/`), replacing the old
      `W Marker`/`D Marker` name-gated approach and the `WallTopology.cs`
      hop-tracing classifier it depended on (both retired/deleted). Two
      candidate strategies (`BlockAttributeStrategy`,
      `PerpendicularLabeledLineStrategy`), exterior/interior classified
      by direct distance to the `OVK` boundary, best-effort type/
      confidence scoring, deduplication, and per-floor diagnostics
      (`OpeningExtractionDiagnostics`, surfaced as a warning on the
      Preview page instead of a silently-empty table). Validated against
      the 3 real `floor1-3.dxf` samples (0 openings before → 46/77/76
      after) and synthetic in-memory documents with deliberately
      arbitrary layer/block names, proving detection doesn't depend on
      recognizing any specific name. See decisions.md, 2026-08-14, for
      the full architecture and the rejected "8m snap radius" approach
      that had to be walked back after producing 100+ false positives.
- [x] **Validated against a real reference table**, supplied by the user
      the same day: 106 openings extracted vs. 108 in the reference,
      9/14 real sizes matching exactly (all 4 directions). Two real bugs
      found and fixed in the process — see decisions.md, 2026-08-14
      (follow-up entry): (1) one physical opening detected multiple times
      from its own body geometry (frame/jamb lines), not deduplicated
      because the hits landed farther apart than the position-based
      merge tolerance — fixed by deduplicating on label-entity identity
      instead of position; (2) a handful of false positives from
      furniture/dimension/installation annotation layers coincidentally
      matching the geometric pattern — fixed with a small, evidence-
      driven negative name-hint list (`WordHints.NonOpening`).
- [x] **The `0.8×2.0m` anomaly root-caused and fixed** — it was a real
      interior door (room → balcony), confirmed by the user against the
      original drawing. Exterior classification needed a wall-*backing*
      check (is the opening's own host wall itself part of the OVK
      boundary?), not just an OVK-*distance* check — added
      `WallGeometryClassifier.CollectWallLikeSegments` (both endpoints
      near OVK) plus a targeted, margin-guarded override for walls
      explicitly labeled interior. Also fixed a real bug found along the
      way: the wall-layer name-hint path accepted `Стени - интериор`
      (interior) as readily as `Стени - екстериор` (exterior), since
      Bulgarian "стен" doesn't distinguish them — removed, proximity to
      OVK is the only wall-likeness signal now. Result: 104/108 openings,
      10/14 sizes matching exactly (up from 9/14). See decisions.md,
      2026-08-14 (second follow-up).
- [x] Preview page north-arrow rendering bug fixed — the arrow's fixed
      screen anchor let the "N" label land outside the canvas's clipped
      bounds for some north angles, leaving a stray line fragment. See
      decisions.md, 2026-08-14.
- [ ] `BlockAttributeStrategy`'s looser exterior tolerance (2.5m, vs.
      0.8m for the leader-line strategy) is an explicitly acknowledged
      trade-off versus the old full topology-hop-tracing for a detached
      annotation whose real wall is ambiguous by distance alone — the
      original floor1-4/`example.dxf` sample files that motivated that
      old approach no longer exist on disk, so this trade-off is only
      synthetic-tested, not re-validated against the real historical
      edge case.

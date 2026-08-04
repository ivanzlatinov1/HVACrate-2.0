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

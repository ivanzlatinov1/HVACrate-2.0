# HVACrate 2.0 — Context for Claude Code

## What this project is

A desktop application (C#, .NET 10) that automates part of the daily
workflow of producing a building energy/thermal performance assessment.
The user (an engineer) currently does this by hand in Excel: for each
floor of a building they enter the floor area, height, length of the
exterior walls by compass direction, number of exterior corners/edges,
and number/size of windows and doors by direction.

Goal of the application: read a DWG/DXF drawing of a single floor and
automatically extract most of this data, then write it into the user's
existing Excel template.

## Technology choices

- **C# / .NET 10**, desktop application (WPF), not web.
- Reason: the end deliverable must be a downloadable `.exe`, linked from
  an already-existing static website owned by the user (not a server app).
- **netDxf** — DXF geometry reading (LINE, POLYLINE2D, INSERT, attributes).
- **ClosedXML** — reading/writing `.xlsx`.
- Publishing: `dotnet publish -r win-x64 --self-contained true
-p:PublishSingleFile=true` → a single .exe, uploaded to GitHub Releases,
  linked from the static site.

## Key assumptions (confirmed against a real project)

- **Input: one DXF file per floor.** The user supplies a separately
  cropped DXF for each floor (not one combined Model Space with all
  floors together), even for projects where the original DWG has all
  floors drawn in a single Model Space — the split is done by the user
  beforehand (e.g. via WBLOCK in AutoCAD from the relevant Layout/viewport).
- **Wall layer:** in the analyzed real project it was `_A [walls]`
  (LINE, ARC, POLYLINE2D). The layer name likely varies between projects
  from different architecture firms — it is not hardcoded permanently,
  it should be configurable/selectable by the user per project.
- **Windows and doors are treated the same**, in one combined table (per
  explicit user instruction — not distinguished as separate types).
- **Openings are read from marker blocks**, not from the geometry of the
  window block itself in plan view. INSERT blocks whose name contains
  "Marker" (`W Marker`, `D Marker`) carry ATTRIB attributes with text for
  width (in cm) and height (in m). Which attribute index/tag corresponds
  to width vs height must be re-confirmed against real data for each new
  batch of drawings — it is not guaranteed to be stable across projects.
- **Height and north direction are entered manually** by the user for
  each new project/floor — they are NOT auto-detected from the drawing.
- **8 directions:** N, NE, E, SE, S, SW, W, NW — 45° sectors, "N" is
  offset by the user-supplied north angle.
- **Still unresolved:** automatic calculation of floor area (Af) and
  volume (V) from the drawing outline — requires a closed external
  boundary, which does not always exist as a separate polyline.
- **Wall ARC segments** (curved/bay sections) are currently NOT included
  in the wall-length-by-direction calculation.

## Mapping to the Excel template (sheet "Calculations")

- "Geometric characteristics" block, floor row (e.g. A7 = Floor I):
  `C` = h, `E` = ... (see decisions.md for the full column list).
- "Wall description by facade" block, floor row (e.g. A31 = Floor I):
  `D..K` = lengths by direction N,NE,E,SE,S,SW,W,NW; `L` = perimeter.
- "Description of transparent doors and windows" block, table starting
  at row 57: `A` = #, `B` = width (a), `C` = height (b), `D..K` = count
  by direction, `N..U` = area by direction (computed by Excel formulas).

## End-of-session workflow

**At the end of every working session, before you finish:**

1. Append a short summary to `docs/session-log.md` of what was done this
   session (date, what was tested, results, what remains open).
2. If a new significant decision was made or an assumption was
   confirmed/rejected — add an entry to `docs/decisions.md`.
3. If a phase's status in `docs/plan.md` changed — update it.

Do this automatically as the last step of the session — do not wait for
the user to ask for it explicitly.

## Useful commands

```
dotnet build
dotnet run --project src/HVACrate.App
dotnet publish src/HVACrate.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

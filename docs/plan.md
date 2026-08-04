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
- [ ] **Validation:** the original reference values (Floor I,
      N=9.7m/E=12.55m/S=9.7m/W=12.55m, perimeter 44.5m, window #1 =
      1.5m×1.7m) were never actually reproducible by the original
      code (see direction-formula bug in decisions.md) and have not
      been re-validated against a same-floor `OVK`-based run yet —
      `floor2.dxf` is a different floor with no independent reference
      values to check against. Still open.

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
- [ ] Number of "exterior corners" — not yet addressed. May be
      derivable from `OVK` polygon vertex angles (interior corners vs.
      exterior/convex corners), not yet designed
- [ ] `AC_WIDO_ID` marker attribute — meaning unconfirmed, not used
- [ ] `OVK` boundary approach validated on one sample only
      (`floor2.dxf`). Needs testing against more floor plans: a
      non-rectangular shape, a rotated building (non-zero north angle),
      and a marker sitting equidistant between two `OVK` edges at a
      corner (tie-break behavior untested)
- [ ] Decide whether `OvkLayer` name should be user-configurable per
      project (like `WallLayer` was) rather than hardcoded `"OVK"`

## Phase 3 — WPF UI

- [ ] DXF file picker
- [ ] Manual input form: floor height, north direction, wall layer name
- [ ] 2D preview of the recognized walls/windows (canvas) — for visual
      verification that the correct elements are being read
- [ ] Results table before writing
- [ ] Excel file (template) picker + "Write" button
- [ ] Support repeating the process for multiple floors in one project

## Phase 4 — Packaging and distribution

- [ ] `dotnet publish` single-file self-contained build for win-x64
- [ ] Test the `.exe` on a "clean" machine (no .NET installed)
- [ ] Upload to GitHub Releases
- [ ] Link to the `.exe` from the existing static website

## Phase 5 — Polish (later, not urgent)

- [ ] Configurable wall-layer selection per project (different firms
      use different layer naming conventions)
- [ ] Better error handling for missing/malformed DXF data
- [ ] End-user documentation (short usage guide)

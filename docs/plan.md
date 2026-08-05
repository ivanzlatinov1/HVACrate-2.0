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
- [ ] `OVK` boundary approach validated on one sample only
      (`floor2.dxf`). Needs testing against more floor plans: a
      non-rectangular shape, a rotated building (non-zero north angle),
      and a marker sitting equidistant between two `OVK` edges at a
      corner (tie-break behavior untested). **Deferred until after
      Phase 3 (UI).**
- [x] `OvkLayer` name — final decision: hardcoded `"OVK"`/`"ovk"`, not
      configurable. See decisions.md, 2026-08-05.
- [x] Columns H (Аок)/I (Аерк)/J (Lерк) — final decision: left blank,
      no calculation. See decisions.md, 2026-08-05.

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

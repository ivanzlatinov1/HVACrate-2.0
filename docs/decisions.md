# Decisions Log — HVACrate 2.0

Short chronological log of significant decisions and why they were made.
The goal is to avoid re-litigating the same debates later without context.

---

## 2026-08-03 — Technology choice: C# / .NET 10, not Python/JS

**Decision:** the application is built as a desktop WPF application in
C# / .NET 10, published as a single-file `.exe`.

**Reason:** the end goal is a `.exe` that the user links for download
from an already-existing static website. Alternatives considered:

- Python script — good for quickly prototyping/validating the logic, but
  does not easily produce a distributable `.exe` without extra tooling.
- Static web app (JS in the browser, dxf-parser + SheetJS) — works
  entirely client-side, no server needed, but the user explicitly
  preferred a desktop `.exe` over a web interface for a richer UI (WPF)
  and easier distribution as a link to a binary file.

**Rejected:** a server-based web app with a backend — not needed, since
the entire process (DXF reading, calculations, Excel writing) can happen
fully locally, and a server adds unnecessary complexity/hosting cost.

---

## 2026-08-03 — Windows and doors in one combined table

**Decision:** openings (windows and doors) are treated the same — one
combined table with width/height/count by direction, no split by type.

**Reason:** explicit user instruction — for the purposes of the energy
assessment the distinction is not needed at this stage.

---

## 2026-08-03 — Input: one DXF file per floor, not a combined Model Space

**Decision:** the application accepts a separate DXF file for each
floor. The user pre-splits the drawing (e.g. via WBLOCK in AutoCAD from
the relevant Layout), even for projects where the original DWG has all
floors drawn together in one Model Space.

**Reason:** analysis of a real project (Brizstroy_Misari.dwg) showed
that although each floor has its own named Layout (`03 0.00`,
`04 +2.85`, ...), the actual geometry of all floors lives in one shared
Model Space, separated only spatially (viewport bounding box).
Automatic geometric separation by viewport turned out to be fragile and
hard to verify without live visualization. Manually pre-splitting the
input is a more reliable and simpler option, even with some manual
effort from the user.

**Alternative considered and rejected (for now):** automatically
computing a bounding box from the VIEWPORT entities of each Layout and
filtering geometry by it. Left as a possible future improvement if it
can be proven reliable.

---

## 2026-08-03 — Opening dimensions extracted from markers, not block geometry

**Decision:** window/door width and height are read from ATTRIB
attributes on `W Marker` / `D Marker` INSERT blocks, not from the
bounding box of the window/door block geometry itself in plan view.

**Reason:** the block geometry in plan (top-down view) only gives an
X/Y extent (~width), not the height of the opening (not visible in plan
view). It was found that the marker blocks carry text ATTRIB attributes
with exactly these two numbers (e.g. "150" for width in cm and "1.7" for
height in m), which matched manually entered values in the sample
project.

**Open question:** the attribute index/tag that corresponds to width vs
height has only been confirmed with one example. Needs additional
validation against several different markers before it can be relied on
as stable across projects.

---

## 2026-08-03 — Floor area and volume: still unresolved

**Status:** no final decision made. Requires a closed outline of the
floor's exterior envelope, which does not always exist as a separate,
easily identifiable polyline in the drawing. To be addressed in a
future session, likely via a combination of: (a) looking for a
dedicated "area boundary" polyline if one exists, or (b) constructing
an outline from the exterior walls themselves.

**Superseded 2026-08-04** — see "OVK boundary layer" decision below,
which resolves this via option (a).

---

## 2026-08-04 — Direct wall-layer/marker extraction abandoned; real

sample files use per-instance blocks, not plain LINE/LWPOLYLINE

**Decision:** the original extraction approach (scan LINE/LWPOLYLINE on
a named wall layer directly in model space; find window/door data from
top-level INSERT blocks named `W Marker`/`D Marker`) does not work on
real sample files and has been replaced (see next entry).

**Reason:** tested against two real samples (`samples/floor1.dxf`,
`samples/floor2.dxf`), both produced by the user's actual CAD workflow:

- Walls are not plain LINE/LWPOLYLINE on the wall layer. Each wall run
  is its own uniquely-named block (`Wall_1_2`, `Wall_2_2`, ... one block
  definition per instance), placed via a single top-level INSERT with
  an identity transform (position = block's own base point, rotation 0,
  scale 1). The block's own geometry already contains absolute/world
  coordinates, not small local/unit coordinates — i.e. local space and
  world space coincide for these particular blocks.
- Window/door `W Marker`/`D Marker` blocks are never inserted at the
  top level. They are nested one level deeper, inside the specific
  `Wall_N_2` block that contains the opening. Because of the
  identity-transform property above, the nested marker's raw
  `Insert.Position` (as read from the parent block's entity list) can be
  used directly as its world position — no matrix composition needed
  for these files.
- `floor1.dxf` (no `OVK` layer, an early/incomplete sample) had **no**
  `W Marker`/`D Marker` instances anywhere — confirms doors/windows in
  this project are optional per drawing and must not be assumed present.

**Also confirmed (real data, `floor2.dxf`):** the marker's ATTRIB tags
are `AC_MarkerText_2` = width and `AC_MarkerText_3` = height, **both in
centimeters** (e.g. width=150, height=170 → 1.50m × 1.70m). This
replaces the earlier, unconfirmed assumption in the marker-extraction
code (positional index 0/1, height assumed to already be in meters).
There is also an `AC_WIDO_ID` attribute on window markers (not
consistently present on door markers) whose value does not correspond
cleanly to any dimension seen so far — not used, meaning unclear.

---

## 2026-08-04 — OVK boundary layer: user manually draws the floor's

exterior envelope; extraction is derived from it instead of from wall
geometry

**Decision:** the user manually traces the floor's exterior envelope as
a single closed polyline on a dedicated layer named `OVK`, one per
floor DXF (in addition to the existing per-floor DXF split). All of the
following are now derived from that one polyline instead of from wall
layer geometry:

- **Wall length by direction** — each edge of the `OVK` polyline is
  bucketed into one of the 8 compass directions by its outward-normal
  direction (see the winding/direction-formula fix below), and summed.
- **Floor area (Af)** and **volume (V = Af × h)** — shoelace formula on
  the `OVK` polygon. This resolves the "Floor area and volume: still
  unresolved" item above.
- **Window/door direction** — any `W Marker`/`D Marker` INSERT found
  anywhere in the document (top-level or nested in any block) is kept
  only if its position is within a tolerance (currently 0.5 m) of the
  `OVK` boundary; its direction is taken from the nearest `OVK` edge.
  Markers farther than the tolerance are treated as interior
  partitions and dropped. Validated on `floor2.dxf`: an interior door
  marker ~6–9 m from the boundary was correctly excluded, while
  exterior markers ~0.25 m from the boundary were correctly kept.

**Reason:** user's proposal, made after direct wall-layer scanning
repeatedly failed on real files (see previous entry) — walls are drawn
with inconsistent, per-project/per-CAD-tool internal representations,
but a manually-traced boundary is simple, always available if the user
draws it, and — as a bonus — solves the floor area/volume problem for
free, which the wall-geometry approach never could (no reliable closed
outline existed from wall geometry alone).

**Trade-off accepted:** one extra small manual step per floor (tracing
the `OVK` boundary), replacing the wall-layer-name configuration step.
Given wall geometry has proven unreliable across even two real sample
files from the same project, this is a net reduction in fragility, not
an increase in manual work.

**Open questions carried forward:**

- Only tested on `floor2.dxf` (one sample). Tolerance (0.5 m) and the
  "nearest edge wins" tie-break are not yet validated against more
  varied floor plans (e.g. a marker equidistant between two OVK edges
  at a corner).
- `AC_WIDO_ID` meaning still unconfirmed — may turn out to be useful
  (e.g. a family/type ID for future window/door type splitting) or
  irrelevant.
- Whether `OVK` is the right/final name for this layer, and whether it
  should be user-configurable per project like the old wall layer was,
  is not yet decided — currently hardcoded as `ProjectConfig.OvkLayer`
  default `"OVK"`.

---

## 2026-08-04 — Direction formula fixed: was unable to distinguish

opposite-facing walls (e.g. north vs. south)

**Decision:** direction-by-edge is now computed from the edge's true
outward-normal angle, using the `OVK` polygon's overall winding
(clockwise vs. counter-clockwise, from the sign of the shoelace sum) to
resolve which of the two perpendicular directions is "outward" — see
`EdgeOutwardDirection`/`BearingToDirection` in
`src/HVACrate2.Core/Program.cs`.

**Reason:** the original formula (`AngleToDirection`, still present in
the pre-2026-08-04 code) computed a wall's compass direction from its
_undirected_ run angle (`angle % 180`), discarding which of the two
endpoints came first. This is mathematically incapable of distinguishing
a horizontal wall on the north side of a building from one on the south
side (both reduce to the exact same angle), or east from west for
vertical walls. It could therefore never have reproduced the reference
values in `CLAUDE.md` (N and S both non-zero, E and W both non-zero) —
this was a **latent, never-actually-validated bug** in the original
Phase-1 code, not something introduced by the `OVK` change. It surfaced
immediately when tested against `floor2.dxf`: before the fix, all wall
length landed in only 3 of 8 buckets (e.g. all north+south length
lumped under "И"); after the fix, opposite sides split correctly and
equal-length parallel sides (`С`/`Ю` = 15.80m each) came out equal, as
expected for a rectangle-derived boundary.

**How it works:** a polygon edge's outward normal is `(dy, -dx)` for a
counter-clockwise polygon or `(-dy, dx)` for clockwise (rotate the edge
vector 90° toward the exterior). The resulting normal angle is then
converted straight to a compass bearing (`BearingToDirection`), with no
further ambiguity, because — unlike a lone LINE entity — a closed
polygon's winding order is a global, unambiguous property. This is an
additional reason the `OVK` boundary approach (previous entry) is more
robust than direct wall-geometry scanning: a loose collection of wall
LINE segments has no inherent winding order, so this fix would not have
been fully applicable to the old approach without extra work (e.g.
inferring "inside" from wall thickness or adjacency).

**Validated against:** `floor2.dxf` — С=15.80m, Ю=15.80m (equal, as
expected for parallel sides), И=12.55m, З=12.50m (both close to a
15.80×12.5 m rectangle-with-a-notch shape, small difference explained
by the notch, not a bug). Not yet validated against a non-rectangular
or rotated (non-zero north angle) floor plan.

---

## 2026-08-04 — `WriteToExcel` tested against a real `.xlsx`; two bugs

found and fixed (`OutputType`, opening-row finder)

**Decision:** ran the full pipeline (`floor2.dxf` → `WriteToExcel`)
against a real Excel template for the first time this session, using a
scratch copy of the user's actual working file
(`output/Топлотехника V6.0.16.xls`, converted to `.xlsx` via Excel COM
automation since ClosedXML cannot read the legacy binary `.xls`
format). Two bugs found and fixed in the process:

1. **`HVACrate2.Core.csproj` was missing `<OutputType>Exe</OutputType>`**
   — `dotnet run --project src/HVACrate2.Core` failed outright
   ("Ensure you have a runnable project type... current OutputType is
   'Library'"). Added it, since `Program.cs`'s `Main` is the console
   test harness this phase relies on.
2. **Opening-row finder skipped past the entire empty opening table.**
   `WriteToExcel` located the first free row for writing opening data
   via `while (!ws.Cell($"A{row}").IsEmpty()) row++;` starting at row 57. In the real template, column `A` in that block is **pre-filled
   by the template itself** with row index numbers (1, 2, 3, ...) all
   the way to row 91, even though the actual data columns (`B`
   width, `C` height, ...) are empty. The old check treated those
   pre-numbered-but-dataless rows as "already written" and would have
   started writing real data at row 93 — one row before an unrelated
   table ("Описание на плътни врати по типове и фасади", starting row
   94), misplacing/risking clobbering that table's header. Fixed by
   checking column `B` (an actual data column, never template-prefilled)
   instead of `A`. Verified after the fix: data for `floor2.dxf` landed
   correctly starting at row 57, matching the console output byte for
   byte (widths/heights/direction counts), and the wall block
   (`C31`..`L31`) wrote correctly for the row-31 (Floor I) slot.

**Important caveat:** this write test was run with `floor2.dxf`'s data
into the row-7/row-31 slot ("I етаж" / Floor I) purely to validate the
write mechanics — `floor2.dxf` is **not actually Floor I** of any real
project and has no known correct row mapping (see the still-open
"Validation" item in plan.md Phase 1). The write was done on a
**scratch copy** of the template
(`C:\Users\XXX\AppData\Local\Temp\claude\...\scratchpad\test_write.xlsx`),
never on the user's real file in `output/`, specifically because that
real file already contains live project data in those rows/columns —
overwriting it with test data would have destroyed real work.

**Not committed:** `output/Топлотехника V6.0.16.xls` and the converted
`output/Toplotehnika_V6.0.16.xlsx` are real client data, left
untracked (not covered by `.gitignore` — currently relies on the user
not `git add`-ing that folder; could add an explicit `.gitignore` rule
for `output/` in a future session if this recurs).

---

## 2026-08-04 — Real Floor I validation (`floor1.dxf`); exterior vs.

interior (reflex) corners distinguished; full "geometric
characteristics" Excel block now written

**Context:** the user replaced `samples/floor2.dxf` with the real
`samples/floor1.dxf` — the actual Floor I file behind the original
reference values quoted in CLAUDE.md (Af=110.9m², perimeter 44.5m,
С=9.7m/И=12.55m/Ю=9.7m/З=12.55m, window #1=1.5×1.7m), which the
original pre-`OVK` code had never been able to reproduce (see the
2026-08-04 direction-formula entry above).

**Result of first real run:** area matched exactly (110.90m² vs.
110.9), С and Ю matched exactly (9.70m), window #1 matched (1.5×1.7 →
С=1). И and З each came out 12.50m vs. the reference's 12.55m — a
0.05m gap on each. **User reviewed and explicitly accepted this gap as
fine** — not investigated further, not treated as a bug.

**Corner-count discussion:** the user initially expected `n=9` "outer
edges," then self-corrected to `n=6`, then clarified precisely: the
OVK boundary has **8** total edges, of which **6 are "outer"** (border
the true open exterior) and **2 are "inner"** — the two short (0.90m)
walls of a small stepped notch on the south side, which face each
other across the notch rather than facing open exterior air.

**Decision:** implemented this distinction as **convex vs. reflex
vertex classification**, not an ad hoc per-edge rule. For each OVK
vertex, compute the cross product of its incoming and outgoing edge
vectors; a vertex is convex (exterior corner) if the sign of that
cross product matches the polygon's overall winding sign (`ccwSign`,
already computed from the shoelace sum), reflex (interior/notch
corner) otherwise. See `CountConvexCorners` in
`src/HVACrate2.Core/Program.cs`.

**Validated against `floor1.dxf`:** of the 8 OVK vertices, exactly 2
(the notch's inner corners) came out reflex and 6 convex — matching
the user's `n=6` exactly. This resolves the previously-open Phase 2
item "Number of exterior corners."

**Also fixed while validating:** the "Geometric characteristics" block
(row 7 for Floor I: `C`=Af, `E`=h, `F`=V, `G`=P, `K`=n) was never
actually written to Excel before this session — `WriteToExcel` only
ever wrote the wall-by-facade block (row 31) and the opening table
(row 57+); area/volume/perimeter/corner-count existed only as console
output. `WriteToExcel` now also writes this block. Confirmed the real
column layout directly from the user's template (previously
undocumented — CLAUDE.md's placeholder note "`C` = h, `E` = ... see
decisions.md" was itself wrong/incomplete, now corrected in CLAUDE.md):
`C`=Af (m²), `E`=h (m), `F`=V (m³), `G`=P (m), `H`=Aок (m², opening
area), `I`=Аерк (m², envelope area), `J`=Lерк (m), `K`=n (count).
Columns `H`/`I`/`J` are not yet written — no calculation for them
exists yet.

---

## 2026-08-05 — Session 4 cleanup decisions: H/I/J left blank, `OVK`
name final, `AC_WIDO_ID` dropped, template tracked in `output/`

**H/I/J columns (Аок, Аерк, Lерк):** **final decision — leave blank.**
No calculation will be implemented for these. Not a gap to revisit.

**`OvkLayer` name:** **final decision — hardcoded, not configurable.**
The boundary layer must be named exactly `OVK` or `ovk`. This closes
the open Phase 2 item questioning whether it should be user-configurable
per project (like the old `WallLayer` was) — it will not be.

**`AC_WIDO_ID` marker attribute:** **final decision — unused, ignore.**
This is an ATTRIB tag found on some window marker blocks (not
consistently present on door markers) whose value never corresponded
cleanly to any known dimension. User confirmed it is not needed — the
only layer/attribute set relevant to extraction is the `OVK` boundary
plus the width/height marker tags already in use. No further
investigation planned.

**Template Excel file tracked in git:** `output/Топлотехника V6.0.16.xlsx`
is the user's blank template — empty and ready for the app to write
into — not live client data as previously assumed (see the Session 2
entry above, which had classified it as real client data and left it
untracked; that assumption was wrong for this file). Now committed to
the repo. `.gitignore` updated: `output/*` stays ignored (for actual
generated client output), with an explicit exception
(`!output/Топлотехника V6.0.16.xlsx`) so the template itself is tracked.

**OVK edge-case testing** (non-rectangular plan, rotated/non-zero-north
building, corner tie-break) — explicitly deferred until after Phase 3
(UI) is done, not dropped.

---

## 2026-08-05 — Phase 3 UI session: `FloorProcessor` extracted, Projects
menu (in-memory), theme system, custom compass, crash fix

**`HVACrate2.Core.Program.Main` refactored into a reusable API.** The
extraction/write logic that used to live only in the console harness's
`Main` method now lives in public `FloorProcessor.ProcessFloor` /
`WriteFloorToExcel` / `ProcessAndWriteFloors` (`src/HVACrate2.Core/FloorProcessor.cs`),
taking a `FloorInput` and returning a `FloorResult`. `Program.Main` is
now a thin console test harness on top of it, writing to a scratch file
instead of overwriting the tracked template. Necessary because the WPF
app needs to call this logic for an arbitrary number of floors, not just
run a single hardcoded `ProjectConfig`. Re-verified against
`samples/floor1.dxf` after the refactor — output unchanged from the
previously validated values.

**Navigation shape:** Start → Projects → Floors (Work), plus Start →
Instructions. Not the original two-page shape implied by CLAUDE.md's
"work page" / "instructions page" wording — a Projects list was added
between Start and the Floors page per explicit user request mid-session.

**Projects are in-memory only (`ProjectStore`), not persisted.**
**Decision, explicit user instruction:** "in memory database or
whatever you choose to store the data." No database, no file-backed
persistence — projects disappear when the app closes. Revisit only if
the user asks for persistence later; not an oversight.

**Excel template is bundled with the app, not picked per run.**
`output/Топлотехника V6.0.16.xlsx` (tracked per the Session 4 decision
above) is linked into the App project's build output as
`Assets/Template.xlsx` (`CopyToOutputDirectory`), so the app works
regardless of where it's installed relative to the source tree. The
Work page's "Extract & Fill Excel" button writes to a scratch temp file
via `FloorProcessor.ProcessAndWriteFloors`, never touching the bundled
template; "Download filled Excel" then lets the user Save As. There is
no template *file picker* in the UI — this wasn't asked for and the
bundled-template approach avoids a whole class of "wrong file selected"
mistakes.

**Compass control: ended on a hand-drawn vector needle, not an image.**
Went through three iterations this session: (1) image-based
(`compass.png`/`compass-dark.png` supplied by the user, theme-swapped),
(2) user decided against the images entirely ("I don't like it like
that"), (3) replaced with `Controls/CompassControl` — a fixed dial
(N/E/S/W + intercardinal NE/SE/SW/NW, never rotates) with a needle that
animates to the selected direction via `RotateTransform` + shortest-path
`DoubleAnimation` (`CompassControl.AnimateTo`, unbounded running angle
so e.g. NW→N animates +45° forward rather than spinning −315° backward).
The supplied PNG assets were deleted from the repo — not kept as a
fallback option.

**Light/dark theme system.** `ThemeManager` swaps
`Resources/Theme.Light.xaml` / `Theme.Dark.xaml` merged dictionaries at
runtime; every themed brush reference had to move from `StaticResource`
to `DynamicResource` for the swap to apply live without restarting the
app (this includes a from-scratch themed `TextBox`/`ComboBox` template,
since the default WPF chrome ignores custom brushes for those two
controls). Page backgrounds are bright gradients per theme, not flat
fills, per explicit user request. Not originally in the Phase 3 plan —
added at user request this session, on top of the base UI work.

**Bug found and fixed: `Run.Text` binding crash on the Projects list.**
`<Run Text="{Binding CreatedAt, StringFormat=...}"/>` defaults to a
`TwoWay` binding for `Run.Text`; `CreatedAt` is a get-only property, and
WPF throws a `XamlParseException` the instant the binding engine tries
to attach it — which only happens once the Projects list actually
renders a row (an empty list never applies the `DataTemplate`, so the
bug was invisible on first load and only surfaced via Start → Create
project → Back). Fixed with an explicit `Mode=OneWay`. Confirmed via a
scripted UI Automation repro of the exact reported flow against the
real built `.exe`, before and after the fix — not just code review.

**Added a permanent global exception handler** (`App.xaml.cs`,
`DispatcherUnhandledException`) that logs full details to
`%TEMP%\hvacrate-crash.log` and shows a friendly error dialog instead of
a silent hard crash. This is what surfaced the real stack trace for the
bug above; kept in permanently as a safety net rather than removed after
diagnosis.

**Still open / deferred, carried forward:**
- 2D preview of recognized walls/windows, and a results-review table
  before writing — neither built yet (see plan.md Phase 3).
- Instructions page is still a stub — blocked on the user recording a
  screen-capture video of the (now more stable) Work page.
- OVK edge-case testing (non-rectangular plan, rotated/non-zero-north
  building, corner tie-break) — still deferred, per the Session 4
  decision above.

---

## 2026-08-05 — Instructions page session: content, video hosting,
`samples/` untracked from git

**Instructions page built out** (closing the stub from the previous
session): two parts, each pairing a video with numbered written steps,
content and step text specified verbatim by the user —

1. Exporting the `.dxf` from AutoCAD: open the project, go to the
   floor, create the `OVK` layer (mandatory), enclose the floor with
   it, select the floor, `WBLOCK`, pick a start point on an OVK edge,
   choose a destination, save as `.dxf` (mandatory).
2. Using HVACrate 2.0: open the app, create a project (or add a floor
   to an existing one), import the `.dxf`, set height/direction, then
   download the filled Excel once all floors are in.

Built `Controls/VideoPlayerControl` (a `MediaElement` wrapper with
Play/Pause/Restart) rather than using `MediaElement` directly inline,
since the page needs two independent players with identical behavior.

**Video hosting: local-first, GitHub Releases URL fallback.** The two
tutorial videos (~65MB + ~11MB) were evaluated for where to live — see
the options discussed with the user: bundled into the repo/exe,
YouTube (would need a `WebView2` embed since `MediaElement` can't play
YouTube URLs directly), or GitHub Releases (same place the app's
`.exe` is already published). **Decision: GitHub Releases**, since
`MediaElement` can stream an https URL with no architecture change,
it's free, versioned alongside the app, and requires no new
dependency. The user uploaded both videos to the repo's `Pre-Release`
release. `VideoPlayerControl` resolves local-first (`videos/` next to
the repo root — fast, works offline, dev convenience) and falls back
to the hosted URL if the local file isn't present; a `MediaFailed`
handler shows a friendly message instead of a blank player if the
remote stream can't be reached. Verified both paths against the real
built `.exe` — local playback, and remote streaming/playback with the
local `videos/` folder temporarily hidden to force the fallback.

**`videos/` and `samples/` both kept local-only, not tracked in git.**
`videos/` was never tracked (added straight to `.gitignore` — large
binaries, now redundant with the app anyway since it streams from
GitHub Releases). `samples/` (real DXF project files, also large
binaries) was explicitly **untracked by user request** this session —
`samples/floor1.dxf` removed from the git index via `git rm --cached`
(kept on disk) and `samples/` added to `.gitignore`. Note this reverses
the earlier 2026-08-04 decision to track `samples/floor1.dxf` for
Phase 1 validation reproducibility — that validation already happened
and is recorded in this file, so the file no longer needs to live in
git for that purpose.

---

## 2026-08-07 — Apartment count drives the "electric consumers"/lamp Excel
block; per-floor input, but written as one building-wide total

**Decision:** added an "Apartments" input per floor row on the Work
page (next to height and up-direction), and used it to fill in cells
that the user previously had to compute and enter by hand:
`D317`=stoves, `D321`=fridges, `D331`=TVs (2x), `D332`=laundries,
`D333`=PCs (2x), `D336`=others (5x), `D348`=occupant count — all equal
to or a multiple of the apartment count — and `D291`=lamps, computed
as `ceil(7 * totalFloorArea / 20)` (initially plain rounding, changed
to always-round-up per explicit user follow-up the same session).

**Reason these are building totals, not per-floor:** unlike the
per-floor blocks (geometric characteristics row 7+i, wall description
row 31+i, openings table), this "Описание на електроуредите..."
section only exists once in the template — it is a whole-building
electrical load calculation, not a per-floor one. So although the
apartment-count *input* is collected per floor (matching how
height/direction already work in the UI), `FloorProcessor.ProcessAndWriteFloors`
sums `ApartmentCount` and `AreaM2` across every floor in the project
and writes the appliance block exactly once, after the per-floor loop
— see `WriteApplianceBlock` in `src/HVACrate2.Core/FloorProcessor.cs`.

**Overwrote pre-existing template formulas.** The target cells already
had formulas chaining off each other in the blank template (e.g.
`D321='=D317'`, `D332='=D321'`, `D333='=D331'`, `D336='=D333*3'`,
`D291='=4*(D317*3+D331)'`) — apparently the engineer's own manual
shortcut formulas. `D336`'s old `*3` multiplier against `D333` (itself
`=D331`, i.e. 2×apartments) would have given 6×apartments, not the
5×apartments the user asked for — so all six cells are now written as
plain literal values by the app, replacing whatever formula was there,
per explicit instruction to fill these specific cells with these exact
multiples.

**Validated:** built a throwaway console harness (referencing
`HVACrate2.Core.dll` directly) against `samples/floor2.dxf` with two
synthetic floors (3 + 2 apartments = 5 total). Result: D317=D321=D332=5,
D331=D333=10, D336=25, D348=5, D291=142 (ceil of 141.288, computed from
the two floors' combined area) — all matching spec.

---

## 2026-08-07 — Loading spinner on "Extract & Fill Excel"; extraction
moved off the UI thread

**Decision:** added `Controls/LoadingSpinner` (a UserControl with a
partial-ring `Path` + `RotateTransform`, animated via the same
`EventTrigger`-on-`Loaded` + `Storyboard` pattern as the existing
`CompassControl` needle) and show it next to the Extract button while
`FloorProcessor.ProcessAndWriteFloors` runs. `OnExtractClick` is now
`async void`, running the actual processing via `Task.Run` so the UI
thread stays responsive and the spinner animates; the Extract button
is disabled for the duration.

**Reason:** extraction (DXF parse + Excel write) was previously
synchronous on the UI thread — for larger DXFs or multi-floor projects
this could freeze the window with no feedback. A custom control was
used instead of a stock WPF `ProgressBar` (whose indeterminate style is
a sliding bar, not a circular spinner) to match the app's existing
hand-drawn-vector-control aesthetic (see the compass control decision,
2026-08-05).

**Also same session:** the "Download filled Excel" dialog's default
file name was changed from `Топлотехника.xlsx` to
`Топлотехника V6.0.16.xlsx`, matching the real template's name.
The Projects page's "New project name..." textbox placeholder is now
horizontally centered (`TextAlignment="Center"` on the placeholder
`TextBlock` in the shared `TextBox` control template in `App.xaml`) —
previously left-aligned like normal typed text. This shared style
otherwise only affects textboxes that set a `Tag` placeholder (only
this one currently does), so no other input's behavior changed.

**Testing note:** verified via `dotnet build` (catches XAML markup
errors, as it did for the earlier `Run.Text` binding bug) and by
launching the real built `.exe` and driving it with `System.Windows.Automation`
to reach the Work page and confirm no crash/layout issue. Did not
force the native `OpenFileDialog` via automation to get a live
screenshot of the spinner mid-animation, since scripting native
dialogs on the real desktop (not a sandbox) is more failure-prone and
was judged not worth the added risk for this change — confidence
instead comes from reusing the already-proven `CompassControl`
animation mechanism.

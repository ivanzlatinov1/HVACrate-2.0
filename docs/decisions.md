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

---

## 2026-08-09 — OVK boundary picked by largest area, not first match

**Decision:** `OvkVertices` now picks, among every `LWPOLYLINE` on the
OVK layer, the one enclosing the largest area — not the first one
found in the file (`FirstOrDefault`).

**Reason:** the user supplied a new real sample, `samples/example.dxf`
(Archicad export), which produced wildly wrong results (tiny area,
only 2 of 8 wall directions populated, zero windows/doors). Traced the
cause: this file reuses the `OVK` layer for far more than the building
boundary — 55 separate polylines in model space alone, one per
apartment/room, plus assorted annotation frames — matching
decisions.md's 2026-08-04 concern about the layer being used loosely,
now confirmed in a real file. `FirstOrDefault` grabbed an arbitrary
small room polygon instead of the real 20-vertex envelope. The real
envelope's area (267,038,861 raw units²) was 47x larger than the next
biggest candidate (a room, 5,625,000) — not a close call, so
"largest area wins" is safe and unambiguous. This is a no-op for
floor1-4, which only ever had one OVK-layer polyline each.

**Also found and fixed the vertex-closing assumption:** `OvkVertices`
assumed the polyline's vertex list already ends with a duplicate of
the first vertex (true for floor1-4). `example.dxf`'s real envelope
closes via the LWPOLYLINE's own "closed" flag instead, with no
duplicate vertex. `OvkVertices` now normalizes both conventions to the
same duplicate-closing shape before returning, so every other function
in the file (which all assume that shape) doesn't need to care which
convention a given file used.

---

## 2026-08-09 — Cross-floor opening rows merged instead of duplicated

**Decision:** `ProcessAndWriteFloors` now accumulates every floor's
opening counts into one building-wide dictionary and writes the
openings table once, after the per-floor loop, instead of each floor
appending its own fresh rows to the table.

**Reason:** explicit user request — the openings table (`WriteToExcel`,
row 57+) previously grew by whatever each floor's `WriteFloorToExcel`
call found "next free," so the same window/door size recurring across
floors (very common — most floors in a building share window types)
produced multiple rows with the same width×height instead of one
summed row. `MergeOpeningGroups` sums per-direction counts for any
`(width, height)` key already seen on an earlier floor; `WriteOpeningsTable`
is the only thing that writes to the opening rows now.

---

## 2026-08-09 — Opening exterior/interior classification: distance-based
approach abandoned; wall-topology tracing adopted

**Context:** `example.dxf` (see above) also produced zero windows/doors
even after the OVK-selection fix, because `ExtractOpeningsTouchingOvk`
required a marker to be within a fixed tolerance (0.5m) of the OVK
boundary to count as exterior — and this file's markers sit 1.2-2.5m
out (an Archicad annotation-leader convention: the marker is a label
with a leader line, not glued to the wall).

**What was tried and rejected, in order, each with real-data evidence:**

1. **Raise the tolerance** (tried 3.0m). Fixed `example.dxf`, but
   `floor1.dxf`'s doors sit at a continuous spread of distances
   (1.15m to 4.44m) with *no gap anywhere* between "close" and "far" —
   any tolerance that admits `example.dxf`'s exterior openings also
   admits several of floor1's genuinely interior doors. There is no
   single distance threshold that works for both files' drafting
   conventions.
2. **Network-wide hop-bounded BFS** from the opening's host wall to any
   OVK-coincident node. Rejected: in a small, tightly-packed floor
   plan, interior partitions physically touch the exterior wall at a
   T-junction within a couple of hops, so this credits an interior
   partition with the exterior wall it merely joins, not one it's part
   of.
3. **Collinear-run tracing** (adopted, this entry): from the opening's
   host wall, follow only near-straight continuations (max ~35° turn
   per step) toward OVK. A real corner (~90°) stops the trace, so an
   interior partition meeting its host exterior wall at a corner is
   correctly excluded, while the small real-world kinks a wall run has
   at drafting/party-wall seams (~15-20° in validated data) are still
   tolerated.

**Host wall determination** (`WallTopology.FindHostWallNodes`) differs
by DXF convention, both confirmed by direct investigation (not
assumed):

- **Nested markers** (floor1-4's `Wall_N_2` convention, per the
  2026-08-04 entry): the host wall is a structural fact — whichever
  block directly contains the marker. No search needed.
- **Top-level/annotation-only markers** (Archicad): no structural
  host-wall link exists anywhere in the DXF for this convention —
  checked exhaustively (XDATA, extension dictionaries, reactor
  pointers, owner handles, HATCH boundary-loop counts) and found
  nothing. Instead, the *real* window/door body object (a separate
  INSERT Archicad places next to the annotation marker, e.g.
  `Variable Window 27_5`, distinct from the `W Marker`/`D Marker`
  label) is found by proximity and its own geometry — transformed
  through its own INSERT (translation, rotation, mirror scale, *and*
  the block's own base point, which floor1-4's `Wall_N_2` blocks need
  and Archicad's companion objects don't, since their base point is
  `(0,0,0)`) — is matched to the wall network at drafting precision
  (≤0.15m). Verified concretely: this transformed geometry lands
  exactly (0.001-0.01m) on real wall-graph vertices.

**A wall graph** (`WallTopology.BuildWallGraph`) is built once per
floor from every wall-layer `LINE`/`Polyline2D` in the document,
covering both conventions with the same code: entities already at top
level (Archicad's flat `STR- Exterior/Interior walls` layers) are
added as-is; entities nested one level inside a per-wall block
(floor1-4's convention) are transformed through that block's own
INSERT first. A top-level wall segment is just the identity-transform
case of the same logic, not a separate path.

**Validated:** `example.dxf` now produces a full openings table across
all 8 directions (previously zero); floor1-4's previously-correct
values are unchanged (re-verified against the merged 4-floor reference
table supplied by the user — see the two entries below for the
remaining discrepancies and how each was investigated).

---

## 2026-08-09 — Coordinate scale auto-detected (centimeters vs
millimeters); `$INSUNITS` confirmed unreliable

**Decision:** `FloorProcessor.DetectCoordinateDivisor` tries
centimeters first (divide raw units by 100 — the convention every
sample used until now) and falls back to millimeters (divide by 1000)
only if that produces an implausible floor area (>20,000 m²).

**Reason:** user reported `example.dxf`'s computed area (26,703.89 m²)
and total wall perimeter (888.41m) were both unrealistic, and gave the
correct values (267.03 m², 88.84m) — exactly a 100x and 10x factor
respectively, which is exactly what a cm-vs-mm mixup produces (area
scales as the square of a linear-unit error). Checked `$INSUNITS` in
both `example.dxf` and `floor1.dxf` directly: **both declare the
identical value** (6 = meters), despite needing different divisors —
confirming this header cannot be trusted to distinguish the two real
conventions found so far. `DetectCoordinateDivisor` computes the OVK
area at the default centimeter divisor and only switches conventions
if that area is nonsensical for a single floor plate; the two
conventions are never close enough in the resulting area to produce a
false switch (100x apart, not a few percent).

**Also fixed:** `WallTopology`'s `CompanionSearchRadiusCm` constant
(6.0, i.e. 600cm) was being compared directly against raw,
un-converted DXF distances — silently assuming the centimeter
convention regardless of which one was actually detected. Renamed to
`CompanionSearchRadiusM` (6.0) and scaled by the detected divisor at
the comparison site.

---

## 2026-08-09 — Opening direction: assigned from the OVK edge the host
wall reaches, not from the marker's raw position

**Context:** direction was computed by a fresh nearest-OVK-edge search
from the marker's own position. This is unreliable near a building
corner, where a marker can sit close to two differently-facing edges —
confirmed on a real case (`floor1.dxf`, `Wall_44_2`, a 1.58×2.43m
door): the user's own annotated drawing showed the door's swing
opening toward the south (Ю) facade, but the app reported west (З).

**Two follow-up approaches tried and rejected, each with concrete
evidence, before settling on the final one:**

1. **Direction from the OVK point the topology trace reached**, via a
   fresh nearest-edge search from *that* point instead of the marker.
   Regressed three previously-correct rows: the reached point can
   itself be a polygon vertex shared by two OVK edges facing different
   directions (the exact ambiguity being fixed, just relocated).
2. **Direction from the traced wall run's own edge**, matched to
   whichever OVK edge runs parallel to it and closest. Regressed even
   more rows once implemented: `Wall_44_2` (the target case) has *no*
   long face line of its own — its whole geometry is short
   perpendicular jamb ticks (0.06-0.1m stubs) — and a jamb tick,
   despite being perpendicular to the true wall, can sit within a
   generous perpendicular tolerance of a *different, unrelated* OVK
   edge purely by coincidence of the building's own proportions
   (confirmed: a south-wall tick sat within 0.26m of the west edge's
   X-coordinate). Tightening the tolerance to reject that coincidence
   (0.1m) then broke *other* real matches that legitimately need more
   slack. No single tolerance value works.

**Adopted:** `WallTopology.MarkOvkNodes` already computes, per wall-
graph node, which OVK edge it's nearest to, at tight (0.05m)
drafting-precision tolerance — the same authoritative fact already
used to decide a node is "on OVK" at all. `FindExteriorOvkPaths` now
returns every full path (not just one edge) from the opening's host
wall to an OVK node, and `AssignOvkEdgeIndex` looks up each path's
*already-known* edge index instead of re-deriving direction from
(noisy) path geometry. Ties among candidate edges are broken by
**plurality vote — the edge reached by the most paths wins**, not the
edge with the most summed path length: tried summed length first and
found a real case where one 10.1m path wandering through an unrelated
wall outweighed 24 short (0-2.7m) paths correctly reaching the
opening's true wall. Counting how many independent trace attempts
agree is robust to that one outlier; summing length is not. Remaining
ties break on lower total path length, then lower edge index, for full
determinism.

**Validated against the user-supplied reference table** (all 4 floors
merged): all 16 opening-size/direction rows now match, including the
previously-wrong corner case, with zero regressions to the 15 rows
that were already correct.

---

## 2026-08-09 — Two flagged discrepancies investigated to conclusion;
DXF data confirmed consistent in both cases

**Context:** cross-checking the merged 4-floor output against the
user's reference table surfaced two apparent problems. Both were
investigated exhaustively per explicit instruction not to conclude
"probably sample drift" without concrete evidence.

**1. A 0.4m×2.46m window in `floor2.dxf`/`floor3.dxf` the user said
"does not exist."** Checked every angle available in the DXF:

- The real window body (`Window 22_2`, a separate INSERT from the
  `W Marker` label, same pattern as the Archicad convention above),
  transformed to world coordinates, lands at X[753.83,754.29] /
  Y[-65.60,-65.20] — matching its host wall's own jamb-gap opening
  (X[753.85,754.27]) almost exactly (frame overhangs the rough opening
  by ~2cm each side, as real trim does).
- Exactly one marker + one body object exist at this location — no
  duplicates, no second reference to either block anywhere in the file.
- No invisibility flag (DXF group code 60) on either entity; the
  `_A [walls]` layer itself is on and unfrozen (flags=0, color=+7).

Every check supports a real, deliberately-drawn opening. If it's
still wrong, that's a property of the source drawing, not something
detectable from the DXF's own data — flagged for the user to check the
original CAD file at that exact location.

**2. `floor3.dxf`'s w=90cm doors report height 210cm; reference implies
208cm.** Checked whether any other DXF representation could disagree:

- The raw `AC_MarkerText_3` ATTRIB text was pulled directly from two
  separate door-marker instances in `Wall_213_2` — both say `210`,
  verbatim, byte for byte.
- Every w=90 door marker across the whole file reports 210; the only
  w=90/h=208 entries anywhere are *windows*, a different marker type.
- The paired real door body (`Door 22_3`, same convention as the
  window above) was checked for a conflicting height. It can't have
  one: 2D plan-view door geometry (swing arc + frame lines) never
  encodes height at all — height is the vertical/Z dimension, invisible
  in a top-down projection. The swing arc radius (82cm) and outer
  frame width (92cm) are both consistent with the declared 90cm width,
  which at least confirms this marker's own attributes are trustworthy.

**Conclusion:** every representation of this door's height in the DXF
says 210cm; there is no second representation to disagree with it.
Per the evidence-based rule for this investigation: **the DXF is
internally consistent at 210 — if the correct real-world value is
208, that's a discrepancy in the source drawing (or in what the
reference table describes), not in extraction.** Separately, and more
likely the actual issue: the reference table has no row at all for a
90×210 *door*, only 90×208 (fully explained by the windows alone) —
suggesting some of `floor3.dxf`'s 90×210 doors may not belong in the
exterior table to begin with. That is the still-open classification
question in plan.md, not a height-reading bug.

---

## 2026-08-09 — Sample file drift discovered: `floor1.dxf`/`floor2.dxf`
on disk no longer match their last-committed, validated versions

**Found while debugging** an apparent regression (a fix that worked
against the historically-documented `floor1.dxf` reference numbers
appeared to break when tested against the *current* `samples/floor1.dxf`
on disk). Compared the current file against the last commit that
tracked it before `samples/` was gitignored (2026-08-05):

- `samples/floor1.dxf` (commit `57162c2`): OVK boundary had 9 vertices
  (8 distinct + closing duplicate), area 110.90 m² — exactly matching
  every historically-validated number in this file. **Current file:**
  OVK boundary has only 6 vertices (5 distinct), area 112.78 m² — the
  small notch/step feature documented in the 2026-08-04 corner-count
  entry is gone.
- `samples/floor2.dxf` (commit `fe90845`): OVK boundary had 10
  vertices, area 181.82 m² — matching its own historical numbers.
  **Current file:** 17 vertices, area 202.38 m².

Confirmed by re-running the fixed pipeline against the *committed*
byte-for-byte versions: it reproduces every historical number exactly
(floor1: С=9.70, И=12.50, Ю=9.70, З=12.50, Af=110.90, n=6, window
1.5×1.7→С=1). The drift is in the sample files themselves, not in the
code. Not investigated further (no way to know why the files changed,
since `samples/` has never been tracked for these edits) — flagged in
plan.md as open, since it means historical reference numbers in this
file no longer reproduce from the current on-disk samples as-is.

---

## 2026-08-09 — Session 10: root cause found for the w=90cm door
misclassification — CAD exporter splits one exterior wall into many
`Wall_N_2` blocks; three approaches tried, third one shipped

**Context:** picking up the previous session's open item — floor2/floor3's
w=90cm doors classified inconsistently, matching no reference table.
User supplied a full reference table (4 floors merged, 17 rows) for
rigorous validation this session, which made root-causing possible.

**Approach 1 — exclude openings on a "notch" OVK edge (both endpoints
reflex), rejected.** Traced the bad doors: both hit an OVK edge bounded
by two reflex vertices (a re-entrant light-well corner). Implemented
the exclusion, re-validated against the full reference table, and found
it also excluded three genuinely correct windows (1.50×1.70, 1.60×2.50,
1.60×2.48) that happen to reach the *same* edges as the bad doors —
edge geometry alone cannot separate a genuine facade window from a
connector door on the same wall. Reverted.

**Investigation — structural comparison of a genuine door
(`Wall_44_2`, 1.58×2.43, floor1) vs a bad connector door (`Wall_213_2`,
floor3).** Neither block's own geometry touches OVK directly. The
difference is what's one hop away: `Wall_44_2` neighbors `Wall_34_2`,
a short pier 0.037m from the boundary, itself an interior link of a
continuous wall; `Wall_213_2` neighbors `Wall_212_2`, a *different*
wall system reached by crossing 3.65m of open interior space. Chain-
following (walk through same-node, same-direction neighbors, stop at a
real ~90° turn) confirmed: `Wall_44_2`'s chain is 5 blocks, 14.60m,
terminating at two genuine corners, including a block
(`Wall_26_2`) with 10.55m of its own geometry lying directly on OVK.
`Wall_213_2`'s chain is a single isolated block — every neighbor is
orthogonal, no collinear continuation exists at all.

**Approach 2 — reconstruct the wall run (`WallTopology.BuildWallRun`)
and gate + path-find on the whole run's node union, over-corrected.**
Implemented: walk outward through collinear same-orientation neighbor
blocks (reusing the existing `MaxCollinearDeviationDeg` constant),
gate on whether any block in the run has a segment lying on the
*finite* OVK edge (not just its infinite line — an early version of
this check had a bug there, caught before shipping). This correctly
recovered the previously-missing 1.58×2.43 door. But validating against
the full reference table surfaced 8 new mismatches: 5 false-positive
1.10×2.10 doors and 3 wrong direction assignments (0.40×2.46, 1.60×2.05).
Root-caused via full decision-path traces using the real pipeline
internals: `FindExteriorOvkPaths`/`AssignOvkEdgeIndex`, once fed the
*entire* run's node union (sometimes 7 blocks / 81 nodes), treat every
node as an equally valid trace origin — including nodes from blocks the
opening never touches, several hops/rooms away. A false-positive door's
"genuine OVK touch" belonged to an unrelated block in the same
(correctly-reconstructed) run; a wrong-direction window's vote was
decided by which edge the run's *shape* happened to have more nodes
near, not which edge was nearest that specific window.

**Approach 3 — shipped. Keep `BuildWallRun` for the gate only; scope
path-finding to the opening's own host block.** Design-reviewed against
4 candidate locality definitions (host block only; host block +
hop-limited continuation; nearest run nodes by graph/physical distance;
projection onto the run) before implementing. Hop-limited and distance-
based approaches were rejected on structural grounds, not just
untested: the genuine door's real match and two of the false positives'
bad matches are at the *identical* hop distance (1), so no fixed radius
can separate them. Tested "host block only" empirically first (read-only
diagnostic, no code change) against all 8 mismatches plus 5 other
currently-correct multi-block cases: fixed all 8, zero regressions —
because `FindExteriorOvkPaths`'s own collinear trace already walks the
full graph regardless of block ownership, so the run's extra nodes were
never needed for reachability, only for (over-)voting. Implemented:
`ExtractOpeningsTouchingOvk` still calls `BuildWallRun` and gates on
`OwnsGenuineOvkSegment` unchanged, but passes `FindHostWallNodes` (the
opening's own block) — not `run.Nodes` — into `FindExteriorOvkPaths`.
`FindExteriorOvkPaths`, `AssignOvkEdgeIndex`, and the Archicad/top-level
branch are byte-unchanged.

**Validated:** all 17 rows of the reference table match exactly —
53/53 openings, 0 false positives, 0 false negatives, 0 direction
errors, 0 duplicates. Wall lengths/area/corner counts per floor
unchanged from pre-session baseline (this fix only touches opening
classification). `example.dxf` (Archicad convention) output confirmed
byte-identical before and after.

**Process note:** most of this session was throwaway diagnostic
tooling (`DebugTrace.cs`, extra `Program.cs` command branches) built to
get real numbers at every step rather than reason from assumption —
removed after the fix was validated; `git diff` on the shipped commit
touches only `FloorProcessor.cs`/`WallTopology.cs`, no dead code left
from the two rejected approaches.

**Shipped:** branch `fix/exterior-opening-classification-host-wall-topology`,
merged to `main`.

---

## 2026-08-09 — Three remaining open items closed by explicit user decision

**Sample-file drift** (`floor1.dxf`/`floor2.dxf` no longer matching their
originally-validated versions): user confirmed a 2-3 m² variance doesn't
matter and may originate from their own manual reference calculations,
not from extraction. Not being investigated further.

**Coordinate-unit auto-detection** (cm vs mm): **final decision — stays
as the plausibility-threshold heuristic it is.** User confirmed the
coordinate unit is a choice made by the user when exporting the `.dxf`
from AutoCAD — if a third, untested convention produces a wrong result,
that's a user-side export issue, not something the app needs to guard
against further.

**Wall-layer detection** (`WallTopology.IsWallLayer`, English "wall"
substring match): **final decision — stays as-is.** User confirmed every
real project's wall-layer naming convention is English; no need to
support non-English layer names.

---

## 2026-08-09 — Session 11: 2D preview + results table before the Excel
write; `floor1.dxf` sample drift root-caused and fixed (bad `OVK` layer
in the file, not a code bug)

**2D preview / results table shipped.** Closed the last two open Phase 3
items (`plan.md`) as one combined review step, per explicit user
request: `FloorProcessor.ProcessAndWriteFloors` split into
`ProcessFloors` (compute per-floor `FloorResult`s, no Excel) and
`WriteFloorsToExcel` (write already-computed results — avoids
re-parsing every DXF a second time). `FloorResult` now also carries
`OvkVerticesM` and per-opening `Openings` (with world position), both
previously computed and discarded inside `ProcessFloor`. New
`Controls/FloorPreviewControl` (a `Canvas`-based `UserControl`,
following the existing `CompassControl`/`LoadingSpinner` pattern) draws
the OVK boundary, opening markers (tooltipped with size/direction), and
a north arrow — the arrow's direction is derived from the exact same
bearing formula `FloorProcessor.BearingToDirection` uses
(`mathAngle = 90 - northDeg`), not a separately-guessed rotation, so it
can never visually disagree with the direction letters in the table
next to it. New `Pages/PreviewPage` shows this plus a results table
(area/volume/perimeter/corners/wall-lengths/openings) per floor, with
"Confirm & Write Excel" (calls the new `WriteFloorsToExcel`) and
"Download" (moved from the Work page). `WorkPage`'s "Extract & Fill
Excel" became "Extract & Preview", navigating to `PreviewPage` instead
of writing directly. `PreviewPage`'s "Back" uses `NavigationService.GoBack()`
rather than constructing a fresh `WorkPage` — a deliberate deviation
from every other page's "always construct fresh" back-navigation
convention, specifically because `WorkPage`'s `_floors` are real,
easily-lost per-instance state (DXF paths, heights, apartment counts)
and users are now much more likely to need to go back mid-flow (e.g.
to fix a value after seeing the preview) than before.

**Verified live, not just built** — per project convention for UI
changes: launched the real `.exe`, drove it with `System.Windows.Automation`
(including scripting the native `OpenFileDialog` via its `HWND`, found
through `EnumWindows` since `AutomationElement.FromHandle` needed the
real handle rather than a name match) through the full path: create
project → choose `floor1.dxf` → fill height/apartments → Extract &
Preview → confirmed the preview showed the exact known-correct values
→ Confirm & Write Excel → Download appeared → Back preserved the
already-entered floor data. No crashes.

**`samples/floor1.dxf` drift (open since 2026-08-09) root-caused and
fixed.** User identified the actual cause: the previously-uploaded
`floor1.dxf` had an incorrectly-drawn `OVK` layer — not a code issue.
User re-exported a corrected file. Re-running the full pipeline against
it reproduces the **original 2026-08-04 validated reference exactly**:
Af=110.90m² (was 112.78m²), С=9.70/И=12.50/Ю=9.70/З=12.50 (was
С=9.70/И=11.61/Ю=9.70/З=11.65), n=6 (was 4). Opening extraction was
unaffected by the drift either way (same 6 rows, same counts, both
before and after). This confirms the extraction/classification code
was never the source of the discrepancy — closes the item for real,
superseding the earlier "accepted, 2-3 m² doesn't matter" framing.
`samples/floor2.dxf`'s drift (flagged in the same original 2026-08-09
entry) — user confirmed the current on-disk file is the correct one
(no export bug there); nothing to fix. Both files' drift items are now
closed.

**Full re-validation after both changes:** floor1-4 merged openings
table still matches all 17 reference rows exactly (53/53 openings, 0
mismatches — floor1's opening counts were unaffected by its OVK-layer
fix, only its area/wall-length/corner numbers changed). `example.dxf`
and floor2-4 unchanged. Full solution build (`dotnet build` at the
solution root, all 3 projects including the `HVACrate2.Core.Tests`
scaffold) clean, one pre-existing unrelated placeholder-test warning
(`TUnitAssertions0005` on the scaffold `Tests.cs`, predates this
session, not touched).

---

## 2026-08-10 — Both flagged discrepancies from 2026-08-09 closed by
user verification against the real CAD file

**0.4m×2.46m window (`floor2.dxf`/`floor3.dxf`):** user checked the
original CAD file at the flagged location and confirmed the window is
real — the earlier "does not exist" report was the user's own mistake,
not a drawing or extraction problem. Matches the investigation's
conclusion (real window body geometry, no duplicates, no hidden flags).
Closed, no code change.

**90×210cm door height (`floor3.dxf`, reference implied 208cm):** user
confirmed this was a bug in the source drawing/reference, not in
extraction — the DXF's own marker data (`AC_MarkerText_3` = 210,
verbatim on every w=90 door marker in the file) stands as correct.
Closed, no code change.

---

## 2026-08-10 — Floor Heating: a new, independent calculation track;
project-gated Start page; architecture decisions

**Context:** the user requested an entirely new feature not in
CLAUDE.md's original scope — per-room floor heating heat-flow
calculation, alongside the existing Energy Efficiency (DXF→Excel) flow.
Given the scope (new domain model, new page, a restructured Start menu,
a project-selection concept that didn't exist before), this went through
`EnterPlanMode` first, including an `AskUserQuestion` round to lock three
open design forks before writing code.

**Project selection/gating — "select & return to Start," not a
project hub page.** Considered two shapes: (a) opening a project in
Project Management sets it "current" and returns to the Start page,
whose Energy Efficiency/Floor Heating buttons are then enabled and
route into that project; (b) opening a project goes to a per-project
hub page with its own buttons. User picked (a). Implemented as
`ProjectStore.CurrentProject` (nullable static, cleared by
`DeleteProject` if the deleted project was current); `ProjectsPage`'s
Create/Open now set it and navigate back to `StartPage` instead of
jumping straight into `WorkPage` as before. `StartPage` reads
`CurrentProject` in its constructor to enable/disable the two gated
buttons and show a "Current project: X" / "No project selected"
subtitle. `NavButtonStyle`/`SecondaryButtonStyle` in `App.xaml` had no
`IsEnabled` visual state at all before this — added a dimmed-opacity
trigger to both, otherwise disabled buttons would have looked identical
to enabled ones.

**Floor Heating's floor/room list is independent of the DXF-driven
floors on the Energy Efficiency side.** No DXF is involved in floor
heating at all — the user adds floors and rooms manually. Considered
reusing the Energy Efficiency floor count instead; rejected by the user
since a project may not have DXF floors set up yet, and the two tracks
don't need to agree on floor count.

**Deltas and Qпт are entered per room, not per floor.** Matches how the
formulas are actually consumed (every room has its own construction and
its own required heat) and the user's own phrasing ("the required heat
of a room").

**Formulas, from user-supplied reference-sheet screenshots** (not yet
committed anywhere as source files — only transcribed into
`FloorHeatingCalculator.cs`):

```
Rог = 1/αпод + δбет/λбет + δзам_под/λзам_под + δтер/λтер     [m²K/W]
Rод = 1/αтав + δбет/λбет + δизол/λизол + δплоч/λплоч + δзам_тав/λзам_тав  [m²K/W]
Ro  = Rог + Rод
r_пд = Rод / Ro
Qc  = Qпт / r_пд                                              [W]
m   = 3600 · Qc / 41870                                       [kg/h]
Qдол = Qc − Qпт                                                [W]
```

All eight physical constants (`αпод`, `λбет`, `λзам_под`, `λтер`,
`αтав`, `λизол`, `λплоч`, `λзам_тав`) are hardcoded per explicit user
instruction — only the deltas (meters) and Qпт (watts) are user input.
Note `δбет` (half the pipe-screed thickness) is the *same* physical
layer in both Rог and Rод — one input, reused in both formulas, not two
separate fields that happen to share a name.

**Verified against the user's own reference numbers, not just unit
logic:** the worked example in the screenshots (Ro1 = 0.1408 + 1.3473 =
1.4881 m²K/W) reproduces exactly from the example delta values shown.
A later real reference-table row (Qпт=1860, r_пд=0.9054 → Qc=2054.4,
m=176.64, Qдол≈194.4) also reproduces exactly once m/Qдол were added —
confirms the `41870` constant and the Qдол formula, not just Ro.

**Results moved to a separate page (`HeatingResultsPage`), split from
the data-entry page (`FloorHeatingPage`), per explicit user request**
mid-session — the first implementation showed the results table inline
on the same page as the input rows; the user wanted the split instead
(mirrors the existing Energy Efficiency `WorkPage`/`PreviewPage` split).
`FloorHeatingPage`'s "Calculate & View Results" now validates every
room, computes results, and navigates to `HeatingResultsPage`, which
takes just the `ProjectRecord` and renders one `DataGrid` per floor.
Back on the results page uses `NavigationService.GoBack()`, matching
`PreviewPage`'s existing convention.

**Floor/room data persistence — reversed an earlier assumption in the
same session.** The first implementation deliberately kept the
floor/room list local to the `FloorHeatingPage` instance (matching how
`WorkPage`'s own DXF-floor list already worked, per the original design
note). The user explicitly rejected this once they saw it: re-entering
Floor Heating for a project they'd already filled in showed blank rows.
Fixed by moving the collection onto `ProjectRecord.HeatingFloors`
(`ObservableCollection<HeatingFloorViewModel>`) — since `ProjectRecord`
instances are shared/reused via `ProjectStore`, the same object is
handed to `FloorHeatingPage` every time the user re-enters, so its data
now survives navigating away and back (still lost on app restart, same
as every other in-memory project field — not a new gap, consistent with
the existing "in-memory only" project-store decision from 2026-08-05).

**Explicitly deferred, not a code bug or oversight:** the feature is
incomplete because the user doesn't yet have enough information to spec
the rest of it — a second table "for the serpentines for each room" was
requested but never given formulas/columns, and whether floor heating
needs any Excel output (vs. staying in-app-only, unlike Energy
Efficiency) is undecided. Picked up again in a future session once the
user has the missing reference material.

---

## 2026-08-10 — Language toggle (English/Bulgarian): reused
`ThemeManager`'s pattern exactly; formula notation stays fixed by
design, not an oversight

**Context:** branched `feature/language-toggle` off `feature/floor-heating`
(not `main`) for a persistent EN/BG toggle next to the existing theme
toggle. Sized the scope first (grepped every hardcoded `Text=`/`Content=`
in `HVACrate2.App` — ~80 XAML occurrences across 9 files, ~30 code-behind
occurrences across 8 files) before entering plan mode, then ran an
`AskUserQuestion` round to settle two real scope forks before writing
code.

**Formula/domain notation stays fixed in both languages — explicit user
decision, not an oversight.** Rог, Rод, r_пд, Qc, Qпт, Qдол, δ-symbols,
room labels ("пом.01"), and the wall/opening compass-direction letters
`HVACrate2.Core` already produces (С/И/Ю/З/etc.) are calculation
notation, not UI prose — and the underlying Excel template is always
Bulgarian regardless of app language, so translating the app's own
formula labels away from what the template/reference sheets use would
make cross-checking harder, not easier. The compass *dial* widget's
fixed N/E/S/W-style labels (`CompassControl.xaml`) were treated the same
way by the same reasoning, though this specific control wasn't asked
about directly. Only the Work page's direction *dropdown*
(`CompassDirectionOption`) translates, since that's a UI input control
a user reads and picks from, not a calculation result.

**Instructions page's full step-by-step prose is in scope, not
deferred** — the one place the user explicitly asked for more than
short UI chrome. All 14 numbered steps and both part headings across
both languages, plus the "(mandatory)" emphasis runs, translated.

**Mechanism — reused `Shared/ThemeManager.cs`'s pattern byte-for-byte,
not a new architecture.** `ThemeManager` already solved "a setting that
must apply live, everywhere, without restarting the app": swap a merged
`ResourceDictionary`, everything themed uses `DynamicResource` so it
rebinds live. `LocalizationManager` does the same with
`Strings.En.xaml`/`Strings.Bg.xaml` (`sys:String` entries, same
convention `Theme.Light.xaml` already used for `TitleShadowDepth`)
instead of brushes. `Shared/Loc.cs` is a two-line static helper
(`Application.Current.FindResource(key)` + a `string.Format` overload)
for the cases `DynamicResource` can't reach: dynamically-built text
(`"Floors — " + project.Name`), `MessageBox` content, and
`CompassDirectionOption.ToString()`.

**Real technical snag found and solved: `StringFormat` can't be
`DynamicResource`-bound.** Every "Floor {0}" label
(`WorkPage`/`PreviewPage`/`FloorHeatingPage`/`HeatingResultsPage`) used
`Text="{Binding FloorNumber, StringFormat='Floor {0}'}"` — but
`StringFormat` is evaluated at XAML parse time, not a runtime dependency
property, so embedding a translated word inside it can't rebind live no
matter what. Fixed uniformly by adding a computed `FloorLabel` property
(`Loc.Get("Str_FloorLabel", FloorNumber)`) to every affected view model
(`FloorRowViewModel`, `PreviewFloorViewModel`, `HeatingFloorViewModel`)
and binding `Text="{Binding FloorLabel}"` instead — same pattern already
used for `HeatingRoomViewModel.RoomLabel` ("пом.01"). Where the source
view model already raises `PropertyChanged` on the underlying number
(`HeatingFloorViewModel`), the setter now also raises `FloorLabel`; where
it didn't (`FloorRowViewModel`'s `FloorNumber` was a bare
get/set with no `Raise()` at all — a pre-existing quirk, not something
this session introduced or was asked to fix), adding the computed
property doesn't regress anything since nothing live-updated it before
either.

**Scope boundary, stated explicitly rather than silently accepted:**
code-behind-computed text (page titles like `"Floors — {ProjectName}"`,
error/status messages) reflects whatever language is active *at the
moment it's set* — always correct for messages (generated fresh per
click) and for titles set in a page's constructor (every page in this
app is already reconstructed fresh on navigation, per the project's
existing convention), but a language toggle mid-page won't retroactively
rewrite already-computed title text without navigating away and back.
Matches `ThemeChanged`'s own actual behavior today — it's declared but
nothing subscribes to it, since `DynamicResource` already makes that
unnecessary for anything XAML-bound; `LocalizationManager.LanguageChanged`
was added for the same parity/future-use reason and is equally unused
right now.

**Repeated near-duplicate error messages unified into one parameterized
key.** `FloorHeatingPage`'s seven per-field validation messages ("enter
a valid δбет." / "δзам-под." / ... / "Qпт.") collapsed into one
`Str_Heating_Err_Field = "Floor {0}, Room {1}: enter a valid {2}."`,
with the fixed symbol name passed as the third argument — avoids seven
near-identical resource keys differing only in one embedded symbol.

**Verified:** `dotnet build` clean (0 warnings beyond the pre-existing
test-scaffold one) after every stage; re-grepped every `Text=`/`Content=`
in `HVACrate2.App` after finishing to confirm the only hardcoded strings
left are the intentional fixed ones (formula notation, "HVACrate 2.0",
dead `TitleText` XAML defaults immediately overwritten in every page's
constructor, compass dial letters, and lone punctuation-only `Run`s).
App launched and left running for the user to click through directly —
no UI-automation tooling used this session either, matching the Floor
Heating session's approach.

**Merged into `feature/floor-heating`**, not `main` — the branch point,
since `feature/floor-heating` itself is still unmerged.

---

## 2026-08-14 — Locale bug: `double.TryParse` without an explicit culture
broke both typed decimal input and DXF marker-attribute parsing on
non-`.`-decimal machines

**Decision:** every place the app parsed a user-typed or DXF-sourced
decimal number now normalizes `,`→`.` and parses with
`NumberStyles.Float` + `CultureInfo.InvariantCulture` explicitly, instead
of the default `double.TryParse(string, out double)` overload (which uses
`NumberFormatInfo.CurrentInfo`, i.e. the OS locale).

**Reason:** the user's machine is set to `bg-BG` (decimal separator `,`,
group separator a space). `double.TryParse("2.5")` under that culture
returns `false` outright — `.` isn't a recognized separator at all — so
the Work page's height field rejected any decimal input while a bare
integer like `"2"` parsed fine, which is exactly why it looked like "only
accepts integers." The same unguarded `TryParse` pattern existed in three
places: `FloorRowViewModel.TryGetHeightM`, `HeatingRoomViewModel.TryParse`
(the six delta fields + Qпт), and `FloorProcessor`'s DXF marker-attribute
width/height parsing. Confirmed directly: `[double]::TryParse('2.5', ...)`
under `bg-BG` returns `false`.

**Verified:** a throwaway console harness (`scratchpad/diag`, not
committed) run under the machine's real `bg-BG` culture confirmed all
three call sites now accept both `2.5` and `2,5` correctly.

---

## 2026-08-14 — Opening extraction rebuilt as a name-agnostic
geometry/topology pipeline; the old `W Marker`/`D Marker` name-gated
approach retired

**Context:** the user supplied three new real floors
(`samples/floor1-3.dxf`) for a first end-to-end test. Wall
length/area/volume/corner count (all `OVK`-polygon-based) were correct,
but **zero windows/doors were ever extracted** — confirmed directly (see
below) — because these files use a third, structurally different export
convention neither of the two previously-handled conventions covers: the
whole drawing is one exploded `INSERT` block (`Drawing_1_1`), there are
**no `W Marker`/`D Marker` `INSERT`+`ATTRIB` blocks at all**, dimension
labels are bare `MText` pairs on a `Line` leader, and the wall layers are
pure Bulgarian (`Стени - екстериор`/`Стени - интериор`), which also broke
`WallTopology.IsWallLayer`'s English-only `"wall"` substring check — a
"final decision" from 2026-08-09 that this real data has now overturned.

**User's explicit direction, not a patch-in-a-fourth-convention fix:**
stop depending on block/layer/marker **names** as the primary detection
mechanism; detect openings from geometry and their spatial relationship
to the building's exterior wall topology instead, with any recognizable
name used only as a confidence hint. Full requirements (semantic output
model, pluggable strategies, exterior/interior from topology not names,
confidence/evidence per opening, never-silently-zero diagnostics,
deduplication, dimension cross-validation, synthetic renamed-layer
regression tests) captured in the approved plan
(`C:\Users\ivanz\.claude\plans\harmonic-doodling-twilight.md`).

**Two things kept deliberately as-is, not generalized away:**
- **`OVK` stays the authoritative exterior-boundary source.** It is not
  an arbitrary third-party CAD convention — it's this app's own
  documented, user-authored step (the Instructions page tells every user
  to trace it by hand before export). Reconstructing the boundary from
  raw, unlabeled wall geometry instead was tried and rejected in the
  project's very first session (2026-08-03) as too fragile without a
  hand-drawn boundary; this session doesn't reopen that.
- **Area/volume/wall-length/corner-count code is untouched.** User
  confirmed these were already correct on the new samples — the bug was
  scoped entirely to opening extraction.

**Empirical grounding, measured directly on `floor1.dxf` before writing
any pipeline code:**
- Every `Стени - екстериор` (exterior wall) line lands within 0.25m of
  the `OVK` boundary (100% of 303 sampled endpoints ≤0.3m); every
  `Стени - интериор` (interior wall) line is ≥1.57m away — a clean,
  name-independent gap, the same kind of "clean split, not a threshold
  call" pattern this project has relied on before (see the 2026-08-09
  `HostWallOwnsOvkNode` entry). This is what lets exterior/interior
  classification work without ever reading the wall layer's name.
- Each door/window marker in this file is one short `Line` (a leader,
  ~0.25-0.9m long) with two `MText` labels (width, height) clustered near
  one endpoint. The endpoint **farther** from the labels consistently
  sits closer to the real door-body geometry than the label-side one
  (e.g. 37.75 vs 50.78 raw units in sampled cases) — confirming it's the
  wall-side tip, not the label anchor, and should be the candidate's
  anchor point.
- Confirmed **why** a fourth name-gated patch wasn't the right fix: even
  after normalizing decimal parsing, the old code found zero markers on
  these files because it only ever looked for `INSERT` blocks named
  `W Marker`/`D Marker` — a structural check, not a parsing bug.

**Architecture — pipeline replacing `FloorProcessor.ExtractOpeningsTouchingOvk`
and superseding `WallTopology.cs`'s hop-tracing classifier** (new folder
`src/HVACrate2.Core/Openings/`):

1. `DxfEntityIndex.Build` — recursively flattens every `Line`/`Arc`/
   `Polyline2D`/`Text`/`MText`/`Insert` to world-meter coordinates at
   **arbitrary** `INSERT` nesting depth (composing each level's transform
   via closures), not just the one-level special case the old code
   handled — the actual structural reason `Drawing_1_1`'s single
   top-level wrapper insert didn't block detection.
2. `WallGeometryClassifier.CollectWallLikePoints` — a segment counts as
   wall-like if its layer name hints at "wall" in one of a small
   multilingual list (`WordHints.Wall`: en/bg/de/fr/it/es — confidence
   only) **or** it lies within 0.4m of an `OVK` edge (the name-independent
   signal that actually does the work, per the measurement above).
3. Two independent `IOpeningCandidateStrategy` implementations, each
   producing candidates with an evidence trail:
   - `BlockAttributeStrategy` — generalizes both old `INSERT`+`ATTRIB`
     conventions into one name-independent rule: any `INSERT`, at any
     nesting depth, carrying ≥2 numeric `ATTRIB` values in a plausible
     range (10-500cm) is a candidate. A marker/window/door name hint only
     adds an evidence note. Looser exterior tolerance (2.5m) since a
     detached annotation's own position isn't guaranteed to sit at the
     wall — an explicitly acknowledged trade-off vs. the old full
     topology-hop-tracing for that specific historical edge case (whose
     original sample files no longer exist on disk to re-validate
     against — see Verification below).
   - `PerpendicularLabeledLineStrategy` — the primary signal, matching
     the user's literal correction mid-planning ("an opening is a
     perpendicular line to the OVK layer with two numbers next to the
     line," replacing an earlier "gap in the wall run" idea that doesn't
     hold for every file): a `Line` near-perpendicular (≥70°) to the
     nearest `OVK` edge, short (0.05-1.5m — a real leader tick, not a
     room-dimension chain), with exactly two numeric text labels
     clustered both near one endpoint (≤0.6m) **and near each other**
     (≤0.5m — this is what actually separates a marker's own paired
     numbers from two unrelated dimension-chain labels that both happen
     to sit within range of the same line; first cut without this check
     produced 100+ obviously-wrong sub-0.5m-wide "openings" per floor
     from generic dimension annotations elsewhere in the drawing). Anchor
     = the far (wall-side) endpoint; tight exterior tolerance (0.8m),
     since that anchor is already confirmed to sit at the real wall.
4. `ExteriorClassifier` — **direct** distance from the candidate's own
   anchor to the `OVK` boundary curve, tolerance per-candidate (set by
   the strategy that produced it). An earlier version snapped each
   candidate to the nearest wall-like point within an 8m search radius —
   found to be a real bug, not a refinement: in a compact floor plan,
   almost *any* point sits within 8m of *some* exterior wall, so that
   check accepted nearly everything regardless of true proximity. Direct
   distance against the boundary curve doesn't have this failure mode.
5. `TypeClassifier` — best-effort Door/Window scoring from a nearby swing
   arc or multiple nearby frame-like lines, plus word hints — corroborating
   evidence only, never gates detection; has no effect on the Excel write
   (windows/doors already share one combined table per `CLAUDE.md`).
6. `OpeningDeduper` — merges candidates from different strategies
   describing the same physical opening (position within 0.5m, same `OVK`
   edge, width/height within 15%/0.1m).
7. `OpeningExtractionDiagnostics` (new `Models/` type, on `FloorResult`) —
   entities inspected, candidates per strategy, accepted/rejected counts
   with reasons, and an explicit warning when accepted count is 0 despite
   wall-like geometry existing. Surfaced on `PreviewPage` as a small
   warning line per floor (`PreviewFloorViewModel.OpeningWarningVisibility`)
   — the mechanism that would have caught this exact bug immediately
   instead of a silently-empty table.

**`Opening` model extended, additively:** `Type`, `Confidence`,
`DimensionSource`, `Evidence` — existing consumers (`GroupOpenings`,
`WriteOpeningsTable`, `FloorPreviewControl`) only ever read
`WidthM`/`HeightM`/`Direction`/`PositionXM`/`PositionYM` and are
unaffected.

**`WallTopology.cs` deleted, not deprecated-in-place.** Confirmed (grep)
its only caller was the retired `ExtractOpeningsTouchingOvk`; the
hop-tracing machinery it existed for
(`BuildWallRun`/`FindHostWallNodes`/`FindExteriorOvkPaths`/
`AssignOvkEdgeIndex`/`HostWallOwnsOvkNode`) solved a problem — an
unreliable *marker-label* position — that no longer exists once
candidates are anchored at real geometry per the strategies above.

**Verified:**
- Direct pipeline run against all 3 real files (not just `dotnet build`):
  floor1 46 openings / floor2 77 / floor3 76, all with plausible
  width/height (0.47-1.8m / 0.62-2.83m) and all 8 compass directions
  represented across the three floors. Before the fix: 0/0/0.
  **Not independently verified against a reference count** — unlike the
  original floor1-4 dataset, no manually-confirmed table exists for
  these three files; flagged to the user to sanity-check the printed
  per-size/per-direction counts against what they know of the real
  building.
- `samples/`'s older convention-A (`Wall_N_2`) and convention-B (Archicad
  companion-object) sample files are no longer present on disk (confirmed
  — `samples/` now only has `floor1-3.dxf`), so those two conventions
  could only be regression-tested via synthetic in-memory documents this
  session, not live files.
- Added `tests/HVACrate2.Core.Tests/OpeningExtractionRegressionTests.cs`
  (skips, doesn't fail, when a sample file isn't present — `samples/` is
  gitignored/local-only per the 2026-08-05 decision) and
  `SyntheticOpeningExtractionTests.cs` — minimal in-memory `DxfDocument`s
  built via netDxf's document API with deliberately arbitrary layer/block
  names (`Layer_ABC`, `Zorp_9`, `FOO1`/`FOO2` ATTRIB tags — never
  "wall"/"window"/"door"/"marker" in any language), one per implemented
  strategy plus one proving an interior-positioned candidate is correctly
  rejected. `FloorProcessor.ProcessFloorFromDocument` added (public) as
  the test entry point that accepts an already-constructed `DxfDocument`,
  split out of `ProcessFloor` (which still just loads a file and calls
  it) specifically so synthetic in-memory documents don't need a real
  file on disk.
- `dotnet build` clean (0 warnings, 0 errors) across all 3 projects.
  `dotnet test` doesn't work on this SDK (`dotnet run --project
  tests/HVACrate2.Core.Tests` instead — the new Microsoft.Testing.Platform
  runner); all 7 tests pass (1 pre-existing placeholder + 3 real-sample
  regressions + 3 synthetic).
- App built and launched, left running for the user to click through the
  real `Work → Extract & Preview` flow directly — no UI-automation
  scripting this session.

---

## 2026-08-14 (same day, follow-up) — Opening extraction validated
against a real reference table; two concrete over-counting bugs found and
fixed

**Context:** the user supplied their own manually-extracted reference
`Топлотехника V6.0.16.xls` for the same building the 3 new samples
belong to (converted to `.xlsx` via Excel COM automation the same way as
the app's bundled template, since ClosedXML can't read legacy `.xls`).
This is the first real ground truth for `floor1-3.dxf` — the initial
46/77/76-openings result reported earlier the same session had no
independent reference to check against. The reference's merged
14-row/108-opening table revealed the new pipeline was **over-counting
by ~1.84x** (199 raw openings across the 3 floors) — matching the user's
own direct report ("much more than the one I have extracted manually...
you extract windows that are not even existing or part of the OVK
layer").

**Bug 1 — the real root cause of most of the over-count: one physical
opening detected multiple times, too far apart to position-dedupe.**
Inspected the worst offender (`0.6×1.85`, 40 raw hits on `floor2.dxf`
alone against a reference of 28 for the *combined* 3 floors) by printing
each hit's evidence. Confirmed: a single real window's own body geometry
(`Archicad Windows` layer — frame/jamb lines, not just the marker leader)
has *multiple* short lines, each independently satisfying the
perpendicular-line-with-two-numbers pattern, anchored at different points
spread across the window's own width — often farther apart than
`OpeningDeduper`'s 0.5m position tolerance, especially for wider openings
(1.2-1.8m). **Fix:** `PerpendicularLabeledLineStrategy` now groups
candidates by the *identity* of the two label (`MText`/`Text`) entities
they matched — not by the resulting candidate's position — and keeps
only the one whose tip lands closest to OVK per unique label pair. Two
lines that matched the exact same two label objects are unambiguously
annotating the same real opening, regardless of how far apart their tips
land. This alone cut the raw total from 199 to 110 (reference: 108) and
took 9 of 14 real opening sizes/direction-breakdowns to an **exact**
match.

**Bug 2 — a handful of false positives from geometry on layers that are
unambiguously not a window/door.** The remaining ~5 spurious sizes (e.g.
`0.47×0.62`, `0.47×1.51`, `1.22×1.8`) traced to real but *irrelevant*
geometry that happened to coincidentally satisfy the geometric pattern: a
furniture dimension callout (`Интериор - мебели`), generic
room-dimension annotation lines (`ДИМЕНСИИ ...`), and an MEP/installations
annotation (`Част-ИНСТАЛАЦИИ`) — each printed in evidence, not guessed.
**Fix, consistent with the "names are hints, not requirements" design
(not a reversal of it):** `WordHints.NonOpening` — a small, narrow,
evidence-driven list (furniture/мебел, dimension/дименси,
installation/инсталаци, interior/интериор) used as a **negative** hint.
A hint lowering confidence/excluding a coincidental geometric match is
the same kind of evidence the design already uses positively elsewhere
(e.g. a "window" hint raising type confidence) — it doesn't gate
detection for anything lacking a recognizable name, it only suppresses a
match already contradicted by a name that *is* present and unambiguous.
Applied in both `PerpendicularLabeledLineStrategy` and
`BlockAttributeStrategy`. Caught and fixed a transliteration typo in the
same change (`димензи` doesn't match the real Cyrillic spelling
`дименсии` — Bulgarian уses с/S, not з/Z, for this word; verified via a
direct string-match check before trusting the fix).

**Result after both fixes:** 106 total openings (reference: 108) across
the 3 floors — 9 of 14 real sizes match the reference **exactly**
(including all 4 direction counts), the remaining rows are off by 1-3 in
individual direction cells with matching or near-matching totals, and
only one residual anomaly remains: a single `0.8×2.0m` opening on
`floor3.dxf` (evidence: a genuine `Archicad Doors` body line, 0.59m from
OVK, labeled `'200'/'80'`) that doesn't appear in the reference at all —
plausibly a wrong-label-pairing near a real `0.8×2.45` door rather than a
new distinct opening, not yet root-caused to a specific fix. Flagged to
the user, not silently left in the diagnostics as "resolved."

**Verified:** re-ran the same real-sample diagnostic comparison before
committing the fix; `dotnet build` clean (0 warnings/errors); all 7
existing tests (real-sample regression + synthetic name-independence)
still pass unchanged — neither fix required touching the synthetic
tests' fixture data, confirming they didn't rely on the specific bugs
being fixed.

---

## 2026-08-14 (same day, second follow-up) — the residual `0.8×2.0`
false positive root-caused and fixed: exterior classification needed a
wall-*backing* check, not just an OVK-*distance* check

**Context:** user pointed at the specific real drawing (a screenshot of
the DXF around the flagged opening) and confirmed directly: it's a door
between a room and a balcony — an interior partition, not an exterior
opening — and said to fix it, not just note it.

**Root cause, confirmed by direct geometry inspection (not guessed):**
the door's tip sits ~0.00-0.12m from its real host wall, on-layer
`Стени - интериор_Pen_No__27` — genuinely interior. But the *nearest
point on the OVK curve* to that same tip is only 0.59m away, because a
real exterior wall happens to run close by (a balcony recess brings
interior and exterior wall systems into close proximity). Distance to
the abstract OVK curve alone cannot tell "near the boundary" apart from
"embedded in the wall that actually forms the boundary" — this is
exactly the distinction the deleted `WallTopology.cs` used a full
connectivity graph to make; this session's simplified geometry-first
design didn't have an equivalent until now.

**Three additions to `ExteriorClassifier`/`WallGeometryClassifier`, each
found necessary by testing against this real case in turn — not designed
in one pass:**

1. **Wall-backing check, segment-based.** `WallGeometryClassifier` now
   requires **both** endpoints of a wall-layer segment to sit within
   0.4m of OVK (not name-hinted — see the interior/exterior naming bug
   below) before it counts as "wall-like", and returns whole segments
   (not just vertices) so the backing check can measure true
   point-to-*segment* distance the same way OVK distance already works
   — a candidate sitting mid-wall, far from either endpoint, needs this;
   point-to-vertex-only broke a synthetic test immediately (caught before
   this shipped, not after). Requiring both endpoints near OVK (not just
   one) is deliberate: a single near-OVK endpoint isn't enough, since an
   interior partition merely joining an exterior wall at a T-junction
   shares exactly that one corner point with it — confirmed this was
   real by testing a "single point" version first, which still passed
   the flagged door (its interior wall's far endpoint, where it meets the
   exterior wall, is trivially close to OVK).
2. **The wall-name-hint path in `WallGeometryClassifier` was itself a
   bug, found while building the check above.** It accepted a segment as
   "wall-like" if its layer name merely hinted at "wall" in any language
   — but Bulgarian "стен" matches both `Стени - екстериор` and
   `Стени - интериор` equally, since the word doesn't distinguish
   interior from exterior. That let the flagged door's own interior wall
   count as valid backing. Removed the name-hint path entirely; proximity
   to OVK is the only signal now (matches the already-validated 0.25m/
   1.57m+ real-data gap between exterior and interior walls).
3. **`WallGeometryClassifier.CollectExplicitInteriorSegments` +
   `ExteriorClassifier`'s third check.** Segment-based backing alone
   still passed the flagged door: the nearest exterior wall's own corner
   geometry (not the wall the door sits in) landed within the 0.5m
   backing tolerance. Distance-threshold tightening to exclude it was
   tried (0.25m) and reverted — it dropped the grand total from 106 to 97
   and exact-match rows from 9/14 to 7/14, i.e. it broke more
   genuinely-correct matches than it fixed, because the bad case (0.29m)
   and several good cases sit too close together in raw distance for any
   single threshold to separate. Added a **targeted, name-based
   override** instead: if a wall explicitly labeled interior
   (`WordHints.ExplicitInterior` = "interior"/"интериор" — a narrow
   subset of the existing `NonOpening` list, used here as an unambiguous
   positive signal rather than a broad exclusion) sits *meaningfully*
   closer to the candidate than the nearest confirmed exterior-forming
   wall, that interior wall is treated as the real host. A bare "closer
   at all" comparison over-triggered near real corners (where an interior
   partition legitimately meets the exterior wall right next to a
   genuine opening) — dropped the total to 101 and exact matches to
   9/14. Added a 0.15m margin requirement (only override on a *clear*
   case, not a near-tie) — recovered to the best result yet.

**Final result this round:** 104 openings vs. the 108-opening reference
— **10 of 14 sizes now match exactly** (up from 9), and the flagged
`0.8×2.0` balcony-door false positive is gone. This uses the existing
"names are hints, not requirements" design faithfully: the override only
fires when an explicit, unambiguous name is present and gives a clear
signal; it does nothing on a file with no such naming convention, and it
never gates whether an opening can be *detected* — only whether an
already-detected candidate's host-wall ambiguity gets resolved.

**Verified:** `dotnet build` clean; all 7 tests pass (including the
synthetic test that briefly broke when backing switched to point-based
distance, before the segment-based fix); full 3-floor comparison re-run
against the same reference table after every intermediate change, not
just at the end, to catch regressions immediately rather than compounding
them (this is exactly what caught the 0.25m-tolerance and
bare-closer-than regressions before either was left in place).

---

## 2026-08-14 (same day) — Preview page north-arrow rendering bug: fixed
anchor position clipped the "N" label for some north angles

**Context:** user circled a screenshot of the 2D floor preview showing a
stray gray line fragment in the top-left corner instead of a proper
north arrow + "N" label.

**Root cause:** `FloorPreviewControl.DrawNorthArrow` anchored the arrow
at a fixed `(20, 20)` and placed the "N" label further out along the
arrow's direction. For `NorthDeg = 0` (arrow pointing straight up), the
label's computed Y coordinate is `20 - 22 - 7 = -9` — outside the
canvas's `[0, 300]` bounds. `DrawCanvas` has `ClipToBounds="True"`, so
the label is silently clipped; only a short line fragment (which stayed
within bounds) remained visible, exactly matching what was circled. The
anchor position had never been checked against every possible north
angle, only whichever angle the floors tested so far happened to use.

**Fix:** moved the anchor to `(40, 40)` — enough clearance (arrow length
14 + label gap 8 + the label's own ~9px half-extent ≈ 31px worst case)
that the label stays inside the canvas regardless of which direction the
arrow points. `dotnet build` clean.

---

## 2026-08-20 — Floor Heating "wrong results" report: calculator confirmed
correct; real bug was `HeatingResultsPage`'s `DataGrid` unreadable in dark
mode

**Context:** user reported the floor-heating calculation was producing
wrong numbers for a specific set of test deltas (δбет‑под=0.015,
δзам=0.01, δтер=0.005, δбет‑таван=0.015, δизол=0.03, δплоч=0.4,
δзам=0.01), expecting Rог=0.140802, Rод=1.3473, Ro=1.4881.

**Investigated by hand-computing `FloorHeatingCalculator.Calculate`**
against the six physical deltas the model actually accepts (δбет=0.015,
δзам‑под=0.01, δтер=0.005, δизол=0.03, δплоч=0.4, δзам‑таван=0.01):
reproduces 0.140802 / 1.347277 / 1.488079 exactly. **Conclusion: the
calculator and formula wiring are correct, not a bug** — the user
confirmed this once shown the arithmetic. (The extra `δбет‑таван` value
in the report has no separate field by design — see the 2026-08-10 entry
above — δбет is one shared input; this was never root-caused further
since the user confirmed the calculation itself was fine.)

**Real, separate bug found and fixed: `HeatingResultsPage`'s results
`DataGrid` was unreadable in dark mode.** The `DataGrid` set
`Background`/`Foreground`/`RowBackground`/`BorderBrush` at the
`DataGrid` level via `DynamicResource`, but WPF's default
`DataGridCell`/`DataGridColumnHeader` control templates don't inherit
`Foreground` from the parent `DataGrid` — they fall back to
`SystemColors`-based defaults, which stayed dark/near-black regardless
of theme. In light mode this happened to still read fine (dark text on
the app's light card background); in dark mode the cell text became
near-invisible against the dark card background. Fixed by adding
explicit `DataGrid.Resources` styles for `DataGridCell` (Foreground,
transparent Background so `RowBackground` shows through, themed
selection colors) and `DataGridColumnHeader` (Background/Foreground/
BorderBrush, all `DynamicResource`-bound to the existing theme brushes),
plus `AlternatingRowBackground`/`HorizontalGridLinesBrush`/
`VerticalGridLinesBrush` for full theme consistency. This is the first
`DataGrid` in the app (Energy Efficiency's results use plain
`TextBlock`s, not a grid), so this styling gap hadn't been hit before.
`dotnet build` clean. Not verified with a live screenshot — no desktop
UI screenshot tool was available in this session, only build-level
verification.

---

## 2026-08-20 (same day) — 85%+ TUnit coverage added for HVACrate2.Core,
branch `test/core-coverage-85`

**Decision:** added `InternalsVisibleTo` from `HVACrate2.Core` to
`HVACrate2.Core.Tests`, so the new tests can exercise the `internal`
classes in `Openings/` (`GeometryUtils`, `OpeningDeduper`,
`TypeClassifier`, `WordHints`, `DxfEntityIndex`, `ExteriorClassifier`,
`WallGeometryClassifier`, `BlockAttributeStrategy`,
`PerpendicularLabeledLineStrategy`, `OpeningExtractor`) and
`FloorProcessor`'s internal bearing/direction helpers
(`BearingToDirection`, `EdgeOutwardDirection`) directly, in isolation —
not only indirectly through the full DXF→Excel pipeline. This gives far
more precise, fast-to-diagnose tests than only driving everything
through `ProcessFloorFromDocument`, at the cost of coupling tests to
implementation internals — judged the right tradeoff here since these
internals (candidate strategies, classifiers, dedup) are exactly where
this codebase's real logic/bugs have lived per the rest of this file.

**Coverage tooling:** added `Microsoft.Testing.Extensions.CodeCoverage`
to the test project (Microsoft.Testing.Platform-native, works with
`dotnet run --coverage --coverage-output-format cobertura` — plain
`dotnet test` was already noted as broken on this SDK, see 2026-08-14).
Had to bump the pinned version from an initially-guessed `17.14.4` to
`18.9.0` — TUnit 1.63.0 itself already depends on `18.9.0` transitively,
and NuGet's central version resolution treats a lower explicit pin as a
downgrade error, not a silent override.

**`Program.cs` excluded from the coverage target** via
`[ExcludeFromCodeCoverage]`. It is a manual console harness with
hardcoded local sample/template paths (per its own long-standing
description in this file and in plan.md) — not part of the library's
real public contract exercised by the app or by any test, and not
meaningfully testable without real local files that aren't guaranteed
to exist in every checkout. Excluding it (rather than writing a test
that would just assert it doesn't crash against paths that may not
exist) keeps the coverage number meaningful.

**New test files, one per production file/concern, plus a new
`FloorProcessorExcelTests.cs`** covering the Excel-write path
(`WriteFloorsToExcel`/`ProcessAndWriteFloors`) against the real, tracked
blank template (`output/Топлотехника V6.0.16.xlsx`) — geometry block,
wall-by-direction block, the merged openings table, and the
apartment/appliance block — plus the previously-untested
`DetectCoordinateDivisor` millimeter-fallback branch, the
largest-area OVK selection among multiple same-layer polylines, the
missing-OVK-layer exception, and a non-rectangular (reflex-corner)
floor plan for `CountConvexCorners`. All Excel-write tests write to a
temp-directory scratch copy, cleaned up in a `finally` block — the
tracked template itself is never modified, consistent with the
existing `WriteFloorsToExcel` design (see the 2026-08-04 entry above).

**Removed the placeholder `Tests.cs`** (`Assert.That(true).IsTrue()`,
flagged by TUnit's own `TUnitAssertions0005` analyzer) now that real
coverage exists — no longer any reason to keep a scaffold test around.

**Result:** 129 tests, all passing; **99.9% line coverage / 94.9%
branch coverage** on `HVACrate2.Core` (`dotnet run --coverage
--coverage-output-format cobertura` from `tests/HVACrate2.Core.Tests`),
well past the 85% target. The one class not at 100% is `FloorProcessor`
itself, at 99.5%/95.9% — a couple of defensive branches (e.g. the
`cross == 0` exact-collinear-vertex case in `CountConvexCorners`) are
untested edge cases with no known real-world trigger, not gaps in the
tested behavior.

---

## 2026-08-20 (same day) — All non-summary comments removed from the
codebase, branch `chore/strip-comments`

**Decision:** per explicit user request, every plain `//` comment and
XAML `<!-- -->` comment was removed from `src/` and `tests/`. `///
<summary>...</summary>` XML doc comments were kept as-is — confirmed
first (via a targeted grep for `<param>`/`<returns>`/`<remarks>`/etc.)
that every `///` block in this codebase is already just a `<summary>`
(no separate doc sections to selectively drop), so "keep the summary"
reduces cleanly to "keep every `///` line untouched."

**Mechanism:** a throwaway Roslyn-based console tool (not committed —
lived in the session scratchpad only) parsed each `.cs` file into a
syntax tree and rewrote token trivia: leading-trivia line-groups whose
only content was a plain `//` comment were dropped entirely (comment,
its indentation, and its newline — so no blank line is left behind);
trailing same-line `// ...` comments had just the comment (and the
whitespace immediately before it) stripped, keeping the line's own
end-of-line trivia intact so two source lines are never merged
together. `///` trivia was left untouched by construction (a different
`SyntaxKind`, never matched by the plain-comment check). A regex pass
over the final text then collapsed any 3+ consecutive blank lines left
behind by whole-block removals down to one. Chosen over a naive
line-based regex specifically because the codebase has several string
literals containing `//` (e.g. GitHub Releases URLs) that a text-level
approach could have corrupted; a real C# parser can't confuse a
comment with a string literal's contents. Verified on two representative
files first (`FloorProcessor.cs`, `WallGeometryClassifier.cs`) before
running against every real source file. The 4 XAML files with `<!--
-->` section-label comments (`Strings.En/Bg.xaml`, `CompassControl.xaml`,
`InstructionsPage.xaml`) were handled separately with a plain line-delete
`sed` pass, since XAML has no analogous "doc comment" concept to
protect.

**Verified:** `dotnet build` clean (0 warnings) and all 129 tests still
passing after the rewrite — confirms the trivia-only rewrite changed no
executable semantics. Spot-checked several diffs by hand (`FloorProcessor.cs`,
`ExteriorClassifier.cs`, `PerpendicularLabeledLineStrategy.cs`,
`OpeningExtractorTests.cs`) to confirm every removed block was a plain
`//` comment and every `/// <summary>` block survived intact.

**Note:** this session found that `main` had already advanced past what
this session last knew about — both the dark-mode-fix PR and the
test-coverage PR (prepared in earlier turns of this session) had been
merged on GitHub and pulled locally, moving the working tree's branch
from `feature/floor-heating` to `main` between turns. This comment-removal
work was branched from that already-updated `main`, not from
`feature/floor-heating`, since both of that branch's commits are now
part of `main` anyway.

---

## 2026-08-20 (same day) — Phase 4 (packaging/distribution) closed out:
tag-triggered GitHub Actions release workflow, branch
`feature/release-automation`

**Context:** the user asked to make the app downloadable via a GitHub
Releases link for their website — the last untouched item from
CLAUDE.md's original architecture ("uploaded to GitHub Releases, linked
from the static site"). Went through `EnterPlanMode` (an Explore pass
confirmed `HVACrate2.App.csproj` had no publish-related properties yet,
that `.github/workflows/ci.yml` already dry-run-validates the exact
publish command via its `publish-check` job, and that `gh` CLI is still
unavailable in this environment with no other GitHub API credentials),
then an `AskUserQuestion` round settled two forks: automate via GitHub
Actions (vs. one-off manual publish) and use a proper `v1.0.0` tag with
asset name `HVACrate2.exe` (vs. reusing the existing `Pre-Release` tag,
which stays as the two tutorial videos' unrelated home).

**`HVACrate2.App.csproj`:** added `AssemblyName=HVACrate2` (so the
built/published binary is `HVACrate2.exe`/`HVACrate2.dll`, not the
default `HVACrate2.App.exe`) and `Version=1.0.0`. Deliberately did
*not* bake `RuntimeIdentifier`/`SelfContained`/`PublishSingleFile` into
the csproj as defaults — kept them as explicit `dotnet publish`
command-line flags (matching `ci.yml`'s existing convention) so plain
local `dotnet build`/`dotnet run` for any of the three projects is
unaffected.

**New `.github/workflows/release.yml`**, triggered on `v*` tag push: a
`test` job (same restore/build/test steps as `ci.yml`, so a broken
commit can never produce a release) gates a `release` job that
publishes and uploads via `softprops/action-gh-release@v2`
(`generate_release_notes: true`). This makes every future release a
two-command action for the user (`git tag vX.Y.Z && git push origin
vX.Y.Z`) — no `gh` CLI or manual upload ever needed again, working
around this environment's lack of `gh`/API credentials by moving the
actual "create a public release" step onto GitHub's own runner, which
has its own token.

**Real bug caught by actually running the published output, not just
building it:** verified locally end-to-end — published with
`--self-contained true -p:PublishSingleFile=true`, and found `HVACrate2.exe`
alone is **not** standalone: several WPF native interop DLLs
(`D3DCompiler_47_cor3.dll`, `PenImc_cor3.dll`, `PresentationNative_cor3.dll`,
`vcruntime140_cor3.dll`, `wpfgfx_cor3.dll`) are always emitted as
separate files next to the exe by `PublishSingleFile` — .NET's
single-file host only bundles managed assemblies, native (non-CLR)
libraries are loaded via `LoadLibrary` and can't be folded in the same
way. Fixed by adding `-p:IncludeNativeLibrariesForSelfExtract=true`,
which does embed them (self-extracting to a temp dir at process start)
— re-verified the native DLLs disappeared from the output folder after
adding the flag.

**Second real constraint found the same way:** even with that flag,
`Assets/Template.xlsx` (the bundled blank Excel template, an existing
`CopyToOutputDirectory="PreserveNewest"` loose file per the 2026-08-05
Phase 3 decision) still isn't embeddable — the app reads it from disk
next to the exe at runtime, and single-file publishing has no mechanism
to fold arbitrary non-assembly content files into the bundle. Rather
than change the app's template-loading code (out of scope for this
session, and higher regression risk than the alternative), the release
workflow zips the whole publish output (`HVACrate2.exe` + `Assets/`)
into `HVACrate2-win-x64.zip` and uploads *that* as the release asset —
still one link, one download, for the user. `plan.md`/`CLAUDE.md` both
updated to describe the download as a zip, not a bare `.exe`.

**Verified end-to-end, not just built:** published locally with the
exact flags the workflow uses, zipped it with the same `Compress-Archive`
command the workflow runs, extracted the zip to a **fresh** directory,
and launched `HVACrate2.exe` from there — confirmed it starts with no
`dotnet`/SDK involved and its main window title reads "HVACrate 2.0".
This is the closest this session could get to Phase 4's "test on a
clean machine" item without an actual clean VM (self-contained
publishing means no .NET runtime install dependency either way, which
is the concern that checklist item was really getting at).

**Not done by this session, deliberately:** pushing the `v1.0.0` tag
that actually triggers the first public release. Creating a public
GitHub Release is treated as a publish action for the user to trigger
themselves (documented in `CLAUDE.md`'s new "Publishing a release"
section) — this session prepared everything up to that point (branch
pushed, PR link handed to the user) but did not tag/push it.

**Closes Phase 4** except: (1) a literal clean-VM test, left to the
user; (2) actually linking the download URL from their static website,
which lives outside this repo entirely.

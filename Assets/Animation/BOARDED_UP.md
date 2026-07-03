**# Boarded Up — the vacant house · Unity implementation**



**The \*\*derelict\*\* state of a home on the street: boarded windows, a barricaded door, rotting**

**warped siding, missing shingles, a crumbling chimney and dead ivy creeping the walls. Authored**

**as the abandoned cousin of \*\*Neighbor B\*\* (the hipped house) — same \*\*24×24 modular grammar\*\*,**

**same cell layout — so it drops into the exact same builder and \*\*swaps in tile-for-tile\*\*.**



**> 24-px world, \*\*PPU 24, Point (no) filter, Compression None\*\*, real alpha. Palette is sampled**

**> from `neighbor\_B\_tiles.png` (weathered spruce, barn-red trim, grey-brown shingle, grey**

**> fieldstone) then pushed toward abandonment: grey-green mould, water stain, dead-plant browns,**

**> and a paler fresh-sawn pine for the nailed-on boards, so the boarding reads as \*newer\* wood**

**> slapped over the tired old house.**



**\*\*Vacancy is the state — there is one sheet, no home/nightmare split.\*\* Swap the whole atlas**

**(or individual cells) when a house goes empty; the geometry never changes, so no collider,**

**pivot, or layout edits are needed.**



**---**



**## Files**



**| File | Size | Grid |**

**|---|---|---|**

**| `boarded\_up\_tiles.png` | 192×144 | 8×6 · 24-px cells — \*\*this is what to import\*\* |**

**| `boarded\_up\_tiles-8x.png` | 1536×1152 | 8× preview only (do not import) |**

**| `boardedgen.js` | — | generator: `eval(await readFile('boardedgen.js'))` → `{P, renderSheet, CELL, COLS, ROWS, LAYOUT}` |**

**| `Boarded Up.dc.html` | — | animated showcase: assembles a derelict house + shows the raw atlas |**



**---**



**## The tile atlas — rows \& cells**



**Cell `(col\*24, row\*24, 24, 24)`. \*\*Every cell is the derelict version of the cell at the same**

**(col,row) in `neighbor\_B\_tiles.png`\*\* — so a house built from the neighbor atlas becomes vacant**

**by pointing the same builder at this sheet.**



**\*\*Row 0 (static):\*\***

**`wallA` (rotting, warped lap siding) · `wallB` (busted board + dead-ivy tendril) ·**

**`postV` (split, mossy corner post) · `beamH` (sagging, cracked belt) ·**

**`foundation` (cracked fieldstone, weeds sprouting) · `doorTop` (boarded — planks + a condemned**

**notice) · `doorBottom` (boarded — plank brace, threshold weeds) · `window` (\*\*boarded\*\* — three**

**planks nailed across black glass, static)**



**\*\*Row 1 (static):\*\***

**`roofField` (missing + slipped shingles, batten showing) · `roofRakeL` · `roofRakeR`**

**(broken-edge rakes) · `eave` (sagging, half-detached gutter) · `chimney` (crumbling brick,**

**missing bricks, moss) · `chimneyTop` (cold cracked pot — the fire's been out for years)**



**\*\*Rows 2–5 (animated strips)\*\* — same grid \& slots as the player/neighbor house, so the swap is**

**clean. These carry the "wore-down" life:**



**| Strip | Row | Frames | ms | Reads as |**

**|---|---|---|---|---|**

**| `brokenWindow` | 2 | 6 | 220 | a smashed pane, one splintered board still nailed diagonally, \*\*flies orbiting\*\* the hole |**

**| `mothWindow` | 3, cols 0–3 | 4 | 260 | a \*\*moth\*\* flutters at the lit slit between two boards |**

**| `overgrowth` | 4 | 6 | 200 | \*\*dead ivy / weeds\*\* climbing the siding, swaying gently |**

**| `loosePlank` | 5, cols 0–3 | 4 | 240 | a board pulls free of its nails and \*\*rattles\*\* in the wind |**



**Same wrong-timing philosophy as the rest of the game — never a metronome. The strips are short**

**and out of phase with each other; gate them to player proximity so the decay only stirs when**

**someone is near.**



**---**



**## Building / swapping a vacant house**



**Because the cell layout is identical to `neighbor\_B\_tiles.png`, you have two options:**



**1. \*\*Whole-house swap\*\* — point your existing Neighbor-B tile builder at `boarded\_up\_tiles.png`**

&#x20;  **instead. Same posts, walls, belt, foundation, roof rakes/field/eave and door cells land in**

&#x20;  **the same places; the house is now derelict. Roof shape stays an assembly choice (lay**

&#x20;  **`roofField` across the plate, `eave` along the sides, `roofRakeL/R` on the slopes for a**

&#x20;  **gable; a hip reads the same way with the trapezoid slope on the end faces).**

**2. \*\*Spot repair-in-reverse\*\* — swap only some cells (e.g. board just the ground-floor windows**

&#x20;  **and the door, leave the upper storey intact) for a house that's \*going\* vacant rather than**

&#x20;  **long-abandoned.**



**Front face, per column left→right, is unchanged: `postV | wall/window/door | … | postV`. Stack**

**foundation → story 1 → `beamH` belt → story 2 → eave → roof; corner posts at both ends of every**

**wall row; `chimney` + `chimneyTop` offset on the roof (no smoke overlay — it's a dead chimney).**



**The showcase in `Boarded Up.dc.html` assembles exactly this (6-wide, two storeys) with all four**

**animated strips live, if you want a reference layout.**



**---**



**## Contexts — when the vacant sheet shows**



**- \*\*A house the player can't enter / has been abandoned:\*\* use `boarded\_up\_tiles.png` for the**

&#x20; **whole elevation. Windows are boarded, the door is barricaded, weeds are taking the foundation.**

**- \*\*A house going empty over time:\*\* start from `neighbor\_B\_tiles.png` and swap cells to boarded**

&#x20; **ones as the story progresses (board the windows first, then the door).**

**- Drive the animated strips (`brokenWindow`, `mothWindow`, `overgrowth`, `loosePlank`) off a**

&#x20; **proximity check, same as the neighbor houses' windows — the flies, the moth, the rattling board**

&#x20; **and the swaying ivy only move when the player is close.**



**\*\*Layer order:\*\* sky → mountain range → \*\*houses\*\* (ground/street layer, sorted by base Y) →**

**props → characters. The vacant house sorts exactly like Neighbor B — the geometry is unchanged.**




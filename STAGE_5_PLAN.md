# Stage 5: Full Campaign — Draft Plan

**Goal**: All 21 alpha levels playable with every special tile mechanic, every AI type, every remaining card and equipment item, plus the equipment prerequisite chain.

**Starting state**: 8 floors, 27 cards (5 starter + 22 reward, including Recall variants and food cards), 12 equipment items, copper economy + shop, 373 tests.

**End state**: 21 floors, ~33 cards (Pose, Taunt, 4× Gaze, 4× Fetch added) with *all* enhanced variants implemented, ~30 equipment items (12 base + ~18 stretch/chain), three AI types (NoGuess / Conservative / Reasoning), four new special-tile mechanics (courtier / soirée / lounging noble / sanctum + inner), rival intent point system, rival-noble-reveal completes floor as win, ~500+ tests.

---

## Key Design Decisions

### Replace placeholder Level1–8 with the alpha's configs
The placeholder configs we wrote in Stages 2–3 don't match the alpha. Stage 5 imports the alpha's `levels-config.json` verbatim for floors 1–8 once the supporting mechanics exist (Conservative AI, courtiers, soirées). After that, floors 9–21 are added in two batches (mechanics ramp, then full combinations).

### AI configured per-level via `LevelConfig.RivalAi`
`AiType` enum: `Random` (current behavior, retained as fallback), `NoGuess`, `Conservative`, `Reasoning`. Default per level comes from the alpha config's `rivalAI` field. `Random` is used only when no AI is specified (e.g., L1–L4 in the alpha pre-Conservative).

### Intent point system replaces the random rival reveal
The alpha drives rival turns via per-tile intent point weights, regenerated each turn and decayed across reveals. We mirror it directly:
- **Generate** at turn start: pick 2 random rival tiles + 6 random other tiles, sort stable by safety (Rival > Neutral > Player > Noble), assign points `[5, 3, 3, 3, 3, 1, 1, 1]`, then add 4 baseline distractions (random nonzero-point tiles get +1).
- **Equipment modifiers**: Eyeshadow +1 distraction, Mascara +1 distraction, Ramble card adds N distractions on play.
- **Decay** after rival reveals: remove points for revealed tiles, remove neighbors of any rival reveal with `adjacencyCount=0`, decrement remaining by 1, drop zeros.
- **AI selection**: each AI type uses these points as input weights when picking which tile(s) to reveal.

### Equipment prerequisites
Some equipment is gated behind owning a specific item (e.g., Triple Broom requires Double Broom; Favor requires Tea + Cocktail). Implementation: prereqs filter offerings (a chained item never appears unless the prereq is owned). Existing M29 equipment offering generation already filters owned items; we extend that filter with a prereq check.

### Reasoning AI is its own milestone
Per the user's instruction. Implemented after all special-tile mechanics exist so it can reason about the full game state (lounging nobles, courtiers, sanctums, etc.).

### Rival noble reveals complete the floor in the player's favor
A noble revealed *by the player* (without Excuses protection) is still a loss. A noble revealed *by the rival* (without `rivalMineProtection`) is a floor *win* — same outcome as finding all player tiles. With `rivalMineProtection > 0`, the protection is consumed and the floor continues. This applies to both regular nobles and lounging nobles.

### Lounging nobles are nobles
The alpha's "surface mines" are reskinned as **Lounging Nobles** (themed: a noble reclining where the maid expected to clean). Mechanically:
- They overlay player or neutral tiles (placed during board generation, or by the rival via `rivalPlacesMines`).
- Player reveal: ends the run unless Excuses protects (identical to regular nobles).
- Rival reveal: completes the floor as a win unless `rivalMineProtection` absorbs it (identical to regular nobles).
- Sweep cleans them (removes the lounging noble overlay; underlying tile reverts to normal).
- They count as nobles for all purposes — Excuses, win/loss checks, AI avoidance.

### Courtiers and Soirées
The alpha's "goblins" are reskinned as **Courtiers** (themed: a courtier wandering the palace, getting in the maid's way). Lairs are reskinned as **Soirées** (themed: parties that draw new courtiers in).

### Inner-tile adjacency through portals
Sanctum tiles act as portals between regular tiles and inner-tile clusters. Adjacency calculation:
- **Standard rules** (king or manhattan-2) still apply for movement steps.
- A **revealed sanctum** doesn't "cost" a move — its neighbors collapse together (inner side and outer side both count as 1 step from the portal).
- An **unrevealed sanctum** blocks adjacency entirely — neither inner nor outer tiles can "see" through it.
- Adjacency numbers therefore must recompute when a sanctum's revealed state changes (reveal, or unreveal via Brat).
- Card-targeting rules are **per-card** and decided case by case. Default rule for line/area cards: an unrevealed sanctum blocks the line; a revealed sanctum is traversable. Example: Gaze starting on a regular tile cannot see inner tiles in its line unless its line crosses a revealed sanctum first.

---

## Milestones

### Milestone 35: Rival Intent Point System
**Goal**: Per-tile intent points drive rival reveals; values regenerate at turn start and decay across reveals. Replaces the current "random rival tile" behavior.

**Tasks**:
- `IReadOnlyDictionary<Position, int> RivalIntentPoints` on `GameState`
- `IntentSystem.GenerateTurnIntent(state, rng)`: 2 rivals + 6 others, sort by safety (Rival > Neutral > Player > Noble), points `[5, 3, 3, 3, 3, 1, 1, 1]`, +4 distractions
- `IntentSystem.AddDistractionPoint(points, excluded, rng)`: pick a random tile with nonzero points, +1
- `IntentSystem.DecayIntent(state, revealedPositions)`: remove revealed, remove neighbors of 0-adj rival reveals, decrement, drop zeros
- Wire into `TurnSystem.StartPlayerTurn` (carry over + generate new) and rival-turn reveal flow (decay after)
- Replace `ExecuteRivalTurn`'s random pick with intent-weighted pick (highest-points first; ties random) — this is the temporary "Random" AI implementation, formalized in M36
- Equipment hooks for Eyeshadow/Mascara already exist (M30); update to add intent points instead of `DistractionStacks`
- Update Ramble card to add intent distraction points instead of `DistractionStacks` 

**Tests** (~10):
- Generate: produces 8 weighted positions with point sum 4+24+4 = 32 (or whatever the math works out to — verify against alpha)
- Generate: rival tiles always picked when available
- Decay: revealed positions removed
- Decay: neighbors of 0-adj rival reveal removed
- Decay: remaining points decrement by 1, zeros dropped
- Carry-over: previous-turn points combine with new generation
- Eyeshadow integration: +1 distraction added every turn
- Excluded positions (already-deduced) not picked
- Empty board (no unrevealed): generates empty map
- Distraction targets only nonzero-point tiles

**Status**: Complete (390 tests)

---

### Milestone 36: AI Framework + NoGuess
**Goal**: Pluggable AI system with per-level selection. Implement Random (default, refactored) and NoGuess (constraint satisfaction — never reveals a noble).

**Tasks**:
- `IRivalAi` interface: `SelectTilesToReveal(state, intentPoints, context) → IReadOnlyList<Position>`
- `AiType` enum: `Random`, `NoGuess`, `Conservative`, `Reasoning`
- `AiRegistry` factory keyed by `AiType`
- `LevelConfig.RivalAi` field with default `AiType.Random`
- `RandomAi`: weighted random by intent points
- `NoGuessAi`: filters out tiles whose ownership isn't yet certain to be safe (uses constraint propagation across revealed adjacency counts to identify guaranteed-safe positions); falls back to a non-noble random if no guaranteed-safe exists
- Wire `AiRegistry` into rival-turn flow

**Tests** (~10):
- Random: weighted distribution proportional to points
- Random: respects intent zero (skips zero-point tiles)
- NoGuess: never reveals a tile whose ownership is provably noble
- NoGuess: prefers guaranteed-safe tiles over uncertain
- NoGuess: handles "no safe deductions possible" (falls back to non-noble random)
- AI selection: `LevelConfig.RivalAi` routes to the right type
- Default (no AI specified): falls back to Random

**Status**: Complete (400 tests)

---

### Milestone 37: Conservative AI
**Goal**: Rival prefers high-safety tiles weighted by intent points. When `rivalNeverMines`, avoids both regular nobles and lounging nobles.

**Tasks**:
- `ConservativeAi`: weighted preference by `(intentPoints, safetyRank)` tuple; skips nobles (regular + lounging) when `specialBehaviors.rivalNeverMines`
- "Prefer guaranteed rivals" pass before falling back to weighted random
- Honor `excludedPositions` (positions already deduced via adjacency)

**Tests** (~6):
- Picks guaranteed rivals first
- Skips both regular and lounging nobles when `rivalNeverMines = true`
- Reveals nobles when `rivalNeverMines = false` (default; rival reveal completes floor as win — see M40)
- Weights by intent points among non-excluded tiles
- Falls back gracefully when nothing eligible
- Does not skip lounging nobles when `rivalNeverMines = false` (they're part of the intent-driven selection)

**Status**: Complete (407 tests; lounging-noble filtering deferred to M40)

---

### Milestone 38: Special Tile Foundation
**Goal**: Generic special tile data model and placement system, ready for courtier/soirée/lounging-noble/sanctum implementations.

**Tasks**:
- Extend `SpecialTileType` enum: `Courtier`, `Soiree`, `LoungingNoble`, `Sanctum`, `InnerTile` (already has `ExtraDirty`)
- `Tile` model fields: `IsCourtier`, `IsSoiree`, `IsLoungingNoble`, `IsSanctum`, `IsInner` (booleans on `Tile.SpecialTile?` extension or flags on the existing `SpecialTile` field — design TBD)
- Placement extensions to `BoardSystem.CreateBoard`:
  - `placement: "nonmine"` — any non-noble tile (alpha keyword retained for config compatibility, despite the rename)
  - `placement: "empty"` — places on a previously unused position (i.e., creates a tile that's a soirée-only tile with no underlying owner)
  - `placement: [[x,y], ...]` — explicit positions
- Generic special-tile rendering hook in `TileView` (subclasses or per-flag draw blocks)

**Tests** (~6):
- Each placement strategy produces correct counts
- Explicit placement honors exact positions
- "empty" placement only uses unused-location slots
- Tile count totals (player+rival+neutral+noble) ignore "empty"-placed special tiles

**Status**: Complete (415 tests; rendering hook deferred to M39+ per-tile-type milestones)

---

### Milestone 39: Courtiers + Soirées
**Goal**: Courtier tiles move when interacted with (predetermined targets); soirées spawn courtiers after rival turns.

**Tasks**:
- `Courtier` state: `MoveTarget: Position` (to random adjacent tile but predetermined and visible to player)
- Reveal interaction: clicking a courtier tile → courtier moves to `MoveTarget`, original tile becomes "cleaned" (which does *not* reveal the tile; the courtier serves as a roadblock), courtier re-roots at target
- Collision: if `MoveTarget` already has a courtier, merge
- Card play that targets a courtier tile: same move-and-clean behavior (where applicable)
- Soirée: tile that can only be on *empty* tiles, not revealable tiles, that spawns 1 courtier at a random adjacent tile without a courtier already after every rival turn (unless the soirée tile itself is destroyed)
- `BoardSystem.SpawnCourtiersFromSoirees(board)` called at start of rival turn

**Tests** (~14):
- Courtier tile reveals → tile cleaned, courtier appears at MoveTarget, new MoveTarget exists
- Card targeting on courtier behaves identically (Spritz/Scurry/etc. clean and move)
- Cleaning a courtier doesn't end the player's turn unless the cleaning was done by trying to reveal the tile normally
- Soirée spawns 1 courtier after rival turn
- Courtier collision fallback works
- Multiple soirées spawn independently
- Courtier doesn't move on annotation-only effects (Eavesdrop, Tingle)

**Status**: Complete (427 tests; rendering deferred to a Stage-5-UI milestone)

---

### Milestone 40: Lounging Nobles + Rival Noble Reveal Floor-Win
**Goal**: Lounging nobles (alpha: surface mines) overlay player/neutral tiles and behave as nobles for all reveal-outcome purposes. Update game-status logic so rival-revealed nobles complete the floor in the player's favor.

**Tasks**:
- `LoungingNoble` flag on tile (overlays the underlying owner — a "noble reclining on a player tile")
- Reveal a lounging-noble tile (player): treated as a noble reveal — Excuses protects per existing logic; without Excuses, run ends
- Reveal a lounging-noble tile (rival): treated as a rival noble reveal — completes floor in player's favor unless `rivalMineProtection` absorbs (see below)
- Sweep card cleans lounging nobles (removes the overlay; underlying owner remains; in addition to ExtraDirty)
- `rivalPlacesMines: N`: after each rival turn, place N lounging-noble overlays on random unrevealed player/neutral tiles
- `rivalMineProtection: N`: state field `RivalMineProtectionCount`; when rival reveals **any noble** (regular or lounging), decrement; player gains 5 copper per protected reveal; when 0 → next unprotected rival noble reveal completes floor as a win
- `rivalNeverMines: bool`: AI filter (already wired into M37); does not affect noble placement
- **Update `TurnSystem.CheckGameStatus`**: distinguish player- vs rival-revealed nobles
  - Player-revealed unprotected noble → `GameStatus.Lost`
  - Rival-revealed unprotected noble → `GameStatus.Won` (floor complete in player's favor)
- **Update `GameRunner.ConsumeExcusesIfNeeded`**: only consume Excuses for player-revealed nobles
- Update existing M25/M26 tests that asserted "any revealed noble = loss" to reflect the new player-vs-rival distinction

**Tests** (~14):
- Player reveals a lounging noble: ends run (no Excuses)
- Player reveals a lounging noble: Excuses absorbs (1→0 triggers M25 Complaints/Mollify penalty)
- Rival reveals a lounging noble (no protection): floor completes as win
- Rival reveals a lounging noble (with protection): protection consumed, +5 copper, floor continues
- Rival reveals a *regular* noble (no protection): floor completes as win
- Sweep cleans lounging nobles, underlying owner restored
- `rivalPlacesMines = 1`: 1 overlay placed on random player/neutral tile after rival turn
- `rivalPlacesMines` doesn't place on already-overlaid, revealed, or rival tiles
- `rivalMineProtection = 2`: rival can absorb 2 noble reveals (regular or lounging) before floor wins
- AI-side: Conservative AI still avoids lounging nobles when `rivalNeverMines = true`
- Existing test: player win on revealing all player tiles still passes
- Excuses doesn't trigger from rival noble reveals (only player ones)

**Status**: Complete (442 tests; rendering deferred)

---

### Milestone 41: Sanctum + Inner Tiles
**Goal**: Sanctum tiles act as portals into inner tile clusters; revealed sanctums become traversable for adjacency calculation, unrevealed ones block. Adjacency numbers recompute on portal-state changes.

**Tasks**:
- `Sanctum` flag on tile; placed on neutral or noble owners (per alpha config)
- `InnerTile` flag: tiles structurally inside a sanctum-bounded cluster
- **Adjacency recomputation through portals**: extend `BoardSystem.GetNeighbors` (and `CalculateAdjacency`) to incorporate portal traversal:
  - Standard king (or manhattan-2) movement still applies for ordinary steps
  - A **revealed sanctum** is "free" — its own neighbors collapse into a single set on both sides of the portal (inner and outer)
  - An **unrevealed sanctum** blocks adjacency (it's a wall)
  - Inner tiles whose only path to outer tiles is through unrevealed sanctums are *unreachable* (cannot be targeted by adjacency-using effects)
- **Reveal/unreveal triggers recomputation**: when a sanctum's `IsRevealed` changes (reveal, or Brat un-reveal), recalculate `AdjacencyCount` for all already-revealed neighbors that could be affected
- **Click-targeting**: inner tiles can be clicked only when reachable (i.e., at least one path of revealed sanctums exists between them and an outer-anchor); enforce in `GameRunner.ProcessReveal`
- **Card targeting per-card** (decided case-by-case; document rule per card):
  - **Default for line/area cards**: an unrevealed sanctum blocks the line; revealed sanctums are traversable
  - **Gaze**: from an outer tile, cannot see inner tiles unless its line crosses a revealed sanctum first (and vice versa from inner to outer)
  - **Fetch**: same line rule as Gaze
  - **Spritz / Eavesdrop / Tingle / Peek**: respect reachability — can't single-target an unreachable inner tile
  - **Brush / Sweep / Argue (area)**: only affect reachable tiles within the area
  - **Brat (revealed-tile target)**: legal on a revealed inner tile (which by definition was reachable when revealed); after un-reveal, the tile may become unreachable again

**Tests** (~14):
- Adjacency: revealed sanctum's neighbors include both inner and outer perimeter
- Adjacency: unrevealed sanctum doesn't bridge inner/outer
- Adjacency recomputes when a sanctum is revealed (revealed-tile adjacency counts update)
- Adjacency recomputes when a sanctum is un-revealed via Brat
- Reachability: inner tile reachable iff a path of revealed sanctums exists
- Unreachable inner tile: click is rejected
- Unreachable inner tile: Spritz/Eavesdrop/Tingle reject
- Gaze line stops at unrevealed sanctum
- Gaze line passes through revealed sanctum (sees inner tiles)
- Fetch line same behavior
- Sweep area excludes unreachable inner tiles
- Multiple sanctums route to different reachability sets correctly
- Inner tile reveal counts toward win as normal once accessible
- Manhattan-2 + sanctum: combined rule respects portal traversal (verify against alpha)

**Status**: Complete (459 tests; manhattan-2 portal-bonus rule deferred)

---

### Milestone 42: Reasoning AI
**Goal**: Monte Carlo + hill-climbing AI that simulates board states and picks reveals minimizing risk to itself (and, where applicable, maximizing player frustration). The most complex single component.

**Tasks**:
- Constraint propagation pass (mirroring NoGuess) to mark guaranteed-safe and guaranteed-rival tiles
- Monte Carlo: sample N random ownership assignments consistent with revealed clues, weight by frequency
- Hill climbing: starting from a candidate reveal set, iteratively swap in higher-priority tiles (priority = expected value via points + safety)
- Exclusion logic: tiles with adjacency-info that contradict an ownership assignment are skipped during MC sampling
- Honor `rivalNeverMines`, lounging nobles (count as nobles), sanctum reachability
- Performance: cap MC samples to keep turn under ~200ms

**Tests** (~12):
- Reasoning AI never reveals a guaranteed noble (regular or lounging) when `rivalNeverMines = true`
- Reasoning AI prefers a guaranteed rival when one exists
- MC sampling produces sensible probabilities on a small constructed board
- Hill climbing improves on initial candidate
- Honors `rivalNeverMines`
- Skips unreachable inner tiles (treats them as non-targets)
- Performance smoke test: turn completes in < 500ms on 10×10 board
- Determinism with seeded RNG

**Status**: Complete (471 tests; hill-climbing tension reduction deferred — see stretch goals)

---

### Milestone 43: Directional Cards (Gaze, Fetch)
**Goal**: 4-direction targeted cards that scan a row/column for the first matching tile.

**Tasks**:
- `Gaze` (4 variants — ↑↓←→): from a target tile, scan in direction; annotate the first unrevealed rival tile encountered with exact owner
- `Fetch` (4 variants — ↑↓←→): from a target tile, scan in direction; reveal the first instance of the most-common owner type in that direction
  - Reveals end the turn if non-player (existing reveal-card pattern)
- Targeting UI: card-then-direction-then-tile, OR origin-then-direction picker
- Add to reward pool
- **Enhanced effects deferred to M53** (logic-change category)

**Tests** (~12):
- Gaze ↑: finds the first rival above the target
- Gaze: handles "no rival in direction" gracefully (no-op or skip annotation)
- Gaze: respects unrevealed-only
- Fetch ↑: reveals first instance of most-common owner type in direction
- Fetch: tie-breaking on most-common
- Each direction works (4 tests) — parameterized
- Cards in reward pool

**Status**: Complete (489 tests; targeting UI deferred to Stage-5-UI milestone)

---

### Milestone 44: Pose + Taunt
**Goal**: Two interaction-twist cards that depend on Stage 5 mechanics (courtiers, AI intent).

**Tasks**:
- **Pose** (alpha: Donut): chooses an unrevealed player tile at random; spawn a courtier on it (depends on M39). Player tile is unaffected; courtier will move when interacted with.
- **Taunt**: select N tiles (default 4); status effect: rival's turn ends if they reveal `N-1` of the tagged tiles
  - Status effect with `tauntPositions: Position[]` and `tauntRequiredReveals: int`
  - Decremented as rival reveals tagged tiles; trigger ends rival turn early
- **Enhanced effects deferred to M52 (Pose: numerical) and M53 (Taunt: logic)**

**Tests** (~8):
- Pose: spawns courtier on target player tile
- Pose: tile retains underlying ownership
- Pose: courtier moves correctly when interacted with
- Taunt: status effect created with N tagged positions
- Taunt: rival reveals N-1 tagged tiles → rival turn ends
- Taunt: rival reveals N (or fewer) without trigger → no early end
- Cards in reward pool

**Status**: Complete (500 tests; targeting UI deferred to Stage-5-UI milestone)

---

### Milestone 45: Stage 5 Equipment (Mop, Espresso)
**Goal**: Two equipment items requiring Stage 5 systems (courtiers, Coffee prereq).

**Tasks**:
- **Mop**: when a courtier is cleaned, draw 1 card. Hook into courtier-clean event in M39
- **Espresso**: prereq Coffee. At turn start, draw an extra card and auto-play it (if affordable). Auto-play picks a non-targeted card; if none, no-op
  - Hook into `EquipmentSystem.ApplyOnTurnStart`
  - Auto-play picks the lowest-cost playable card (TBD against alpha's selection rule)

**Tests** (~6):
- Mop: cleaning a courtier draws 1 card
- Mop: revealing a non-courtier tile doesn't draw
- Espresso: at turn start, draws + auto-plays
- Espresso: skips if no auto-playable card
- Espresso: prereq Coffee enforced in offerings (M47 dependency)

**Status**: Not Started

---

### Milestone 46: Stage 4 Stretch Equipment (No Prereqs)
**Goal**: 8 equipment items deferred from Stage 4 that don't need Stage 5 mechanics.

**Items**:
- **Hyperfocus**: at floor start (turn 1 only), put 1 random net-cost-0 card from persistent deck into hand
- **Choker**: rival turn ends early when 5 unrevealed tiles remain in the rival's selection
- **Mirror**: at floor start, reveal 1 random rival tile + add adjacency annotations to its neighbors (player adjacency info)
- **Busy Canary**: at floor start, run up to 2 Peek-style cross scans for nobles on random positions
- **Double Broom**: when a tile is revealed, Brush 2 random adjacent unrevealed tiles
- **Broom Closet**: on acquisition, remove all Spritz from persistent deck and add 3 Sweep cards
- **Cocktail**: on acquisition, remove all Scurry from persistent deck and add 2 random bonus-spoon cards
- **Novel**: on acquisition, replace all Recall cards in persistent deck with doubly-upgraded Sarcastic Recall

**Tasks**:
- One implementation per item; new triggers as needed:
  - `OnTileReveal` hook (Double Broom)
  - `OnFloorStart` extensions (Hyperfocus, Mirror, Busy Canary)
  - `ApplyOnAcquisition` extensions (Broom Closet, Cocktail, Novel)
- Add to offering pool

**Tests** (~12):
- Each item: one focused happy-path test plus one edge case (e.g., Choker with <5 tiles remaining; Hyperfocus with no cost-0 cards)

**Status**: Not Started

---

### Milestone 47: Equipment Prerequisite Chains
**Goal**: 10 chained equipment items + offering filter that respects prerequisites.

**Items** (with prereqs):
- **Tea** (prereq: Frilly Dress): unlimited neutral reveals on turn 1 (replaces Frilly Dress's "first 4")
- **Mascara** (prereq: Eyeshadow): +2 Distraction at turn start (Eyeshadow stacks for +3 total)
- **Pockets** (prereq: Handbag): draw +3 cards on turn 1 (replaces Handbag's +2)
- **Mated Pair** (prereq: Dust Bunny): reveal 2 player tiles at floor start
- **Baby Bunny** (prereq: Mated Pair): reveal 3 player tiles at floor start
- **Triple Broom** (prereq: Double Broom): Brush 3 random adjacent on reveal
- **Quadruple Broom** (prereq: Triple Broom): Brush 4 random adjacent on reveal
- **DIY Gel** (prereq: Progesterone): all future cards added to deck are auto-enhanced
- **Geode** (prereq: Crystal Ball): playing Tingle draws a card
- **Disco Ball** (prereq: Geode): on acquisition, add 2 doubly-upgraded Tingles
- **Fanfic** (prereq: Novel): playing Sarcastic Recall draws a card and costs 1 copper
- **Favor** (prereq: Tea + Cocktail): win the floor with 1 player tile remaining unrevealed
- **Espresso** (prereq: Coffee): handled in M45 — verify the prereq filter covers it

**Tasks**:
- `Equipment.Prereqs: IReadOnlyList<EquipmentEffectType>`
- `GenerateEquipmentOptions` filters offerings to require all prereqs are owned
- Same filter applies to shop equipment slots (M32)
- Implement each chained effect (most are extensions of base behaviors)

**Tests** (~15):
- Prereq filter: chained item not offered without prereq
- Prereq filter: offered after prereq acquired
- Multi-prereq (Favor): both required
- Each chained effect: one focused test
- Chain replacement: Pockets replaces Handbag's behavior (or stacks — verify against alpha)

**Status**: Not Started

---

### Milestone 48: Replace Placeholder Levels 1–8 with Alpha Configs
**Goal**: Import the alpha's `levels-config.json` for floors 1–8 verbatim; retire our placeholder configs.

**Tasks**:
- Replace `Level1` through `Level8` in `LevelConfigs` with the alpha's exact configs (board sizes, tile counts, unused locations, special tile placements, special behaviors)
- Add `RivalAi` field on each (Random for L1–L4, Conservative for L5–L8)
- Add `rivalNeverMines`, `rivalMineProtection`, `rivalPlacesMines`, `initialRivalReveal` mappings
- Update existing tests that hardcode old configs:
  - `Level3Board_HasCenterHole`, `Level2Board_HasNoble` — match new positions
  - Reward-flow tests (M33) — re-derive expected phases per alpha's `uponFinish`
  - `BoardLayoutTests` — new dimensions may exceed current `MaxGridWidthPx/HeightPx`; bump constants and `BoardMargin.custom_minimum_size`
- Floor-end summary tests for L8 (now has courtiers + soirées)

**Tests** (~6):
- Each level config matches alpha (parameterized): dimensions, tile counts, special tiles, special behaviors
- Reward flow per level matches alpha's `uponFinish`
- L8 has courtiers and soirées as configured

**Status**: Not Started

---

### Milestone 49: Floors 9–15 (Mechanic Ramp)
**Goal**: Levels 9 through 15 — gradual introduction of more courtiers, soirées, and lounging nobles, plus the first Reasoning AI floor (L14).

**Tasks**:
- Add `Level9` through `Level15` `LevelConfig`s from alpha
- L9: weird spacing, manhattan-2, more soirées
- L10: easy banquets (lots of courtiers/soirées in corners)
- L11: hard banquets
- L12: lounging nobility (introduces lounging nobles via `rivalPlacesMines`)
- L13: boss #3 (manhattan-2 + many soirées)
- L14: rival places lounging nobles (first Reasoning AI floor)
- L15: all but sanctums

**Tests** (~10):
- Each level config matches alpha (parameterized)
- AI assignment is correct per level (Conservative vs. Reasoning)
- Special tile counts and behaviors line up

**Status**: Not Started

---

### Milestone 50: Floors 16–21 (Sanctums + Final Boss)
**Goal**: Levels 16 through 21 — sanctums introduced, full mechanic combinations, final boss.

**Tasks**:
- Add `Level16` through `Level21` `LevelConfig`s from alpha
- L16: introduces sanctums
- L17: boss #4 (everything but sanctums)
- L18: several manhattan-2 sanctums
- L19: everything but adjacency
- L20: gold mine
- L21: final boss (all mechanics, 10×10)

**Tests** (~8):
- Each level config matches alpha (parameterized)
- L21 marks `winTheGame = true` (`UponFinish.NextLevelId == null`)
- Sanctum + inner cluster integrity (pairs of sanctums route to inner tiles correctly)

**Status**: Not Started

---

### Milestone 51: Stage 5 Godot UI
**Goal**: Render every special tile and AI-related state. Mostly TileView extensions plus a few HUD additions.

**Tasks**:
- TileView visual states:
  - **Courtier**: distinct color/shape overlay; arrow indicating MoveTarget
  - **Soirée**: distinct icon; spawn-pulse animation hint (animation deferred to Stage 6)
  - **Lounging Noble**: noble-styled overlay on player/neutral tile (e.g., noble shape inset over the underlying owner shape)
  - **Sanctum**: portal-styled overlay; visually distinct between revealed/unrevealed states
  - **Inner tile (unreachable)**: dimmed/grayed; padlock or chain icon
  - **Inner tile (reachable)**: normal rendering
  - **Destroyed**: already exists (M22)
- Rival intent indicators on tiles: small number badge showing `rivalIntentPoints[pos]` when > 0 (toggleable via debug or always-on?)
- HUD: rival AI type display ("Rival AI: Conservative" badge)
- HUD: `rivalMineProtectionCount` indicator
- Status effect entries for `rivalNeverMines`, `rivalPlacesMines`, taunt
- Targeting UI for directional cards (Gaze/Fetch arrow picker)

**Status**: Not Started

---

### Milestone 52: Enhanced Effects — Numerical Bumps
**Goal**: Audit and implement enhanced versions of every card whose enhancement is a numerical or area bump. Each row is alpha-confirmed (file path: `sweep-the-dungeons/src/game/cards/<file>.ts`). Status: ✓ = already implemented; □ = needs implementation.

**Cards** (10 total):

| Card | Alpha File | Enhanced Effect | Status |
|---|---|---|---|
| Breathe | `options.ts` | Draws **5** cards (vs 3) | □ |
| Lock In | `monster.ts` | Draws **4** cards (vs 2) | □ |
| Sweep | `sweep.ts` | Area range **3** (7×7, vs range 2 / 5×5) | □ |
| Pose | `donut.ts` | Spawns **2** courtiers (vs 1) | □ (introduced in M44; enhanced here) |
| Twirl | `twirl.ts` | Gains **5** copper (vs 3) | ✓ |
| Brat | `brat.ts` | Gains **+2** copper (vs +0) | ✓ |
| Ramble | `ramble.ts` | Adds **4** distractions (vs 2) | ✓ |
| Read | `burger.ts` | Adds **3** stacks (vs 2) | ✓ |
| Hydrate | `iceCream.ts` | Adds **3** stacks (vs 2) | ✓ |
| Adopt | `carrots.ts` | Adds **3** stacks (vs 2) | ✓ |

**Tasks**:
- For each □ card: implement the enhanced branch in `CardEffectSystem.Execute<Card>` and update the card's Description to mention the enhanced behavior.
- For each ✓ card: verify the implemented behavior matches the alpha row above; add a verification test if not already covered.

**Tests** (~12):
- 4 new enhanced-vs-base tests for the □ cards (Breathe, Lock In, Sweep, Pose)
- ~6 verification tests for the ✓ cards if not already present (Twirl, Brat, Ramble, Read, Hydrate, Adopt)
- Spot-check: enhanced description text matches actual behavior on each card

**Status**: Not Started

---

### Milestone 53: Enhanced Effects — Logic Changes
**Goal**: Audit and implement enhanced versions of every card whose enhancement changes the effect's logic. Includes "no enhanced effect in alpha" cards documented for completeness. Status: ✓ = already implemented; □ = needs implementation; — = no enhanced behavior in alpha.

**Cards** (22 total — every remaining NAME_MAPPING.md card):

| Card | Alpha File | Enhanced Effect | Status |
|---|---|---|---|
| Spritz | `scout.ts` | Always defuses any lounging-noble overlay on target; **also** scouts a random adjacent unrevealed tile (clean + annotate; defuse if lounging-noble) | □ |
| Tingle | `report.ts` | Adds **player adjacency info** to the annotated tile (in addition to the rival/noble owner annotation) | □ |
| Rendezvous | `tryst.ts` | Player **picks the target** for the player-side reveal (vs random); also adds annotations on revealed tiles | □ |
| Brush | `brush.ts` | Applies the per-tile exclusion **twice** (2 iterations, each picks a fresh random non-owner) | □ |
| Caffeinate | `energized.ts` | **Does not exhaust** (vs base which exhausts) | □ |
| Taunt | `taunt.ts` | Targets **3** tiles (vs 4), requires **2** rival reveals to trigger (vs 3) — easier to satisfy | □ (introduced in M44; enhanced here) |
| Gaze | `gaze.ts` | Also detects **lounging nobles** in the line; scans further (no early stop after first rival) | □ (introduced in M43; enhanced here) |
| Fetch | `fetch.ts` | **Draws 1 card** in addition to the reveal | □ (introduced in M43; enhanced here) |
| Recall (Imperious) | `imperiousInstructions.ts` | Adds owner-subset annotation **excluding nobles** to all affected tiles | ✓ |
| Recall (Vague) | `vagueInstructions.ts` | More guaranteed-target draws (5 vs 3) — implementation per M28 | ✓ |
| Recall (Sarcasm) | `sarcasticInstructions.ts` | **Refunds 1 spoon** if any other Recall already played this floor | ✓ |
| Argue | `argument.ts` | **Draws 1 card** | ✓ |
| Eavesdrop | `eavesdropping.ts` | Annotates **exact owner** (vs binary "you/not-you") and adds **full adjacency info** (player + rival + neutral + noble counts) | ✓ |
| Peek | `canary.ts` | Scans **3×3 area** (vs cross-shape) | ✓ |
| Explode | `emanation.ts` | **Does not** add a Mollify card or Complaint stack | ✓ |
| Deliver | `snipSnip.ts` | Adds **noble + player adjacency info** annotation regardless of whether the target was a noble | ✓ |
| Accept Help | `horse.ts` | **Annotates** all safest-type tiles instead of revealing them (and annotates non-safest with the appropriate exclusion) | ✓ |
| Glaze | `underwire.ts` | **Does not exhaust** | ✓ |
| Mask | `cardSystem.ts` (inline) | The **Mask card itself does not exhaust** (the selected card still always exhausts) | ✓ |
| Nap | `cardSystem.ts` (inline) | Gains spoons equal to the **retrieved card's cost** | ✓ |
| Scurry | `scurry.ts` | Best of 3, not best of 2 | ✓ |
| Mollify | (alpha "Evidence" is a different mechanic — junk card) | no enhanced version, can only generate during floor, never in permanent deck | n/a |

**Tasks**:
- For each □ card: implement the enhanced branch in `CardEffectSystem.Execute<Card>` (or `PlayMaskedCard`/`PlayNap` for the inline ones) and update the card's Description.
- For each ✓ card: verify implemented behavior matches alpha row; add verification test if missing.
- For each — card: confirm with the user whether to implement a designed enhanced effect or leave un-enhanced. (The proposals listed are starting points only.)

**Tests** (~22):
- 8 new enhanced-vs-base tests for □ cards (Spritz, Tingle, Rendezvous, Brush, Caffeinate, Taunt, Gaze, Fetch)
- ~12 verification tests for ✓ cards (one per card if not already covered)
- Decision recorded for — cards (Scurry, Mollify) before implementing or skipping

**Status**: Not Started

---

## Stretch Goals

| Item | Notes |
|---|---|
| Reasoning AI performance tuning | If MC sampling proves too slow on 10×10 boards, profile and optimize (memoization, narrower sample space). Defer to Stage 6 if not blocking. |
| Sanctum cluster auto-generation | Alpha may have specific rules for how inner clusters are grouped; if our placement diverges, document and either fix or accept. |
| Courtier animation | Movement/scurry animation for courtiers. Deferred to Stage 6. |
| Reasoning AI deterministic-seed tests | Testing MC behavior is inherently flaky; focus on bounds rather than exact picks. |

---

## Stage 5 Summary

| Milestone | Key Systems | Est. Tests |
|---|---|---|
| M35: Intent Points | Per-tile weighted intent + decay | ~10 |
| M36: AI Framework + NoGuess | IRivalAi, registry, two implementations | ~10 |
| M37: Conservative AI | Weighted preference + noble avoidance | ~6 |
| M38: Special Tile Foundation | Generic flags, placement strategies | ~6 |
| M39: Courtiers + Soirées | Movement + spawning | ~14 |
| M40: Lounging Nobles + Floor-Win on Rival Noble Reveal | Noble overlay, protection, status check | ~14 |
| M41: Sanctums + Inner | Portal adjacency w/ recompute, reachability gating | ~14 |
| M42: Reasoning AI | MC + hill climb + exclusion | ~12 |
| M43: Directional Cards | Gaze + Fetch (8 variants) | ~12 |
| M44: Pose + Taunt | Courtier spawn card + intent-end card | ~8 |
| M45: Stage 5 Equipment | Mop + Espresso | ~6 |
| M46: Stage 4 Stretch Equipment | 8 no-prereq items | ~12 |
| M47: Equipment Prereq Chain | 12 chained items + filter | ~15 |
| M48: Replace L1–8 Configs | Import alpha configs verbatim | ~6 |
| M49: Floors 9–15 | Mechanic ramp | ~10 |
| M50: Floors 16–21 | Sanctums + boss | ~8 |
| M51: Stage 5 Godot UI | Special tiles, AI display, intent badges | manual |
| M52: Enhanced Effects — Numerical | Audit + implement 10 cards (Breathe/LockIn/Sweep/Pose new; rest verify) | ~12 |
| M53: Enhanced Effects — Logic | Audit + implement 22 cards (Spritz/Tingle/Rendezvous/Brush/Caffeinate/Taunt/Gaze/Fetch new; rest verify) | ~22 |
| **Total** | | **~191** |

**Projected total tests at Stage 5 end**: ~564 (373 existing + ~191 new)

**Deferred to Stage 6**:
- All animations (tile reveal, courtier movement, lounging-noble reveal effect, sanctum portal effects, rival turn sequence, card animations)
- Sound effects
- Reasoning AI performance optimization (if needed)
- Save/load
- Help/tooltip system
- Build pipeline (Windows export, web export)
- Theme polish, fonts, accessibility

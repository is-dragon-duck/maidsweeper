# Maidsweeper: Godot C# Port Implementation Plan

## Context

Porting "Sweep The Dungeons" (browser-based React/TypeScript/Zustand alpha) to Godot 4.6 C#. The alpha has a complete 21-level campaign and has been playtested extensively. A prior partial port to Godot GDScript ("sweeping-toy") implemented the basic structure with starter cards across 3 floors. This port starts fresh in C# targeting a polished beta.

### What We're Porting

**Core Loop**: Grid of tiles (player/rival/neutral/noble/empty). Reveal tiles to find all yours. Use cards for imperfect information. Avoid nobles. Turn-based: player acts, then AI rival reveals.

**Full Scope**: ~30+ cards, ~20+ equipment items, 21-level campaign, 3 AI types, clue pip system, player annotations, special tiles (extraDirty/goblin/lair/surfaceMine/sanctum), copper/shops, status effects, card upgrades, animations.

### Starter Deck (10 cards)
These are the cards every run begins with. They're the foundation of the core puzzle:
- **1x Imperious Instructions** (cost 2): Distributes clue pips across tiles via bag draw — the primary information-gathering card
- **2x Scurry** (cost 1): Select 2 unrevealed tiles, auto-reveals the safer one
- **3x Spritz** (cost 1): Target a tile, learn if it's safe or dangerous (scout)
- **3x Tingle** (cost 1): Marks a random unrevealed rival/noble tile with its owner type
- **1x Twirl** (cost 3, exhaust): Gain 3 copper (dead card until shops in Stage 4, by design)

---

## Porting Strategy

**Horizontal slices**: Every stage produces a playable game. Each stage deepens the experience rather than building isolated systems. Logic and UI are built together for each feature.

**Architecture**: Pure C# game logic classes (no Godot dependencies) for testability, with thin Godot node wrappers for rendering and input. Signal-driven communication between nodes, matching the pattern established in the GDScript port.

**Faithful port first**: Resist redesigning mechanics during porting. Get it working like the alpha, then rename/polish for beta.

---

## Stage 1: First Playable Floor
**Goal**: Play a single floor of Minesweeper-with-cards that feels like the real game. All 5 starter card types work end-to-end with visible information feedback.
**Status**: Complete (Milestones 1-10 done — headless game logic + Godot UI, 90 tests passing)

### What the player experiences
Launch the game, see a 6x5 grid. Draw 5 cards. Play Spritz on a tile to learn it's safe. Play Imperious Instructions and see colored pips appear on tiles. Click a tile to reveal it — see an adjacency number. Non-player tile ends your turn. Simple AI reveals a tile. New turn begins. Find all your tiles to win.

### Game Logic (C#, no Godot deps)
- [ ] Core types: `Position`, `Tile` (owner, revealed, revealedBy, adjacencyCount, annotations, specialTiles), `Board`, `Card`, `GameState`
- [ ] Board system: `CreateBoard` (from tile counts), `GetTile`, `GetNeighbors` (standard 8-neighbor), `CalculateAdjacency`, `RevealTile`
- [ ] Level 1 config: 6x5, 12 player / 10 rival / 8 neutral / 0 noble
- [ ] Deck system: draw pile, hand, discard, shuffle-on-empty, draw N cards, discard hand
- [ ] Spoons system: 3 spoons per turn, cards cost spoons to play
- [ ] Card effect: Spritz (scout) — single-tile targeting, marks safe/dangerous annotation
- [ ] Card effect: Imperious Instructions — bag-draw pip distribution across tiles (the full clue system with ClueResult tracking)
- [ ] Card effect: Scurry — two-tile targeting, auto-reveals safer tile
- [ ] Card effect: Tingle — marks random unrevealed rival/noble tile with its type (instant, animation later)
- [ ] Card effect: Twirl — draw 2 cards, exhaust
- [ ] Turn flow: player turn → discard hand → AI turn (reveal 1 random rival tile) → draw 5, reset spoons
- [ ] Win: all player tiles revealed. Lose: noble revealed (not possible on level 1, but wire it up)
- [ ] Game status tracking (playing / won / lost)
- [ ] Unit tests for board, adjacency, card effects, deck cycling, win/lose detection

### Godot UI
- [ ] Scene structure: Main scene, Board node, Tile scene, Hand display, HUD
- [ ] Tile grid: render tiles at correct positions, show unrevealed/revealed states
- [ ] Revealed tile: show adjacency number, colored by owner type
- [ ] Tile interaction: left-click to reveal (or target for card), right-click for basic annotation cycling
- [ ] Clue pip rendering on tiles (colored dots showing clue results from Instructions cards)
- [ ] Safe/dangerous annotation display (from Spritz)
- [ ] Owner type annotation display (from Tingle)
- [ ] Hand display: show cards with name/cost, click to play
- [ ] Card targeting UI: highlight that a target is needed, click tile to select
- [ ] HUD: spoons count, deck/discard pile sizes, turn indicator
- [ ] Tile count display: show unrevealed counts per type (player/rival/neutral/noble)
- [ ] Win/lose display

### Success Criteria
- A floor can be played start to finish using all 5 starter card types
- Clue pips from Imperious Instructions are visible and useful for deduction
- Spritz annotations help identify safe tiles
- Adjacency numbers display correctly after reveal
- The core puzzle loop is recognizable: gather info → deduce → reveal → repeat

---

## Stage 2: Three-Floor Campaign
**Goal**: Play through 3 floors with increasing difficulty and card rewards between floors. Matches the GDScript port's target scope.
**Status**: Complete (Milestones 11-15 done — renames, level infrastructure, 6 reward cards, campaign system, campaign UI, 135 tests passing)

### What the player experiences
Beat floor 1, get offered 3 new cards to add to your deck. Play floor 2 (now with 1 noble and a dirty tile). Beat it, pick another card. Floor 3 is bigger with 3 nobles and a center hole. Win all 3 to complete the run.

### Game Logic
- [ ] Level config system: data-driven level definitions (at minimum levels 1-3)
- [ ] Unused locations (holes in the grid — empty tiles)
- [ ] ExtraDirty special tile: must be clicked twice to reveal (first click cleans, second reveals)
- [ ] Noble tile: revealing one loses the game
- [ ] Persistent deck: cards carry over between floors
- [ ] Reset per floor: shuffle persistent deck into new draw pile, clear hand/discard/exhaust
- [ ] Card reward pool: subset of available non-starter cards (port enough for 3 choices per floor)
- [ ] Reward flow: floor complete → pick 1 of 3 cards → next floor
- [ ] Level advancement: create new board from next level's config

### New Cards (reward pool for early floors)
- [ ] At least 6-8 reward cards so each floor offers meaningful choices
- [ ] Brush (cost 1): target 3x3 area, foreach tile pick one of its non-owners at random and annotate it to exclude that non-owner
- [ ] Sweep (cost 1): target 5x5 area, remove dirt from all tiles in area
- [ ] Caffeinate (cost 1, exhaust): gain 2 spoons
- [ ] Breathe (cost 1): draw 3 cards
- [ ] Lock In (cost 0, exhaust): draw 2 cards
- [ ] Rendezvous (cost 1): reveal one of your tiles at random, but get *rival* adjacency info on it; then reveal one of your rival's tiles at random, but get *player* adjacency info on it

### Godot UI
- [ ] Card selection screen: show 3 cards with names/descriptions, click to pick, option to skip
- [ ] Floor transition: clear board, show reward, load next floor
- [ ] Start menu: New Game button
- [ ] ExtraDirty visual: tile looks different when dirty, reverts to normal after cleaning
- [ ] Victory screen after completing all 3 floors

### Success Criteria
- Complete 3-floor run is playable and feels like the alpha's early game
- Card rewards add meaningful choices between floors
- Dirty tiles and nobles add challenge in floors 2-3
- Persistent deck grows across floors

---

## Stage 3: Deeper Deckbuilder
**Goal**: Full deckbuilder feel with card upgrades, more cards, player annotation depth, and the card upgrade/removal system. Expand to ~8 floors.
**Status**: Complete (Milestones 16-24 done — 11 new cards, upgrades, annotations, shape-coded UI, saturation, 8 floors, Manhattan-2, overlay manager, pile viewing, debug tools, 257 tests passing)

### What the player experiences
More card variety in rewards. Upgrade screen offers cost reduction, enhanced effects, or card removal. Player can right-click tiles to cycle through owner-possibility annotations. Annotation views let you track which tiles could be which type.

### Game Logic
- [x] Card upgrades: enhanced (stronger effect), bonus-spoon (+1 spoon on play)
- [x] Upgrade options: remove card, bonus spoon, enhance effect
- [x] Exhaust mechanic: some cards are removed from play for the floor after use
- [x] Expand card pool with targeting cards: Argue, Accept Help, Eavesdrop, Peek, Explode, Deliver, Brat
- [x] Expand card pool with immediate cards: Ramble, Glaze, Mollify
- [x] Mask card: play a card from hand for free (both exhaust)
- [x] Nap card: retrieve a card from exhaust pile
- [x] Player annotation system: per-owner-view possibility tracking (player/rival/neutral/noble views)
- [x] Saturation detection: check mark on adjacency badge when all matching neighbors revealed
- [x] Level configs for floors 4-8 (introduce manhattan-2 adjacency, more special tiles)
- [x] Manhattan-2 adjacency rule (tiles within manhattan distance 2)
- [x] Tests for upgrades, new card effects, annotation logic (257 tests)

### Godot UI
- [x] Upgrade selection screen: 3 upgrade options, card removal flow
- [x] Card removal UI: browse persistent deck, select card to remove
- [x] Pile viewing: browse deck/discard/exhaust piles
- [x] Enhanced/upgraded card visual indicators
- [x] Player annotation rendering: shape-coded per-owner possibility markers on tiles
- [x] Annotation view switching UI (P/R/N/X buttons with perspective crossouts)
- [x] Saturation check mark on revealed tile adjacency badges
- [x] Targeting UI refinements: area preview on hover, multi-tile highlight, card-selection targeting

### Success Criteria
- Card upgrades meaningfully change gameplay
- Player annotation system enables deeper deductive reasoning
- Masking and Nap add strategic deck manipulation
- ~8 floors playable with good variety

---

## Stage 4: Equipment, Economy & Status Effects
**Goal**: Equipment adds persistent passive effects, copper currency enables shops, status effects layer on temporary modifiers. Full reward flow between floors.
**Status**: Not Started

### What the player experiences
After some floors, pick equipment that changes how you play (Frilly Dress lets neutrals not end your first turn, Mop draws cards when cleaning goblins). Earn copper from unrevealed rival tiles. Visit shops to buy cards, equipment, or remove cards. Status effects show in the HUD.

### Game Logic
- [ ] Equipment data model: name, description, passive effect, prerequisites
- [ ] Port equipment items in priority order (Frilly Dress, Mop, Caffeinated, Glasses, Boots, etc.)
- [ ] Equipment triggers: on floor start, on turn start, on tile reveal, on card play, etc.
- [ ] Status effect system: add/remove/decrement, icons, tooltips
- [ ] Status effects: grace, underwire_protection, ramble_active, burger/ice_cream/carrots stacks, horse_discount, etc.
- [ ] Copper currency: earned from unrevealed rival tiles when floor is won
- [ ] Evidence card copper penalty
- [ ] Shop system: generate offerings, progressive pricing, purchase flow
- [ ] Equipment selection screen: 3 options with prerequisite filtering
- [ ] Reward flow sequence: card → upgrade → equipment → shop → next floor (based on level config)
- [ ] Tests for equipment triggers, status effect lifecycle, copper/shop math

### Godot UI
- [ ] Equipment selection screen
- [ ] Shop screen: items, prices, copper display, purchase/sell
- [ ] Status effect bar: icons with tooltips, pulsing for new effects
- [ ] Equipment display in HUD
- [ ] Copper counter in HUD

### Success Criteria
- Equipment meaningfully changes gameplay strategies
- Shop provides interesting copper-spending decisions
- Status effects are visible and comprehensible
- Full reward flow works for any level configuration

---

## Stage 5: Full Campaign
**Goal**: All 21 levels playable with all content — every card, every equipment item, every special tile, every AI type.
**Status**: Not Started

### What the player experiences
Full 21-floor campaign with escalating complexity. Goblin tiles that move when you try to reveal them. Lairs that spawn goblins after rival turns. Surface mines the rival places on your tiles. Sanctum portals that gate access to inner tiles. Smart AI that avoids mines and reasons about the board. The complete game.

### Game Logic
- [ ] All 21 level configs
- [ ] Special tiles: goblin (move on interact), lair (spawn goblins after rival turn), surfaceMine (destroys tile), sanctum + inner tiles (portal adjacency)
- [ ] Goblin mechanics: predetermined movement targets, clean-and-move, collision, lair spawning
- [ ] Surface mine mechanics: cleaning, explosion, rival placement after turns
- [ ] Sanctum mechanics: inner tile access gating, portal adjacency for neighbors
- [ ] NoGuess AI: constraint satisfaction — never reveals a noble
- [ ] Conservative AI: weighted tile preference
- [ ] Reasoning AI: Monte Carlo simulation, hill climbing, exclusion logic (stretch — can defer)
- [ ] Rival intent point system: base distractions + equipment modifiers
- [ ] Special behaviors: rivalNeverMines, rivalMineProtection, rivalPlacesMines, initialRivalReveal
- [ ] All remaining cards not yet ported (Gaze variants, Fetch variants, Taunt, Donut, food cards, etc.)
- [ ] All remaining equipment not yet ported
- [ ] Espresso equipment flow (draw + auto-play on turn start)
- [ ] Tests for special tiles, AI behavior, all card/equipment interactions

### Godot UI
- [ ] Visual states for all special tiles: goblin, lair, surfaceMine, sanctum, inner tile, destroyed
- [ ] Rival intent indicators on tiles
- [ ] Inner tile access indicators (locked/unlocked by sanctum reveal)
- [ ] AI type status effect display

### Success Criteria
- Full 21-floor run is completable
- All cards, equipment, and special tiles work correctly
- AI types behave distinctly and appropriately per level
- No game-breaking bugs across a full campaign

---

## Stage 6: Polish, Animations & Beta Readiness
**Goal**: Beta-quality experience with animations, audio, theming, and distribution readiness.
**Status**: Not Started

### Tasks
- [ ] Tile reveal animation (flip/fade)
- [ ] Tingle card animation (sequential tile marking sweep)
- [ ] Tryst card animation (sequential reveals)
- [ ] Rival turn animation (sequential reveals with timing)
- [ ] Card play/draw/discard animations
- [ ] Goblin movement and surface mine explosion animations
- [ ] Sound effects: tile reveal, card play, noble hit, level complete, card draw, goblin move
- [ ] Thematic rename pass: card/equipment names for evolved maid/court theme
- [ ] UI polish: consistent visual style, layout, hierarchy
- [ ] Keyboard shortcuts for common actions (end turn, cancel targeting)
- [ ] Settings screen (audio volume, annotation preferences)
- [ ] Save/load system for run persistence
- [ ] Debug controls (win level, give equipment/cards, skip to level, toggle flags)
- [ ] Help/tooltip system for cards and equipment
- [ ] Performance profiling
- [ ] Accessibility: colorblind palette options, font scaling
- [ ] Build pipeline (Windows export, possibly web)

### Success Criteria
- Game feels polished and intentional
- Animations are fast enough for rapid play, clear enough for comprehension
- Complete run from floor 1-21 is a satisfying experience
- Builds and runs standalone

---

## Architecture Notes

### C# Game Logic Layer (no Godot dependencies)
Core game logic lives in pure C# classes that can be unit tested without running Godot:
- `GameState` — immutable-style state object (create new state rather than mutate)
- `BoardSystem` — board creation, adjacency, reveal, special tile mechanics
- `CardSystem` — deck management, card play, effects
- `LevelConfig` — data-driven level definitions
- `AIController` — rival decision making

### Godot Node Layer
Thin wrappers that render state and capture input:
- Nodes read from GameState to update visuals
- Input events call into game logic, get new state back, re-render
- Signals for inter-node communication (matching GDScript port patterns)
- Tile scene: state node + view node (separation from GDScript port works well)

### Testing Strategy
- Unit tests for game logic using Godot's built-in test runner or a .NET test project
- Each stage includes tests for its new game logic
- Manual playtesting at each stage (the game is always playable)

---

## Notes

- **Theme evolution**: Card/equipment names from the alpha will be renamed during or after porting. Code is the authority for mechanics; names are mutable.
- **Alpha code is source of truth**: `.md` files in the alpha repo may be outdated. The TypeScript source code defines how mechanics actually work.
- **GDScript port as reference**: Useful for Godot-specific patterns (signal routing, tile view separation, scene structure) but C# port uses C# idioms.
- **Reasoning AI**: Most complex single system. Consider deferring to late Stage 5 or even Stage 6 if Conservative AI provides adequate challenge.

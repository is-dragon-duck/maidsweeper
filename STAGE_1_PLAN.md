# Stage 1: First Playable Floor — Detailed Plan

## Goal
Play a single floor (Level 1: 6x5, 12 player / 10 rival / 8 neutral / 0 mines) using all 5 starter card types with visible information feedback. The core deduction loop — gather info, reason, reveal — should be recognizable.

---

## Resolved: Twirl Card

Twirl matches the alpha: **cost 3, exhaust, gain 3 copper** (5 if enhanced). It will be a dead card until copper/shops arrive in Stage 4. This is by design — it's a hard card to play, most often finessed by expert players or used via Masking.

---

## Project Structure

```
maidsweeper/
  Maidsweeper.sln
  project.godot

  core/                              # Pure C# class library (no Godot deps)
    Maidsweeper.Core.csproj
    Models/
      Position.cs                    # Grid coordinate (row, col)
      TileOwner.cs                   # Enum: Player, Rival, Neutral, Mine
      Tile.cs                        # Owner, revealed, revealedBy, adjacency, annotations, specials
      Board.cs                       # Grid of tiles + metadata
      Card.cs                        # Name, cost, exhaust, effect type, enhanced/energyReduced
      CardEffect.cs                  # Enum/union of effect types
      GameState.cs                   # Board, deck, hand, discard, exhaust, energy, turn, status
      LevelConfig.cs                 # Grid dimensions, tile counts, special tiles, behaviors
      ClueResult.cs                  # Pip strength, affected tiles, clue ID
      Annotation.cs                  # Owner subset, clue results per tile
    Systems/
      BoardSystem.cs                 # CreateBoard, GetNeighbors, CalculateAdjacency, RevealTile
      DeckSystem.cs                  # DrawCards, DiscardHand, ShuffleDiscard, ExhaustCard
      CardEffectSystem.cs            # ExecuteSpritz, ExecuteInstructions, ExecuteScurry, etc.
      ClueSystem.cs                  # Bag-draw pip generation for Instructions cards
      TurnSystem.cs                  # StartTurn, EndTurn, RivalTurn, CheckWinLose
      GameRunner.cs                  # Top-level orchestrator: create game, process actions

  tests/
    Maidsweeper.Tests.csproj         # xUnit tests for Core
    BoardSystemTests.cs
    DeckSystemTests.cs
    CardEffectTests.cs
    ClueSystemTests.cs
    TurnSystemTests.cs
    GameRunnerTests.cs

  Scenes/                            # Godot scenes
    Main.tscn
    Board.tscn
    Tile.tscn
    CardUI.tscn

  Scripts/                           # Godot-dependent C# scripts
    BoardNode.cs                     # Renders Board state as tile grid
    TileNode.cs                      # Single tile: visual state + input
    TileView.cs                      # Tile visual rendering (colors, labels, overlays)
    HandDisplay.cs                   # Card hand rendering
    CardUINode.cs                    # Single card visual
    HUD.cs                           # Energy, deck/discard counts, tile counts, turn indicator
    GameController.cs                # Bridges Godot input/UI to GameRunner
    TargetingController.cs           # Card targeting mode (highlight, click-to-select)
```

---

## Milestones

### Milestone 1: Project Structure & Core Types
**AI writes**: Solution file, .csproj files, folder structure, all model classes
**Human does**: Open project in Godot, build once (ensures .csproj integration), run `dotnet build` to verify

#### Deliverables
- `Maidsweeper.sln` with three projects (Core, Godot, Tests)
- Core model types with no logic yet — just data definitions:
  - `Position`: row/col struct, equality, neighbor offsets
  - `TileOwner` enum: Player, Rival, Neutral, Mine
  - `Tile`: owner, revealed, revealedBy, adjacencyCount, annotations dict, special tile flags
  - `Board`: 2D tile array, width/height, level config ref
  - `Card`: id, name, cost, exhaust flag, effect type, enhanced flag, energyReduced flag
  - `CardEffectType` enum: Scout, Instructions, Scurry, Tingle, Twirl
  - `GameState`: board, hand, drawPile, discardPile, exhaustPile, energy, maxEnergy, currentPlayer, gameStatus, turnNumber
  - `LevelConfig`: width, height, playerCount, rivalCount, neutralCount, mineCount
  - `ClueResult`: tilePosition, pipStrength, allAffectedTiles, clueId
  - `Annotation`: ownerSubset (HashSet<TileOwner>), clueResults list
- xUnit test project that builds and runs (even if no real tests yet)
- Level 1 config defined as a static constant

#### Success criteria
- `dotnet build Maidsweeper.sln` succeeds
- `dotnet test` runs (0 tests, 0 failures)
- Godot project opens without errors

---

### Milestone 2: Board System + Tests
**AI writes**: BoardSystem implementation + tests
**Human does**: Run `dotnet test`, review test output

#### Deliverables
- `BoardSystem.CreateBoard(LevelConfig, Random)` — creates board with shuffled tile placement
  - Generates flat list of tile owners from config counts, shuffles, assigns to grid positions
  - No unused locations needed for Level 1
- `BoardSystem.GetNeighbors(Board, Position)` — returns 8-neighbor positions (king adjacency), clamped to grid bounds
- `BoardSystem.CalculateAdjacency(Board)` — for each tile, count neighbors matching the revealer's owner type
  - **Key asymmetry**: adjacency count is from the perspective of who reveals the tile, not the tile itself
  - Actually for Stage 1, adjacency = count of tiles matching the tile's own owner type among neighbors (standard minesweeper-style, but per-owner)
  - Need to verify exact alpha behavior before implementing
- `BoardSystem.RevealTile(Board, Position, TileOwner revealedBy)` — marks tile revealed, sets revealedBy, returns updated board
- Tests:
  - Board has correct total tiles (6x5 = 30)
  - Board has correct counts per owner type (12/10/8/0)
  - All tiles start unrevealed
  - GetNeighbors returns correct count (corners=3, edges=5, center=8)
  - RevealTile marks tile as revealed with correct revealedBy
  - Adjacency calculation is correct for known board layouts (use seeded Random)

#### Success criteria
- All board tests pass
- Edge cases covered (corners, edges, re-revealing already-revealed tile)

---

### Milestone 3: Deck & Energy System + Tests
**AI writes**: DeckSystem implementation + tests
**Human does**: Run `dotnet test`

#### Deliverables
- Starter deck definition: 1x Imperious Instructions (cost 2), 3x Spritz (cost 1), 3x Tingle (cost 1), 2x Scurry (cost 1), 1x Twirl (TBD)
- `DeckSystem.CreateStarterDeck()` — returns list of 10 starter cards
- `DeckSystem.ShuffleDeck(List<Card>, Random)` — Fisher-Yates shuffle
- `DeckSystem.DrawCards(GameState, int count)` — draw N cards from draw pile to hand
  - If draw pile empty, shuffle discard into draw pile first
  - If both empty, stop drawing
  - Draw from end of list (pop)
- `DeckSystem.DiscardHand(GameState)` — move all hand cards to discard pile
- `DeckSystem.ExhaustCard(GameState, Card)` — move card to exhaust pile instead of discard
- `DeckSystem.DiscardCard(GameState, Card)` — move single card from hand to discard
- Energy: `GameState.energy` starts at `maxEnergy` (3), decremented on card play
- `DeckSystem.CanPlayCard(GameState, Card)` — checks energy >= cost
- Tests:
  - Starter deck has 10 cards with correct distribution
  - Drawing 5 from 10-card deck leaves 5 in draw pile
  - Drawing when deck empty shuffles discard into deck first
  - Drawing when both empty returns partial hand
  - DiscardHand moves all hand cards to discard
  - ExhaustCard puts card in exhaust pile, not discard
  - Energy check prevents playing unaffordable cards

#### Success criteria
- All deck tests pass
- Deck cycling (draw → discard → shuffle → draw) works correctly

---

### Milestone 4: Card Effects + Tests
**AI writes**: All 5 starter card effect implementations + ClueSystem + tests
**Human does**: Run `dotnet test`, **decide on Twirl version** (see Open Question above)

This is the largest milestone. The clue pip system (Instructions) is the most complex piece.

#### Deliverables

**Spritz (Scout)**
- Target: 1 unrevealed tile
- Effect: Adds `ownerSubset` annotation to the tile
  - If tile is Player or Neutral → subset = {Player, Neutral} (safe)
  - If tile is Rival or Mine → subset = {Rival, Mine} (dangerous)
- Multiple Spritz on same tile: intersect subsets (narrows possibilities)
- `CardEffectSystem.ExecuteSpritz(GameState, Position target)` → new GameState

**Imperious Instructions (Clue Pips)**
- Target: none (immediate)
- Effect: Bag-draw pip distribution algorithm:
  1. Pick 2 random unrevealed player tiles ("targets")
  2. Pick 6 random other unrevealed tiles ("spoilers")
  3. Build weighted bag: targets get 12 copies each, spoilers get 4 copies (mines get 3)
  4. Draw 10 from bag: first 2 guaranteed one-each from targets, rest random
  5. Count draws per tile = pip strength
  6. Validate: at least one target has max pip count (retry up to 10x)
  7. Store ClueResult on each affected tile's annotations
- `ClueSystem.GenerateClue(GameState, Random)` → list of ClueResult
- `CardEffectSystem.ExecuteInstructions(GameState)` → new GameState

**Scurry**
- Target: 2 unrevealed tiles
- Effect: Reveals the safer tile
  - Safety ranking: Player(4) > Neutral(3) > Rival(2) > Mine(1)
  - Ties broken randomly
  - Non-revealed tiles get ownerSubset annotation: all types at-most-as-safe as revealed
  - E.g., if Player tile revealed, other gets {Player, Neutral, Rival, Mine}
  - If Neutral revealed, other gets {Neutral, Rival, Mine}
- `CardEffectSystem.ExecuteScurry(GameState, Position[] targets, Random)` → new GameState

**Tingle**
- Target: none (immediate)
- Effect: Picks 1 random unrevealed rival/mine tile, adds exact ownerSubset ({Rival} or {Mine})
  - Prefers "ambiguous" tiles (no single-owner annotation yet)
- `CardEffectSystem.ExecuteTingle(GameState, Random)` → new GameState

**Twirl** (pending decision)
- Cost 3, exhaust, gain 3 copper (5 if enhanced)
- `CardEffectSystem.ExecuteTwirl(GameState)` → new GameState

**Card Play Orchestrator**
- `CardEffectSystem.PlayCard(GameState, Card, Position[]? targets, Random)` → new GameState
  - Deduct energy
  - Execute effect based on card type
  - Move card to discard (or exhaust if card.exhaust)

**Tests:**
- Spritz marks safe tile as {Player, Neutral}
- Spritz marks dangerous tile as {Rival, Mine}
- Two Spritz on same tile intersects subsets correctly
- Instructions generates pips for 8 tiles with correct strength distribution
- Instructions: player tiles tend to have highest pips (statistical test over many runs)
- Instructions: validation ensures player tile has max count
- Scurry reveals the safer of 2 tiles
- Scurry: annotation on non-revealed tile reflects safety ordering
- Tingle marks a rival tile as {Rival}
- Tingle marks a mine tile as {Mine}
- Tingle prefers ambiguous tiles
- Twirl: gain copper, exhaust works correctly
- PlayCard deducts energy correctly
- PlayCard exhausts cards with exhaust flag

#### Success criteria
- All card effect tests pass
- Clue pip generation matches alpha algorithm behavior
- Annotation intersection logic narrows correctly

---

### Milestone 5: Turn Flow & Game Loop + Tests
**AI writes**: TurnSystem, simple rival AI, win/lose, GameRunner + tests
**Human does**: Run `dotnet test`

#### Deliverables
- `TurnSystem.StartPlayerTurn(GameState, Random)` — discard hand, draw 5, reset energy to 3, increment turn
- `TurnSystem.EndPlayerTurn(GameState)` — transition to rival turn
- `TurnSystem.ExecuteRivalTurn(GameState, Random)` — reveal 1 random unrevealed rival tile, transition back to player
- `TurnSystem.CheckGameStatus(GameState)` — returns Playing/Won/Lost
  - Won: all player tiles revealed
  - Lost: a mine tile was revealed (not possible on Level 1, but wire it up)
- Turn-ending reveal: if player reveals a non-player tile, their turn ends
  - Player reveals player tile → continue turn
  - Player reveals rival/neutral/mine tile → turn ends immediately
- `GameRunner.CreateGame(LevelConfig, Random)` — full initial state: board + shuffled deck + draw 5
- `GameRunner.ProcessReveal(GameState, Position)` — reveal tile, check game status, possibly end turn
- `GameRunner.ProcessCardPlay(GameState, Card, Position[]? targets, Random)` — play card, check game status
- `GameRunner.ProcessEndTurn(GameState)` — manual end turn
- Tests:
  - New turn draws 5 cards and resets energy
  - Rival turn reveals exactly 1 rival tile
  - Revealing all player tiles triggers win
  - Revealing a mine triggers loss (test with mine-containing board)
  - Revealing non-player tile ends player turn
  - Revealing player tile does NOT end turn
  - Full game loop: create → play cards → reveal → rival turn → new turn (multi-turn test)
  - GameRunner rejects card play when insufficient energy
  - GameRunner rejects reveal on already-revealed tile

#### Success criteria
- All turn flow tests pass
- Can "play" a complete game headlessly through test code
- Win condition reachable in tests

**This milestone completes the headless game logic. Everything after this is Godot UI.**

---

### Milestone 6: Godot Tile Grid
**AI writes**: Scene files (.tscn), TileNode.cs, TileView.cs, BoardNode.cs
**Human does**: Open in Godot editor, verify tiles render correctly, adjust visual properties if needed

#### Deliverables
- `Tile.tscn` — simple scene: Control root (TileNode.cs) + Control child (TileView.cs)
  - Fixed size (64x64 suggested, adjustable)
  - TileView handles all visual rendering programmatically
- `Board.tscn` — Node2D root (BoardNode.cs), spawns Tile instances as children
  - Manual positioning (not GridContainer) — tile_size + gap spacing
  - BoardNode reads from Board model to create/position tiles
- `Main.tscn` — root scene with Board as child, placeholder areas for hand/HUD
  - GameController.cs on root: creates GameState, passes to BoardNode
- Tile visual states:
  - **Unrevealed**: dark gray rectangle
  - **Revealed player**: pink, shows adjacency number
  - **Revealed rival**: blue, shows adjacency number
  - **Revealed neutral**: white/light gray, shows adjacency number
- Tile input: left-click emits signal (for reveal or card targeting)
- Right-click: reserved for annotations (wired but no-op for now)
- BoardNode exposes C# events that GameController listens to

#### Success criteria
- Run game in Godot, see 6x5 grid of dark gray tiles
- Click a tile, it reveals with correct color and adjacency number
- Grid is properly spaced and centered

---

### Milestone 7: Hand Display & HUD
**AI writes**: HandDisplay.cs, CardUINode.cs, HUD.cs, scene files or programmatic UI
**Human does**: Verify layout in Godot, adjust positioning/sizing

#### Deliverables
- **Hand display** at bottom of screen:
  - HBoxContainer with CardUI children
  - Each card shows: name, cost badge, basic description
  - Click card to select it for play
  - Unaffordable cards visually dimmed
  - Refreshes on state change (draw, play, discard)
- **HUD** showing:
  - Energy: "Energy: 2 / 3"
  - Deck count / Discard count
  - Turn indicator: "Your Turn" / "Rival Turn"
  - Tile counts: unrevealed per type (Player: 8, Rival: 6, etc.)
  - Game status (Playing / Won / Lost)
- **End Turn button**
- GameController updates all UI when GameState changes

#### Success criteria
- Cards visible in hand at bottom
- Energy decrements when playing cards
- Deck/discard counts update correctly
- End Turn button works

---

### Milestone 8: Card Targeting & Play Flow
**AI writes**: TargetingController.cs, targeting UI logic
**Human does**: Playtest card plays, verify targeting flow

#### Deliverables
- **Targeting mode**: when a targeting card is clicked in hand:
  - UI enters targeting mode with a banner/message ("Select a tile" / "Select 2 tiles")
  - Valid tiles get hover highlight
  - Click tile to select as target
  - For multi-target (Scurry): track selections, confirm when count reached
  - Cancel button or right-click to cancel targeting
  - ESC key cancels targeting
- **Immediate cards** (Instructions, Tingle, Twirl): play on click, no targeting needed
- **Card play flow**:
  1. Click card in hand
  2. If targeting needed → enter targeting mode → select targets → execute
  3. If immediate → execute directly
  4. State updates → UI refreshes
  5. If reveal caused by card (Scurry), check turn-ending / win/lose
- **Turn-ending reveal feedback**: clear indication when turn ends because of non-player reveal

#### Success criteria
- Can play Spritz: click card, click tile, see safe/dangerous annotation
- Can play Instructions: click card, see pips appear on tiles
- Can play Scurry: click card, click 2 tiles, safer one reveals
- Can play Tingle: click card, random tile gets owner annotation
- Can play Twirl: click card, effect executes
- Targeting mode is cancelable

---

### Milestone 9: Annotation & Clue Display
**AI writes**: Annotation rendering in TileView, pip display system
**Human does**: Verify readability, adjust colors/sizes

This milestone makes the information feedback visible, which is critical for the game to be actually playable as a deduction puzzle.

#### Deliverables
- **Clue pip display** (from Instructions):
  - Small colored dots on the tile (one per pip)
  - Color indicates clue source/order (different clue casts get different colors)
  - More pips = more likely to be player tile (player should learn this intuitively)
  - Pips visible on unrevealed tiles only
- **Safe/dangerous annotation** (from Spritz):
  - Clear visual indicator: checkmark or green tint for safe, X or red tint for dangerous
  - Visible on unrevealed tiles
- **Owner type annotation** (from Tingle):
  - Shows the confirmed owner type letter/icon on unrevealed tile
  - "R" for rival, "M" for mine (or colored border/icon)
- **Annotation stacking**: tile can have pips + safe/dangerous + owner type simultaneously
  - Layout needs to accommodate multiple annotation types without becoming unreadable
- **Adjacency number styling**: colored by tile owner type, centered on revealed tile

#### Success criteria
- Pips from Instructions are visible and countable
- Spritz safe/dangerous is immediately clear
- Tingle owner marking is unambiguous
- Multiple annotations on one tile are all readable
- A player can use the displayed information to make deductive decisions

---

### Milestone 10: Integration, Polish & Full Playtest
**AI writes**: Bug fixes, edge case handling
**Human does**: Play full floors, report issues, visual adjustments

#### Deliverables
- End-to-end playable floor:
  1. Game starts → see 6x5 grid, 5 cards in hand, 3 energy
  2. Play cards to gather information
  3. Reveal tiles based on deductions
  4. Non-player reveal ends turn → rival reveals 1 tile → new turn
  5. Find all 12 player tiles to win
- Win/lose screen with "Play Again" option
- Edge cases verified:
  - Drawing when deck and discard are both empty
  - Playing last card in hand
  - Trying to reveal already-revealed tile
  - Tingle when no rival/mine tiles remain unrevealed
  - Instructions when fewer than 2 player tiles unrevealed
  - Scurry when target tile was revealed by another effect between selections
- No crashes or soft-locks during normal play
- Game "feels right" — the deduction loop works

#### Success criteria
- Can play a full floor from start to win using all 5 starter card types
- Clue pips from Instructions are useful for identifying player tiles
- Spritz helps confirm safe tiles
- Adjacency numbers display correctly
- The core puzzle is recognizable: gather info → deduce → reveal → repeat
- No bugs during normal play

---

## Milestone Summary

| # | Milestone | AI Writes | Human Does | Depends On |
|---|-----------|-----------|------------|------------|
| 1 | Project Structure & Core Types | .sln, .csproj, model classes | Build in Godot, `dotnet build` | — |
| 2 | Board System + Tests | BoardSystem.cs, tests | `dotnet test` | 1 |
| 3 | Deck & Energy + Tests | DeckSystem.cs, tests | `dotnet test` | 1 |
| 4 | Card Effects + Tests | CardEffectSystem.cs, ClueSystem.cs, tests | `dotnet test`, **Twirl decision** | 2, 3 |
| 5 | Turn Flow & Game Loop + Tests | TurnSystem.cs, GameRunner.cs, tests | `dotnet test` | 4 |
| 6 | Godot Tile Grid | .tscn files, node scripts | Verify in editor, run game | 5 |
| 7 | Hand Display & HUD | UI scripts, scene files | Verify layout, adjust sizing | 6 |
| 8 | Card Targeting & Play | TargetingController.cs | Playtest card plays | 7 |
| 9 | Annotation & Clue Display | TileView overlay rendering | Verify readability, adjust visuals | 8 |
| 10 | Integration & Playtest | Bug fixes, edge cases | Play full floors, report issues | 9 |

**Milestones 2 & 3 can be done in parallel** (no dependency between board and deck).
**Milestones 1-5 are pure C#** — no Godot editor needed.
**Milestones 6-10 require Godot** — human involvement increases.

---

## Key Alpha References

These are the source-of-truth files in `C:\Users\srini\Code\sweep-the-dungeons\`:

| File | What to reference |
|------|-------------------|
| `src/types.ts` | All type definitions, especially Tile, Card, GameState, Annotation |
| `src/game/boardSystem.ts` | Board creation, adjacency, reveal mechanics |
| `src/game/cardSystem.ts` | Deck management, card play flow, turn logic |
| `src/game/clueSystem.ts` | Bag-draw pip generation algorithm |
| `src/game/cards/scout.ts` | Spritz effect implementation |
| `src/game/cards/scurry.ts` | Scurry effect + safety ranking |
| `src/game/cards/report.ts` | Tingle effect implementation |
| `src/game/cards/imperiousInstructions.ts` | Instructions effect |
| `src/game/cards/twirl.ts` | Twirl effect |
| `src/game/cardEffects.ts` | `addOwnerSubsetAnnotation` (intersection logic) |
| `levels-config.json` | Level 1 config: 6x5, 12/10/8/0 |

---

## Notes

- **Immutable state pattern**: GameState should be treated as immutable. All system methods return new GameState rather than mutating. This matches the alpha's Zustand pattern and makes testing straightforward.
- **Random seed**: All randomness goes through a `Random` parameter for deterministic testing.
- **Adjacency detail**: Need to verify exact alpha behavior. The alpha counts neighbors matching the tile's own type (classic minesweeper per-type count). This is set at board creation time, not at reveal time.
- **No animations in Stage 1**: All effects are instant. Tingle, rival turns, etc. happen immediately. Animations are Stage 6.
- **Scene files**: AI will write .tscn files as text where practical. Human should verify they load correctly in the editor and adjust visual properties as needed.

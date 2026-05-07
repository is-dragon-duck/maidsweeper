# Stage 4: Equipment, Economy & Recall Variants — Draft Plan

**Goal**: Equipment adds persistent passive effects, copper currency enables shops, Recall variants and food cards deepen strategy. Full reward flow between floors.

**Starting state**: 8 playable floors, 22 cards (5 starter + 6 S2 reward + 11 S3), card upgrades, player annotations, 257 tests.

**End state**: Equipment system with ~12 core items, copper economy, shops, 2 Recall variants, 3 food cards, Excuses penalty rework, full reward flow across 8 floors, ~350+ tests.

---

## Key Design Decisions

### Naming Convention
Cards use **verbs**, equipment uses **nouns**. Full mapping in `NAME_MAPPING.md`.

### Scope
- **Equipment**: Core subset (~12 items) that don't require Stage 5 systems (goblins, surface mines, advanced AI). Non-special-tile equipment as stretch goals.
- **Floors**: Stay at 8 floors, add full reward flow (equipment/shops) to existing floors.
- **Prerequisites**: No equipment prerequisite chains for now — flat offerings only.
- **Deck-modifying equipment**: Immediate effects on acquisition only (no ongoing triggers needed).

### Excuses/Complaints Rework
Align with alpha's Grace/Evidence penalty pattern, adapted to our naming:

**Current system (Stage 3)**:
- Excuses consumed → no penalty
- Explode (base) → +1 Complaints stack + 1 Mollify to hand
- Complaints: lose 2 copper per stack at floor end

**New system (Stage 4)**:
- When Excuses is reduced **to 0** (not any other reduction): +2 Complaints stacks, 1 Mollify to discard, 1 Mollify to top of draw pile
- Explode (base) still adds +1 Complaints stack + 1 Mollify to hand (unchanged)
- Complaints: lose 2 copper per stack at floor end (unchanged)
- Mollify: 1 cost, exhaust, -1 Complaints stack (unchanged)
- Net effect: surviving a noble when you had exactly 1 Excuses left → 2 junk cards in your deck + 4 copper penalty if not cleared

---

## Milestones

### Milestone 25: Excuses Penalty Rework
**Goal**: Excuses consumption generates Complaints + Mollify when reduced to 0.

**Tasks**:
- Modify Excuses consumption logic in `GameRunner.ProcessReveal` (or wherever noble protection fires)
- When ExcusesStacks goes from any value to 0: add 2 Complaints stacks, add 1 Mollify to discard pile, add 1 Mollify to top of draw pile
- Going from 2→1 or 3→2: no penalty, just decrement
- Ensure Mollify cards are removed from persistent deck at floor transition (already implemented)

**Tests** (~6):
- Excuses 1→0: +2 Complaints, +2 Mollify (1 discard, 1 draw top)
- Excuses 2→1: no Complaints, no Mollify
- Excuses 3→0 (hit 3 nobles): penalty fires only once, on the final reduction to 0
- Mollify in draw pile is actually on top (first to draw)
- Complaints copper penalty still works at floor end

**Status**: Complete (262 tests)

---

### Milestone 26: Copper Economy
**Goal**: Copper earned from gameplay, displayed in HUD (already showing copper count).

**Tasks**:
- Copper from unrevealed rival tiles at floor end (1 per tile)
- Copper from every 5th player tile revealed (cumulative counter across floors, `PlayerTilesRevealedCount` on GameState)
- Complaints copper penalty at floor end (already implemented in logic, hook into floor-end flow)
- Copper persistence across floors (already persists via GameState)
- Copper cannot go negative (floor at 0)

**Tests** (~8):
- Copper gained from unrevealed rival tiles at floor end
- Copper gained per 5th player tile reveal
- Reveal counter persists across floors (reveal 3 on floor 1, 2 more on floor 2 → copper at 5th)
- Complaints penalty applied at floor end (2 per stack)
- Complaints + rival tile copper calculated together
- Copper doesn't go below 0
- Twirl/Brat/Deliver copper (already tested, verify integration)

**Status**: Complete (270 tests)

---

### Milestone 27: Food Cards (Read, Hydrate, Adopt)
**Goal**: Three food cards with multi-floor status effects.

**Read** (alpha: Burger, cost 2, exhaust):
- Base: +1 card draw per turn for 2 floors (2 `ReadStacks`)
- Enhanced: +1 card draw per turn for 3 floors (3 `ReadStacks`)
- Stacks decrement at floor end; effect removed when 0
- Applied during draw phase: draw +1 card if `ReadStacks` > 0

**Hydrate** (alpha: Ice Cream, cost 2, exhaust):
- Base: +1 spoon when a player tile reveal grants copper (i.e., on every 5th reveal of a player tile for any reason), for 2 floors
- Enhanced: Same, 3 floors
- Stacks decrement at floor end

**Adopt** (alpha: Carrots, cost 2, exhaust):
- Base: Reveal 1 random player tile at floor start, for 2 floors
- Enhanced: Same, 3 floors
- Stacks decrement at floor end
- Fires before first turn draw

**System additions**:
- `ReadStacks`, `HydrateStacks`, `AdoptStacks` on GameState
- Floor-end decrement for all food status stacks
- Integration with draw system (Read), copper system (Hydrate), floor-start (Adopt)

**Tests** (~12):
- Read: draw 6 cards instead of 5 with 1 stack
- Read: stacks decrement at floor end
- Read: enhanced gives 3 stacks
- Hydrate: +1 spoon on 5th player reveal with stacks
- Hydrate: no bonus without stacks
- Adopt: reveals 1 player tile at floor start
- Adopt: multiple stacks still reveal just 1 tile
- All food stacks: decrement correctly, removed at 0
- Food cards added to reward pool

**Status**: Complete (286 tests)

---

### Milestone 28: Recall Variants
**Goal**: Vague and Sarcastic Recall variants with distinct pip rendering.

**Recall - Vague** (alpha: Vague Instructions, cost 2):
- 5 player tiles chosen as targets (vs 2 for standard Recall)
- Bag: 5 targets × 4 copies each = 20, plus 14 spoilers (neutral/rival ×2, player/noble ×1)
- Draw 8 pips total (not 10 like in the alpha)
- First 3 draws guaranteed from targets (enhanced: all 5 guaranteed)
- Same green rounded pips as standard Recall
- Weaker signal per tile but broader coverage

**Recall - Sarcastic** (alpha: Sarcastic Instructions, cost 2):
- Carefully read the alpha code before implementing this!!
- Two methods (selected automatically based on board state):
  1. Find cluster of player tiles, highlight surrounding area
  2. Distribute pips weighted toward non-player tiles
- Red square pips (distinct from green rounded pips) (8, not 10 like in the alpha)
- Enhanced: refunds 1 spoon if any other Recall was already played this floor
- Anti-pips: "this tile probably isn't yours"

**System additions**:
- `RecallVariant` enum or new `CardEffectType` values for Vague/Sarcastic
- Red square pip rendering in TileView (new pip shape)
- Track "Recall played this floor" for Sarcastic enhanced refund
- Add to reward pool

**Tests** (~12):
- Vague: distributes pips across 5 target tiles
- Vague: 10 draws total
- Vague: first 3 guaranteed from targets
- Vague: enhanced guarantees 5 from targets
- Sarcastic: produces red anti-pips
- Sarcastic: enhanced refunds 1 spoon if Recall already played
- Sarcastic: pips weighted toward non-player tiles
- Pip rendering: red squares distinct from green circles

**Status**: Complete (299 tests)

---

### Milestone 29: Equipment Data Model & Core System
**Goal**: Equipment can be defined, offered, selected, and stored. No prerequisites.

**Tasks**:
- `Equipment` record: Id, Name, Description, EffectType
- `EquipmentEffectType` enum for all equipment effects
- Equipment definitions in `EquipmentDefinitions.cs`
- `List<Equipment> Equipment` on GameState (persists across floors)
- Equipment offering generation: 3 options, filtered by already-owned only (no prerequisites)
- Equipment selection flow in `CampaignSystem`
- `GamePhase.EquipmentReward` phase
- One-shot deck-modifying equipment: immediate effect on acquisition, no ongoing trigger

**Tests** (~8):
- Equipment definition properties
- Can't get duplicate equipment in offerings
- Equipment persists across floors
- Equipment selection adds to inventory
- Skip equipment works
- Offering generates 3 distinct items

**Status**: Complete (308 tests)

---

### Milestone 30: Equipment Set 1 (Simple Passives)
**Goal**: First batch of equipment with straightforward passive effects.

**Items** (using maidsweeper names from NAME_MAPPING.md):
- **Coffee** (alpha: Caffeinated): +1 max spoon per turn, -1 card draw (except turn 1)
- **Frilly Dress**: First 4 neutral reveals on turn 1 don't end turn
- **Dust Bunny**: Reveal 1 random player tile at floor start
- **Handbag**: Draw +2 cards on first turn
- **Eyeshadow**: +1 Distraction stack at turn start
- **Glasses**: Free Tingle effect at turn start (doesn't cost a card)

**System additions**:
- Equipment trigger points in GameRunner: `OnFloorStart`, `OnTurnStart`
- Integration with turn system (Coffee: modify max spoons and draw count)
- Frilly Dress: track neutral reveals on turn 1, suppress turn end for first 4

**Tests** (~12):
- Coffee: 4 max spoons, draw 4 cards on non-first turns, draw 5 on turn 1
- Frilly Dress: first 4 neutral reveals on turn 1 don't end turn, 5th does
- Dust Bunny: 1 player tile revealed at floor start
- Handbag: draw 7 cards on first turn (5+2)
- Eyeshadow: +1 Distraction per turn
- Glasses: Tingle annotation applied at turn start without card cost

**Status**: Complete (324 tests)

---

### Milestone 31: Equipment Set 2 (Deck Modifiers)
**Goal**: Equipment with immediate deck-modifying effects on acquisition.

**Items**:
- **Bleach**: On acquisition, enhance all Spritz/Sweep/Brush in persistent deck. Future Spritz/Sweep/Brush added to deck are auto-enhanced.
- **Estrogen**: On acquisition, replace 3 random non-enhanced cards with bonus-spoon versions.
- **Progesterone**: On acquisition, replace 3 random non-bonus-spoon cards with enhanced versions.
- **Crystal Ball** (alpha: Crystal): On acquisition, add 3 doubly-upgraded (Enhanced + BonusSpoon) Tingles to persistent deck.
- **Boots**: On acquisition, replace 1 random card with a random doubly-upgraded card from the reward pool.
- **Tiara**: Ongoing passive — double all copper rewards (rival tiles, 5th-reveal, card effects).

**Tests** (~10):
- Bleach: all Spritz/Sweep/Brush in deck become enhanced
- Estrogen: 3 cards gain BonusSpoon
- Progesterone: 3 cards gain Enhanced
- Crystal Ball: 3 doubly-upgraded Tingles added
- Boots: 1 card replaced with doubly-upgraded random
- Tiara: copper doubled from rival tiles at floor end
- Tiara: copper doubled from 5th player reveal

**Status**: Complete (336 tests)

---

### Milestone 32: Shop System
**Goal**: Between-floor shop where players spend copper.

**Shop layout** (9 slots, matching alpha):
1. Regular card (5 copper base)
2. Regular card (5 copper base)
3. Bonus-spoon card (11 copper base)
4. Enhanced card (10 copper base)
5. Equipment (19 copper base)
6. Equipment (23 copper base)
7. Remove Card (14 copper base)
8. Visiting Bunny — reveal 1 player tile at next floor start (4 copper base)
9. Enhance — randomly enhance a card in deck (9 copper base)

**Progressive pricing**: `ceil(baseCost * (1 + 0.1 * (shopVisitCount - 1)))`

**Tasks**:
- `ShopSystem`: generate offerings, calculate prices, process purchases
- Shop visit counter on GameState
- Card pool for shop (distinct base names, weighted selection)
- Equipment filtered to exclude already-owned
- `GamePhase.Shop` phase
- Purchase flow: validate affordability, deduct copper, apply effect

**Tests** (~10):
- Shop generates correct number of slots
- Progressive pricing increases by 10% per visit
- Can't buy what you can't afford
- Purchasing card adds to persistent deck
- Purchasing equipment adds to inventory
- Remove card works from shop
- Visiting Bunny adds Adopt-like effect for 1 floor
- Enhance randomly enhances a card
- Equipment slots exclude already-owned items

**Status**: Not Started

---

### Milestone 33: Reward Flow
**Goal**: Complete between-floor reward sequence for all 8 floors.

**Reward flow order**: Card Reward → Upgrade → Equipment → Shop (each optional per level config flags).

**Provisional floor reward config** (reasonable placeholder until real level design):

| Floor | Card | Upgrade | Equipment | Shop |
|-------|------|---------|-----------|------|
| 1 | yes | | | |
| 2 | yes | yes | | |
| 3 | | | yes | |
| 4 | | | | yes |
| 5 | yes | | yes | |
| 6 | yes | | | |
| 7 | yes | yes | | |
| 8 | — (final floor, campaign ends) | | | |

**Tasks**:
- Add `HasEquipmentReward`, `HasShop` flags to LevelConfig
- Extend `CampaignSystem` reward flow: after upgrade phase, check equipment, then shop
- GamePhase additions: `EquipmentReward`, `Shop`
- Transition logic between phases
- Update existing floors 1-7 reward configs

**Tests** (~6):
- Reward flow progresses through all phases in order
- Skipping a phase advances to next
- Floor with no rewards goes directly to next floor
- Equipment phase only appears on configured floors
- Shop phase only appears on configured floors

**Status**: Not Started

---

### Milestone 34: Stage 4 Godot UI
**Goal**: All Stage 4 features rendered and interactive.

**Tasks**:
- Equipment selection overlay (3 options with name/description, click to select, skip button)
- Equipment display in HUD (list of owned equipment names)
- Shop overlay (9 slots with prices, buy buttons, copper display, "Done" button)
- Food status effect display in HUD (Read/Hydrate/Adopt stacks with floor count)
- Red square pip rendering for Sarcastic Recall (distinct from green round pips)
- Copper earned notification at floor end (brief display of gains/losses)
- Visiting Bunny indicator (if purchased, show in status effects)

**Status**: Not Started

---

## Stretch Goals (Non-Special-Tile Equipment)

These equipment items don't require Stage 5 mechanics but are lower priority. Implement if time permits:

| Equipment | Effect |
|---|---|
| Hyperfocus | Put 1 random net-cost-0 card into first turn's hand |
| Choker | Rival turn ends when 5 unrevealed tiles left |
| Mirror | Floor start: reveal 1 rival tile + sense adjacent player tiles |
| Busy Canary | Floor start: scan up to 2 areas for nobles |
| Double Broom | On tile reveal: Brush 2 random adjacent unrevealed tiles |
| Broom Closet | On acquisition: remove all Spritz, add 3 Sweep cards |
| Cocktail | On acquisition: remove all Scurry, add 2 random bonus-spoon cards |
| Novel | On acquisition: replace all Recall with doubly-upgraded Sarcastic |

---

## Stage 4 Summary

| Milestone | Key Systems | Est. Tests |
|-----------|-------------|------------|
| M25: Excuses Rework | Penalty on Excuses→0 | ~6 |
| M26: Copper Economy | Copper earn/spend/persist | ~8 |
| M27: Food Cards | Read, Hydrate, Adopt + multi-floor stacks | ~12 |
| M28: Recall Variants | Vague + Sarcastic + red pips | ~12 |
| M29: Equipment Model | Data model, offerings, selection | ~8 |
| M30: Equipment Set 1 | 6 simple passives | ~12 |
| M31: Equipment Set 2 | 6 deck modifiers + Tiara | ~10 |
| M32: Shop | 9 slots, pricing, purchase | ~10 |
| M33: Reward Flow | Full sequence across 8 floors | ~6 |
| M34: Godot UI | Equipment/shop/food/pip UI | manual |
| **Total** | | **~84** |

**Projected total tests at Stage 4 end**: ~341 (257 existing + ~84 new)

**Deferred to Stage 5**: Donut/Pose (goblins), Taunt (rival AI), Gaze/Fetch (directional), Mop (goblins), prerequisite equipment chains, floors 9-21.

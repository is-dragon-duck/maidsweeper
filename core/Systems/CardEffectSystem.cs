namespace Maidsweeper.Core.Systems;

using Maidsweeper.Core.Models;

public static class CardEffectSystem
{
    /// <summary>
    /// Plays a card: spend spoons, execute effect, discard/exhaust card.
    /// </summary>
    public static GameState PlayCard(GameState state, Card card, Position[]? targets, Random rng)
    {
        if (!DeckSystem.CanPlayCard(state, card))
            throw new InvalidOperationException(
                $"Not enough spoons to play '{card.Name}' (cost {card.Cost}, have {state.Spoons})");

        // Spend spoons
        state = DeckSystem.SpendSpoons(state, card);

        // Remove card from hand
        var hand = state.Hand.ToList();
        hand.Remove(card);
        state = state with { Hand = hand };

        // Execute effect — Peek needs special handling for conditional exhaust
        bool shouldExhaust = card.Exhaust;

        if (card.EffectType == CardEffectType.Peek)
        {
            bool foundNobles;
            (state, foundNobles) = ExecutePeek(state, targets, card);
            shouldExhaust = foundNobles;
        }
        else if (card.EffectType == CardEffectType.Glaze)
        {
            state = ExecuteGlaze(state, card);
            shouldExhaust = !card.Enhanced; // Base exhausts, enhanced doesn't
        }
        else
        {
            state = card.EffectType switch
            {
                CardEffectType.Spritz => ExecuteSpritz(state, targets, card, rng),
                CardEffectType.Recall => ExecuteInstructions(state, rng, card),
                CardEffectType.Scurry => ExecuteScurry(state, targets, rng, card),
                CardEffectType.Tingle => ExecuteTingle(state, rng, card),
                CardEffectType.Twirl => ExecuteTwirl(state, card),
                CardEffectType.Brush => ExecuteBrush(state, targets, rng),
                CardEffectType.Sweep => ExecuteSweep(state, targets, rng),
                CardEffectType.Caffeinate => ExecuteCaffeinate(state),
                CardEffectType.Breathe => ExecuteBreathe(state, rng),
                CardEffectType.LockIn => ExecuteLockIn(state, rng),
                CardEffectType.Rendezvous => ExecuteRendezvous(state, rng),
                CardEffectType.Argue => ExecuteArgue(state, targets, rng, card),
                CardEffectType.Eavesdrop => ExecuteEavesdrop(state, targets, card),
                CardEffectType.Explode => ExecuteExplode(state, targets, card),
                CardEffectType.Deliver => ExecuteDeliver(state, targets, card),
                CardEffectType.Brat => ExecuteBrat(state, targets, card),
                CardEffectType.Mollify => ExecuteMollify(state),
                CardEffectType.AcceptHelp => ExecuteAcceptHelp(state, targets, card, rng),
                CardEffectType.Ramble => ExecuteRamble(state, card, rng),
                CardEffectType.Read => ExecuteRead(state, card),
                CardEffectType.Hydrate => ExecuteHydrate(state, card),
                CardEffectType.Adopt => ExecuteAdopt(state, card),
                CardEffectType.RecallVague => ExecuteRecallVague(state, rng, card),
                CardEffectType.RecallSarcastic => ExecuteRecallSarcastic(state, rng, card),
                CardEffectType.Gaze => ExecuteGaze(state, targets, card),
                CardEffectType.Fetch => ExecuteFetch(state, targets, card),
                CardEffectType.Pose => ExecutePose(state, card, rng),
                CardEffectType.Taunt => ExecuteTaunt(state, targets, card),
                CardEffectType.Mask => throw new InvalidOperationException("Use PlayMaskedCard for Mask"),
                CardEffectType.Nap => throw new InvalidOperationException("Use PlayNap for Nap"),
                _ => throw new ArgumentException($"Unknown card effect type: {card.EffectType}")
            };
        }

        // Grant bonus spoon after successful play
        if (card.BonusSpoon)
        {
            state = state with { Spoons = state.Spoons + 1 };
        }

        // Discard or exhaust
        if (shouldExhaust)
        {
            state = DeckSystem.ExhaustCard(state, card);
        }
        else
        {
            var discardPile = state.DiscardPile.ToList();
            discardPile.Add(card);
            state = state with { DiscardPile = discardPile };
        }

        return state;
    }

    /// <summary>
    /// Plays a Mask card: pay Mask's cost, remove both Mask and selected card from hand,
    /// execute selected card's effect for free, exhaust selected card (always),
    /// exhaust or discard Mask based on Enhanced.
    /// </summary>
    public static GameState PlayMaskedCard(GameState state, Card maskCard, Card selectedCard,
        Position[]? selectedCardTargets, Random rng)
    {
        if (maskCard.EffectType != CardEffectType.Mask)
            throw new ArgumentException("PlayMaskedCard requires a Mask card");
        if (!DeckSystem.CanPlayCard(state, maskCard))
            throw new InvalidOperationException("Not enough spoons for Mask");

        // Pay Mask's cost (0) and remove from hand
        state = DeckSystem.SpendSpoons(state, maskCard);
        var hand = state.Hand.ToList();
        hand.Remove(maskCard);
        state = state with { Hand = hand };

        // Remove selected card from hand
        hand = state.Hand.ToList();
        if (!hand.Remove(selectedCard))
            throw new ArgumentException("Selected card not in hand");
        state = state with { Hand = hand };

        // Execute the selected card's effect (for free — cost was 0 via Mask)
        state = ExecuteEffect(state, selectedCard, selectedCardTargets, rng);

        // Handle bonus spoon for selected card
        if (selectedCard.BonusSpoon)
            state = state with { Spoons = state.Spoons + 1 };

        // Handle Mask bonus spoon
        if (maskCard.BonusSpoon)
            state = state with { Spoons = state.Spoons + 1 };

        // Exhaust the selected card (always, regardless of its Exhaust flag)
        state = DeckSystem.ExhaustCard(state, selectedCard);

        // Exhaust or discard Mask
        if (!maskCard.Enhanced)
        {
            state = DeckSystem.ExhaustCard(state, maskCard);
        }
        else
        {
            var discard = state.DiscardPile.ToList();
            discard.Add(maskCard);
            state = state with { DiscardPile = discard };
        }

        return state;
    }

    /// <summary>
    /// Plays a Nap card: pay cost, retrieve a card from exhaust pile to hand, exhaust Nap.
    /// Enhanced: also gain spoons equal to retrieved card's cost.
    /// </summary>
    public static GameState PlayNap(GameState state, Card napCard, Card? retrievedCard, Random rng)
    {
        if (napCard.EffectType != CardEffectType.Nap)
            throw new ArgumentException("PlayNap requires a Nap card");
        if (!DeckSystem.CanPlayCard(state, napCard))
            throw new InvalidOperationException("Not enough spoons for Nap");

        // Pay Nap's cost and remove from hand
        state = DeckSystem.SpendSpoons(state, napCard);
        var hand = state.Hand.ToList();
        hand.Remove(napCard);
        state = state with { Hand = hand };

        // Retrieve card from exhaust pile (if one was selected)
        if (retrievedCard != null)
        {
            var exhaust = state.ExhaustPile.ToList();
            if (!exhaust.Remove(retrievedCard))
                throw new ArgumentException("Retrieved card not in exhaust pile");

            hand = state.Hand.ToList();
            hand.Add(retrievedCard);
            state = state with { Hand = hand, ExhaustPile = exhaust };

            // Enhanced: gain spoons equal to retrieved card's cost
            if (napCard.Enhanced)
            {
                state = state with { Spoons = state.Spoons + retrievedCard.Cost };
            }
        }

        // Handle bonus spoon for Nap
        if (napCard.BonusSpoon)
            state = state with { Spoons = state.Spoons + 1 };

        // Exhaust Nap
        state = DeckSystem.ExhaustCard(state, napCard);

        return state;
    }

    /// <summary>
    /// Executes a card's effect without handling cost, hand removal, or exhaust.
    /// Used by PlayMaskedCard to execute the selected card's effect.
    /// </summary>
    private static GameState ExecuteEffect(GameState state, Card card, Position[]? targets, Random rng)
    {
        // For Peek's conditional exhaust, we ignore it here (Mask always exhausts the played card)
        if (card.EffectType == CardEffectType.Peek)
        {
            var (newState, _) = ExecutePeek(state, targets, card);
            return newState;
        }

        if (card.EffectType == CardEffectType.Glaze)
            return ExecuteGlaze(state, card);

        return card.EffectType switch
        {
            CardEffectType.Spritz => ExecuteSpritz(state, targets, card, rng),
            CardEffectType.Recall => ExecuteInstructions(state, rng, card),
            CardEffectType.Scurry => ExecuteScurry(state, targets, rng, card),
            CardEffectType.Tingle => ExecuteTingle(state, rng, card),
            CardEffectType.Twirl => ExecuteTwirl(state, card),
            CardEffectType.Brush => ExecuteBrush(state, targets, rng),
            CardEffectType.Sweep => ExecuteSweep(state, targets, rng),
            CardEffectType.Caffeinate => ExecuteCaffeinate(state),
            CardEffectType.Breathe => ExecuteBreathe(state, rng),
            CardEffectType.LockIn => ExecuteLockIn(state, rng),
            CardEffectType.Rendezvous => ExecuteRendezvous(state, rng),
            CardEffectType.Argue => ExecuteArgue(state, targets, rng, card),
            CardEffectType.Eavesdrop => ExecuteEavesdrop(state, targets, card),
            CardEffectType.Explode => ExecuteExplode(state, targets, card),
            CardEffectType.Deliver => ExecuteDeliver(state, targets, card),
            CardEffectType.Brat => ExecuteBrat(state, targets, card),
            CardEffectType.Mollify => ExecuteMollify(state),
            CardEffectType.AcceptHelp => ExecuteAcceptHelp(state, targets, card, rng),
            CardEffectType.Ramble => ExecuteRamble(state, card, rng),
            CardEffectType.Read => ExecuteRead(state, card),
            CardEffectType.Hydrate => ExecuteHydrate(state, card),
            CardEffectType.Adopt => ExecuteAdopt(state, card),
            CardEffectType.RecallVague => ExecuteRecallVague(state, rng, card),
            CardEffectType.RecallSarcastic => ExecuteRecallSarcastic(state, rng, card),
            CardEffectType.Gaze => ExecuteGaze(state, targets, card),
            CardEffectType.Fetch => ExecuteFetch(state, targets, card),
            CardEffectType.Pose => ExecutePose(state, card, rng),
            CardEffectType.Taunt => ExecuteTaunt(state, targets, card),
            _ => throw new ArgumentException($"Unknown card effect type: {card.EffectType}")
        };
    }

    /// <summary>
    /// Spritz: Target 1 unrevealed tile.
    /// If Player or Neutral → safe: annotate {Player, Neutral}
    /// If Rival or Mine → dangerous: annotate {Rival, Mine}
    /// </summary>
    public static GameState ExecuteSpritz(GameState state, Position[]? targets, Card card, Random rng)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Spritz requires exactly 1 target tile");

        var pos = targets[0];
        var tile = state.Board.GetTile(pos);

        if (tile.IsRevealed)
            throw new ArgumentException("Cannot Spritz a revealed tile");
        if (tile.IsInner && !BoardSystem.CanReachInnerTile(state.Board, pos))
            throw new ArgumentException("Cannot Spritz an unreachable inner tile");

        // Spritz cleans courtiers (moves them) before annotating
        if (tile.IsCourtier)
        {
            state = state with { Board = BoardSystem.CleanCourtier(state.Board, pos, rng) };
            state = EquipmentSystem.OnCourtierCleaned(state, rng);
            tile = state.Board.GetTile(pos);
        }

        // Spritz also cleans ExtraDirty
        if (tile.IsDirty)
        {
            var cleanedTile = tile.WithoutSpecial(SpecialTileType.ExtraDirty);
            var newTiles = state.Board.Tiles.ToList();
            newTiles[state.Board.TileIndex(pos)] = cleanedTile;
            state = state with { Board = state.Board with { Tiles = newTiles } };
        }

        var isSafe = tile.Owner == TileOwner.Player || tile.Owner == TileOwner.Neutral;
        var subset = isSafe
            ? new HashSet<TileOwner> { TileOwner.Player, TileOwner.Neutral }
            : new HashSet<TileOwner> { TileOwner.Rival, TileOwner.Noble };

        return AnnotationSystem.AddOwnerSubset(state, pos, subset);
    }

    /// <summary>
    /// Recall - Imperious: No targeting. Distributes clue pips via bag draw.
    /// </summary>
    public static GameState ExecuteInstructions(GameState state, Random rng, Card card)
    {
        var clueResults = ClueSystem.GenerateImperiousClue(state, rng, card.Enhanced);

        foreach (var result in clueResults)
        {
            state = AnnotationSystem.AddClueResult(state, result.TilePosition, result);
        }

        return state with { RecallPlayedThisFloor = true };
    }

    /// <summary>
    /// Recall - Vague: Broader but weaker clue pips (5 targets, 8 draws).
    /// </summary>
    public static GameState ExecuteRecallVague(GameState state, Random rng, Card card)
    {
        var clueResults = ClueSystem.GenerateVagueClue(state, rng, card.Enhanced);

        foreach (var result in clueResults)
        {
            state = AnnotationSystem.AddClueResult(state, result.TilePosition, result);
        }

        return state with { RecallPlayedThisFloor = true };
    }

    /// <summary>
    /// Recall - Sarcastic: Anti-pips showing where tiles probably aren't yours.
    /// Enhanced: refunds 1 spoon if any Recall was already played this floor.
    /// </summary>
    public static GameState ExecuteRecallSarcastic(GameState state, Random rng, Card card)
    {
        // Enhanced: refund 1 spoon if any Recall already played this floor
        if (card.Enhanced && state.RecallPlayedThisFloor)
        {
            state = state with { Spoons = state.Spoons + 1 };
        }

        // Fanfic: playing Sarcastic Recall draws a card and costs 1 copper.
        if (EquipmentSystem.HasEquipment(state, EquipmentEffectType.Fanfic))
        {
            state = DeckSystem.DrawCards(state, 1, rng);
            state = state with { Copper = Math.Max(0, state.Copper - 1) };
        }

        var clueResults = ClueSystem.GenerateSarcasticClue(state, rng, card.Enhanced);

        foreach (var result in clueResults)
        {
            state = AnnotationSystem.AddClueResult(state, result.TilePosition, result);
        }

        return state with { RecallPlayedThisFloor = true };
    }

    // ===========================================================
    // Stage 5 directional cards (M43)
    // ===========================================================

    /// <summary>
    /// Gaze: from a target tile, scan in the card's `Direction` for the first unrevealed
    /// rival. Annotate it as `{Rival}`. Annotate all other checked tiles before/after as
    /// `{Player, Neutral, Noble}` (not-rival). Line stops at unrevealed sanctums and
    /// at unreachable inner tiles. Revealed tiles are passed through (not annotated).
    /// </summary>
    public static GameState ExecuteGaze(GameState state, Position[]? targets, Card card)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Gaze requires exactly 1 target tile");
        if (card.Direction == null)
            throw new ArgumentException("Gaze card must have a Direction");

        var checkedPositions = ScanLine(state.Board, targets[0], card.Direction.Value);

        Position? foundRival = null;
        foreach (var pos in checkedPositions)
        {
            if (state.Board.GetTile(pos).Owner == TileOwner.Rival)
            {
                foundRival = pos;
                break;
            }
        }

        // Annotate found rival
        if (foundRival != null)
        {
            state = AnnotationSystem.AddOwnerSubset(state, foundRival.Value,
                new HashSet<TileOwner> { TileOwner.Rival });
        }

        // Annotate other checked tiles as "not rival"
        var notRival = new HashSet<TileOwner> { TileOwner.Player, TileOwner.Neutral, TileOwner.Noble };
        foreach (var pos in checkedPositions)
        {
            if (foundRival.HasValue && pos == foundRival.Value) continue;
            state = AnnotationSystem.AddOwnerSubset(state, pos, notRival);
        }

        return state;
    }

    /// <summary>
    /// Fetch: from a target tile, scan in the card's `Direction` collecting unrevealed
    /// tiles. Determine the most-common owner type (tiebreak: Player > Neutral > Rival > Noble).
    /// Reveal all checked tiles of that owner. Annotate the rest as "anything except majority".
    /// </summary>
    public static GameState ExecuteFetch(GameState state, Position[]? targets, Card card)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Fetch requires exactly 1 target tile");
        if (card.Direction == null)
            throw new ArgumentException("Fetch card must have a Direction");

        var checkedPositions = ScanLine(state.Board, targets[0], card.Direction.Value);
        if (checkedPositions.Count == 0) return state;

        // Tally owner counts
        var counts = new Dictionary<TileOwner, int>
        {
            [TileOwner.Player] = 0, [TileOwner.Neutral] = 0,
            [TileOwner.Rival] = 0, [TileOwner.Noble] = 0
        };
        foreach (var pos in checkedPositions)
        {
            counts[state.Board.GetTile(pos).Owner]++;
        }

        // Tiebreak: safety order Player > Neutral > Rival > Noble
        var safetyOrder = new[] { TileOwner.Player, TileOwner.Neutral, TileOwner.Rival, TileOwner.Noble };
        var majority = safetyOrder.OrderByDescending(o => counts[o]).First();
        if (counts[majority] == 0) return state;

        // Reveal majority-owner tiles
        foreach (var pos in checkedPositions)
        {
            if (state.Board.GetTile(pos).Owner != majority) continue;
            var newBoard = BoardSystem.RevealTile(state.Board, pos, PlayerType.Player);
            state = state with { Board = newBoard };
        }

        // Annotate non-majority checked tiles as "anything but majority"
        var notMajority = new HashSet<TileOwner> { TileOwner.Player, TileOwner.Neutral, TileOwner.Rival, TileOwner.Noble };
        notMajority.Remove(majority);
        foreach (var pos in checkedPositions)
        {
            var tile = state.Board.GetTile(pos);
            if (tile.IsRevealed) continue; // already revealed (majority or partial)
            state = AnnotationSystem.AddOwnerSubset(state, pos, notMajority);
        }

        return state;
    }

    // ===========================================================
    // Stage 5 interaction-twist cards (M44)
    // ===========================================================

    /// <summary>
    /// Pose: spawn a courtier on a random unrevealed player tile (no targeting).
    /// The underlying player tile is unaffected — the courtier moves when
    /// interacted with, just like initially-placed courtiers.
    /// </summary>
    public static GameState ExecutePose(GameState state, Card card, Random rng)
    {
        var candidates = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position)
                        && !t.IsRevealed && !t.IsDestroyed
                        && t.Owner == TileOwner.Player
                        && !t.IsCourtier)
            .ToList();
        if (candidates.Count == 0) return state;

        var pick = candidates[rng.Next(candidates.Count)];
        var moveTarget = BoardSystem.SelectCourtierTarget(state.Board, pick.Position, rng);
        var spawned = pick
            .WithSpecial(SpecialTileType.Courtier) with { CourtierMoveTarget = moveTarget };

        var newTiles = state.Board.Tiles.ToList();
        newTiles[state.Board.TileIndex(pick.Position)] = spawned;
        return state with { Board = state.Board with { Tiles = newTiles } };
    }

    /// <summary>
    /// Taunt: tag N target tiles. Creates a TauntEffect with required-reveals = N-1.
    /// During subsequent rival turns, the rival's chained reveals stop early once
    /// any active Taunt's threshold is met.
    /// </summary>
    public static GameState ExecuteTaunt(GameState state, Position[]? targets, Card card)
    {
        if (targets == null || targets.Length == 0)
            throw new ArgumentException("Taunt requires at least 1 target tile");

        var positions = new HashSet<Position>(targets);
        var taunt = new TauntEffect
        {
            Positions = positions,
            RequiredReveals = Math.Max(1, targets.Length - 1)
        };

        var newTaunts = state.ActiveTaunts.ToList();
        newTaunts.Add(taunt);
        return state with { ActiveTaunts = newTaunts };
    }

    /// <summary>
    /// Walks a 4-directional line starting from the given origin and returns every
    /// unrevealed, processable tile encountered. Stops on:
    ///   - out-of-bounds
    ///   - unrevealed sanctum (M41 line-blocking rule)
    ///   - unreachable inner tile (M41)
    /// Revealed tiles are passed through (line continues, but they aren't returned).
    /// </summary>
    private static List<Position> ScanLine(Board board, Position origin, LineDirection direction)
    {
        var (dRow, dCol) = direction switch
        {
            LineDirection.Up => (-1, 0),
            LineDirection.Down => (1, 0),
            LineDirection.Left => (0, -1),
            LineDirection.Right => (0, 1),
            _ => (0, 0)
        };

        var result = new List<Position>();
        var current = origin;

        // Process the origin first (same step rules)
        while (true)
        {
            if (!board.IsUsablePosition(current)) break;
            var tile = board.GetTile(current);
            if (tile.IsDestroyed) break;

            if (tile.IsInner && !BoardSystem.CanReachInnerTile(board, current))
                break; // Unreachable inner blocks the line

            if (tile.IsRevealed)
            {
                // Pass through: don't add to result, but continue line
            }
            else if (tile.IsSanctum)
            {
                // Unrevealed sanctum blocks the line; don't add and stop
                break;
            }
            else
            {
                result.Add(current);
            }

            current = new Position(current.Row + dRow, current.Col + dCol);
            if (dRow == 0 && dCol == 0) break; // safety
        }

        return result;
    }
    /// Safety: Player(4) > Neutral(3) > Rival(2) > Mine(1).
    /// Non-revealed tiles get an ownerSubset annotation of types at-most-as-safe as the revealed tile.
    /// </summary>
    public static GameState ExecuteScurry(GameState state, Position[]? targets, Random rng, Card card)
    {
        if (targets == null || targets.Length != 2)
            throw new ArgumentException("Scurry requires exactly 2 target tiles");

        var tiles = targets.Select(p => (pos: p, tile: state.Board.GetTile(p))).ToList();

        if (tiles.Any(t => t.tile.IsRevealed))
            throw new ArgumentException("Cannot Scurry revealed tiles");

        // Sort by safety descending; break ties randomly
        var sorted = tiles.OrderByDescending(t => GetSafety(t.tile.Owner)).ToList();

        // Break ties with random
        if (sorted.Count == 2 && GetSafety(sorted[0].tile.Owner) == GetSafety(sorted[1].tile.Owner))
        {
            if (rng.Next(2) == 1)
                (sorted[0], sorted[1]) = (sorted[1], sorted[0]);
        }

        var safest = sorted[0];
        var other = sorted[1];

        // If the safer tile is a Courtier, clean (move) it and annotate true owner instead of revealing.
        var safestTile = state.Board.GetTile(safest.pos);
        if (safestTile.IsCourtier)
        {
            state = state with { Board = BoardSystem.CleanCourtier(state.Board, safest.pos, rng) };
            state = EquipmentSystem.OnCourtierCleaned(state, rng);
            state = AnnotationSystem.AddOwnerSubset(state, safest.pos, new HashSet<TileOwner> { safestTile.Owner });
        }
        // If the safer tile is ExtraDirty, clean it and annotate with true owner instead of revealing
        else if (safestTile.IsDirty)
        {
            var cleanedTile = safestTile.WithoutSpecial(SpecialTileType.ExtraDirty);
            var newTiles = state.Board.Tiles.ToList();
            newTiles[state.Board.TileIndex(safest.pos)] = cleanedTile;
            state = state with { Board = state.Board with { Tiles = newTiles } };

            // Annotate the cleaned tile with its true owner
            state = AnnotationSystem.AddOwnerSubset(state, safest.pos, new HashSet<TileOwner> { safestTile.Owner });
        }
        else
        {
            // Normal reveal of the safer tile
            state = BoardSystem.RevealTile(state.Board, safest.pos, PlayerType.Player) is var newBoard
                ? state with { Board = newBoard }
                : state;
        }

        // Annotate the other tile: types at-most-as-safe as what was revealed
        var revealedSafety = GetSafety(safest.tile.Owner);
        var possibleOwners = new HashSet<TileOwner>();
        foreach (TileOwner owner in Enum.GetValues<TileOwner>())
        {
            if (GetSafety(owner) <= revealedSafety)
                possibleOwners.Add(owner);
        }

        state = AnnotationSystem.AddOwnerSubset(state, other.pos, possibleOwners);

        return state;
    }

    /// <summary>
    /// Tingle: No targeting. Picks 1 random unrevealed rival/noble tile
    /// and annotates it with its exact owner type.
    /// Prefers "ambiguous" tiles (no single-owner annotation yet).
    /// </summary>
    public static GameState ExecuteTingle(GameState state, Random rng, Card card)
    {
        var candidates = state.Board.Tiles
            .Where(t => state.Board.IsUsablePosition(t.Position) && !t.IsRevealed
                        && (t.Owner == TileOwner.Rival || t.Owner == TileOwner.Noble)
                        && (!t.IsInner || BoardSystem.CanReachInnerTile(state.Board, t.Position)))
            .ToList();

        if (candidates.Count == 0)
            return state; // No valid targets

        // Prefer ambiguous tiles (no single-owner annotation)
        var ambiguous = candidates
            .Where(t => t.Annotations.OwnerSubset == null || t.Annotations.OwnerSubset.Count != 1)
            .ToList();

        var pool = ambiguous.Count > 0 ? ambiguous : candidates;
        var target = pool[rng.Next(pool.Count)];

        var exactOwner = new HashSet<TileOwner> { target.Owner };
        state = AnnotationSystem.AddOwnerSubset(state, target.Position, exactOwner);

        // Geode: playing Tingle draws a card.
        if (EquipmentSystem.HasEquipment(state, EquipmentEffectType.Geode))
        {
            state = DeckSystem.DrawCards(state, 1, rng);
        }
        return state;
    }

    /// <summary>
    /// Twirl: Gain 3 copper (5 if enhanced). Exhausts.
    /// </summary>
    public static GameState ExecuteTwirl(GameState state, Card card)
    {
        var copperGain = (card.Enhanced ? 5 : 3) * EquipmentSystem.CopperMultiplier(state);
        return state with { Copper = state.Copper + copperGain };
    }

    /// <summary>
    /// Brush: Target 1 tile (center of 3x3). For each unrevealed tile in area,
    /// pick a random non-owner and annotate to exclude it.
    /// </summary>
    public static GameState ExecuteBrush(GameState state, Position[]? targets, Random rng)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Brush requires exactly 1 target tile");

        var center = targets[0];
        var tilesInArea = BoardSystem.GetTilesInArea(state.Board, center, 1);
        var allOwners = new[] { TileOwner.Player, TileOwner.Rival, TileOwner.Neutral, TileOwner.Noble };

        // Clean (move) any courtiers in the area first
        var courtierPositions = tilesInArea
            .Where(t => t.IsCourtier)
            .Select(t => t.Position)
            .ToList();
        foreach (var p in courtierPositions)
        {
            state = state with { Board = BoardSystem.CleanCourtier(state.Board, p, rng) };
            state = EquipmentSystem.OnCourtierCleaned(state, rng);
        }

        foreach (var tile in BoardSystem.GetTilesInArea(state.Board, center, 1))
        {
            if (tile.IsRevealed) continue;
            if (tile.IsInner && !BoardSystem.CanReachInnerTile(state.Board, tile.Position)) continue;

            // Pick a random owner that ISN'T the tile's actual owner
            var nonOwners = allOwners.Where(o => o != tile.Owner).ToList();
            if (nonOwners.Count == 0) continue;

            var excludedOwner = nonOwners[rng.Next(nonOwners.Count)];

            // Annotate: all owners EXCEPT the excluded one
            var subset = new HashSet<TileOwner>(allOwners.Where(o => o != excludedOwner));
            state = AnnotationSystem.AddOwnerSubset(state, tile.Position, subset);
        }

        return state;
    }

    /// <summary>
    /// Sweep: Target 1 tile (center of 5x5). Remove ExtraDirty from all tiles in area;
    /// also clean (move) any courtiers in the area.
    /// </summary>
    public static GameState ExecuteSweep(GameState state, Position[]? targets, Random rng)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Sweep requires exactly 1 target tile");

        var center = targets[0];
        var tilesInArea = BoardSystem.GetTilesInArea(state.Board, center, 2);

        // Clean (move) courtiers first; CleanCourtier mutates the board.
        var courtierPositions = tilesInArea
            .Where(t => t.IsCourtier
                        && (!t.IsInner || BoardSystem.CanReachInnerTile(state.Board, t.Position)))
            .Select(t => t.Position)
            .ToList();
        foreach (var p in courtierPositions)
        {
            state = state with { Board = BoardSystem.CleanCourtier(state.Board, p, rng) };
            state = EquipmentSystem.OnCourtierCleaned(state, rng);
        }

        // Then clear ExtraDirty and LoungingNoble in the area
        var newTiles = state.Board.Tiles.ToList();
        var changed = false;
        foreach (var tile in BoardSystem.GetTilesInArea(state.Board, center, 2))
        {
            if (tile.IsInner && !BoardSystem.CanReachInnerTile(state.Board, tile.Position)) continue;
            var idx = state.Board.TileIndex(tile.Position);
            var t = newTiles[idx];
            var cleaned = t;
            if (t.IsDirty)
                cleaned = cleaned.WithoutSpecial(SpecialTileType.ExtraDirty);
            if (t.IsLoungingNoble)
                cleaned = cleaned.WithoutSpecial(SpecialTileType.LoungingNoble);
            if (!ReferenceEquals(cleaned, t) && cleaned != t)
            {
                newTiles[idx] = cleaned;
                changed = true;
            }
        }

        if (changed)
            state = state with { Board = state.Board with { Tiles = newTiles } };

        return state;
    }

    /// <summary>
    /// Caffeinate: Gain 2 spoons.
    /// </summary>
    public static GameState ExecuteCaffeinate(GameState state)
    {
        return state with { Spoons = state.Spoons + 2 };
    }

    /// <summary>
    /// Breathe: Draw 3 cards.
    /// </summary>
    public static GameState ExecuteBreathe(GameState state, Random rng)
    {
        return DeckSystem.DrawCards(state, 3, rng);
    }

    /// <summary>
    /// Lock In: Draw 2 cards.
    /// </summary>
    public static GameState ExecuteLockIn(GameState state, Random rng)
    {
        return DeckSystem.DrawCards(state, 2, rng);
    }

    /// <summary>
    /// Rendezvous: Reveal a random unrevealed player tile with rival adjacency,
    /// and a random unrevealed rival tile with player adjacency.
    /// </summary>
    public static GameState ExecuteRendezvous(GameState state, Random rng)
    {
        var board = state.Board;

        // Find unrevealed player tiles
        var playerTiles = board.Tiles
            .Where(t => board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Player)
            .ToList();

        // Find unrevealed rival tiles
        var rivalTiles = board.Tiles
            .Where(t => board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Rival)
            .ToList();

        // Reveal a random player tile with RIVAL adjacency
        if (playerTiles.Count > 0)
        {
            var target = playerTiles[rng.Next(playerTiles.Count)];
            var rivalAdj = BoardSystem.CalculateAdjacency(board, target.Position, PlayerType.Rival);
            var newTiles = board.Tiles.ToList();
            newTiles[board.TileIndex(target.Position)] = target with
            {
                IsRevealed = true,
                RevealedBy = PlayerType.Rival,
                AdjacencyCount = rivalAdj
            };
            board = board with { Tiles = newTiles };
        }

        // Reveal a random rival tile with PLAYER adjacency
        // Re-query because the board changed
        rivalTiles = board.Tiles
            .Where(t => board.IsUsablePosition(t.Position) && !t.IsRevealed && t.Owner == TileOwner.Rival)
            .ToList();

        if (rivalTiles.Count > 0)
        {
            var target = rivalTiles[rng.Next(rivalTiles.Count)];
            var playerAdj = BoardSystem.CalculateAdjacency(board, target.Position, PlayerType.Player);
            var newTiles = board.Tiles.ToList();
            newTiles[board.TileIndex(target.Position)] = target with
            {
                IsRevealed = true,
                RevealedBy = PlayerType.Player,
                AdjacencyCount = playerAdj
            };
            board = board with { Tiles = newTiles };
        }

        return state with { Board = board };
    }

    /// <summary>
    /// Argue: Target 1 tile (center of 3x3 area).
    /// Neutral tiles → annotated {Neutral}. Non-neutral tiles → annotated {Player, Rival, Noble}.
    /// Enhanced: also draw 1 card.
    /// </summary>
    public static GameState ExecuteArgue(GameState state, Position[]? targets, Random rng, Card card)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Argue requires exactly 1 target tile");

        var center = targets[0];
        var tilesInArea = BoardSystem.GetTilesInArea(state.Board, center, 1);

        foreach (var tile in tilesInArea)
        {
            if (tile.IsRevealed) continue;
            if (tile.IsInner && !BoardSystem.CanReachInnerTile(state.Board, tile.Position)) continue;

            var subset = tile.Owner == TileOwner.Neutral
                ? new HashSet<TileOwner> { TileOwner.Neutral }
                : new HashSet<TileOwner> { TileOwner.Player, TileOwner.Rival, TileOwner.Noble };

            state = AnnotationSystem.AddOwnerSubset(state, tile.Position, subset);
        }

        if (card.Enhanced)
        {
            state = DeckSystem.DrawCards(state, 1, rng);
        }

        return state;
    }

    /// <summary>
    /// Eavesdrop: Target 1 unrevealed tile. Does not reveal.
    /// Base: Player → {Player}, else → {Rival, Neutral, Noble}. Adds player adjacency info.
    /// Enhanced: exact owner type + full adjacency info.
    /// </summary>
    public static GameState ExecuteEavesdrop(GameState state, Position[]? targets, Card card)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Eavesdrop requires exactly 1 target tile");

        var pos = targets[0];
        var tile = state.Board.GetTile(pos);

        if (tile.IsRevealed)
            throw new ArgumentException("Cannot Eavesdrop a revealed tile");
        if (tile.IsInner && !BoardSystem.CanReachInnerTile(state.Board, pos))
            throw new ArgumentException("Cannot Eavesdrop an unreachable inner tile");

        if (card.Enhanced)
        {
            // Exact owner type
            state = AnnotationSystem.AddOwnerSubset(state, pos, new HashSet<TileOwner> { tile.Owner });
            // Full adjacency info
            var fullAdj = BoardSystem.CalculateFullAdjacency(state.Board, pos);
            state = AnnotationSystem.AddAdjacencyInfo(state, pos, fullAdj);
        }
        else
        {
            // Is yours or not yours
            var subset = tile.Owner == TileOwner.Player
                ? new HashSet<TileOwner> { TileOwner.Player }
                : new HashSet<TileOwner> { TileOwner.Rival, TileOwner.Neutral, TileOwner.Noble };
            state = AnnotationSystem.AddOwnerSubset(state, pos, subset);

            // Player adjacency only
            var playerAdj = BoardSystem.CalculatePlayerAdjacency(state.Board, pos);
            state = AnnotationSystem.AddAdjacencyInfo(state, pos, playerAdj);
        }

        return state;
    }

    /// <summary>
    /// Peek: Target 1 tile (center of burst-1-cross, or 3x3 if enhanced).
    /// Noble tiles → annotated {Noble}. Non-noble tiles → annotated {Player, Rival, Neutral}.
    /// Returns (state, foundNobles) for conditional exhaust.
    /// </summary>
    public static (GameState state, bool foundNobles) ExecutePeek(GameState state, Position[]? targets, Card card)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Peek requires exactly 1 target tile");

        var center = targets[0];
        var tilesInArea = card.Enhanced
            ? BoardSystem.GetTilesInArea(state.Board, center, 1)
            : BoardSystem.GetTilesInCross(state.Board, center);

        var foundNobles = false;

        foreach (var tile in tilesInArea)
        {
            if (tile.IsRevealed) continue;
            // Skip unreachable inner tiles (no adjacent revealed sanctum)
            if (tile.IsInner && !BoardSystem.CanReachInnerTile(state.Board, tile.Position)) continue;

            if (tile.Owner == TileOwner.Noble)
            {
                state = AnnotationSystem.AddOwnerSubset(state, tile.Position,
                    new HashSet<TileOwner> { TileOwner.Noble });
                foundNobles = true;
            }
            else
            {
                state = AnnotationSystem.AddOwnerSubset(state, tile.Position,
                    new HashSet<TileOwner> { TileOwner.Player, TileOwner.Rival, TileOwner.Neutral });
            }
        }

        return (state, foundNobles);
    }

    /// <summary>
    /// Explode: Destroy an unrevealed tile.
    /// Base: gain 1 Complaints stack, add Mollify to hand.
    /// Enhanced: no Complaints or Mollify.
    /// </summary>
    public static GameState ExecuteExplode(GameState state, Position[]? targets, Card card)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Explode requires exactly 1 target tile");

        var pos = targets[0];
        var tile = state.Board.GetTile(pos);

        if (tile.IsRevealed)
            throw new ArgumentException("Cannot Explode a revealed tile");
        if (tile.IsDestroyed)
            throw new ArgumentException("Cannot Explode a destroyed tile");

        // Destroy the tile
        var newTiles = state.Board.Tiles.ToList();
        newTiles[state.Board.TileIndex(pos)] = tile with { IsDestroyed = true };
        state = state with { Board = state.Board with { Tiles = newTiles } };

        if (!card.Enhanced)
        {
            // Gain 1 Complaints stack
            state = state with { ComplaintsStacks = state.ComplaintsStacks + 1 };

            // Add Mollify card to hand
            var mollify = CardDefinitions.Mollify with { Id = $"mollify_{Guid.NewGuid():N}" };
            var hand = state.Hand.ToList();
            hand.Add(mollify);
            state = state with { Hand = hand };
        }

        return state;
    }

    /// <summary>
    /// Deliver: Target an unrevealed tile.
    /// If Noble: convert to Neutral, reveal with player adjacency, +2 copper, does not end turn.
    /// If not Noble (base): no effect.
    /// Enhanced: also adds noble adjacency info regardless.
    /// </summary>
    public static GameState ExecuteDeliver(GameState state, Position[]? targets, Card card)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Deliver requires exactly 1 target tile");

        var pos = targets[0];
        var tile = state.Board.GetTile(pos);

        if (tile.IsRevealed)
            throw new ArgumentException("Cannot Deliver a revealed tile");

        // Enhanced: add noble adjacency info BEFORE reveal (since annotation skips revealed tiles)
        if (card.Enhanced)
        {
            var nobleCount = BoardSystem.GetNeighbors(state.Board, pos)
                .Count(n => state.Board.GetTile(n).Owner == TileOwner.Noble);
            state = AnnotationSystem.AddAdjacencyInfo(state, pos, new AdjacencyInfo { NobleCount = nobleCount });
        }

        if (tile.Owner == TileOwner.Noble)
        {
            // Convert noble to neutral, clear all special flags
            var newTiles = state.Board.Tiles.ToList();
            var currentTile = state.Board.GetTile(pos); // Re-fetch after possible annotation
            var converted = currentTile with { Owner = TileOwner.Neutral, Specials = SpecialTileType.None };
            newTiles[state.Board.TileIndex(pos)] = converted;
            var board = state.Board with { Tiles = newTiles };

            // Reveal the converted tile (now neutral, so player adjacency)
            board = BoardSystem.RevealTile(board, pos, PlayerType.Player);
            state = state with { Board = board };

            // Gain 2 copper (x2 with Tiara)
            state = state with { Copper = state.Copper + 2 * EquipmentSystem.CopperMultiplier(state) };
        }

        return state;
    }

    /// <summary>
    /// Brat: Target a revealed tile. Unreveal it, keeping annotations.
    /// Enhanced: also gain 2 copper.
    /// </summary>
    public static GameState ExecuteBrat(GameState state, Position[]? targets, Card card)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Brat requires exactly 1 target tile");

        var pos = targets[0];
        var tile = state.Board.GetTile(pos);

        if (!tile.IsRevealed)
            throw new ArgumentException("Brat can only target revealed tiles");

        // Unreveal the tile, keeping annotations and adjacency info
        var unrevealedTile = tile with
        {
            IsRevealed = false,
            RevealedBy = null
        };

        var newTiles = state.Board.Tiles.ToList();
        newTiles[state.Board.TileIndex(pos)] = unrevealedTile;
        var newBoard = state.Board with { Tiles = newTiles };

        // Un-revealing a sanctum closes its portal — recompute affected revealed tiles.
        if (tile.IsSanctum)
            newBoard = BoardSystem.RecomputeAdjacencyCounts(newBoard);

        state = state with { Board = newBoard };

        if (card.Enhanced)
        {
            state = state with { Copper = state.Copper + 2 * EquipmentSystem.CopperMultiplier(state) };
        }

        return state;
    }

    /// <summary>
    /// Accept Help: Target 1 tile (center of burst-1-cross).
    /// Find the safest owner type among unrevealed tiles. Reveal all tiles of that type.
    /// ExtraDirty tiles are cleaned and annotated instead of revealed.
    /// After playing, sets AcceptHelpDiscount for the rest of the floor.
    /// Enhanced: annotate all tiles with exact owner instead of revealing.
    /// </summary>
    public static GameState ExecuteAcceptHelp(GameState state, Position[]? targets, Card card, Random rng)
    {
        if (targets == null || targets.Length != 1)
            throw new ArgumentException("Accept Help requires exactly 1 target tile");

        var center = targets[0];
        var tilesInCross = BoardSystem.GetTilesInCross(state.Board, center);

        // Clean (move) any courtiers in the cross before evaluating
        var courtierPositions = tilesInCross
            .Where(t => t.IsCourtier)
            .Select(t => t.Position)
            .ToList();
        foreach (var p in courtierPositions)
        {
            state = state with { Board = BoardSystem.CleanCourtier(state.Board, p, rng) };
            state = EquipmentSystem.OnCourtierCleaned(state, rng);
        }
        // Re-fetch cross after cleaning
        tilesInCross = BoardSystem.GetTilesInCross(state.Board, center);
        var unrevealed = tilesInCross.Where(t => !t.IsRevealed).ToList();

        if (unrevealed.Count == 0)
        {
            // No unrevealed tiles, just set discount
            return state with { AcceptHelpDiscount = true };
        }

        // Find the safest owner type present
        var safestType = unrevealed
            .Select(t => t.Owner)
            .Distinct()
            .OrderByDescending(GetSafety)
            .First();

        if (card.Enhanced)
        {
            // Annotate safest tiles with exact owner
            foreach (var tile in unrevealed)
            {
                if (tile.Owner == safestType)
                {
                    state = AnnotationSystem.AddOwnerSubset(state, tile.Position,
                        new HashSet<TileOwner> { tile.Owner });
                }
                else
                {
                    // Non-safest: annotate as "anything less safe than the safest"
                    var possibleOwners = new HashSet<TileOwner>();
                    foreach (TileOwner owner in Enum.GetValues<TileOwner>())
                    {
                        if (GetSafety(owner) <= GetSafety(safestType))
                            possibleOwners.Add(owner);
                    }
                    state = AnnotationSystem.AddOwnerSubset(state, tile.Position, possibleOwners);
                }
            }
        }
        else
        {
            // Base: reveal all tiles of the safest type
            foreach (var tile in unrevealed)
            {
                if (tile.Owner != safestType) continue;

                if (tile.IsDirty)
                {
                    // Clean ExtraDirty, annotate with exact owner instead of revealing
                    var cleanedTile = tile.WithoutSpecial(SpecialTileType.ExtraDirty);
                    var newTiles = state.Board.Tiles.ToList();
                    newTiles[state.Board.TileIndex(tile.Position)] = cleanedTile;
                    state = state with { Board = state.Board with { Tiles = newTiles } };
                    state = AnnotationSystem.AddOwnerSubset(state, tile.Position,
                        new HashSet<TileOwner> { tile.Owner });
                }
                else
                {
                    var newBoard = BoardSystem.RevealTile(state.Board, tile.Position, PlayerType.Player);
                    state = state with { Board = newBoard };
                }
            }

            // Annotate remaining unrevealed tiles: exclude the safest type
            var remainingSubset = new HashSet<TileOwner>();
            foreach (TileOwner owner in Enum.GetValues<TileOwner>())
            {
                if (owner != safestType)
                    remainingSubset.Add(owner);
            }
            foreach (var tile in unrevealed)
            {
                if (tile.Owner == safestType) continue;
                state = AnnotationSystem.AddOwnerSubset(state, tile.Position, remainingSubset);
            }
        }

        // Set discount for future Accept Help cards
        return state with { AcceptHelpDiscount = true };
    }

    /// <summary>
    /// Ramble: Add Distraction stacks to the rival.
    /// Base: 2 stacks. Enhanced: 4 stacks.
    /// </summary>
    public static GameState ExecuteRamble(GameState state, Card card, Random rng)
    {
        var distractions = card.Enhanced ? 4 : 2;
        var points = new Dictionary<Position, int>(state.RivalIntentPoints);
        var excluded = IntentSystem.GetExcludedPositions(state.Board);
        for (var i = 0; i < distractions; i++)
        {
            IntentSystem.AddDistractionPoint(points, excluded, rng);
        }
        return state with { RivalIntentPoints = points };
    }

    /// <summary>
    /// Glaze: Gain 1 Excuses stack. Protects against the next noble reveal.
    /// Exhaust behavior handled by PlayCard (base exhausts, enhanced doesn't).
    /// </summary>
    public static GameState ExecuteGlaze(GameState state, Card card)
    {
        return state with { ExcusesStacks = state.ExcusesStacks + 1 };
    }

    /// <summary>
    /// Mollify: Reduce Complaints stacks by 1.
    /// </summary>
    public static GameState ExecuteMollify(GameState state)
    {
        var newStacks = Math.Max(0, state.ComplaintsStacks - 1);
        return state with { ComplaintsStacks = newStacks };
    }

    // ========== Food Cards ==========

    /// <summary>
    /// Read: +1 card draw per turn for N floors (base 2, enhanced 3).
    /// </summary>
    public static GameState ExecuteRead(GameState state, Card card)
    {
        var stacks = card.Enhanced ? 3 : 2;
        return state with { ReadStacks = state.ReadStacks + stacks };
    }

    /// <summary>
    /// Hydrate: +1 spoon on copper-granting player reveals for N floors (base 2, enhanced 3).
    /// </summary>
    public static GameState ExecuteHydrate(GameState state, Card card)
    {
        var stacks = card.Enhanced ? 3 : 2;
        return state with { HydrateStacks = state.HydrateStacks + stacks };
    }

    /// <summary>
    /// Adopt: Reveal 1 random player tile at floor start for N floors (base 2, enhanced 3).
    /// </summary>
    public static GameState ExecuteAdopt(GameState state, Card card)
    {
        var stacks = card.Enhanced ? 3 : 2;
        return state with { AdoptStacks = state.AdoptStacks + stacks };
    }

    /// <summary>
    /// Safety ranking for Scurry: Player(4) > Neutral(3) > Rival(2) > Noble(1).
    /// </summary>
    private static int GetSafety(TileOwner owner) => owner switch
    {
        TileOwner.Player => 4,
        TileOwner.Neutral => 3,
        TileOwner.Rival => 2,
        TileOwner.Noble => 1,
        _ => 0
    };
}

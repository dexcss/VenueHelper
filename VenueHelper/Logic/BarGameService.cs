using VenueHelper.Data;

namespace VenueHelper.Logic;

// Manages saved bar-game definitions and derives human-readable rules + the
// command players should roll + the payout.
public class BarGameService
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;

    public BarGameService(Plugin plugin) => Plugin = plugin;

    public List<BarGame> Games => Config.BarGames;

    public BarGame AddGame(string name)
    {
        var g = new BarGame(string.IsNullOrWhiteSpace(name) ? "New Game" : name.Trim());
        Config.BarGames.Add(g);
        Config.SelectedBarGame = Config.BarGames.Count - 1;
        Config.Save();
        return g;
    }

    public void RemoveGame(BarGame g)
    {
        Config.BarGames.Remove(g);
        if (Config.SelectedBarGame >= Config.BarGames.Count)
            Config.SelectedBarGame = Math.Max(0, Config.BarGames.Count - 1);
        Config.Save();
    }

    // ---- Derived helpers ----

    public static string RollCommand(BarGame g) => g.Roll switch
    {
        RollKind.RandomPlain => "/random",
        RollKind.RandomOutOf => $"/random {g.RollCeiling}",
        RollKind.Dice => $"/dice {g.RollCeiling}",
        _ => "/random",
    };

    // Describes the survival win/payout goal.
    // Short phrase describing what a "success" roll is, per survival mode.
    public static string SurvivalSuccessText(BarGame g) => g.Survival switch
    {
        SurvivalMode.StaticHL => $"rolling {(g.StaticHigher ? "higher" : "lower")} than {g.StaticThreshold}",
        SurvivalMode.DynamicHL => "calling higher/lower correctly",
        _ => $"rolling {(g.WinningNumbers.Count > 0 ? string.Join("/", g.WinningNumbers) : "the success number")}",
    };

    public static string SurvivalGoal(BarGame g)
    {
        if (g.SurvivalPrizeKind == SurvivalPrize.Tiered)
            return $"in a row \u2014 {g.TierPerStep:N0} gil for each success past {g.TierThreshold}";
        if (g.SurvivalPrizeKind == SurvivalPrize.HighScore)
            return "in a row \u2014 longest streak wins the pot";
        return $"{Math.Max(1, g.StreakNeeded)} times in a row";
    }

    public static string WinDescription(BarGame g) => g.Condition switch
    {
        WinCondition.SpecificNumbers => g.WinningNumbers.Count == 0
            ? "land on the winning number"
            : $"land on {string.Join(" or ", g.WinningNumbers)}",
        WinCondition.InRange => $"land between {g.RangeLow} and {g.RangeHigh}",
        WinCondition.Highest => "roll the highest",
        WinCondition.Lowest => "roll the lowest",
        WinCondition.ClosestTo => $"land closest to {g.ClosestTarget}",
        WinCondition.SurvivalStreak => g.Survival switch
        {
            SurvivalMode.StaticHL => $"roll {(g.StaticHigher ? "higher" : "lower")} than {g.StaticThreshold} {SurvivalGoal(g)}",
            SurvivalMode.DynamicHL => $"call higher or lower and beat your last roll {SurvivalGoal(g)}",
            _ => $"roll {(g.WinningNumbers.Count > 0 ? string.Join("/", g.WinningNumbers) : "the success number")} {SurvivalGoal(g)}",
        },
        _ => "win",
    };

    public static string PrizeDescription(BarGame g) => g.Prize switch
    {
        PrizeKind.FixedGil => $"{g.PrizeGil:N0} gil",
        PrizeKind.PercentOfPot => $"{g.PrizePercent:0}% of the pot",
        _ => "a prize",
    };

    // The payout for a win right now, given the live pot.
    public static long Payout(BarGame g) => g.Prize switch
    {
        PrizeKind.FixedGil => g.PrizeGil,
        PrizeKind.PercentOfPot => (long)(g.CurrentPot * (g.PrizePercent / 100f)),
        _ => 0,
    };

    // A one-line rules summary for announcing.
    public static string RulesLine(BarGame g)
    {
        var cmd = RollCommand(g);
        var win = WinDescription(g);
        var cost = g.EntryCost > 0 ? $" ({g.EntryCost:N0} gil to play)" : "";

        // Survival games: the goal text already describes the prize for tiered,
        // and for fixed we append the flat amount.
        if (g.Condition == WinCondition.SurvivalStreak)
        {
            if (g.SurvivalPrizeKind == SurvivalPrize.Tiered)
                return $"{g.Name}: {cmd} and {win}!{cost}";
            var fixedPrize = g.Prize == PrizeKind.PercentOfPot
                ? (g.PrizePercent >= 100f ? $"the whole pot ({Payout(g):N0} gil)" : $"{Payout(g):N0} gil ({g.PrizePercent:0}% of the pot)")
                : $"{g.PrizeGil:N0} gil";
            if (g.SurvivalPrizeKind == SurvivalPrize.HighScore)
                return $"{g.Name}: {cmd} \u2014 longest streak {SurvivalSuccessText(g)} wins {fixedPrize}!{cost}";
            return $"{g.Name}: {cmd} and {win} to win {fixedPrize}!{cost}";
        }

        string prize;
        if (g.Prize == PrizeKind.PercentOfPot)
        {
            var payout = Payout(g);
            prize = g.PrizePercent >= 100f
                ? $"the whole pot ({payout:N0} gil)"
                : $"{payout:N0} gil ({g.PrizePercent:0}% of the pot)";
        }
        else
        {
            prize = $"{g.PrizeGil:N0} gil";
        }

        return $"{g.Name}: {cmd} and {win} to win {prize}!{cost}";
    }

    // Check whether a roll value wins (for SpecificNumbers / InRange only;
    // Highest/Lowest/ClosestTo are comparative and judged across players).
    public static bool IsWinningRoll(BarGame g, int roll) => g.Condition switch
    {
        WinCondition.SpecificNumbers => g.WinningNumbers.Contains(roll),
        WinCondition.InRange => roll >= g.RangeLow && roll <= g.RangeHigh,
        _ => false,
    };

    // Pot management.
    public void ResetPot(BarGame g) { g.CurrentPot = g.StartingPot; g.PotStarted = true; Config.Save(); }
    public void AdjustPot(BarGame g, long delta) { g.CurrentPot = Math.Max(0, g.CurrentPot + delta); g.PotStarted = true; Config.Save(); }

    // ---- Live play tracking --------------------------------------------

    public BarGame? ActiveTrackingGame => Config.BarGames.FirstOrDefault(g => g.Tracking);

    public void StartTracking(BarGame g)
    {
        foreach (var other in Config.BarGames) other.Tracking = false;
        g.Tracking = true;
        Config.Save();
    }

    public void StopTracking(BarGame g) { g.Tracking = false; Config.Save(); }

    public void ClearPlays(BarGame g)
    {
        g.Players.Clear();
        g.Plays.Clear();
        Config.Save();
    }

    // Reset one player's survival run so they can buy in and play again.
    public void ResetPlayerRun(BarGame g, string fullName)
    {
        var p = ResolvePlayer(g, fullName);
        p.Streak = 0;
        p.StreakBusted = false;
        p.StreakWon = false;
        p.LastRoll = -1;
        p.PendingCall = 0;
        p.TierWinnings = 0;
        p.BestStreak = 0;
        Config.Save();
    }

    // Current high-score leader (highest best-streak); null if no one has scored.
    public static BarGamePlayer? HighScoreLeader(BarGame g)
    {
        BarGamePlayer? best = null;
        foreach (var p in g.Players.Values)
        {
            if (p.BestStreak <= 0) continue;
            if (best == null || p.BestStreak > best.BestStreak) best = p;
        }
        return best;
    }

    // Reset every player's survival run (streak/bust/calls), keeping their
    // buy-ins/pot. Best streak and banked tier gil are preserved.
    public void ResetAllRuns(BarGame g)
    {
        foreach (var p in g.Players.Values)
        {
            p.Streak = 0;
            p.StreakBusted = false;
            p.StreakWon = false;
            p.LastRoll = -1;
            p.PendingCall = 0;
        }
        Config.Save();
    }

    // Host sets a player's higher/lower call for their next dynamic roll.
    public void SetCall(BarGame g, string fullName, bool higher)
    {
        var p = ResolvePlayer(g, fullName);
        p.PendingCall = higher ? 1 : -1;
        Config.Save();
    }

    private BarGamePlayer ResolvePlayer(BarGame g, string fullName)
    {
        if (g.Players.TryGetValue(fullName, out var p))
            return p;
        var bare = StripWorldB(fullName);
        var existing = g.Players.Values.FirstOrDefault(x => StripWorldB(x.FullName) == bare);
        if (existing != null) return existing;
        var np = new BarGamePlayer(fullName);
        g.Players[fullName] = np;
        return np;
    }

    // Trade watcher calls this: credit gil toward the active game's buy-ins.
    // Paid buy-ins also grow a stacking pot.
    public void CreditTrade(string fullName, long gil)
    {
        var g = ActiveTrackingGame;
        if (g == null || gil <= 0) return;
        var p = ResolvePlayer(g, fullName);
        var before = p.PlaysBought(g.EntryCost);
        p.GilPaid += gil;
        var after = p.PlaysBought(g.EntryCost);
        // Grow the stacking pot by the number of new plays this trade bought.
        GrowPot(g, after - before);
        RestartFinishedRun(g, p);
        Config.Save();
    }

    // If a survival player's previous run is over, buying in again starts a
    // fresh run (clears the bust/won/streak so new rolls count). High-score
    // mode keeps their best streak so the leaderboard isn't wiped by a rebuy.
    private void RestartFinishedRun(BarGame g, BarGamePlayer p)
    {
        if (g.Condition != WinCondition.SurvivalStreak) return;
        if (!p.StreakBusted && !p.StreakWon) return;
        p.Streak = 0;
        p.StreakBusted = false;
        p.StreakWon = false;
        p.LastRoll = -1;
        p.PendingCall = 0;
        // Note: TierWinnings and BestStreak are intentionally preserved so a
        // re-buy keeps banked tier gil / leaderboard score across runs.
    }

    // Manually add ONE paid play (e.g. paid another way). Grows the pot.
    public void AddManualPlay(BarGame g, string fullName)
    {
        var p = ResolvePlayer(g, fullName);
        p.GilPaid += Math.Max(1, g.EntryCost);
        GrowPot(g, 1);
        RestartFinishedRun(g, p);
        Config.Save();
    }

    // Add a FREE play: the player gets a roll but it does NOT add to the pot.
    public void AddFreebie(BarGame g, string fullName)
    {
        var p = ResolvePlayer(g, fullName);
        // Grant a play's worth of "paid" allowance so a roll is accepted, but
        // don't grow the pot.
        p.GilPaid += Math.Max(1, g.EntryCost);
        RestartFinishedRun(g, p);
        Config.Save();
    }

    // Grow a stacking pot by n plays' worth (the entry cost each). No-op if not
    // stacking.
    private void GrowPot(BarGame g, int plays)
    {
        if (!g.StackingPot || plays <= 0) return;
        if (!g.PotStarted) { g.CurrentPot = g.StartingPot; g.PotStarted = true; }
        g.CurrentPot += g.EntryCost * plays;
    }

    // Hook calls this on every /random or /dice while a game is tracking.
    public void OnRoll(string fullName, int result, int outOf)
    {
        var g = ActiveTrackingGame;
        if (g == null) return;
        ProcessRoll(g, fullName, result, outOf);
    }

    // Manually enter a roll for a player (e.g. they rolled before capture
    // started). Routes through the same scoring as a captured roll, so survival
    // streaks, win detection, and play-gating all behave identically. Uses the
    // game's own ceiling for the roll's "out of".
    public void ManualRoll(BarGame g, string fullName, int result)
    {
        if (g == null) return;
        var outOf = g.Roll == RollKind.RandomPlain ? 1000 : g.RollCeiling;
        ProcessRoll(g, fullName, result, outOf);
    }

    private void ProcessRoll(BarGame g, string fullName, int result, int outOf)
    {
        var normalized = outOf <= 0 ? (g.Roll == RollKind.RandomPlain ? 1000 : g.RollCeiling) : outOf;

        // If the game expects a specific ceiling, ignore mismatched rolls.
        if (g.Roll != RollKind.RandomPlain && g.RollCeiling > 0 && normalized != g.RollCeiling)
            return;

        // Survival-streak games work differently: one buy-in buys a whole run of
        // rolls (not one roll per buy-in). The player keeps rolling until they
        // win or miss, depending on the survival mode.
        if (g.Condition == WinCondition.SurvivalStreak)
        {
            var sp = ResolvePlayer(g, fullName);

            if (sp.StreakBusted || sp.StreakWon)
                return; // run over; ignore until reset

            // A run is "in progress" once it has consumed a paid play (or the
            // baseline roll for dynamic mode). Only require a fresh paid play to
            // START a run, not for every roll within it.
            var runInProgress = sp.Streak > 0 || sp.LastRoll >= 0;
            if (!runInProgress && g.EntryCost > 0)
            {
                if (sp.PlaysRemaining(g.EntryCost) <= 0)
                {
                    Plugin.Log.Information($"Bar game: ignored roll from {sp.NameOnly} (no paid play to start a run).");
                    return;
                }
                // Consume one play to start this run.
                sp.PlaysUsed += 1;
            }

            // Decide success based on the survival mode.
            bool success;
            switch (g.Survival)
            {
                case SurvivalMode.StaticHL:
                    success = g.StaticHigher ? result > g.StaticThreshold : result < g.StaticThreshold;
                    break;
                case SurvivalMode.DynamicHL:
                    if (sp.LastRoll < 0)
                    {
                        // First roll just sets the baseline; not a scored step.
                        sp.LastRoll = result;
                        g.Plays.Add(new BarGamePlay(fullName, result, normalized, false));
                        Config.Save();
                        return;
                    }
                    if (sp.PendingCall == 0)
                    {
                        // No call made yet \u2014 ignore this roll, host must call first.
                        Plugin.Log.Information($"Bar game: {sp.NameOnly} rolled but no higher/lower call set.");
                        return;
                    }
                    success = sp.PendingCall > 0 ? result > sp.LastRoll : result < sp.LastRoll;
                    sp.LastRoll = result;
                    sp.PendingCall = 0;
                    break;
                default: // SameNumber
                    success = g.WinningNumbers.Contains(result);
                    break;
            }

            if (success)
            {
                sp.Streak += 1;
                if (sp.Streak > sp.BestStreak) sp.BestStreak = sp.Streak;
                // Tiered payout: each success past the threshold banks a step.
                if (g.SurvivalPrizeKind == SurvivalPrize.Tiered && sp.Streak > g.TierThreshold)
                    sp.TierWinnings += g.TierPerStep;
                if (g.SurvivalPrizeKind == SurvivalPrize.Fixed && sp.Streak >= Math.Max(1, g.StreakNeeded))
                    sp.StreakWon = true;
                // High-score mode: no fixed target \u2014 the streak just keeps
                // climbing as their score until they miss.
            }
            else
            {
                sp.StreakBusted = true;
            }

            g.Plays.Add(new BarGamePlay(fullName, result, normalized, sp.StreakWon));
            Config.Save();
            return;
        }

        if (g.EntryCost > 0)
        {
            var p = ResolvePlayer(g, fullName);
            if (p.PlaysRemaining(g.EntryCost) <= 0)
            {
                Plugin.Log.Information($"Bar game: ignored roll from {p.NameOnly} (no paid play).");
                return;
            }
            p.PlaysUsed += 1;
        }
        else
        {
            ResolvePlayer(g, fullName);
        }

        var won = IsWinningRoll(g, result);
        g.Plays.Add(new BarGamePlay(fullName, result, normalized, won));
        Config.Save();
    }

    public static BarGamePlay? ComparativeWinner(BarGame g)
    {
        if (g.Plays.Count == 0) return null;
        return g.Condition switch
        {
            WinCondition.Highest => g.Plays.OrderByDescending(p => p.Roll).First(),
            WinCondition.Lowest => g.Plays.OrderBy(p => p.Roll).First(),
            WinCondition.ClosestTo => g.Plays.OrderBy(p => Math.Abs(p.Roll - g.ClosestTarget)).First(),
            _ => null,
        };
    }

    public static bool IsComparative(BarGame g) =>
        g.Condition is WinCondition.Highest or WinCondition.Lowest or WinCondition.ClosestTo;

    private static string StripWorldB(string full)
    {
        var idx = full.IndexOf('\uE05D');
        return idx < 0 ? full : full[..idx];
    }
}

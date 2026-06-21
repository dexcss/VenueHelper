using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private string newBarGameName = string.Empty;
    private string barManualPlayer = string.Empty;
    private bool showBarHistory = false;
    private int barManualRoll = 0;
    private ChatChannel barAnnounceChannel = ChatChannel.Shout;
    // null = on the game-picker screen; otherwise editing this game.
    private BarGame? openBarGame = null;
    // When a game is open: false = clean play view, true = full setup editor.
    private bool barEditMode = false;

    private static readonly string[] RollKindLabels = { "/random (0-999)", "/random N", "/dice N" };
    private static readonly string[] WinCondLabels = { "Specific number(s)", "In a range", "Highest roll", "Lowest roll", "Closest to", "Survival streak (X in a row)", "Prize tiers (+ optional jackpot)" };
    private static readonly string[] SurvivalModeLabels = { "Same number each roll", "Higher/lower than a set number", "Higher/lower than previous roll (call it)" };
    private static readonly string[] SurvivalPrizeLabels = { "Fixed (reach a streak, win the pot/amount)", "Tiered (pays per success past a threshold)", "High score (longest streak wins the pot)" };
    private static readonly string[] PrizeKindLabels = { "Fixed gil", "% of pot" };

    private BarGameService Bar => Plugin.BarGames;

    private void DrawBarGameTab()
    {
        currentTab = "Bar Game Helper";

        // If the open game was deleted elsewhere, fall back to the picker.
        if (openBarGame != null && !Bar.Games.Contains(openBarGame))
            openBarGame = null;

        if (openBarGame == null)
            DrawBarGamePicker();
        else if (barEditMode)
            DrawBarGameEditor(openBarGame);
        else
            DrawBarGamePlay(openBarGame);

        DrawTabStatus("Bar Game Helper");
    }

    // ---- Picker screen (clean card list) ------------------------------
    private void DrawBarGamePicker()
    {
        ImGui.TextColored(Gold, "Bar Game Helper");
        WrapText(Grey, "Pick a game to run or edit, or make a new one. Build your own roll games with whatever pieces you need.");

        ImGuiHelpers.ScaledDummy(8f);

        // Saved games as selectable cards.
        if (Bar.Games.Count > 0)
        {
            ImGui.TextColored(Blue, "Your games");
            BarGame? toDelete = null;
            for (var i = 0; i < Bar.Games.Count; i++)
            {
                var game = Bar.Games[i];
                ImGui.PushID(i);

                // A framed, clickable row summarising the game.
                if (ImGui.BeginChild($"##card{i}", new Vector2(0, SW(58)), true))
                {
                    ImGui.TextColored(Gold, game.Name);
                    ImGui.TextColored(Grey, BarGameService.RulesLine(game));
                }
                ImGui.EndChild();
                if (ImGui.IsItemClicked())
                    { openBarGame = game; barEditMode = false; }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Click to open this game.");

                // Open / delete controls under the card.
                if (ImGui.SmallButton("Open")) { openBarGame = game; barEditMode = false; }
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
                if (ImGui.SmallButton("Delete")) ImGui.OpenPopup("##delgame");
                ImGui.PopStyleColor();
                if (ImGui.BeginPopup("##delgame"))
                {
                    ImGui.TextColored(Red, $"Delete \"{game.Name}\"?");
                    if (ImGui.Button("Yes, delete")) { toDelete = game; ImGui.CloseCurrentPopup(); }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
                    ImGui.EndPopup();
                }

                ImGuiHelpers.ScaledDummy(4f);
                ImGui.PopID();
            }
            if (toDelete != null) Bar.RemoveGame(toDelete);

            ImGuiHelpers.ScaledDummy(6f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(6f);
        }

        // Create new.
        ImGui.TextColored(Blue, "New game");
        ImGui.SetNextItemWidth(SW(200));
        ImGui.InputTextWithHint("##newgame", "New game name", ref newBarGameName, 48);
        ImGui.SameLine();
        if (ImGui.Button("Create"))
        {
            var g = Bar.AddGame(newBarGameName);
            newBarGameName = string.Empty;
            openBarGame = g;
            barEditMode = true;
        }
    }

    // ---- Play view (clean, setup hidden) ------------------------------
    // Describes a single survival roll for the table (hit/miss where it can be
    // determined from the rules alone). Dynamic mode depends on the live call,
    // so it's shown neutrally.
    private (string label, Vector4 color) SurvivalRollResult(BarGame g, int roll)
    {
        switch (g.Survival)
        {
            case SurvivalMode.SameNumber:
                return g.WinningNumbers.Contains(roll)
                    ? ("\u2713 hit", Green)
                    : ("\u2717 miss", Red);
            case SurvivalMode.StaticHL:
                var ok = g.StaticHigher ? roll > g.StaticThreshold : roll < g.StaticThreshold;
                return ok
                    ? ($"\u2713 {(g.StaticHigher ? ">" : "<")} {g.StaticThreshold}", Green)
                    : ($"\u2717 {(g.StaticHigher ? ">" : "<")} {g.StaticThreshold}", Red);
            default: // DynamicHL \u2014 outcome depends on the call at the time
                return ("rolled", new Vector4(0.85f, 0.85f, 0.85f, 1f));
        }
    }

    private void DrawBarGamePlay(BarGame g)
    {
        if (g.CurrentPot < g.StartingPot && !g.PotStarted)
        {
            g.CurrentPot = g.StartingPot;
            Config.Save();
        }

        if (ImGui.Button("\u2190 Back to games"))
        {
            if (g.Tracking) Bar.StopTracking(g);
            openBarGame = null;
            return;
        }
        ImGui.SameLine();
        if (ImGui.Button("Edit setup"))
        {
            barEditMode = true;
            return;
        }
        ImGui.SameLine();
        DrawExportButton("##export_bargame",
            new ExportItem("Roll history (names, rolls, results)", "bargame_rolls",
                () => ExportData.BarGameHistory(g)),
            new ExportItem("Player summary (gil paid, plays)", "bargame_players",
                () => ExportData.BarGamePlayers(g)),
            new ExportItem("Game history (past rounds, winners)", "bargame_results",
                () => ExportData.GameHistory("Bar Game History", Bar.BarHistory)));

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextColored(Gold, g.Name);
        var rules = BarGameService.RulesLine(g);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(560));
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1f), rules);
        if (!string.IsNullOrWhiteSpace(g.Notes))
            ImGui.TextColored(Grey, g.Notes);
        ImGui.PopTextWrapPos();

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Live jackpot (prize-tier games) --------------------------
        if (g.Condition == WinCondition.PrizeTiers && g.JackpotEnabled)
        {
            if (!g.JackpotStarted) { g.CurrentJackpot = g.JackpotStart; g.JackpotStarted = true; }
            ImGui.TextColored(new Vector4(1f, 0.84f, 0.2f, 1f),
                $"\uE0BE JACKPOT (roll {g.JackpotNumber}): {g.CurrentJackpot:N0} gil");
            ImGui.SameLine();
            if (ImGui.SmallButton("Reset jackpot")) { g.CurrentJackpot = g.JackpotStart; g.JackpotStarted = true; Config.Save(); }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip($"Reset the jackpot back to its starting seed ({GilFormat.Short(g.JackpotStart)}).");
        }

        // ---- Pot / Prize ----------------------------------------------
        if (g.StackingPot || g.Prize == PrizeKind.PercentOfPot)
        {
            ImGui.TextColored(Gold, "Pot");
            ImGui.SameLine();
            ImGui.TextColored(Green, $"{g.CurrentPot:N0} gil");
            ImGui.SameLine(0, 16);
            ImGui.TextColored(Grey, $"winner gets {BarGameService.Payout(g):N0} gil");
            if (ImGui.SmallButton("Reset pot")) Bar.ResetPot(g);
            // Nudge if a stacking pot is paired with a fixed (likely 0) prize.
            if (g.StackingPot && g.Prize == PrizeKind.FixedGil)
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(540));
                ImGui.TextColored(new Vector4(0.85f, 0.65f, 0.2f, 1f),
                    "This game has a stacking pot but the prize is a fixed gil amount. Set the prize to \"% of pot\" in Edit setup if the pot is the prize.");
                ImGui.PopTextWrapPos();
            }
            ImGuiHelpers.ScaledDummy(6f);
        }
        else if (g.Prize == PrizeKind.FixedGil)
        {
            ImGui.TextColored(Gold, $"Prize: {g.PrizeGil:N0} gil");
            ImGuiHelpers.ScaledDummy(6f);
        }

        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // ---- Live capture ---------------------------------------------
        ImGui.TextColored(Gold, "Live");
        if (!g.Tracking)
        {
            if (ImGui.Button("Start capturing rolls"))
            {
                if (Plugin.Raffle.IsClaimingTrades)
                    ImGui.OpenPopup("##barvsraffle");
                else
                    Bar.StartTracking(g);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Watches trades (buy-ins) and /random rolls. With an entry cost set, each buy-in pays for one roll.");

            if (ImGui.BeginPopup("##barvsraffle"))
            {
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(380));
                WrapText(Red, "A raffle is currently active and set to auto-credit trades.");
                WrapText(Grey, "If you start capturing here, incoming trades will count as bar-game buy-ins instead of raffle tickets. Run only one trade-based activity at a time.");
                ImGui.PopTextWrapPos();
                ImGuiHelpers.ScaledDummy(4f);
                if (ImGui.Button("Start anyway (trades go to this game)"))
                {
                    Bar.StartTracking(g);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.4f, 0.2f, 1f));
            if (ImGui.Button("Capturing... (click to stop)"))
                Bar.StopTracking(g);
            ImGui.PopStyleColor();
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear rolls"))
            ImGui.OpenPopup("##barclearrolls");
        if (ImGui.BeginPopup("##barclearrolls"))
        {
            WrapText(Red, "Clear this round? The winner and pot are saved to History first, then the rolls/players are cleared.");
            if (ImGui.Button("Yes, clear"))
            {
                Bar.ClearPlays(g);
                SetStatus("Round archived to history and cleared.", Green);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button(showBarHistory ? "Hide history" : $"History ({Bar.BarHistory.Count})"))
            showBarHistory = !showBarHistory;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Past rounds (winner + pot). A round is archived when you Clear rolls.");

        if (showBarHistory)
            DrawGameHistory(Bar.BarHistory, () => Bar.ClearBarHistory(), "##barhist");

        if (g.EntryCost > 0)
            WrapText(Grey, $"Each {g.EntryCost:N0} gil traded = 1 roll. Players roll {BarGameService.RollCommand(g)}.");
        else
            WrapText(Grey, $"Free game \u2014 one {BarGameService.RollCommand(g)} roll per player. Use \"Add freebie\" to grant someone an extra roll.");

        // Add a buy-in manually: from target or typed name, paid (adds to pot)
        // or freebie (roll without adding to the pot).
        if (ImGui.Button("Target"))
        {
            var t = Plugin.GetTargetName();
            if (string.IsNullOrEmpty(t)) SetStatus("No player targeted.", Red);
            else barManualPlayer = t.Replace('\uE05D', '@');
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Fill the box with your current target.");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(SW(170));
        ImGui.InputTextWithHint("##manualplayer", "Name@World", ref barManualPlayer, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add buy-in"))
        {
            if (!string.IsNullOrWhiteSpace(barManualPlayer))
            {
                Bar.AddManualPlay(g, barManualPlayer.Replace('@', '\uE05D'));
                SetStatus($"Added a paid buy-in for {barManualPlayer}.", Green);
                barManualPlayer = string.Empty;
            }
            else SetStatus("Enter or target a name first.", Red);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Grants one paid play and adds to the pot (for trades that weren't detected or other payment).");
        ImGui.SameLine();
        if (ImGui.Button("Add freebie"))
        {
            if (!string.IsNullOrWhiteSpace(barManualPlayer))
            {
                Bar.AddFreebie(g, barManualPlayer.Replace('@', '\uE05D'));
                SetStatus($"Added a FREE play for {barManualPlayer} (not added to pot).", Green);
                barManualPlayer = string.Empty;
            }
            else SetStatus("Enter or target a name first.", Red);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(g.EntryCost > 0
                ? "Grants one free play \u2014 they get a roll but it does NOT add to the pot."
                : "Grants one extra roll to this player (free games are one roll each by default).");

        // Manual roll entry: enter a roll for someone who rolled early / before
        // capture. Reuses the same name box; routes through the normal scoring.
        ImGui.TextColored(Grey, "Rolled early?");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(SW(110));
        ImGui.InputInt("##manualroll", ref barManualRoll, 1, 10);
        ImGui.SameLine();
        if (ImGui.Button("Enter roll"))
        {
            if (string.IsNullOrWhiteSpace(barManualPlayer))
                SetStatus("Enter or target a name first (uses the box above).", Red);
            else if (barManualRoll <= 0)
                SetStatus("Enter the number they rolled.", Red);
            else
            {
                Bar.ManualRoll(g, barManualPlayer.Replace('@', '\uE05D'), barManualRoll);
                SetStatus($"Entered roll {barManualRoll} for {barManualPlayer}.", Green);
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Manually record a roll for the name in the box above \u2014 scored exactly like a captured /random. Useful when someone rolls before you start capturing. They still need a paid buy-in/freebie to count.");

        ImGuiHelpers.ScaledDummy(6f);

        // Comparative winner highlight.
        BarGamePlay? compWinner = BarGameService.IsComparative(g) ? BarGameService.ComparativeWinner(g) : null;
        if (compWinner != null)
            WrapText(Gold, $"Current winner: {compWinner.NameOnly} with {compWinner.Roll}");

        // Survival-streak per-player status.
        if (g.Condition == WinCondition.SurvivalStreak && g.Players.Count > 0)
        {
            var tiered = g.SurvivalPrizeKind == SurvivalPrize.Tiered;
            var highScore = g.SurvivalPrizeKind == SurvivalPrize.HighScore;
            var need = Math.Max(1, g.StreakNeeded);

            BarGamePlayer? leader = highScore ? BarGameService.HighScoreLeader(g) : null;
            if (highScore)
            {
                ImGui.TextColored(Blue, "Leaderboard (longest streak wins):");
                ImGui.SameLine();
                if (leader != null)
                    WrapText(Gold, $"\u2605 {leader.NameOnly} leads with {leader.BestStreak} \u2014 pot {BarGameService.Payout(g):N0} gil");
                else
                    ImGui.TextColored(Grey, "no scores yet");
            }
            else
            {
                ImGui.TextColored(Blue, tiered
                    ? $"Runs ({g.TierPerStep:N0} gil per success past {g.TierThreshold}):"
                    : $"Runs (need {need} in a row):");
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("Reset all runs")) Bar.ResetAllRuns(g);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Resets everyone's current run at once (keeps buy-ins, pot, best streaks, and banked gil).");

            string? resetTarget = null;
            (string name, bool higher)? callTarget = null;
            var ordered = highScore
                ? g.Players.Values.OrderByDescending(p => p.BestStreak).ThenByDescending(p => p.Streak)
                : g.Players.Values.OrderByDescending(p => p.StreakWon).ThenByDescending(p => p.Streak);

            // "Active" = a run still in progress (not busted, not won).
            var anyActive = g.Players.Values.Any(p => !p.StreakBusted && !p.StreakWon);
            foreach (var pl in ordered)
            {
                var active = !pl.StreakBusted && !pl.StreakWon;
                // Green while actively playing; red when their run is over (or, if
                // nobody at all is active, the line reads red).
                var nameCol = active && anyActive ? Green : Red;
                if (highScore)
                {
                    var isLeader = leader != null && pl.FullName == leader.FullName && pl.BestStreak > 0;
                    var live = pl.StreakBusted ? "out" : $"on {pl.Streak}";
                    var line = $"  {(isLeader ? "\u2605 " : "")}{pl.NameOnly}: best {pl.BestStreak} ({live})";
                    ImGui.TextColored(nameCol, line);
                    ImGui.SameLine();
                }
                else
                {
                    var prog = tiered ? $"{pl.Streak} so far, banked {pl.TierWinnings:N0} gil" : $"{pl.Streak}/{need}";
                    if (pl.StreakWon)
                        ImGui.TextColored(Gold, $"  {pl.NameOnly}: WON! ({prog})");
                    else if (pl.StreakBusted)
                        WrapText(Red, $"  {pl.NameOnly}: out ({prog})" + (tiered && pl.TierWinnings > 0 ? $" \u2014 pay {pl.TierWinnings:N0} gil" : ""));
                    else
                        ImGui.TextColored(Green, $"  {pl.NameOnly}: {prog}");
                }

                // Dynamic higher/lower: show the call buttons + current baseline.
                if (g.Survival == SurvivalMode.DynamicHL && !pl.StreakWon && !pl.StreakBusted)
                {
                    ImGui.SameLine();
                    ImGui.PushID($"call{pl.FullName}");
                    if (pl.LastRoll >= 0)
                    {
                        if (pl.PendingCall != 0)
                        {
                            WrapText(Gold, $"\u2192 last {pl.LastRoll}, called {(pl.PendingCall > 0 ? "HIGHER" : "LOWER")} \u2014 they roll now");
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(1f, 0.8f, 0.2f, 1f), $"\u2192 last {pl.LastRoll}. Set their call:");
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Higher")) callTarget = (pl.FullName, true);
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Lower")) callTarget = (pl.FullName, false);
                        }
                    }
                    else
                    {
                        ImGui.TextColored(Grey, "\u2192 they roll once to set their starting number");
                    }
                    ImGui.PopID();
                }

                ImGui.SameLine();
                ImGui.PushID($"reset{pl.FullName}");
                if (ImGui.SmallButton("Reset run")) resetTarget = pl.FullName;
                ImGui.PopID();
            }
            if (callTarget != null) Bar.SetCall(g, callTarget.Value.name, callTarget.Value.higher);
            if (resetTarget != null) Bar.ResetPlayerRun(g, resetTarget);
            ImGuiHelpers.ScaledDummy(4f);
        }

        // Rolls table.
        const ImGuiTableFlags tflags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

        if (g.Condition == WinCondition.SurvivalStreak)
        {
            // Survival: one line per player, showing their rolls in order with
            // hit/miss coloring, instead of one row per roll.
            if (ImGui.BeginChild("##barsurvlist", new Vector2(0, SW(220)), true))
            {
                // Group plays by player, preserving roll order.
                var byPlayer = new List<(string name, List<BarGamePlay> rolls)>();
                foreach (var play in g.Plays)
                {
                    var entry = byPlayer.FirstOrDefault(e => e.name == play.NameOnly);
                    if (entry.rolls == null)
                    {
                        entry = (play.NameOnly, new List<BarGamePlay>());
                        byPlayer.Add(entry);
                    }
                    entry.rolls.Add(play);
                }

                if (byPlayer.Count == 0)
                    ImGui.TextColored(Grey, "No rolls yet.");

                foreach (var (name, rolls) in byPlayer)
                {
                    ImGui.TextColored(new Vector4(0.95f, 0.9f, 0.75f, 1f), $"{name}:");
                    var avail = ImGui.GetContentRegionAvail().X;
                    var spacing = ImGui.GetStyle().ItemSpacing.X;
                    var xUsed = ImGui.CalcTextSize($"{name}:").X;
                    foreach (var play in rolls)
                    {
                        var (_, col) = SurvivalRollResult(g, play.Roll);
                        var w = ImGui.CalcTextSize($"{play.Roll}").X;
                        // Keep on the same line only if it fits; else wrap.
                        if (xUsed + 8 + w <= avail)
                        {
                            ImGui.SameLine(0, 8);
                            xUsed += 8 + w;
                        }
                        else
                        {
                            xUsed = w; // wrapped to a new line
                        }
                        ImGui.TextColored(col, $"{play.Roll}");
                    }
                }
            }
            ImGui.EndChild();
        }
        else if (ImGui.BeginTable("##barplays", 4, tflags, new Vector2(0, SW(220))))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch, 1.6f);
            ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, SW(90));
            ImGui.TableSetupColumn("Result", ImGuiTableColumnFlags.WidthFixed, SW(90));
            ImGui.TableSetupColumn("Plays left", ImGuiTableColumnFlags.WidthFixed, SW(90));
            ImGui.TableHeadersRow();

            // Newest first.
            for (var i = g.Plays.Count - 1; i >= 0; i--)
            {
                var play = g.Plays[i];
                var isWinner = compWinner != null ? ReferenceEquals(play, compWinner) : play.Won;
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(play.NameOnly);
                ImGui.TableNextColumn();
                ImGui.TextColored(new Vector4(0.85f, 0.85f, 0.85f, 1f), $"{play.Roll}");
                ImGui.TableNextColumn();
                if (g.Condition == WinCondition.PrizeTiers)
                {
                    var (amt, jack) = BarGameService.ResolveTierPrize(g, play.Roll);
                    if (amt > 0)
                        ImGui.TextColored(jack ? new Vector4(1f, 0.84f, 0.2f, 1f) : Green,
                            jack ? $"JACKPOT {GilFormat.Short(amt)}" : GilFormat.Short(amt));
                    else
                        ImGui.TextColored(Grey, "-");
                }
                else
                {
                    ImGui.TextColored(isWinner ? Green : Grey, isWinner ? "WIN" : "-");
                }
                ImGui.TableNextColumn();
                var pb = g.Players.TryGetValue(play.FullName, out var pl) ? pl : null;
                WrapText(Grey, g.EntryCost > 0 && pb != null ? pb.PlaysRemaining(g.EntryCost).ToString() : "-");
            }
            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(6f);

        // Announce.
        if (ImGui.Button("Copy rules"))
        {
            ImGui.SetClipboardText(string.IsNullOrWhiteSpace(g.Notes) ? rules : $"{rules}  {g.Notes}");
            SetStatus("Rules copied to clipboard.", Green);
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(SW(90));
        var bc = (int)barAnnounceChannel;
        if (ImGui.BeginCombo("##announcechan_play", ChannelLabels[bc]))
        {
            for (var i = 0; i < ChannelLabels.Length; i++)
                if (ImGui.Selectable(ChannelLabels[i], i == bc)) barAnnounceChannel = (ChatChannel)i;
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.Button("Announce rules"))
        {
            var text = string.IsNullOrWhiteSpace(g.Notes) ? rules : $"{rules}  {g.Notes}";
            var (ok, msg) = ChatSender.SendToChannel(barAnnounceChannel, text);
            SetStatus(ok ? $"Announced to {ChannelLabels[bc]}." : msg, ok ? Green : Red);
        }
    }


    // ---- Editor screen ------------------------------------------------
    private void DrawBarGameEditor(BarGame g)
    {
        if (ImGui.Button("\u2190 Back to games"))
        {
            openBarGame = null;
            barEditMode = false;
            return;
        }
        ImGui.SameLine();
        if (ImGui.Button("Done editing \u2713"))
        {
            barEditMode = false;
            return;
        }
        ImGui.SameLine(0, 16);
        ImGui.TextColored(Gold, $"Editing: {g.Name}");

        ImGuiHelpers.ScaledDummy(4f);

        // Editable name.
        var name = g.Name;
        ImGui.SetNextItemWidth(SW(280));
        if (ImGui.InputTextWithHint("Game name", "Game name", ref name, 48)) { g.Name = name; Config.Save(); }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // ---- Roll (always shown) --------------------------------------
        ImGui.TextColored(Blue, "Roll");
        var rk = (int)g.Roll;
        ImGui.SetNextItemWidth(SW(160));
        if (ImGui.BeginCombo("Command", RollKindLabels[rk]))
        {
            for (var i = 0; i < RollKindLabels.Length; i++)
                if (ImGui.Selectable(RollKindLabels[i], i == rk)) { g.Roll = (RollKind)i; Config.Save(); }
            ImGui.EndCombo();
        }
        if (g.Roll != RollKind.RandomPlain)
        {
            ImGui.SameLine();
            var ceil = g.RollCeiling;
            ImGui.SetNextItemWidth(SW(140));
            if (ImGui.InputInt("out of / sides", ref ceil, 1, 10)) { g.RollCeiling = Math.Max(1, ceil); Config.Save(); }
        }
        ImGui.TextColored(Grey, $"Players roll:  {BarGameService.RollCommand(g)}");

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Win condition (always shown) -----------------------------
        ImGui.TextColored(Blue, "How to win");
        var wc = (int)g.Condition;
        ImGui.SetNextItemWidth(SW(180));
        if (ImGui.BeginCombo("Condition", WinCondLabels[wc]))
        {
            for (var i = 0; i < WinCondLabels.Length; i++)
                if (ImGui.Selectable(WinCondLabels[i], i == wc)) { g.Condition = (WinCondition)i; Config.Save(); }
            ImGui.EndCombo();
        }
        switch (g.Condition)
        {
            case WinCondition.SpecificNumbers:
                WrapText(Grey, "Winning numbers \u2014 a roll wins if it matches any of these:");
                if (g.WinningNumbers.Count == 0)
                    WrapText(Red, "No winning numbers set yet. Click \"+ Add winning number\" below.");

                int? removeAt = null;
                for (var wn = 0; wn < g.WinningNumbers.Count; wn++)
                {
                    ImGui.PushID($"win{wn}");
                    ImGui.TextColored(Grey, $"#{wn + 1}");
                    ImGui.SameLine();
                    var val = g.WinningNumbers[wn];
                    ImGui.SetNextItemWidth(SW(120));
                    if (ImGui.InputInt("##winnum", ref val, 1, 5))
                    {
                        g.WinningNumbers[wn] = Math.Max(0, val);
                        Config.Save();
                    }
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
                    if (ImGui.Button("Remove")) removeAt = wn;
                    ImGui.PopStyleColor();
                    ImGui.PopID();
                }
                if (removeAt != null)
                {
                    g.WinningNumbers.RemoveAt(removeAt.Value);
                    Config.Save();
                }

                if (ImGui.Button("+ Add winning number"))
                {
                    // Default the new box to one past the current highest (or 1).
                    var next = g.WinningNumbers.Count > 0 ? g.WinningNumbers.Max() + 1 : 1;
                    g.WinningNumbers.Add(next);
                    Config.Save();
                }
                break;
            case WinCondition.InRange:
                var lo = g.RangeLow; var hi = g.RangeHigh;
                ImGui.SetNextItemWidth(SW(120));
                if (ImGui.InputInt("Low", ref lo, 1, 10)) { g.RangeLow = lo; Config.Save(); }
                ImGui.SetNextItemWidth(SW(120));
                if (ImGui.InputInt("High", ref hi, 1, 10)) { g.RangeHigh = hi; Config.Save(); }
                break;
            case WinCondition.ClosestTo:
                var tgt = g.ClosestTarget;
                ImGui.SetNextItemWidth(SW(140));
                if (ImGui.InputInt("Target", ref tgt, 1, 10)) { g.ClosestTarget = tgt; Config.Save(); }
                break;
            case WinCondition.SurvivalStreak:
                WrapText(Grey, "Players keep rolling on one buy-in until they miss. Pick how a roll \"succeeds\":");

                // Mode selector.
                ImGui.SetNextItemWidth(SW(320));
                var sm = (int)g.Survival;
                if (ImGui.BeginCombo("Survival mode", SurvivalModeLabels[sm]))
                {
                    for (var i = 0; i < SurvivalModeLabels.Length; i++)
                        if (ImGui.Selectable(SurvivalModeLabels[i], i == sm)) { g.Survival = (SurvivalMode)i; Config.Save(); }
                    ImGui.EndCombo();
                }

                switch (g.Survival)
                {
                    case SurvivalMode.SameNumber:
                        WrapText(Grey, "Success number(s) \u2014 a roll continues the streak if it matches any:");
                        if (g.WinningNumbers.Count == 0)
                            ImGui.TextColored(Red, "No success numbers set. Click \"+ Add success number\".");
                        int? sRemove = null;
                        for (var wn = 0; wn < g.WinningNumbers.Count; wn++)
                        {
                            ImGui.PushID($"surv{wn}");
                            ImGui.TextColored(Grey, $"#{wn + 1}");
                            ImGui.SameLine();
                            var val = g.WinningNumbers[wn];
                            ImGui.SetNextItemWidth(SW(120));
                            if (ImGui.InputInt("##survnum", ref val, 1, 5)) { g.WinningNumbers[wn] = Math.Max(0, val); Config.Save(); }
                            ImGui.SameLine();
                            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
                            if (ImGui.Button("Remove")) sRemove = wn;
                            ImGui.PopStyleColor();
                            ImGui.PopID();
                        }
                        if (sRemove != null) { g.WinningNumbers.RemoveAt(sRemove.Value); Config.Save(); }
                        if (ImGui.Button("+ Add success number"))
                        {
                            var next = g.WinningNumbers.Count > 0 ? g.WinningNumbers.Max() + 1 : 1;
                            g.WinningNumbers.Add(next);
                            Config.Save();
                        }
                        break;
                    case SurvivalMode.StaticHL:
                        var higher = g.StaticHigher;
                        ImGui.SetNextItemWidth(SW(160));
                        if (ImGui.BeginCombo("Direction", higher ? "Higher than" : "Lower than"))
                        {
                            if (ImGui.Selectable("Higher than", higher)) { g.StaticHigher = true; Config.Save(); }
                            if (ImGui.Selectable("Lower than", !higher)) { g.StaticHigher = false; Config.Save(); }
                            ImGui.EndCombo();
                        }
                        ImGui.SameLine();
                        var thr = g.StaticThreshold;
                        ImGui.SetNextItemWidth(SW(120));
                        if (ImGui.InputInt("Threshold", ref thr, 1, 5)) { g.StaticThreshold = thr; Config.Save(); }
                        WrapText(Grey, $"Each roll must be {(g.StaticHigher ? "higher" : "lower")} than {g.StaticThreshold} to continue.");
                        break;
                    case SurvivalMode.DynamicHL:
                        WrapText(Grey, "Each player rolls a baseline, then you set their Higher/Lower call (in the play view) before each next roll. Beating the previous roll in the called direction continues the streak.");
                        break;
                }

                // ---- Prize style ----
                ImGuiHelpers.ScaledDummy(4f);
                ImGui.TextColored(Blue, "Survival prize");
                var sp = (int)g.SurvivalPrizeKind;
                ImGui.SetNextItemWidth(SW(260));
                if (ImGui.BeginCombo("Payout style", SurvivalPrizeLabels[sp]))
                {
                    for (var i = 0; i < SurvivalPrizeLabels.Length; i++)
                        if (ImGui.Selectable(SurvivalPrizeLabels[i], i == sp)) { g.SurvivalPrizeKind = (SurvivalPrize)i; Config.Save(); }
                    ImGui.EndCombo();
                }
                if (g.SurvivalPrizeKind == SurvivalPrize.Fixed)
                {
                    var need = g.StreakNeeded;
                    ImGui.SetNextItemWidth(SW(140));
                    if (ImGui.InputInt("In a row to win", ref need, 1, 1)) { g.StreakNeeded = Math.Max(1, need); Config.Save(); }
                    WrapText(Grey, "Reaching this streak wins the prize set in Pot & entry below.");
                }
                else if (g.SurvivalPrizeKind == SurvivalPrize.Tiered)
                {
                    var tt = g.TierThreshold;
                    ImGui.SetNextItemWidth(SW(140));
                    if (ImGui.InputInt("Pays after (in a row)", ref tt, 1, 1)) { g.TierThreshold = Math.Max(0, tt); Config.Save(); }
                    var per = (int)g.TierPerStep;
                    ImGui.SetNextItemWidth(SW(160));
                    if (ImGui.InputInt("Gil per success after", ref per, 1000, 10000)) { g.TierPerStep = Math.Max(0, per); Config.Save(); }
                    WrapText(Grey, $"Each success past {g.TierThreshold} pays {g.TierPerStep:N0} gil. E.g. {g.TierThreshold + 3} in a row = {g.TierPerStep * 3:N0} gil. Players keep going until they miss; they keep what they've banked.");
                }
                else // HighScore
                {
                    WrapText(Grey, "Everyone plays; each player's score is their longest streak. The player with the highest streak wins the pot (set in Pot & entry below) \u2014 you declare the winner when the round's done. The current leader is highlighted live in the play view.");
                }
                break;

            case WinCondition.PrizeTiers:
                WrapText(Grey, "Each roll is checked against the tiers below (first matching range wins its payout). Add a jackpot for an exact-number hit that grows with every buy-in.");
                ImGuiHelpers.ScaledDummy(2f);

                PrizeTier? removeTier = null;
                for (var ti = 0; ti < g.PrizeTiers.Count; ti++)
                {
                    var t = g.PrizeTiers[ti];
                    ImGui.PushID($"tier{ti}");
                    var tl = t.Low; var th = t.High;
                    ImGui.TextUnformatted("Roll"); ImGui.SameLine();
                    ImGui.SetNextItemWidth(SW(70));
                    if (ImGui.InputInt("##low", ref tl, 0, 0)) { t.Low = tl; Config.Save(); }
                    ImGui.SameLine(); ImGui.TextUnformatted("to"); ImGui.SameLine();
                    ImGui.SetNextItemWidth(SW(70));
                    if (ImGui.InputInt("##high", ref th, 0, 0)) { t.High = th; Config.Save(); }
                    ImGui.SameLine(); ImGui.TextUnformatted("wins"); ImGui.SameLine();
                    var amtText = t.Amount == 0 ? string.Empty : t.Amount.ToString();
                    ImGui.SetNextItemWidth(SW(120));
                    if (ImGui.InputTextWithHint("##amt", "gil (200k)", ref amtText, 24))
                    {
                        if (GilFormat.TryParse(amtText, out var parsed)) { t.Amount = Math.Max(0, parsed); Config.Save(); }
                        else if (string.IsNullOrWhiteSpace(amtText)) { t.Amount = 0; Config.Save(); }
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("x")) removeTier = t;
                    ImGui.PopID();
                }
                if (removeTier != null) { g.PrizeTiers.Remove(removeTier); Config.Save(); }
                if (ImGui.Button("+ Add tier")) { g.PrizeTiers.Add(new PrizeTier(1, 10, 0)); Config.Save(); }

                ImGuiHelpers.ScaledDummy(4f);
                var jpOn = g.JackpotEnabled;
                if (ImGui.Checkbox("Progressive jackpot", ref jpOn)) { g.JackpotEnabled = jpOn; Config.Save(); }
                if (g.JackpotEnabled)
                {
                    var jn = g.JackpotNumber;
                    ImGui.SetNextItemWidth(SW(90));
                    if (ImGui.InputInt("Jackpot number", ref jn, 0, 0)) { g.JackpotNumber = jn; Config.Save(); }

                    var startText = g.JackpotStart == 0 ? string.Empty : g.JackpotStart.ToString();
                    ImGui.SetNextItemWidth(SW(130));
                    if (ImGui.InputTextWithHint("Starting jackpot", "e.g. 5M", ref startText, 24))
                    {
                        if (GilFormat.TryParse(startText, out var js)) { g.JackpotStart = Math.Max(0, js); Config.Save(); }
                        else if (string.IsNullOrWhiteSpace(startText)) { g.JackpotStart = 0; Config.Save(); }
                    }

                    var perText = g.JackpotPerBuyIn == 0 ? string.Empty : g.JackpotPerBuyIn.ToString();
                    ImGui.SetNextItemWidth(SW(130));
                    if (ImGui.InputTextWithHint("Grows per buy-in", "e.g. 100k", ref perText, 24))
                    {
                        if (GilFormat.TryParse(perText, out var jp)) { g.JackpotPerBuyIn = Math.Max(0, jp); Config.Save(); }
                        else if (string.IsNullOrWhiteSpace(perText)) { g.JackpotPerBuyIn = 0; Config.Save(); }
                    }
                    WrapText(Grey, $"Current jackpot: {GilFormat.Short(g.JackpotStarted ? g.CurrentJackpot : g.JackpotStart)}. It seeds at the starting amount, grows each buy-in, and resets after someone hits {g.JackpotNumber}.");
                }
                break;
        }

        // ---- Pot & entry ----------------------------------------------
        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextColored(Blue, "Pot & entry");
        var start = (int)g.StartingPot;
        ImGui.SetNextItemWidth(SW(170));
        if (ImGui.InputInt("Starting pot", ref start, 1000, 10000))
        {
            g.StartingPot = Math.Max(0, start);
            if (!g.PotStarted) g.CurrentPot = g.StartingPot;
            Config.Save();
        }
        var entry = (int)g.EntryCost;
        ImGui.SetNextItemWidth(SW(170));
        if (ImGui.InputInt("Entry cost (gil)", ref entry, 1000, 10000)) { g.EntryCost = Math.Max(0, entry); Config.Save(); }
        var stacking = g.StackingPot;
        if (ImGui.Checkbox("Stacking pot (each buy-in adds the entry cost to the pot)", ref stacking)) { g.StackingPot = stacking; Config.Save(); }

        // ---- Prize ----------------------------------------------------
        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextColored(Blue, "Prize");
        var pk = (int)g.Prize;
        ImGui.SetNextItemWidth(SW(160));
        if (ImGui.BeginCombo("Payout", PrizeKindLabels[pk]))
        {
            for (var i = 0; i < PrizeKindLabels.Length; i++)
                if (ImGui.Selectable(PrizeKindLabels[i], i == pk)) { g.Prize = (PrizeKind)i; Config.Save(); }
            ImGui.EndCombo();
        }
        if (g.Prize == PrizeKind.FixedGil)
        {
            var pg = (int)g.PrizeGil;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(SW(170));
            if (ImGui.InputInt("Gil", ref pg, 1000, 10000)) { g.PrizeGil = Math.Max(0, pg); Config.Save(); }
        }
        else
        {
            var pp = g.PrizePercent;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(SW(140));
            if (ImGui.InputFloat("% of pot", ref pp, 1f, 5f, "%.0f")) { g.PrizePercent = Math.Clamp(pp, 0f, 100f); Config.Save(); }
        }

        // ---- Notes ----------------------------------------------------
        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextColored(Blue, "Notes (optional)");
        var notes = g.Notes;
        ImGui.SetNextItemWidth(SW(560));
        if (ImGui.InputTextWithHint("##notes", "Extra rules / notes shown when announcing", ref notes, 400)) { g.Notes = notes; Config.Save(); }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextColored(Grey, $"Preview:  {BarGameService.RulesLine(g)}");
    }
}

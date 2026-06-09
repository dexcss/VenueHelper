using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private string newBarGameName = string.Empty;
    private string barGameNewNumber = string.Empty;
    private string barManualPlayer = string.Empty;
    private ChatChannel barAnnounceChannel = ChatChannel.Shout;
    // null = on the game-picker screen; otherwise editing this game.
    private BarGame? openBarGame = null;
    // When a game is open: false = clean play view, true = full setup editor.
    private bool barEditMode = false;

    private static readonly string[] RollKindLabels = { "/random (0-999)", "/random N", "/dice N" };
    private static readonly string[] WinCondLabels = { "Specific number(s)", "In a range", "Highest roll", "Lowest roll", "Closest to" };
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
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(560));
        ImGui.TextColored(Grey, "Pick a game to run or edit, or make a new one. Build your own roll games with whatever pieces you need.");
        ImGui.PopTextWrapPos();

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

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextColored(Gold, g.Name);
        var rules = BarGameService.RulesLine(g);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(560));
        ImGui.TextColored(new Vector4(0.9f, 0.9f, 0.9f, 1f), rules);
        if (!string.IsNullOrWhiteSpace(g.Notes))
            ImGui.TextColored(Grey, g.Notes);
        ImGui.PopTextWrapPos();

        ImGuiHelpers.ScaledDummy(6f);

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
                ImGui.TextColored(Red, "A raffle is currently active and set to auto-credit trades.");
                ImGui.TextColored(Grey, "If you start capturing here, incoming trades will count as bar-game buy-ins instead of raffle tickets. Run only one trade-based activity at a time.");
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
        if (ImGui.Button("Clear rolls")) Bar.ClearPlays(g);

        if (g.EntryCost > 0)
            ImGui.TextColored(Grey, $"Each {g.EntryCost:N0} gil traded = 1 roll. Players roll {BarGameService.RollCommand(g)}.");
        else
            ImGui.TextColored(Grey, $"Free game \u2014 any {BarGameService.RollCommand(g)} roll is captured.");

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
            ImGui.SetTooltip("Grants one free play \u2014 they get a roll but it does NOT add to the pot.");

        ImGuiHelpers.ScaledDummy(6f);

        // Comparative winner highlight.
        BarGamePlay? compWinner = BarGameService.IsComparative(g) ? BarGameService.ComparativeWinner(g) : null;
        if (compWinner != null)
            ImGui.TextColored(Gold, $"Current winner: {compWinner.NameOnly} with {compWinner.Roll}");

        // Rolls table.
        const ImGuiTableFlags tflags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;
        if (ImGui.BeginTable("##barplays", 4, tflags, new Vector2(0, SW(220))))
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
                ImGui.TextColored(isWinner ? Green : Grey, isWinner ? "WIN" : "-");
                ImGui.TableNextColumn();
                var pb = g.Players.TryGetValue(play.FullName, out var pl) ? pl : null;
                ImGui.TextColored(Grey, g.EntryCost > 0 && pb != null ? pb.PlaysRemaining(g.EntryCost).ToString() : "-");
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
                ImGui.TextColored(Grey, "Winning numbers:");
                foreach (var n in g.WinningNumbers.ToList())
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"{n} x##{n}")) { g.WinningNumbers.Remove(n); Config.Save(); }
                }
                ImGui.SetNextItemWidth(SW(90));
                ImGui.InputTextWithHint("##addnum", "number", ref barGameNewNumber, 8);
                ImGui.SameLine();
                if (ImGui.Button("Add number"))
                {
                    if (int.TryParse(barGameNewNumber.Trim(), out var v) && !g.WinningNumbers.Contains(v))
                    {
                        g.WinningNumbers.Add(v);
                        g.WinningNumbers.Sort();
                        Config.Save();
                    }
                    barGameNewNumber = string.Empty;
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

using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private string deathrollManualName = string.Empty;

    private DeathrollManager Dr => Plugin.Deathroll;

    private void DrawDeathrollTab()
    {
        currentTab = "DR Tourny Helper";

        ImGui.TextColored(Gold, "DR Tourny Helper");
        WrapText(Grey, "Open with /random (0-999), roll down each turn. Roll a 1 to lose, a 0 to win instantly.");

        ImGuiHelpers.ScaledDummy(6f);

        if (!Dr.BracketBuilt)
            DrawDeathrollSetup();
        else
            DrawDeathrollBracket();

        DrawTabStatus("DR Tourny Helper");
    }

    private void DrawDeathrollSetup()
    {
        // Format toggle (button-based to match the rest of the UI).
        var single = Dr.Kind == BracketKind.SingleElimination;
        ImGui.TextColored(Grey, "Format:");
        ImGui.SameLine();
        if (single) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.45f, 0.25f, 1f));
        if (ImGui.Button("Single elimination"))
            Dr.Kind = BracketKind.SingleElimination;
        if (single) ImGui.PopStyleColor();
        ImGui.SameLine();
        if (!single) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.45f, 0.25f, 1f));
        if (ImGui.Button("Double elimination"))
            Dr.Kind = BracketKind.DoubleElimination;
        if (!single) ImGui.PopStyleColor();
        if (Dr.Kind == BracketKind.DoubleElimination)
            WrapText(Grey, "Losers get a second chance; a player is out after two losses.");

        ImGuiHelpers.ScaledDummy(6f);

        // Roll-off range (who goes first). 0 = plain /random (0-999).
        var rolloff = Dr.RolloffValue;
        ImGui.SetNextItemWidth(SW(140));
        if (ImGui.InputInt("Roll-off range", ref rolloff, 1, 10))
            Dr.RolloffValue = rolloff;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Both players /random this to decide who goes first; highest wins, ties re-roll.\n0 = plain /random (0-999).");
        ImGui.SameLine();
        WrapText(Grey, Dr.RolloffValue <= 0 ? "(players type /random)" : $"(players type /random {Dr.RolloffValue})");

        ImGuiHelpers.ScaledDummy(6f);

        // Add players.
        ImGui.TextColored(Blue, "Add players");
        if (ImGui.Button("Add Targeted Player"))
        {
            var target = Plugin.GetTargetName();
            if (string.IsNullOrEmpty(target))
                SetStatus("No player targeted.", Red);
            else if (Dr.AddPlayer(target) == null)
                SetStatus("Already added (or bracket locked).", Red);
            else
                SetStatus($"Added {target.Replace('\uE05D', '@')}.", Green);
        }
        ImGui.SameLine(0, 16);
        ImGui.SetNextItemWidth(SW(200));
        ImGui.InputTextWithHint("##drname", "Name@World", ref deathrollManualName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add by Name"))
        {
            if (string.IsNullOrWhiteSpace(deathrollManualName))
                SetStatus("Enter a Name@World first.", Red);
            else if (Dr.AddPlayer(deathrollManualName) == null)
                SetStatus("Already added.", Red);
            else
            {
                SetStatus($"Added {deathrollManualName}.", Green);
                deathrollManualName = string.Empty;
            }
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextColored(Blue, $"Players: {Dr.Players.Count}");

        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                                      | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;
        if (ImGui.BeginTable("##drplayers", 3, flags, new Vector2(0, 240)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 1.8f);
            ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableHeadersRow();

            DeathrollPlayer? remove = null;
            var id = 0;
            foreach (var p in Dr.Players)
            {
                ImGui.TableNextRow();
                ImGui.PushID(id++);
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextUnformatted(p.NameOnly);
                ImGui.TableNextColumn();
                ImGui.TextColored(Grey, p.World);
                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Remove")) remove = p;
                ImGui.PopID();
            }
            ImGui.EndTable();
            if (remove != null) Dr.RemovePlayer(remove);
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.45f, 0.25f, 1f));
        if (ImGui.Button("Build Bracket", new Vector2(140, 0)))
        {
            var (ok, msg) = Dr.BuildBracket();
            SetStatus(msg, ok ? Green : Red);
        }
        ImGui.PopStyleColor();
        ImGui.SameLine();
        if (ImGui.Button("Clear All"))
            ImGui.OpenPopup("##drclear");
        if (ImGui.BeginPopup("##drclear"))
        {
            ImGui.TextColored(Red, "Are you sure? Remove all players?");
            if (ImGui.Button("Yes, clear")) { Dr.ClearAll(); SetStatus("Cleared.", Red); ImGui.CloseCurrentPopup(); }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private void DrawDeathrollBracket()
    {
        // Champion banner.
        var champ = Dr.ChampionPlayer();
        if (champ != null)
            ImGui.TextColored(Gold, $"\uE0BE CHAMPION: {champ.NameOnly} \uE0BE");

        if (ImGui.Button("Reset Bracket"))
            ImGui.OpenPopup("##drreset");
        if (ImGui.BeginPopup("##drreset"))
        {
            ImGui.TextColored(Red, "Are you sure? This clears the bracket and all players.");
            if (ImGui.Button("Yes, reset")) { Dr.ClearAll(); SetStatus("Bracket reset.", Red); ImGui.CloseCurrentPopup(); }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        ImGuiHelpers.ScaledDummy(6f);

        // Active match referee panel.
        var active = Dr.ActiveMatch;
        if (active is { State: MatchState.InProgress })
            DrawActiveMatch(active);

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // Legend so the colours read clearly.
        ImGui.TextColored(Blue, "Bracket");
        ImGui.SameLine(0, 20);
        ImGui.TextColored(Gold, "\u25CF Ready");
        ImGui.SameLine(); ImGui.TextColored(Green, "\u25CF Live");
        ImGui.SameLine(); ImGui.TextColored(Grey, "\u25CF Pending / Done");
        ImGuiHelpers.ScaledDummy(4f);

        // Bracket laid out round by round in side-by-side bordered columns.
        var rounds = Dr.TotalRounds;
        if (ImGui.BeginChild("##bracketscroll", new Vector2(0, 380), false, ImGuiWindowFlags.HorizontalScrollbar))
        {
            const float colWidth = 200f;
            for (var r = 1; r <= rounds; r++)
            {
                var matches = Dr.RoundMatches(r).ToList();

                ImGui.BeginGroup();

                // Round header.
                ImGui.TextColored(Gold, RoundName(r, rounds, matches));
                WrapText(Grey, $"{matches.Count(x => x.State == MatchState.Done)}/{matches.Count} done");
                ImGuiHelpers.ScaledDummy(2f);

                // Each round gets its own bordered column so the matches are
                // visually grouped instead of floating loose. EndChild must be
                // called unconditionally (ImGui requirement), unlike BeginPopup.
                ImGui.BeginChild($"##round{r}", new Vector2(colWidth, 0), true);
                var i = 0;
                foreach (var m in matches)
                {
                    DrawMatchCard(m, i + 1);
                    i++;
                }
                ImGui.EndChild();

                ImGui.EndGroup();

                // Space between round columns.
                if (r < rounds)
                    ImGui.SameLine(0, 18);
            }
        }
        ImGui.EndChild();
    }

    private void DrawMatchCard(DeathrollMatch m, int matchNumber)
    {
        var a = Dr.GetPlayer(m.PlayerA);
        var b = Dr.GetPlayer(m.PlayerB);
        var aName = a?.NameOnly ?? "\u2014 (bye/TBD)";
        var bName = b?.NameOnly ?? "\u2014 (bye/TBD)";

        ImGui.PushID(m.Id.ToString());

        var (dotColor, label) = m.State switch
        {
            MatchState.Done => (Grey, "done"),
            MatchState.InProgress => (Green, "live"),
            MatchState.Ready => (Gold, "ready"),
            _ => (Grey, "waiting"),
        };

        // Card header: status dot + match number + state label.
        ImGui.TextColored(dotColor, "\u25CF");
        ImGui.SameLine(0, 4);
        ImGui.TextColored(Grey, $"Match {matchNumber}");
        ImGui.SameLine();
        ImGui.TextColored(dotColor, label);
        if (m.BestOf > 1)
        {
            ImGui.SameLine();
            ImGui.TextColored(Blue, $"Bo{m.BestOf}");
        }

        // Player A row (winner highlighted gold with a marker, loser dimmed).
        DrawSlot(aName, m.Winner != null && m.Winner == m.PlayerA, m.State == MatchState.Done && m.Winner != m.PlayerA && m.PlayerA != null);
        // Player B row.
        DrawSlot(bName, m.Winner != null && m.Winner == m.PlayerB, m.State == MatchState.Done && m.Winner != m.PlayerB && m.PlayerB != null);

        if (m.State == MatchState.Ready)
        {
            if (ImGui.SmallButton("Referee \u25B6"))
                Dr.StartMatch(m);
        }
        else if (m.State == MatchState.InProgress)
        {
            ImGui.TextColored(Green, "refereeing now");
        }

        // Separator between match cards within a round column.
        ImGuiHelpers.ScaledDummy(2f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);
        ImGui.PopID();
    }

    // One competitor line inside a match card.
    private void DrawSlot(string name, bool isWinner, bool isLoser)
    {
        if (isWinner)
            ImGui.TextColored(Gold, $"\u25B6 {name}");
        else if (isLoser)
            ImGui.TextColored(new Vector4(0.45f, 0.45f, 0.45f, 1f), $"   {name}");
        else
            ImGui.TextColored(new Vector4(0.85f, 0.85f, 0.85f, 1f), $"   {name}");
    }

    private void DrawActiveMatch(DeathrollMatch m)
    {
        var a = Dr.GetPlayer(m.PlayerA);
        var b = Dr.GetPlayer(m.PlayerB);
        if (a == null || b == null) return;

        ImGui.TextColored(Gold, $"Refereeing: {a.NameOnly}  vs  {b.NameOnly}");

        if (m.BestOf > 1)
        {
            var needed = m.BestOf / 2 + 1;
            WrapText(Blue, $"Best of {m.BestOf} (first to {needed})  \u2014  Game {m.CurrentGame}:  {a.NameOnly} {m.WinsA} \u2013 {m.WinsB} {b.NameOnly}");
        }

        if (m.InRolloff)
        {
            var range = Dr.RolloffValue <= 0 ? "/random" : $"/random {Dr.RolloffValue}";
            WrapText(Blue, $"Roll-off \u2014 both players {range} (highest goes first, ties re-roll)");
            var aDone = m.RolloffValueA >= 0 ? m.RolloffValueA.ToString() : "waiting";
            var bDone = m.RolloffValueB >= 0 ? m.RolloffValueB.ToString() : "waiting";
            ImGui.TextColored(Grey, $"{a.NameOnly}: {aDone}    {b.NameOnly}: {bDone}");
        }
        else
        {
            var turn = Dr.GetPlayer(m.ExpectedRoller);
            if (m.CurrentCeiling == 0)
                ImGui.TextColored(Grey, turn != null
                    ? $"{turn.NameOnly} opens with a plain /random"
                    : "Waiting for the opening /random...");
            else
                ImGui.TextColored(Grey, turn != null
                    ? $"{turn.NameOnly}'s turn \u2014 should roll /random {m.CurrentCeiling}"
                    : $"Next roll should be /random {m.CurrentCeiling}");
        }

        // Live roll log (rejected rolls shown greyed with a reason).
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("##drrolls", 4, flags, new Vector2(0, 140)))
        {
            ImGui.TableSetupColumn("Player");
            ImGui.TableSetupColumn("Rolled");
            ImGui.TableSetupColumn("Out Of");
            ImGui.TableSetupColumn("Note");
            ImGui.TableHeadersRow();
            foreach (var roll in m.Rolls)
            {
                ImGui.TableNextRow();
                if (roll.Rejected)
                {
                    ImGui.TableNextColumn(); ImGui.TextColored(Grey, roll.PlayerName);
                    ImGui.TableNextColumn(); ImGui.TextColored(Grey, roll.Roll.ToString());
                    ImGui.TableNextColumn(); ImGui.TextColored(Grey, roll.OutOf.ToString());
                    ImGui.TableNextColumn(); ImGui.TextColored(Red, $"rejected: {roll.RejectReason}");
                }
                else
                {
                    var danger = !m.InRolloff && roll.Roll <= 1;
                    ImGui.TableNextColumn(); ImGui.TextColored(danger ? Red : Grey, roll.PlayerName);
                    ImGui.TableNextColumn(); ImGui.TextColored(danger ? Red : Green, roll.Roll.ToString());
                    ImGui.TableNextColumn(); ImGui.TextColored(Grey, roll.OutOf.ToString());
                    ImGui.TableNextColumn(); ImGui.TextColored(Grey, "");
                }
            }
            ImGui.EndTable();
        }

        // Manual override + stop.
        ImGui.TextColored(Grey, "Manual override:");
        if (ImGui.SmallButton($"{a.NameOnly} wins")) Dr.SetWinnerManually(m, a);
        ImGui.SameLine();
        if (ImGui.SmallButton($"{b.NameOnly} wins")) Dr.SetWinnerManually(m, b);
        ImGui.SameLine(0, 16);
        if (ImGui.SmallButton("Stop refereeing")) Dr.StopMatch();
    }

    private string RoundName(int round, int totalRounds, List<Data.DeathrollMatch> matches)
    {
        // Double elimination: label by bracket.
        if (Dr.Kind == Data.BracketKind.DoubleElimination)
        {
            // The single highest round is the grand final.
            if (round == totalRounds && matches.Count == 1)
                return "Grand Final";
            if (matches.Count > 0 && matches[0].IsLosersBracket)
                return $"Losers Round {round}";
            return $"Winners Round {round}";
        }

        var fromEnd = totalRounds - round;
        return fromEnd switch
        {
            0 => "Final",
            1 => "Semifinals",
            2 => "Quarterfinals",
            _ => $"Round {round}",
        };
    }
}

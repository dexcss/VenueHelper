using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private string raffleManualName = string.Empty;
    private int raffleManualTickets = 1;
    private float raffleHouseCutInput = -1f;
    private string raffleSearch = string.Empty;
    private string raffleImportBuffer = string.Empty;

    private void DrawRaffleTab()
    {
        currentTab = "Raffle Helper";

        if (raffleHouseCutInput < 0)
            raffleHouseCutInput = Raffle.HouseCutPercent;

        DrawTabHeader("Raffle Helper", "##export_raffle",
            new ExportItem("Ticket list (name per ticket, for wheelofnames.com)", "raffle_list",
                () => ExportData.RaffleList(Raffle.Entries)),
            new ExportItem("Summary (1 row per player, includes notes)", "raffle_summary",
                () => ExportData.RaffleSummary(Raffle.Entries)));
        ImGui.TextColored(Grey, "Enter how many tickets each player bought, then assign 0-999 for the draw.");

        // Warn if a bar game is currently capturing trades \u2014 they'd be taken as
        // bar-game buy-ins instead of raffle tickets.
        var activeGame = Plugin.BarGames.ActiveTrackingGame;
        if (activeGame != null)
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(560));
            ImGui.TextColored(Red, $"Heads up: the Bar Game \"{activeGame.Name}\" is capturing trades right now, so incoming trades will count as its buy-ins, NOT raffle tickets. Stop that game's capture before running a trade-based raffle.");
            ImGui.PopTextWrapPos();
        }

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Settings -------------------------------------------------
        ImGui.SetNextItemWidth(SW(140));
        if (ImGui.InputFloat("House cut %", ref raffleHouseCutInput, 1f, 5f, "%.0f"))
            Raffle.SetHouseCut(raffleHouseCutInput);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Percentage the house keeps (e.g. 20 for an 80/20 split). Shown in the pot summary below.");

        ImGui.SameLine(0, 24);
        var autoTrade = Config.RaffleAutoTrade;
        if (ImGui.Checkbox("Auto-credit trades as tickets", ref autoTrade))
        {
            Config.RaffleAutoTrade = autoTrade;
            Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When a trade completes, the gil received is divided by the ticket cost and added as tickets.");
        ImGui.SameLine(0, 12);
        var costInput = (int)Config.TicketCost;
        ImGui.SetNextItemWidth(SW(150));
        if (ImGui.InputInt("Ticket cost (for trades)", ref costInput, 1000, 10000))
        {
            Config.TicketCost = Math.Max(1, costInput);
            Config.Save();
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // ---- Add a buy-in ---------------------------------------------
        ImGui.TextColored(Blue, "Add a buy-in");
        if (ImGui.Button("Fill from Target"))
        {
            var target = Plugin.GetTargetName();
            if (string.IsNullOrEmpty(target))
                SetStatus("No player targeted.", Red);
            else
            {
                // Put their Name@World into the box; the host then chooses
                // Add Tickets or Add Free Ticket.
                raffleManualName = target.Replace('\uE05D', '@');
                SetStatus($"Filled in {VenueHelper.Logic.VenueCounter.NameOnly(target)} \u2014 now pick Add Tickets or Add Free Ticket.", Green);
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Fills the name box with your current target. Then set the ticket count and click Add Tickets (or Add Free Ticket).");
        ImGui.SameLine(0, 16);
        ImGui.SetNextItemWidth(SW(180));
        ImGui.InputTextWithHint("##rname", "Name@World", ref raffleManualName, 64);
        ImGui.SameLine();
        ImGui.TextColored(Grey, "tickets");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(SW(130));
        ImGui.InputInt("##rtickets", ref raffleManualTickets, 1, 5);
        if (raffleManualTickets < 0) raffleManualTickets = 0;
        ImGui.SameLine();
        if (ImGui.Button("Add Tickets"))
        {
            if (string.IsNullOrWhiteSpace(raffleManualName))
            {
                SetStatus("Enter a Name@World first.", Red);
            }
            else
            {
                var entry = Raffle.AddManual(raffleManualName);
                if (raffleManualTickets > 0) Raffle.AddTickets(entry, raffleManualTickets);
                SetStatus($"Added {raffleManualTickets} ticket(s) to {entry.NameOnly}.", Green);
                raffleManualName = string.Empty;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Add Free Ticket"))
        {
            if (string.IsNullOrWhiteSpace(raffleManualName))
            {
                SetStatus("Enter a Name@World first.", Red);
            }
            else
            {
                var entry = Raffle.AddManual(raffleManualName);
                var n = Math.Max(1, raffleManualTickets);
                Raffle.AddFreeTickets(entry, n);
                SetStatus($"Added {n} FREE ticket(s) to {entry.NameOnly} (not added to pot).", Green);
                raffleManualName = string.Empty;
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Adds comp tickets: the name goes into the draw but it doesn't count toward the pot.");

        // ---- Import list ----------------------------------------------
        ImGuiHelpers.ScaledDummy(4f);
        if (ImGui.Button("Import list from clipboard"))
        {
            var clip = ImGui.GetClipboardText() ?? string.Empty;
            var added = Raffle.ImportNames(clip);
            SetStatus(added > 0 ? $"Imported {added} name(s) from clipboard." : "No new names found in clipboard.",
                added > 0 ? Green : Red);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Paste names from a spreadsheet (comma or newline separated) into your clipboard, then click. Each becomes an entry with 0 tickets.");

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // ---- Ticket numbers + pot summary -----------------------------
        var total = Raffle.TotalTickets;
        ImGui.TextColored(Blue, "Ticket numbers");
        ImGui.SameLine(0, 16);
        ImGui.TextColored(total > 1000 ? Red : Grey,
            $"{Raffle.Entries.Count} players, {total} tickets" + (total > 1000 ? "  (over 1000 \u2014 use the wheel!)" : ""));

        if (ImGui.Button("Assign 0-999"))
        {
            var (ok, msg) = Raffle.AssignSequential();
            SetStatus(msg, ok ? Green : Red);
        }
        ImGui.SameLine();
        if (ImGui.Button("Assign (shuffled)"))
        {
            var (ok, msg) = Raffle.AssignShuffled();
            SetStatus(msg, ok ? Green : Red);
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear Numbers"))
        {
            Raffle.ClearNumbers();
            SetStatus("Ticket numbers cleared.", Grey);
        }
        ImGui.SameLine();
        {
            var resetW = ImGui.CalcTextSize("Reset Raffle").X + ImGui.GetStyle().FramePadding.X * 2 + SW(4);
            var avail = ImGui.GetContentRegionAvail().X;
            if (avail > resetW)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - resetW);
        }
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
        if (ImGui.Button("Reset Raffle"))
            ImGui.OpenPopup("##resetraffle");
        ImGui.PopStyleColor();
        if (ImGui.BeginPopup("##resetraffle"))
        {
            ImGui.TextColored(Red, "Are you sure? Clear all raffle entries?");
            if (ImGui.Button("Yes, reset"))
            {
                Raffle.Reset();
                SetStatus("Raffle cleared.", Red);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        // Pot summary uses PAID tickets only (free/comp tickets don't add gil).
        var pot = (long)Raffle.TotalPaidTickets * Config.TicketCost;
        if (pot > 0)
        {
            var cut = (long)(pot * Raffle.HouseCutPercent / 100f);
            ImGui.TextColored(Grey, $"Estimated pot: {pot:N0} gil   |   House ({Raffle.HouseCutPercent:0}%): {cut:N0}   |   Winner: {pot - cut:N0}");
        }

        ImGuiHelpers.ScaledDummy(4f);
        if (ImGui.Button("Copy for external website"))
        {
            var data = ExportData.RaffleList(Raffle.Entries);
            if (data.Rows.Count == 0)
                SetStatus("Nothing to copy yet \u2014 add buy-ins first.", Red);
            else
            {
                ImGui.SetClipboardText(Exporter.BuildText(data));
                SetStatus($"Copied {data.Rows.Count} tickets (names only) \u2014 paste into wheelofnames.com.", Green);
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Copies one NAME per ticket (no notes, no world) for wheelofnames.com.");

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.SetNextItemWidth(SW(220));
        ImGui.InputTextWithHint("##rsearch", "Search by name or note...", ref raffleSearch, 64);
        ImGuiHelpers.ScaledDummy(4f);

        // ---- Table ----------------------------------------------------
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                                      | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

        if (ImGui.BeginTable("##raffletable", 6, flags, new Vector2(0, 280)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch, 1.8f);
            ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthStretch, 1.1f);
            ImGui.TableSetupColumn("Tickets", ImGuiTableColumnFlags.WidthFixed, SW(210));
            ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthStretch, 1.6f);
            ImGui.TableSetupColumn("Numbers", ImGuiTableColumnFlags.WidthStretch, 1.8f);
            ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, SW(28));
            ImGui.TableHeadersRow();

            var rows = Raffle.Entries
                .Where(e => string.IsNullOrWhiteSpace(raffleSearch)
                            || e.NameOnly.Contains(raffleSearch, StringComparison.OrdinalIgnoreCase)
                            || (e.Note ?? "").Contains(raffleSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();

            RaffleEntry? toRemove = null;
            var id = 0;
            foreach (var e in rows)
            {
                ImGui.TableNextRow();
                ImGui.PushID(id++);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Selectable(e.NameOnly);
                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText(e.NameOnly);

                ImGui.TableNextColumn();
                ImGui.TextColored(Grey, e.World);

                // Tickets with +/- (no hidden floor; minus walks to 0).
                // Shows paid count, plus free/comp count if any.
                ImGui.TableNextColumn();
                ImGui.TextColored(e.PaidTickets > 0 ? Green : Grey, e.PaidTickets.ToString());
                ImGui.SameLine();
                if (ImGui.SmallButton("+")) Raffle.AddTickets(e, 1);
                ImGui.SameLine();
                if (ImGui.SmallButton("-")) Raffle.AddTickets(e, -1);
                if (e.FreeTickets > 0)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(Blue, $"+{e.FreeTickets} free");
                    ImGui.SameLine();
                    if (ImGui.SmallButton("-##free")) Raffle.AddFreeTickets(e, -1);
                }

                // Editable note.
                ImGui.TableNextColumn();
                var note = e.Note ?? string.Empty;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("##note", "Discord, etc.", ref note, 128))
                {
                    e.Note = note;
                    Config.Save();
                }

                ImGui.TableNextColumn();
                ImGui.TextColored(Grey, e.TicketNumbers.Count > 0 ? string.Join(", ", e.TicketNumbers) : "-");

                ImGui.TableNextColumn();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                    toRemove = e;

                ImGui.PopID();
            }
            ImGui.EndTable();

            if (toRemove != null)
                Raffle.Remove(toRemove);
        }

        DrawTabStatus("Raffle Helper");
    }
}

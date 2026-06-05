using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private string raffleManualName = string.Empty;
    private int raffleManualGil = 100000;
    private int ticketCostInput = -1; // lazily synced from config on first draw
    private string raffleSearch = string.Empty;

    private void DrawRaffleTab()
    {
        currentTab = "Raffle Helper";

        if (ticketCostInput < 0)
            ticketCostInput = (int)Config.TicketCost;

        DrawTabHeader("Raffle Helper", "##export_raffle",
            new ExportItem("Ticket list (1 line per ticket, for wheelofnames.com)", "raffle_list",
                () => ExportData.RaffleList(Raffle.Entries, Config.TicketCost)),
            new ExportItem("Summary (1 row per player)", "raffle_summary",
                () => ExportData.RaffleSummary(Raffle.Entries, Config.TicketCost)));
        ImGui.TextColored(Grey, "Track buy-ins, auto-detect trades, and assign numbers 1-999 for a draw.");

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Settings -------------------------------------------------
        ImGui.SetNextItemWidth(160);
        if (ImGui.InputInt("Ticket cost (gil)", ref ticketCostInput, 1000, 10000))
        {
            if (ticketCostInput < 1) ticketCostInput = 1;
            Raffle.SetTicketCost(ticketCostInput);
        }
        ImGui.SameLine(0, 24);
        var autoTrade = Config.RaffleAutoTrade;
        if (ImGui.Checkbox("Auto-credit incoming trades", ref autoTrade))
        {
            Config.RaffleAutoTrade = autoTrade;
            Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When a trade completes, the gil you receive is added to that player's buy-in.");

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // ---- Add players ----------------------------------------------
        ImGui.TextColored(Blue, "Add a buy-in");
        if (ImGui.Button("Add Targeted Player"))
        {
            var target = Plugin.GetTargetName();
            if (string.IsNullOrEmpty(target))
                SetStatus("No player targeted.", Red);
            else
            {
                var entry = Raffle.GetOrCreate(target);
                if (raffleManualGil > 0)
                    Raffle.CreditGil(entry.FullName, raffleManualGil);
                SetStatus($"Added {entry.NameOnly} and credited {raffleManualGil:N0} gil.", Green);
            }
        }
        ImGui.SameLine(0, 16);
        ImGui.SetNextItemWidth(180);
        ImGui.InputTextWithHint("##rname", "Name@World", ref raffleManualName, 64);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120);
        ImGui.InputInt("##rgil", ref raffleManualGil, 1000, 10000);
        ImGui.SameLine();
        if (ImGui.Button("Add / Credit Gil"))
        {
            if (string.IsNullOrWhiteSpace(raffleManualName))
            {
                SetStatus("Enter a Name@World first.", Red);
            }
            else
            {
                var entry = Raffle.AddManual(raffleManualName);
                if (raffleManualGil > 0)
                    Raffle.CreditGil(entry.FullName, raffleManualGil);
                SetStatus($"Credited {raffleManualGil:N0} gil to {entry.NameOnly}.", Green);
                raffleManualName = string.Empty;
            }
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // ---- Ticket numbers -------------------------------------------
        var total = Raffle.TotalTickets;
        ImGui.TextColored(Blue, "Ticket numbers");
        ImGui.SameLine(0, 16);
        ImGui.TextColored(total > 999 ? Red : Grey,
            $"{Raffle.Entries.Count} players, {total} tickets" + (total > 999 ? "  (over 999!)" : ""));

        if (ImGui.Button("Assign 1-999 (sequential)"))
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
        // Reset raffle, right-aligned.
        ImGui.SameLine();
        {
            var resetW = ImGui.CalcTextSize("Reset Raffle").X + ImGui.GetStyle().FramePadding.X * 2 + 4;
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

        ImGuiHelpers.ScaledDummy(4f);
        // Quick one-click copy of the ticket list (one line per ticket) for
        // pasting into wheelofnames.com or similar.
        if (ImGui.Button("Copy for external website"))
        {
            var data = ExportData.RaffleList(Raffle.Entries, Config.TicketCost);
            if (data.Rows.Count == 0)
            {
                SetStatus("Nothing to copy yet \u2014 add buy-ins first.", Red);
            }
            else
            {
                ImGui.SetClipboardText(Exporter.BuildText(data));
                SetStatus($"Copied {data.Rows.Count} tickets \u2014 paste into wheelofnames.com.", Green);
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Copies one line per ticket, ready to paste into wheelofnames.com.");

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.SetNextItemWidth(220);
        ImGui.InputTextWithHint("##rsearch", "Search by name...", ref raffleSearch, 64);
        ImGuiHelpers.ScaledDummy(4f);

        // ---- Table ----------------------------------------------------
        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                                      | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

        if (ImGui.BeginTable("##raffletable", 6, flags, new Vector2(0, 280)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch, 2.0f);
            ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthStretch, 1.2f);
            ImGui.TableSetupColumn("Gil Paid", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Tickets", ImGuiTableColumnFlags.WidthFixed, 110);
            ImGui.TableSetupColumn("Numbers", ImGuiTableColumnFlags.WidthStretch, 2.0f);
            ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, 28);
            ImGui.TableHeadersRow();

            var rows = Raffle.Entries
                .Where(e => string.IsNullOrWhiteSpace(raffleSearch)
                            || e.NameOnly.Contains(raffleSearch, StringComparison.OrdinalIgnoreCase))
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

                ImGui.TableNextColumn();
                ImGui.TextColored(Gold, e.GilPaid.ToString("N0"));

                ImGui.TableNextColumn();
                var tickets = e.TicketCount(Config.TicketCost);
                ImGui.TextColored(tickets > 0 ? Green : Grey, tickets.ToString());
                ImGui.SameLine();
                if (ImGui.SmallButton("+"))
                    Raffle.AddManualTickets(e, 1);
                ImGui.SameLine();
                if (ImGui.SmallButton("-"))
                    Raffle.AddManualTickets(e, -1);

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

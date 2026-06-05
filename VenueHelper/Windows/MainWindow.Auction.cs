using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private string auctionManualName = string.Empty;
    private float houseCutInput = -1f; // lazily synced from config
    private string auctionHistorySearch = string.Empty;
    private bool showAuctionHistory = false;

    private void DrawAuctionTab()
    {
        currentTab = "Auction Helper";

        if (houseCutInput < 0)
            houseCutInput = Config.HouseCutPercent;

        DrawTabHeader("Auction Helper", "##export_auction",
            new ExportItem("Auction history", "auction_history", () => ExportData.AuctionHistory(Auction.History)));
        ImGui.TextColored(Grey, "Target a player to put them up for auction, note what they're offering, then record the sale.");

        ImGuiHelpers.ScaledDummy(6f);

        // ---- House cut + view toggle ----------------------------------
        ImGui.SetNextItemWidth(140);
        if (ImGui.InputFloat("House cut %", ref houseCutInput, 1f, 5f, "%.0f"))
        {
            houseCutInput = (float)Math.Round(Math.Clamp(houseCutInput, 0f, 100f));
            Auction.SetHouseCut(houseCutInput);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Percentage the house keeps from each sale. Captured per-sale when you finalize.");

        // Prominent, right-aligned toggle between the active list and history.
        var toggleLabel = showAuctionHistory ? "\u2190 Active Auctions" : "History & Totals \u2192";
        ImGui.SameLine();
        {
            var w = ImGui.CalcTextSize(toggleLabel).X + ImGui.GetStyle().FramePadding.X * 2 + 8;
            var avail = ImGui.GetContentRegionAvail().X;
            if (avail > w)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - w);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.35f, 0.55f, 1f));
            if (ImGui.Button(toggleLabel))
                showAuctionHistory = !showAuctionHistory;
            ImGui.PopStyleColor();
        }

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        if (showAuctionHistory)
            DrawAuctionHistory();
        else
            DrawAuctionActive();

        DrawTabStatus("Auction Helper");
    }

    private void DrawAuctionActive()
    {
        // ---- Add players ----------------------------------------------
        // Players are added without a note here; set the note in the table below.
        ImGui.TextColored(Blue, "Add to the block");
        if (ImGui.Button("Add Targeted Player"))
        {
            var target = Plugin.GetTargetName();
            if (string.IsNullOrEmpty(target))
                SetStatus("No player targeted.", Red);
            else if (Auction.Add(target, string.Empty) == null)
                SetStatus("That player is already on the auction list.", Red);
            else
                SetStatus($"Added {target.Replace('\uE05D', '@')} to the auction.", Green);
        }

        ImGui.SameLine(0, 16);
        ImGui.SetNextItemWidth(200);
        ImGui.InputTextWithHint("##amanual", "Name@World", ref auctionManualName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add by Name"))
        {
            if (string.IsNullOrWhiteSpace(auctionManualName))
                SetStatus("Enter a Name@World first.", Red);
            else if (Auction.Add(auctionManualName, string.Empty) == null)
                SetStatus("That player is already on the auction list.", Red);
            else
            {
                SetStatus($"Added {auctionManualName} to the auction.", Green);
                auctionManualName = string.Empty;
            }
        }

        // Reset Active List, right-aligned.
        ImGui.SameLine();
        {
            var w = ImGui.CalcTextSize("Reset Active List").X + ImGui.GetStyle().FramePadding.X * 2 + 4;
            var avail = ImGui.GetContentRegionAvail().X;
            if (avail > w)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - w);
        }
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
        if (ImGui.Button("Reset Active List"))
            ImGui.OpenPopup("##resetactive");
        ImGui.PopStyleColor();
        if (ImGui.BeginPopup("##resetactive"))
        {
            ImGui.TextColored(Red, "Are you sure? Clear everyone currently on the block?");
            if (ImGui.Button("Yes, clear"))
            {
                Auction.ClearActive();
                SetStatus("Active auction list cleared.", Red);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextColored(Blue, $"On the block now: {Auction.Active.Count}");

        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                                      | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

        if (ImGui.BeginTable("##auctionactive", 6, flags, new Vector2(0, 320)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch, 1.4f);
            ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthStretch, 1.6f);
            ImGui.TableSetupColumn("Won By", ImGuiTableColumnFlags.WidthStretch, 1.4f);
            ImGui.TableSetupColumn("Sale Price", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("Sell", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, 28);
            ImGui.TableHeadersRow();

            AuctionEntry? toRemove = null;
            AuctionEntry? toFinalize = null;
            var id = 0;

            foreach (var e in Auction.Active)
            {
                ImGui.TableNextRow();
                ImGui.PushID(id++);

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Selectable(e.NameOnly);
                if (ImGui.IsItemClicked())
                    ImGui.SetClipboardText(e.NameOnly);

                ImGui.TableNextColumn();
                var note = e.Note;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputText("##note", ref note, 128))
                {
                    e.Note = note;
                    Auction.Save();
                }

                ImGui.TableNextColumn();
                var winner = e.Winner;
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("##winner", "winning bidder", ref winner, 64))
                {
                    e.Winner = winner;
                    Auction.Save();
                }

                ImGui.TableNextColumn();
                var priceInt = (int)e.SalePrice;
                ImGui.SetNextItemWidth(-1);
                // step = 0 hides the +/- buttons; the host just types the amount.
                if (ImGui.InputInt("##price", ref priceInt, 0, 0))
                {
                    e.SalePrice = Math.Max(0, priceInt);
                    Auction.Save();
                }

                ImGui.TableNextColumn();
                if (ImGui.Button("Sold"))
                    toFinalize = e;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Move to history with the current house cut applied.");

                ImGui.TableNextColumn();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                    toRemove = e;

                ImGui.PopID();
            }
            ImGui.EndTable();

            if (toRemove != null)
                Auction.Remove(toRemove);
            if (toFinalize != null)
            {
                var (ok, msg) = Auction.Finalize(toFinalize);
                SetStatus(msg, ok ? Green : Red);
            }
        }
    }

    private void DrawAuctionHistory()
    {
        // ---- Totals summary -------------------------------------------
        ImGui.TextColored(Gold, $"Total gil through the house: {Auction.TotalGilThroughHouse:N0}");
        ImGui.TextColored(Green, $"House made (cut): {Auction.TotalHouseCut:N0}");
        ImGui.TextColored(Grey, $"Paid out to sellers: {Auction.TotalPayouts:N0}   |   Sales recorded: {Auction.History.Count}");

        ImGuiHelpers.ScaledDummy(4f);

        ImGuiHelpers.ScaledDummy(4f);

        ImGui.SetNextItemWidth(220);
        ImGui.InputTextWithHint("##ahsearch", "Search name, note or winner", ref auctionHistorySearch, 64);
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
        if (ImGui.Button("Clear History"))
            ImGui.OpenPopup("##clearauctionhistory");
        ImGui.PopStyleColor();
        if (ImGui.BeginPopup("##clearauctionhistory"))
        {
            ImGui.TextColored(Red, "Are you sure? Permanently clear all auction history?");
            if (ImGui.Button("Yes, clear"))
            {
                Auction.ClearHistory();
                SetStatus("Auction history cleared.", Red);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        ImGui.TextColored(Grey, "Use the Export button (top right) to save history.");

        ImGuiHelpers.ScaledDummy(4f);

        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                                      | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

        if (ImGui.BeginTable("##auctionhistory", 8, flags, new Vector2(0, 300)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 110);
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch, 1.3f);
            ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthStretch, 1.3f);
            ImGui.TableSetupColumn("Won By", ImGuiTableColumnFlags.WidthStretch, 1.3f);
            ImGui.TableSetupColumn("Sale", ImGuiTableColumnFlags.WidthFixed, 95);
            ImGui.TableSetupColumn("Cut %", ImGuiTableColumnFlags.WidthFixed, 50);
            ImGui.TableSetupColumn("House", ImGuiTableColumnFlags.WidthFixed, 95);
            ImGui.TableSetupColumn("Payout", ImGuiTableColumnFlags.WidthFixed, 95);
            ImGui.TableHeadersRow();

            var rows = Enumerable.Reverse(Auction.History)
                .Where(r => string.IsNullOrWhiteSpace(auctionHistorySearch)
                            || r.NameOnly.Contains(auctionHistorySearch, StringComparison.OrdinalIgnoreCase)
                            || r.Note.Contains(auctionHistorySearch, StringComparison.OrdinalIgnoreCase)
                            || r.Winner.Contains(auctionHistorySearch, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var r in rows)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(Grey, r.When.ToString("MM-dd HH:mm"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(r.NameOnly);
                ImGui.TableNextColumn();
                ImGui.TextColored(Grey, r.Note);
                ImGui.TableNextColumn();
                ImGui.TextColored(Blue, string.IsNullOrEmpty(r.Winner) ? "-" : r.Winner);
                ImGui.TableNextColumn();
                ImGui.TextColored(Gold, r.SalePrice.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.TextColored(Grey, r.HouseCutPercent.ToString("0"));
                ImGui.TableNextColumn();
                ImGui.TextColored(Green, r.HouseCut.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.TextColored(Blue, r.Payout.ToString("N0"));
            }
            ImGui.EndTable();

            if (rows.Count == 0)
                ImGui.TextColored(Grey, "No sales recorded yet.");
        }
    }
}

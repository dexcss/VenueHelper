using Dalamud.Interface;
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
    // History date filter (YYYY-MM-DD text; empty = unbounded).
    private string histFrom = string.Empty;
    private string histTo = string.Empty;
    // Buyer tracking UI state.
    private string newBuyerName = string.Empty;
    private string newAliasInput = string.Empty;
    private bool showBuyers = false;

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
        ImGui.SetNextItemWidth(SW(140));
        if (ImGui.InputFloat("House cut %", ref houseCutInput, 1f, 5f, "%.0f"))
        {
            houseCutInput = (float)Math.Round(Math.Clamp(houseCutInput, 0f, 100f));
            Auction.SetHouseCut(houseCutInput);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Percentage the house keeps from each sale. Captured per-sale when you finalize.");

        // Prominent, right-aligned toggles between active list / history / buyers.
        ImGui.SameLine();
        {
            var buyersLabel = "Buyers";
            var toggleLabel = showAuctionHistory ? "\u2190 Active Auctions" : "History & Totals \u2192";
            var w = ImGui.CalcTextSize(toggleLabel).X + ImGui.CalcTextSize(buyersLabel).X
                    + ImGui.GetStyle().FramePadding.X * 4 + ImGui.GetStyle().ItemSpacing.X + SW(8);
            var avail = ImGui.GetContentRegionAvail().X;
            if (avail > w)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - w);

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.30f, 0.30f, 0.45f, 1f));
            if (ImGui.Button(buyersLabel))
            {
                showBuyers = !showBuyers;
                showAuctionHistory = false;
            }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.35f, 0.55f, 1f));
            if (ImGui.Button(toggleLabel))
            {
                showAuctionHistory = !showAuctionHistory;
                showBuyers = false;
            }
            ImGui.PopStyleColor();
        }

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        if (showBuyers)
            DrawBuyers();
        else if (showAuctionHistory)
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
        ImGui.SetNextItemWidth(SW(200));
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
        ImGui.SameLine();
        if (ImGui.Button("Import list from clipboard"))
        {
            var clip = ImGui.GetClipboardText() ?? string.Empty;
            var added = Auction.ImportNames(clip);
            SetStatus(added > 0 ? $"Imported {added} name(s) from clipboard." : "No new names found in clipboard.",
                added > 0 ? Green : Red);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Paste names from a spreadsheet (comma or newline separated), then click to add them all.");

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
            ImGui.TableSetupColumn("Sale Price", ImGuiTableColumnFlags.WidthFixed, SW(130));
            ImGui.TableSetupColumn("Sell", ImGuiTableColumnFlags.WidthFixed, SW(120));
            ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, SW(28));
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
                // step = 0 hides the +/- buttons; the host types the amount.
                // Negative amounts are allowed (a sale TO the house).
                if (ImGui.InputInt("##price", ref priceInt, 0, 0))
                {
                    e.SalePrice = priceInt;
                    Auction.Save();
                }

                ImGui.TableNextColumn();
                if (ImGui.Button("Sold"))
                    toFinalize = e;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Move to history with the current house cut applied. Use a negative price for a sale to the house (no cut).");
                ImGui.SameLine();
                if (ImGui.SmallButton("\u25CE"))
                {
                    // Sold to currently-targeted player: fill Winner with their Name@World.
                    var target = Plugin.GetTargetName();
                    if (string.IsNullOrEmpty(target))
                        SetStatus("No player targeted.", Red);
                    else
                    {
                        e.Winner = target.Replace('\uE05D', '@');
                        Auction.Save();
                        SetStatus($"Set winner to {e.Winner}.", Green);
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Sold to target: fill 'Won By' with your current target's Name@World.");

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

        // Date-range filter so hosts don't have to clear history.
        ImGui.TextColored(Grey, "Filter by date (YYYY-MM-DD, leave blank for all):");
        ImGui.SetNextItemWidth(SW(120));
        ImGui.InputTextWithHint("##histfrom", "from", ref histFrom, 10);
        ImGui.SameLine();
        ImGui.TextColored(Grey, "to");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(SW(120));
        ImGui.InputTextWithHint("##histto", "to", ref histTo, 10);
        ImGui.SameLine();
        if (ImGui.SmallButton("Clear filter")) { histFrom = string.Empty; histTo = string.Empty; }
        if (ImGui.SmallButton("Today"))
        {
            histFrom = DateTime.Now.ToString("yyyy-MM-dd");
            histTo = DateTime.Now.ToString("yyyy-MM-dd");
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("This month"))
        {
            histFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");
            histTo = DateTime.Now.ToString("yyyy-MM-dd");
        }

        var from = ParseDate(histFrom);
        var to = ParseDate(histTo);
        var filtered = Auction.HistoryBetween(from, to);

        ImGui.SameLine();
        if (ImGui.SmallButton("Copy filtered"))
        {
            var data = ExportData.AuctionHistory(filtered);
            if (data.Rows.Count == 0)
                SetStatus("Nothing in that date range.", Red);
            else
            {
                ImGui.SetClipboardText(Exporter.BuildText(data));
                SetStatus($"Copied {data.Rows.Count} sales from the selected range.", Green);
            }
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Copies only the sales matching the date filter \u2014 export a window without exporting everything.");

        ImGuiHelpers.ScaledDummy(4f);

        ImGui.SetNextItemWidth(SW(220));
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
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, SW(110));
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch, 1.3f);
            ImGui.TableSetupColumn("Note", ImGuiTableColumnFlags.WidthStretch, 1.3f);
            ImGui.TableSetupColumn("Won By", ImGuiTableColumnFlags.WidthStretch, 1.3f);
            ImGui.TableSetupColumn("Sale", ImGuiTableColumnFlags.WidthFixed, SW(95));
            ImGui.TableSetupColumn("Cut %", ImGuiTableColumnFlags.WidthFixed, SW(50));
            ImGui.TableSetupColumn("House", ImGuiTableColumnFlags.WidthFixed, SW(95));
            ImGui.TableSetupColumn("Payout", ImGuiTableColumnFlags.WidthFixed, SW(95));
            ImGui.TableHeadersRow();

            var rows = filtered
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

    private static DateTime? ParseDate(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.TryParse(s.Trim(), out var d) ? d : null;
    }

    private void DrawBuyers()
    {
        ImGui.TextColored(Blue, "Track a buyer across their alts");
        ImGui.TextColored(Grey, "Add a buyer, then list the names/alts they bid under. Spend is summed from history across all their aliases.");

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.SetNextItemWidth(SW(200));
        ImGui.InputTextWithHint("##newbuyer", "Buyer label (e.g. Barcode)", ref newBuyerName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add Buyer"))
        {
            if (string.IsNullOrWhiteSpace(newBuyerName))
                SetStatus("Enter a buyer label first.", Red);
            else
            {
                Auction.AddBuyer(newBuyerName);
                SetStatus($"Added buyer {newBuyerName}.", Green);
                newBuyerName = string.Empty;
            }
        }

        ImGuiHelpers.ScaledDummy(6f);

        BuyerProfile? removeBuyer = null;
        var bid = 0;
        foreach (var b in Auction.Buyers)
        {
            ImGui.PushID($"buyer{bid++}");
            var spend = Auction.SpendForBuyer(b);
            var count = Auction.PurchaseCountForBuyer(b);

            if (ImGui.CollapsingHeader($"{b.DisplayName}  \u2014  {spend:N0} gil ({count} win{(count == 1 ? "" : "s")})###bhdr{bid}"))
            {
                ImGui.Indent();

                // Aliases as removable chips.
                if (b.Aliases.Count == 0)
                    ImGui.TextColored(Grey, "No aliases yet \u2014 add the names this buyer bids under.");
                string? removeAlias = null;
                foreach (var a in b.Aliases)
                {
                    ImGui.TextColored(Grey, "\u2022");
                    ImGui.SameLine();
                    ImGui.TextUnformatted(a);
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"x##{a}")) removeAlias = a;
                }
                if (removeAlias != null) Auction.RemoveAlias(b, removeAlias);

                ImGui.SetNextItemWidth(SW(200));
                ImGui.InputTextWithHint("##alias", "alias / alt name", ref newAliasInput, 64);
                ImGui.SameLine();
                if (ImGui.SmallButton("Add alias"))
                {
                    if (!string.IsNullOrWhiteSpace(newAliasInput))
                    {
                        Auction.AddAlias(b, newAliasInput);
                        newAliasInput = string.Empty;
                    }
                }
                ImGui.SameLine(0, 24);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
                if (ImGui.SmallButton("Remove buyer")) removeBuyer = b;
                ImGui.PopStyleColor();

                ImGui.Unindent();
            }
            ImGui.PopID();
        }
        if (removeBuyer != null) Auction.RemoveBuyer(removeBuyer);

        if (Auction.Buyers.Count == 0)
            ImGui.TextColored(Grey, "No buyers tracked yet.");
    }
}

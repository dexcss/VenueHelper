using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow : Window, IDisposable
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;
    private VenueCounter Counter => Plugin.Counter;
    private RaffleService Raffle => Plugin.Raffle;
    private AuctionService Auction => Plugin.Auction;

    internal static readonly Vector4 Gold = new(1.0f, 0.84f, 0.10f, 1.0f);
    internal static readonly Vector4 Green = new(0.40f, 0.95f, 0.40f, 1.0f);
    internal static readonly Vector4 Red = new(0.95f, 0.40f, 0.40f, 1.0f);
    internal static readonly Vector4 Blue = new(0.50f, 0.75f, 1.0f, 1.0f);
    internal static readonly Vector4 Grey = new(0.6f, 0.6f, 0.6f, 1.0f);

    // Scales a fixed pixel width by Dalamud's global UI scale so inputs (and the
    // +/- buttons ImGui draws inside InputInt/InputFloat) don't clip or overflow
    // at high-DPI / high-scale setups like 4K at 230%.
    internal static float SW(float width) => width * ImGuiHelpers.GlobalScale;

    // Draws colored text that wraps to the window width instead of running off
    // the right edge. Use for any multi-word descriptive/help line.
    private static void WrapText(Vector4 color, string text)
    {
        ImGui.PushTextWrapPos(0f); // 0 = wrap at the window's right edge
        ImGui.TextColored(color, text);
        ImGui.PopTextWrapPos();
    }

    // Transient status line shown per tab.
    private string statusMessage = string.Empty;
    private Vector4 statusColor = Grey;
    private string statusTab = string.Empty;
    private string currentTab = string.Empty;

    public MainWindow(Plugin plugin) : base("Venue Helper##VenueHelperMain")
    {
        Plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(660, 540),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.BeginTabBar("##venuehelper_tabs"))
        {
            if (ImGui.BeginTabItem("Venue Counter"))
            {
                DrawCounterTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Raffle Helper"))
            {
                DrawRaffleTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Auction Helper"))
            {
                DrawAuctionTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Giveaway Helper"))
            {
                DrawGiveawayTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("DR Tourny Helper"))
            {
                DrawDeathrollTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Shout/Yell Helper"))
            {
                DrawShoutTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Bar Game Helper"))
            {
                DrawBarGameTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Menu Helper"))
            {
                DrawMenuTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Settings"))
            {
                DrawSettingsTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    // ---- Export -------------------------------------------------------

    // Draws a tab header: the title on the left and a right-aligned Export
    // button (opening the export popup) on the same line. Pass no items to draw
    // just the title with no export button.
    private void DrawTabHeader(string title, string popupId, params ExportItem[] items)
    {
        ImGui.TextColored(Gold, title);
        if (items.Length > 0)
        {
            ImGui.SameLine();
            DrawExportButton(popupId, items);
        }
    }

    private string ResolveExportDir()
    {
        var configured = Config.ExportDirectory?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;
        return Plugin.PluginInterface.GetPluginConfigDirectory();
    }

    // One exportable dataset offered inside the export popup.
    private readonly struct ExportItem
    {
        public readonly string Label;
        public readonly string BaseName;
        public readonly Func<TableData> Build;
        public ExportItem(string label, string baseName, Func<TableData> build)
        {
            Label = label; BaseName = baseName; Build = build;
        }
    }

    private string exportDirInput = string.Empty;
    private bool exportDirInit = false;
    private ExportFormat exportFormat = ExportFormat.Txt;
    private string exportPopupStatus = string.Empty;
    private Vector4 exportPopupStatusColor = Grey;

    // Draws a right-aligned "Export" button that opens a popup holding the
    // format choice, destination folder, and one action row per dataset. This
    // keeps every tab clean - the export machinery lives in the popup, not the
    // tab body.
    private void DrawExportButton(string popupId, params ExportItem[] items)
    {
        if (!exportDirInit)
        {
            exportDirInput = Config.ExportDirectory ?? string.Empty;
            exportDirInit = true;
        }

        // Right-align the button on the current line.
        var btnWidth = ImGui.CalcTextSize("Export").X + ImGui.GetStyle().FramePadding.X * 2 + 16;
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > btnWidth)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - btnWidth);

        if (ImGui.Button($"Export##{popupId}", new Vector2(btnWidth, 0)))
        {
            exportPopupStatus = string.Empty;
            ImGui.OpenPopup(popupId);
        }

        if (ImGui.BeginPopup(popupId))
        {
            ImGui.TextColored(Gold, "Export");
            ImGui.Separator();

            ImGui.TextColored(Grey, "Format");
            ImGui.SetNextItemWidth(SW(220));
            if (ImGui.BeginCombo($"##fmt_{popupId}", exportFormat.Label()))
            {
                foreach (ExportFormat f in Enum.GetValues<ExportFormat>())
                    if (ImGui.Selectable(f.Label(), f == exportFormat))
                        exportFormat = f;
                ImGui.EndCombo();
            }

            ImGuiHelpers.ScaledDummy(4f);

            ImGui.TextColored(Grey, "Destination folder");
            ImGui.SetNextItemWidth(SW(360));
            if (ImGui.InputTextWithHint($"##dir_{popupId}", "Blank = default plugin folder", ref exportDirInput, 512))
            {
                Config.ExportDirectory = exportDirInput.Trim();
                Config.Save();
            }
            ImGui.SameLine();
            if (ImGui.Button($"Default##{popupId}"))
            {
                exportDirInput = string.Empty;
                Config.ExportDirectory = string.Empty;
                Config.Save();
            }
            ImGui.TextColored(Grey, $"Saving to: {ResolveExportDir()}");

            ImGuiHelpers.ScaledDummy(6f);
            ImGui.Separator();

            foreach (var item in items)
            {
                ImGui.TextUnformatted(item.Label);

                if (ImGui.Button($"Save {item.Label}##save_{popupId}_{item.BaseName}", new Vector2(200, 0)))
                {
                    try
                    {
                        var data = item.Build();
                        if (data.Rows.Count == 0)
                        {
                            exportPopupStatus = "Nothing to export yet \u2014 the list is empty.";
                            exportPopupStatusColor = Red;
                        }
                        else
                        {
                            var path = Exporter.Write(ResolveExportDir(), item.BaseName, exportFormat, data);
                            exportPopupStatus = $"Saved {data.Rows.Count} row(s) to: {path}";
                            exportPopupStatusColor = Green;
                        }
                    }
                    catch (Exception ex)
                    {
                        exportPopupStatus = $"Export failed: {ex.Message}";
                        exportPopupStatusColor = Red;
                    }
                }

                if (ImGui.Button($"Copy to clipboard##copy_{popupId}_{item.BaseName}", new Vector2(200, 0)))
                {
                    try
                    {
                        var data = item.Build();
                        if (data.Rows.Count == 0)
                        {
                            exportPopupStatus = "Nothing to copy yet \u2014 the list is empty.";
                            exportPopupStatusColor = Red;
                        }
                        else
                        {
                            var text = Exporter.BuildText(data);
                            ImGui.SetClipboardText(text);
                            exportPopupStatus = $"Copied {data.Rows.Count} row(s). If paste is empty, use the box below.";
                            exportPopupStatusColor = Green;
                        }
                    }
                    catch (Exception ex)
                    {
                        exportPopupStatus = $"Copy failed: {ex.Message}";
                        exportPopupStatusColor = Red;
                    }
                }

                ImGuiHelpers.ScaledDummy(6f);
            }

            if (!string.IsNullOrEmpty(exportPopupStatus))
            {
                ImGuiHelpers.ScaledDummy(4f);
                ImGui.TextColored(exportPopupStatusColor, exportPopupStatus);
            }

            // Manual fallback box: always shown, pre-filled with the first
            // dataset's text. Click inside, Ctrl+A, Ctrl+C to copy by hand if
            // the Copy button's clipboard write ever fails.
            if (items.Length > 0)
            {
                ImGuiHelpers.ScaledDummy(6f);
                ImGui.Separator();
                ImGui.TextColored(Grey, "Manual copy (click in box, Ctrl+A, Ctrl+C):");
                string box;
                try { box = Exporter.BuildText(items[0].Build()); }
                catch { box = string.Empty; }
                if (string.IsNullOrEmpty(box)) box = "(list is empty)";
                ImGui.InputTextMultiline($"##manualcopy_{popupId}", ref box, box.Length + 1,
                    new Vector2(420, 120));
            }

            ImGui.EndPopup();
        }
    }

    // ---- Status -------------------------------------------------------

    private void SetStatus(string message, Vector4? color = null)
    {
        statusMessage = message;
        statusColor = color ?? Grey;
        statusTab = currentTab;
    }

    // Draws a shared game-history table (Raffle/DR/Bar Game) with a confirmed
    // "Clear history" button. popupId must be unique per tab.
    private void DrawGameHistory(System.Collections.Generic.IReadOnlyList<Data.GameHistoryEntry> history, Action clear, string popupId)
    {
        ImGuiHelpers.ScaledDummy(4f);
        if (history.Count == 0)
        {
            WrapText(Grey, "No past results yet. When you reset, the result is archived here.");
            return;
        }

        const ImGuiTableFlags hflags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                                       | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;
        if (ImGui.BeginTable($"{popupId}_tbl", 5, hflags, new Vector2(0, SW(200))))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("When", ImGuiTableColumnFlags.WidthFixed, SW(110));
            ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, SW(90));
            ImGui.TableSetupColumn("Winner", ImGuiTableColumnFlags.WidthStretch, 1.2f);
            ImGui.TableSetupColumn("Pot", ImGuiTableColumnFlags.WidthFixed, SW(80));
            ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthStretch, 1.8f);
            ImGui.TableHeadersRow();
            foreach (var h in history)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(Grey, h.When.ToString("MM-dd HH:mm"));
                ImGui.TableNextColumn();
                ImGui.TextColored(Blue, h.Kind);
                ImGui.TableNextColumn();
                ImGui.TextColored(Gold, h.Winner);
                ImGui.TableNextColumn();
                ImGui.TextColored(Green, h.PotShort);
                if (ImGui.IsItemHovered() && h.Pot > 0) ImGui.SetTooltip($"{h.Pot:N0} gil");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(h.Details);
            }
            ImGui.EndTable();
        }

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
        if (ImGui.SmallButton("Clear history")) ImGui.OpenPopup(popupId);
        ImGui.PopStyleColor();
        if (ImGui.BeginPopup(popupId))
        {
            ImGui.TextColored(Red, "ARE YOU SURE? Delete all history for this tab?");
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1f));
            if (ImGui.Button("Yes, delete all")) { clear(); ImGui.CloseCurrentPopup(); }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }

    private void DrawTabStatus(string tab)
    {
        if (statusTab != tab || string.IsNullOrEmpty(statusMessage))
            return;
        ImGui.Separator();
        ImGui.TextColored(statusColor, statusMessage);
    }
}

using Dalamud.Interface.Utility;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private string backupFolder = string.Empty;
    private string restorePath = string.Empty;
    private bool restoreConfirm = false;

    private void DrawSettingsTab()
    {
        currentTab = "Settings";

        // ===== Behaviour =====================================================
        ImGui.TextColored(Gold, "Settings");
        ImGuiHelpers.ScaledDummy(4f);

        // Panic / master kill switch.
        var panic = Config.PanicMode;
        ImGui.PushStyleColor(ImGuiCol.Text, panic ? new Vector4(1f, 0.4f, 0.4f, 1f) : new Vector4(1f, 1f, 1f, 1f));
        if (ImGui.Checkbox("PANIC: disable all chat-sending and trade-watching", ref panic))
        {
            Config.PanicMode = panic;
            Plugin.Panic = panic;
            Config.Save();
            SetStatus(panic ? "Panic ON \u2014 no chat or trade automation will run." : "Panic off \u2014 automation re-enabled.", panic ? Red : Green);
        }
        ImGui.PopStyleColor();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Emergency stop. Halts serve sequences, macros, shout/announce sends, and trade buy-in detection. Reversible \u2014 untick to resume.");

        ImGuiHelpers.ScaledDummy(4f);

        // Export directory (used as the default in every export popup).
        ImGui.TextColored(Blue, "Default export folder");
        var exportDir = Config.ExportDirectory ?? string.Empty;
        ImGui.SetNextItemWidth(SW(440));
        if (ImGui.InputTextWithHint("##exportdir", "Blank = the plugin's folder", ref exportDir, 260))
        {
            Config.ExportDirectory = exportDir;
            Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Every tab's Export popup starts from this folder. You can still change it per-export.");

        ImGuiHelpers.ScaledDummy(4f);

        // Chat-send delay default (serve/macro pacing baseline).
        ImGui.TextColored(Blue, "Default delay between sequence steps");
        var delay = Config.MenuServeStepDelayMs;
        ImGui.SetNextItemWidth(SW(170));
        if (ImGui.InputInt("milliseconds", ref delay, 100, 500))
        {
            Config.MenuServeStepDelayMs = Math.Clamp(delay, 0, 10000);
            Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Baseline pacing for serve sequences/macros. Each step can still set its own wait; this is the default for new steps. ~1200ms stays under the chat spam-block.");

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(8f);

        // ===== Rules / toggles ===============================================
        ImGui.TextColored(Blue, "Rules");

        var allowDice = Config.GiveawayAllowDice;
        if (ImGui.Checkbox("Giveaways: also accept /dice (default is /random only)", ref allowDice))
        {
            Config.GiveawayAllowDice = allowDice;
            Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Off: only a plain /random counts in giveaways. On: /dice rolls count too. (A /random N is always rejected as a cheat.)");

        var autoTrade = Config.RaffleAutoTrade;
        if (ImGui.Checkbox("Raffle: auto-credit trades as tickets", ref autoTrade))
        {
            Config.RaffleAutoTrade = autoTrade;
            Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When on, gil traded to you during a raffle is converted to tickets automatically.");

        var confirmDestructive = Config.ConfirmDestructive;
        if (ImGui.Checkbox("Require confirmation before destructive actions", ref confirmDestructive))
        {
            Config.ConfirmDestructive = confirmDestructive;
            Config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("When on (recommended), clearing/resetting data asks 'are you sure' first. Turn off to skip the extra step.");

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(8f);

        // ===== Backup & Restore =============================================
        ImGui.TextColored(Gold, "Backup & Restore");
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(560));
        ImGui.TextColored(Grey,
            "Save a copy of everything \u2014 venues, menus, bar games, raffles, auctions, history, settings \u2014 to a file you can keep. " +
            "Dalamud keeps its own internal config, but this gives you a manual, restorable backup you control.");
        ImGui.PopTextWrapPos();

        ImGuiHelpers.ScaledDummy(6f);

        ImGui.TextColored(Blue, "Create a backup");
        ImGui.SetNextItemWidth(SW(380));
        ImGui.InputTextWithHint("##backupfolder", "Folder to save into (blank = plugin folder)", ref backupFolder, 260);
        ImGui.SameLine();
        if (ImGui.Button("Save backup"))
        {
            var (ok, msg) = Config.ExportBackup(backupFolder);
            SetStatus(msg, ok ? Green : Red);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Writes a timestamped VenueHelper-backup-*.json into the folder.");

        ImGuiHelpers.ScaledDummy(10f);

        ImGui.TextColored(Blue, "Restore from a backup");
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(560));
        ImGui.TextColored(Red, "Restoring REPLACES all current data with the file's contents. Make a backup first if unsure.");
        ImGui.PopTextWrapPos();
        ImGui.SetNextItemWidth(SW(440));
        ImGui.InputTextWithHint("##restorepath", "Full path to a VenueHelper-backup-*.json", ref restorePath, 400);
        ImGui.SameLine();
        if (!restoreConfirm)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
            if (ImGui.Button("Restore..."))
            {
                if (string.IsNullOrWhiteSpace(restorePath)) SetStatus("Paste the path to a backup file first.", Red);
                else restoreConfirm = true;
            }
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1f));
            if (ImGui.Button("ARE YOU SURE? Replace everything"))
            {
                var (ok, msg) = Config.ImportBackup(restorePath);
                SetStatus(msg, ok ? Green : Red);
                restoreConfirm = false;
                if (ok) restorePath = string.Empty;
            }
            ImGui.PopStyleColor();
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) restoreConfirm = false;
        }

        DrawTabStatus("Settings");
    }
}

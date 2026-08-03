using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private const int ShoutPresetMin = 3;
    private static readonly string[] ChannelLabels = { "Say", "Yell", "Shout", "Party" };

    private void DrawShoutTab()
    {
        currentTab = "Shout/Yell Helper";

        // Always keep at least a few slots so the tab isn't empty.
        while (Config.ShoutPresets.Count < ShoutPresetMin)
            Config.ShoutPresets.Add(new ShoutPreset());

        ImGui.TextColored(Gold, "Shout / Yell Helper");
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(560));
        ImGui.TextColored(Grey,
            "Pre-write announcements, pick a channel for each, and fire them with one click. " +
            "Messages are sent through the game's chat box, exactly as if you typed and pressed Enter.");
        ImGui.PopTextWrapPos();

        ImGuiHelpers.ScaledDummy(2f);
        ImGui.TextColored(new Vector4(0.85f, 0.65f, 0.2f, 1f),
            "Note: channel cooldowns (Shout/Yell) and length limits still apply \u2014 the game may reject a send.");

        ImGuiHelpers.ScaledDummy(8f);

        // Box height selector (1-4 lines tall).
        ImGui.TextColored(Grey, "Box height:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(SW(70));
        var lines = Math.Clamp(Config.ShoutBoxLines, 1, 4);
        if (ImGui.BeginCombo("##shoutlines", $"{lines} line{(lines == 1 ? "" : "s")}"))
        {
            for (var n = 1; n <= 4; n++)
            {
                if (ImGui.Selectable($"{n} line{(n == 1 ? "" : "s")}", n == lines))
                {
                    Config.ShoutBoxLines = n;
                    Config.Save();
                }
            }
            ImGui.EndCombo();
        }

        ImGuiHelpers.ScaledDummy(6f);

        int? removeAt = null;
        for (var i = 0; i < Config.ShoutPresets.Count; i++)
        {
            var preset = Config.ShoutPresets[i];
            ImGui.PushID(i);

            // Channel dropdown.
            ImGui.SetNextItemWidth(SW(90));
            if (ImGui.BeginCombo($"##channel{i}", ChannelLabels[(int)preset.Channel]))
            {
                for (var c = 0; c < ChannelLabels.Length; c++)
                {
                    var selected = (int)preset.Channel == c;
                    if (ImGui.Selectable(ChannelLabels[c], selected))
                    {
                        preset.Channel = (ChatChannel)c;
                        Config.Save();
                    }
                    if (selected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            // Message box (multi-line, height follows the line-count setting).
            ImGui.SameLine();
            var msg = preset.Message;
            var sendWidth = ImGui.CalcTextSize("Send").X + ImGui.GetStyle().FramePadding.X * 2;
            var xWidth = ImGui.CalcTextSize(" X ").X + ImGui.GetStyle().FramePadding.X * 2;
            var boxW = ImGui.GetContentRegionAvail().X - sendWidth - xWidth - SW(16);
            var boxLines = Math.Clamp(Config.ShoutBoxLines, 1, 4);
            var boxH = ImGui.GetTextLineHeight() * boxLines + ImGui.GetStyle().FramePadding.Y * 2;
            if (ImGui.InputTextMultiline($"##msg{i}", ref msg, 400, new Vector2(boxW, boxH)))
            {
                preset.Message = msg;
                Config.Save();
            }

            // Send button.
            ImGui.SameLine();
            var empty = string.IsNullOrWhiteSpace(preset.Message);
            if (empty) ImGui.BeginDisabled();
            if (ImGui.Button("Send"))
            {
                var (ok, message) = ChatSender.SendToChannel(preset.Channel, preset.Message);
                SetStatus(ok ? $"Sent to {ChannelLabels[(int)preset.Channel]}." : message, ok ? Green : Red);
            }
            if (empty) ImGui.EndDisabled();

            // Remove this preset (only if above the minimum, so there's always 1+).
            ImGui.SameLine();
            var canRemove = Config.ShoutPresets.Count > 1;
            if (!canRemove) ImGui.BeginDisabled();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
            if (ImGui.Button("X")) removeAt = i;
            ImGui.PopStyleColor();
            if (!canRemove) ImGui.EndDisabled();
            if (ImGui.IsItemHovered() && canRemove) ImGui.SetTooltip("Remove this preset.");

            ImGui.PopID();
        }

        if (removeAt != null)
        {
            Config.ShoutPresets.RemoveAt(removeAt.Value);
            Config.Save();
        }

        ImGuiHelpers.ScaledDummy(6f);
        if (ImGui.Button("+ Add another"))
        {
            Config.ShoutPresets.Add(new ShoutPreset());
            Config.Save();
        }
        ImGui.SameLine(0, 16);
        if (ImGui.Button("Clear all"))
            ImGui.OpenPopup("##clearshouts");
        if (ImGui.BeginPopup("##clearshouts"))
        {
            ImGui.TextColored(Red, "Clear the text of all presets?");
            if (ImGui.Button("Yes, clear"))
            {
                foreach (var p in Config.ShoutPresets) { p.Message = string.Empty; p.Channel = ChatChannel.Yell; }
                Config.Save();
                SetStatus("Presets cleared.", Grey);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        DrawTabStatus("Shout/Yell Helper");
    }
}

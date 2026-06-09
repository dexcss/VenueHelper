using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private const int ShoutPresetCount = 10;
    private static readonly string[] ChannelLabels = { "Say", "Yell", "Shout", "Party" };

    private void DrawShoutTab()
    {
        currentTab = "Shout/Yell Helper";

        // Make sure there are always 10 preset slots to fill.
        while (Config.ShoutPresets.Count < ShoutPresetCount)
            Config.ShoutPresets.Add(new ShoutPreset());

        ImGui.TextColored(Gold, "Shout / Yell Helper");
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(560));
        ImGui.TextColored(Grey,
            "Pre-write up to 10 announcements, pick a channel for each, and fire them with one click. " +
            "Messages are sent through the game's chat box, exactly as if you typed and pressed Enter.");
        ImGui.PopTextWrapPos();

        ImGuiHelpers.ScaledDummy(2f);
        ImGui.TextColored(new Vector4(0.85f, 0.65f, 0.2f, 1f),
            "Note: channel cooldowns (Shout/Yell) and length limits still apply \u2014 the game may reject a send.");

        ImGuiHelpers.ScaledDummy(8f);

        for (var i = 0; i < ShoutPresetCount; i++)
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

            // Message box (fills remaining width, leaving room for Send).
            ImGui.SameLine();
            var msg = preset.Message;
            var sendWidth = ImGui.CalcTextSize("Send").X + ImGui.GetStyle().FramePadding.X * 2 + SW(8);
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - sendWidth - SW(8));
            if (ImGui.InputTextWithHint($"##msg{i}", "Type an announcement...", ref msg, 400))
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

            ImGui.PopID();
        }

        ImGuiHelpers.ScaledDummy(8f);
        if (ImGui.Button("Clear all presets"))
            ImGui.OpenPopup("##clearshouts");
        if (ImGui.BeginPopup("##clearshouts"))
        {
            ImGui.TextColored(Red, "Clear all 10 preset messages?");
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

using Dalamud.Interface.Utility;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private void DrawCounterTab()
    {
        currentTab = "Venue Counter";

        DrawTabHeader("Venue Counter", "##export_visitors",
            new ExportItem("Unique visitor list", "visitors", () => ExportData.Visitors(Counter.AllNightVisitors)));
        ImGui.TextColored(Grey,
            "FFXIV only renders ~99 players at once. Walk the venue with a counter running and " +
            "every new person who loads in gets added to the running total.");
        ImGui.TextColored(Grey, $"Currently visible to you: {Counter.CurrentlyVisible} players");

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        // ---- Temporary lap counter ------------------------------------
        ImGui.TextColored(Blue, "Temporary Counter (single sweep)");
        ImGui.TextColored(Grey, "Start, walk a lap of the venue, then Stop to freeze the headcount.");
        ImGuiHelpers.ScaledDummy(2f);

        if (Counter.TempRunning)
        {
            ImGui.TextColored(Green, $"Counting...  Unique so far: {Counter.TempSeen.Count}");
            var elapsed = DateTime.Now - Counter.TempStarted;
            ImGui.SameLine(0, 16);
            ImGui.TextColored(Grey, $"({(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00})");

            if (ImGui.Button("Stop"))
            {
                Counter.StopTemp();
                SetStatus($"Lap complete: {Counter.LastLapTotal} unique players.", Green);
            }
        }
        else
        {
            if (Counter.LastLapTotal > 0)
                ImGui.TextColored(Gold, $"Last lap total: {Counter.LastLapTotal} unique players");
            else
                ImGui.TextColored(Grey, "No lap recorded yet.");

            if (ImGui.Button("Start"))
            {
                Counter.StartTemp();
                SetStatus("Temporary counter started \u2014 walk the venue.", Blue);
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Clear##temp"))
        {
            Counter.ClearTemp();
            SetStatus("Temporary counter cleared.", Grey);
        }

        ImGuiHelpers.ScaledDummy(10f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        // ---- All night counter ----------------------------------------
        ImGui.TextColored(Blue, "All-Night Counter (running total)");
        ImGui.TextColored(Grey, "Tracks unique visitors for the whole night. Survives relogs until you reset it.");
        ImGuiHelpers.ScaledDummy(2f);

        ImGui.TextColored(Gold, $"Total unique visitors tonight: {Counter.AllNightTotal}");
        if (Counter.AllNightRunning)
        {
            var since = DateTime.Now - Counter.AllNightStarted;
            ImGui.TextColored(Green, $"Tracking active \u2014 since {Counter.AllNightStarted:HH:mm} ({(int)since.TotalHours}h {since.Minutes}m)");
        }
        else if (Counter.AllNightTotal > 0)
        {
            ImGui.TextColored(Grey, "Paused. Resume to keep adding to the same total, or reset to start fresh.");
        }
        ImGuiHelpers.ScaledDummy(2f);

        if (!Counter.AllNightRunning)
        {
            if (ImGui.Button("Start New Night"))
            {
                Counter.StartAllNight();
                SetStatus("All-night counter started fresh.", Green);
            }
            if (Counter.AllNightTotal > 0)
            {
                ImGui.SameLine();
                if (ImGui.Button("Resume"))
                {
                    Counter.ResumeAllNight();
                    SetStatus("All-night counter resumed.", Green);
                }
            }
        }
        else
        {
            if (ImGui.Button("Pause##allnight"))
            {
                Counter.StopAllNight();
                SetStatus("All-night counter paused.", Grey);
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset##allnight"))
            ImGui.OpenPopup("##resetallnight");

        if (ImGui.BeginPopup("##resetallnight"))
        {
            ImGui.TextColored(Red, "Reset the all-night total to 0?");
            if (ImGui.Button("Yes, reset"))
            {
                Counter.ResetAllNight();
                SetStatus("All-night counter reset.", Red);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextColored(Grey, "Tip: both counters run off the same scan, so you can run a lap while the all-night total keeps going. Use the Export button (top right) to save the visitor list.");

        DrawTabStatus("Venue Counter");
    }
}

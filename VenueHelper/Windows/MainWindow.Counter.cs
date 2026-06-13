using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private string counterTimeSearch = string.Empty;
    private bool resetTimesConfirm = false;
    private bool resetTimesConfirm2 = false;
    private string newVenueName = string.Empty;
    private string renameVenueBuffer = string.Empty;
    private void DrawCounterTab()
    {
        currentTab = "Venue Counter";

        DrawTabHeader("Venue Counter", "##export_visitors",
            new ExportItem("Unique visitor list", "visitors", () => ExportData.Visitors(Counter.AllNightVisitors)));

        // ---- Venue selector (multiple venues) -------------------------
        ImGui.TextColored(Grey, "Venue:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(SW(200));
        if (ImGui.BeginCombo("##venuepick", Counter.ActiveVenueName))
        {
            var venues = Counter.Venues;
            for (var i = 0; i < venues.Count; i++)
            {
                var sel = i == Counter.ActiveVenueIndex;
                if (ImGui.Selectable(venues[i].Name, sel))
                    Counter.SwitchVenue(i);
                if (sel) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.Button("Add New"))
            ImGui.OpenPopup("##addvenue");
        if (ImGui.BeginPopup("##addvenue"))
        {
            ImGui.TextColored(Gold, "New venue");
            ImGui.SetNextItemWidth(SW(200));
            ImGui.InputTextWithHint("##newvenuename", "Venue name", ref newVenueName, 48);
            if (ImGui.Button("Create"))
            {
                Counter.AddVenue(newVenueName);
                SetStatus($"Added venue \"{(string.IsNullOrWhiteSpace(newVenueName) ? "New Venue" : newVenueName)}\".", Green);
                newVenueName = string.Empty;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        // Rename / remove the current venue.
        ImGui.SameLine();
        if (ImGui.SmallButton("Rename"))
        {
            renameVenueBuffer = Counter.ActiveVenueName;
            ImGui.OpenPopup("##renamevenue");
        }
        if (ImGui.BeginPopup("##renamevenue"))
        {
            ImGui.SetNextItemWidth(SW(200));
            ImGui.InputTextWithHint("##venuerename", "Venue name", ref renameVenueBuffer, 48);
            if (ImGui.Button("Save"))
            {
                Counter.RenameVenue(Plugin.Configuration.ActiveVenueProfile, renameVenueBuffer);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        if (Counter.Venues.Count > 1)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
            if (ImGui.SmallButton("Remove venue"))
                ImGui.OpenPopup("##removevenue");
            ImGui.PopStyleColor();
            if (ImGui.BeginPopup("##removevenue"))
            {
                WrapText(Red, $"Remove \"{Counter.ActiveVenueName}\" and all its records?");
                if (ImGui.Button("Yes, remove"))
                {
                    Counter.RemoveVenue(Plugin.Configuration.ActiveVenueProfile);
                    SetStatus("Venue removed.", Red);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);
        ImGui.TextColored(Grey,
            "FFXIV only renders ~99 players at once. Walk the venue with a counter running and " +
            "every new person who loads in gets added to the running total.");
        WrapText(Grey, $"Currently visible to you: {Counter.CurrentlyVisible} players");

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        // ---- Temporary lap counter ------------------------------------
        ImGui.TextColored(Blue, "Temporary Counter (single sweep)");
        WrapText(Grey, "Start, walk a lap of the venue, then Stop to freeze the headcount.");
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
        WrapText(Grey, "Tracks unique visitors for the whole night. Survives relogs until you reset it.");
        ImGuiHelpers.ScaledDummy(2f);

        ImGui.TextColored(Gold, $"Total unique visitors tonight: {Counter.AllNightTotal}");
        if (Counter.AllNightRunning)
        {
            var since = DateTime.Now - Counter.AllNightStarted;
            WrapText(Green, $"Tracking active \u2014 since {Counter.AllNightStarted:HH:mm} ({(int)since.TotalHours}h {since.Minutes}m)");
        }
        else if (Counter.AllNightTotal > 0)
        {
            WrapText(Grey, "Paused. Resume to keep adding to the same total, or reset to start fresh.");
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
        WrapText(Grey, "Tip: both counters run off the same scan, so you can run a lap while the all-night total keeps going. Use the Export button (top right) to save the visitor list.");

        ImGuiHelpers.ScaledDummy(8f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // ---- Lifetime time tracking -----------------------------------
        ImGui.TextColored(Blue, "Time in Venue (lifetime)");
        var track = Counter.TrackVisitTime;
        if (ImGui.Checkbox("Track time while the all-night counter runs", ref track))
            Counter.TrackVisitTime = track;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Accumulates total time each visitor has spent in your venue, across nights, while the all-night counter is running.");

        if (!Counter.AllNightRunning && Counter.TrackVisitTime)
            ImGui.TextColored(Grey, "(Start the all-night counter above to accumulate time.)");

        ImGui.SameLine();
        {
            var w = ImGui.CalcTextSize("Reset Times").X + ImGui.GetStyle().FramePadding.X * 2 + SW(4);
            var avail = ImGui.GetContentRegionAvail().X;
            if (avail > w) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - w);
        }
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
        if (ImGui.Button("Reset Times"))
        {
            ImGui.OpenPopup("##resettimes");
            resetTimesConfirm = false;
            resetTimesConfirm2 = false;
        }
        ImGui.PopStyleColor();
        if (ImGui.BeginPopup("##resettimes"))
        {
            ImGui.TextColored(Red, "Clear ALL lifetime visit times for this venue?");
            ImGui.TextColored(Grey, "This wipes the whole time database and can't be undone.");
            ImGuiHelpers.ScaledDummy(4f);

            if (!resetTimesConfirm)
            {
                if (ImGui.Button("Yes, continue"))
                    resetTimesConfirm = true;
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                    ImGui.CloseCurrentPopup();
            }
            else if (!resetTimesConfirm2)
            {
                ImGui.Separator();
                ImGui.TextColored(Red, "Are you REALLY sure? Last chance.");
                if (ImGui.Button("Yes, continue##2"))
                    resetTimesConfirm2 = true;
                ImGui.SameLine();
                if (ImGui.Button("No, keep them"))
                {
                    resetTimesConfirm = false;
                    ImGui.CloseCurrentPopup();
                }
            }
            else
            {
                ImGui.Separator();
                WrapText(Red, "ARE YOU REALLY SURE? EVERYTHING YOU HAVE RECORDED WILL BE GONE FOREVER.");
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1f));
                if (ImGui.Button("Yes, delete everything"))
                {
                    Counter.ResetVisitTimes();
                    SetStatus("Visit times cleared.", Red);
                    resetTimesConfirm = false;
                    resetTimesConfirm2 = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.PopStyleColor();
                ImGui.SameLine();
                if (ImGui.Button("No, cancel"))
                {
                    resetTimesConfirm = false;
                    resetTimesConfirm2 = false;
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndPopup();
        }

        ImGui.SetNextItemWidth(SW(220));
        ImGui.InputTextWithHint("##timesearch", "Search by name...", ref counterTimeSearch, 64);

        const ImGuiTableFlags tflags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                                      | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;
        if (ImGui.BeginTable("##visittimes", 4, tflags, new Vector2(0, 240)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch, 1.8f);
            ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthStretch, 1.1f);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, SW(110));
            ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, SW(28));
            ImGui.TableHeadersRow();

            string? removeKey = null;
            var id = 0;
            foreach (var (key, secs) in Counter.VisitTimes)
            {
                var nm = VenueHelper.Logic.VenueCounter.NameOnly(key);
                if (!string.IsNullOrWhiteSpace(counterTimeSearch)
                    && !nm.Contains(counterTimeSearch, StringComparison.OrdinalIgnoreCase))
                    continue;

                ImGui.TableNextRow();
                ImGui.PushID(id++);
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.Selectable(nm);
                if (ImGui.IsItemClicked())
                    ImGui.OpenPopup($"##sessions{key}");
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Click to see each visit (arrived / left).");

                if (ImGui.BeginPopup($"##sessions{key}"))
                {
                    ImGui.TextColored(Gold, $"{nm} \u2014 visit breakdown");
                    WrapText(Grey, $"Total: {VenueHelper.Logic.VenueCounter.FormatDuration(secs)}");
                    WrapText(Grey, "A departure is confirmed after they've been unseen for 30 minutes; the 'left' time shown is when you actually stopped seeing them.");
                    ImGui.Separator();
                    var sessions = Counter.SessionsFor(key);
                    if (sessions.Count == 0)
                        ImGui.TextColored(Grey, "No recorded visits yet.");
                    foreach (var s in sessions)
                    {
                        var dur = VenueHelper.Logic.VenueCounter.FormatDuration((long)s.Duration.TotalSeconds);
                        if (!s.Open)
                        {
                            // Confirmed departure.
                            ImGui.TextColored(Grey,
                                $"{s.Arrived:MMM d, h:mm tt}  \u2192  {s.Left:MMM d, h:mm tt}   ({dur})");
                        }
                        else
                        {
                            var idle = DateTime.Now - s.LastSeen;
                            if (idle.TotalMinutes < 1)
                            {
                                // Actively present right now.
                                ImGui.TextColored(Green,
                                    $"{s.Arrived:MMM d, h:mm tt}  \u2192  present   ({dur})");
                            }
                            else
                            {
                                // Gone from the tracker but still within the grace window.
                                ImGui.TextColored(new Vector4(0.85f, 0.65f, 0.2f, 1f),
                                    $"{s.Arrived:MMM d, h:mm tt}  \u2192  last seen {s.LastSeen:h:mm tt}   ({dur})");
                                ImGui.Indent();
                                ImGui.TextColored(Grey,
                                    $"   stopped seeing them at {s.LastSeen:h:mm tt}; not logged as left until 30 min pass (in ~{Math.Max(0, 30 - (int)idle.TotalMinutes)} min).");
                                ImGui.Unindent();
                            }
                        }
                    }
                    ImGui.EndPopup();
                }

                ImGui.TableNextColumn();
                ImGui.TextColored(Grey, VenueHelper.Logic.VenueCounter.WorldOf(key));
                ImGui.TableNextColumn();
                ImGui.TextColored(Gold, VenueHelper.Logic.VenueCounter.FormatDuration(secs));
                ImGui.TableNextColumn();
                if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Trash))
                    removeKey = key;
                ImGui.PopID();
            }
            ImGui.EndTable();
            if (removeKey != null) Counter.RemoveVisitTime(removeKey);
        }

        DrawTabStatus("Venue Counter");
    }
}

using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private GiveawayTracker Give => Plugin.Giveaway;

    private void DrawGiveawayTab()
    {
        currentTab = "Giveaway Helper";

        DrawTabHeader("Giveaway Helper", "##export_giveaway",
            new ExportItem("Counted rolls", "giveaway", () => ExportData.GiveawayResults(Give.Entries)));

        var exact = Give.ExactMatchOn;
        ImGui.TextColored(Grey, exact
            ? "Everyone keeps rolling until someone hits the exact number."
            : "Start, then have everyone /random. Only each person's FIRST roll counts.");

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Mode selection -------------------------------------------
        ImGui.TextColored(Blue, "Winner mode");

        // Exact-match is a different game style, so it's a single toggle that
        // takes over when on (the highest/lowest/closest options are for the
        // standard "first roll per player" style).
        if (ImGui.Checkbox("Roll until someone hits a number (race)", ref exact))
        {
            Give.Modes = exact
                ? GiveawayMode.ExactMatch
                : GiveawayMode.Highest; // fall back to a sensible default
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Race mode: every roll counts and the first person to roll the target number wins.");

        if (Give.ExactMatchOn)
        {
            ImGui.SameLine(0, 16);
            ImGui.TextColored(Grey, "Target");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(SW(140));
            var mt = Give.MatchTarget;
            if (ImGui.InputInt("##matchtarget", ref mt, 1, 10))
                Give.MatchTarget = mt;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Have everyone /random \u2014 the first to roll your target number wins.");
        }
        else
        {
            // Standard highest / lowest / closest selection.
            var modes = Give.Modes;
            var highest = modes.HasFlag(GiveawayMode.Highest);
            if (ImGui.Checkbox("Highest", ref highest))
                Give.Modes = highest ? modes | GiveawayMode.Highest : modes & ~GiveawayMode.Highest;
            ImGui.SameLine();
            var lowest = Give.Modes.HasFlag(GiveawayMode.Lowest);
            if (ImGui.Checkbox("Lowest", ref lowest))
                Give.Modes = lowest ? Give.Modes | GiveawayMode.Lowest : Give.Modes & ~GiveawayMode.Lowest;
            ImGui.SameLine();
            var closest = Give.Modes.HasFlag(GiveawayMode.Closest);
            if (ImGui.Checkbox("Closest to", ref closest))
                Give.Modes = closest ? Give.Modes | GiveawayMode.Closest : Give.Modes & ~GiveawayMode.Closest;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(SW(140));
            var target = Give.ClosestTarget;
            if (ImGui.InputInt("##closesttarget", ref target, 1, 10))
                Give.ClosestTarget = target;
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // ---- Start / Stop / Reset -------------------------------------
        if (!Give.Running)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.20f, 0.45f, 0.25f, 1f));
            if (ImGui.Button("Start", new Vector2(90, 0)))
            {
                Give.Start();
                SetStatus(Give.ExactMatchOn
                    ? $"Race started \u2014 first to roll {Give.MatchTarget} wins."
                    : "Giveaway started \u2014 first /random per player counts.", Green);
            }
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.2f, 0.2f, 1f));
            if (ImGui.Button("Stop", new Vector2(90, 0)))
            {
                Give.Stop();
                SetStatus($"Giveaway stopped. {Give.Count} rolls.", Gold);
            }
            ImGui.PopStyleColor();
            var since = DateTime.Now - Give.StartedAt;
            ImGui.SameLine();
            ImGui.TextColored(Green, $"LIVE  {(int)since.TotalMinutes:00}:{since.Seconds:00}  \u2014  {Give.Count} rolls");
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset##giveaway"))
            ImGui.OpenPopup("##resetgiveaway");
        if (ImGui.BeginPopup("##resetgiveaway"))
        {
            ImGui.TextColored(Red, "Are you sure? Clear all rolls from this giveaway?");
            if (ImGui.Button("Yes, reset"))
            {
                Give.Reset();
                SetStatus("Giveaway cleared.", Red);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Winner banners -------------------------------------------
        if (Give.ExactMatchOn)
        {
            if (Give.MatchWinner is { } mw)
                ImGui.TextColored(Gold, $"WINNER:  {mw.NameOnly}  hit {Give.MatchTarget}!");
            else if (Give.Running)
                ImGui.TextColored(Grey, $"Waiting for someone to roll {Give.MatchTarget}...");
            else
                ImGui.TextColored(Grey, "No winner yet.");
        }
        else
        {
            if (Give.Modes.HasFlag(GiveawayMode.Highest) && Give.Highest is { } hi)
                ImGui.TextColored(Gold, $"Highest:  {hi.NameOnly}  rolled {hi.Roll}");
            if (Give.Modes.HasFlag(GiveawayMode.Lowest) && Give.Lowest is { } lo)
                ImGui.TextColored(Gold, $"Lowest:   {lo.NameOnly}  rolled {lo.Roll}");
            if (Give.Modes.HasFlag(GiveawayMode.Closest) && Give.Closest is { } cl)
                ImGui.TextColored(Gold, $"Closest to {Give.ClosestTarget}:  {cl.NameOnly}  rolled {cl.Roll}");
            if (Give.Modes == GiveawayMode.None)
                ImGui.TextColored(Grey, "Select at least one winner mode above.");
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // ---- Counted rolls table --------------------------------------
        ImGui.TextColored(Blue, Give.ExactMatchOn
            ? $"All rolls this race: {Give.Count}"
            : $"Counted rolls (first per player): {Give.Count}");

        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                                      | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;

        if (ImGui.BeginTable("##giveawaycounted", 4, flags, new Vector2(0, 220)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch, 1.8f);
            ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Out Of", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableHeadersRow();

            // In race mode, newest rolls at the top; otherwise highest-first.
            var ordered = Give.ExactMatchOn
                ? Give.Entries.OrderByDescending(x => x.When)
                : Give.Entries.OrderByDescending(x => x.Roll);

            foreach (var e in ordered)
            {
                var winner = Give.IsWinner(e);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                if (winner) ImGui.TextColored(Gold, e.NameOnly);
                else ImGui.TextUnformatted(e.NameOnly);
                if (ImGui.IsItemClicked()) ImGui.SetClipboardText(e.NameOnly);

                ImGui.TableNextColumn();
                ImGui.TextColored(Grey, WorldOfDisplay(e.FullName));

                ImGui.TableNextColumn();
                ImGui.TextColored(winner ? Gold : Green, e.Roll.ToString());

                ImGui.TableNextColumn();
                ImGui.TextColored(Grey, e.OutOf.ToString());
            }
            ImGui.EndTable();
        }

        if (Give.Count == 0)
            ImGui.TextColored(Grey, Give.Running ? "Waiting for rolls..." : "Start a giveaway, then have people roll.");

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Verification feed (every roll) ---------------------------
        ImGui.TextColored(Blue, "All rolls (verification feed):");
        if (ImGui.BeginTable("##giveawayfeed", 4, flags, new Vector2(0, 160)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch, 1.8f);
            ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Out Of", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableHeadersRow();

            foreach (var f in Give.Feed)
            {
                // In standard mode, a feed roll is "counted" if it's the first
                // for that player. In race mode every roll counts.
                var counted = !f.Invalid && (Give.ExactMatchOn || Give.IsCounted(f));
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(Grey, f.When.ToString("HH:mm:ss"));
                ImGui.TableNextColumn();
                if (f.Invalid)
                    ImGui.TextColored(Red, $"{f.NameOnly} \u2014 {f.InvalidReason}");
                else if (!Give.ExactMatchOn && !counted)
                    ImGui.TextColored(Grey, $"{f.NameOnly} (later roll)");
                else
                    ImGui.TextUnformatted(f.NameOnly);
                ImGui.TableNextColumn();
                ImGui.TextColored(f.Invalid ? Red : (counted ? Green : Grey), f.Roll.ToString());
                ImGui.TableNextColumn();
                ImGui.TextColored(f.Invalid ? Red : Grey, f.OutOf.ToString());
            }
            ImGui.EndTable();
        }

        DrawTabStatus("Giveaway Helper");
    }

    private static string WorldOfDisplay(string full)
    {
        var idx = full.IndexOf('\uE05D');
        return idx < 0 ? string.Empty : full[(idx + 1)..];
    }
}

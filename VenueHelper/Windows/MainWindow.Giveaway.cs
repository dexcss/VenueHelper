using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private GiveawayTracker Give => Plugin.Giveaway;

    private string giveawayWinnerName = string.Empty;
    private string giveawayContribName = string.Empty;
    private string giveawayContribAmount = string.Empty;
    private bool showGiveawayHistory = false;

    private void DrawGiveawayTab()
    {
        currentTab = "Giveaway Helper";

        DrawTabHeader("Giveaway Helper", "##export_giveaway",
            new ExportItem("Counted rolls", "giveaway_rolls", () => ExportData.GiveawayResults(Give.Entries)),
            new ExportItem("Winners", "giveaway_winners", () => ExportData.GiveawayWinners(Give.LoggedWinners)),
            new ExportItem("Pot & contributors", "giveaway_pot", () => ExportData.GiveawayContributions(Config.GiveawayContributions, Config.GiveawayHousePot, Give.TotalPot)),
            new ExportItem("History (past giveaways)", "giveaway_history", () => ExportData.GiveawayHistoryExport(Give.History)));

        var exact = Give.ExactMatchOn;
        ImGui.TextColored(Grey, exact
            ? "Everyone keeps rolling until someone hits the exact number."
            : "Start, then have everyone /random. Only each person's FIRST roll counts.");

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Optional pot + contributors ------------------------------
        var showPot = Config.GiveawayShowPot;
        if (ImGui.Checkbox("Show pot and additional contributors", ref showPot))
        {
            Config.GiveawayShowPot = showPot;
            Config.Save();
        }
        if (showPot)
        {
            ImGui.TextColored(Blue, "Pot");
            var house = Config.GiveawayHousePot;
            var houseText = house == 0 ? string.Empty : house.ToString();
            ImGui.SetNextItemWidth(SW(160));
            if (ImGui.InputTextWithHint("##housepot", "House pot (e.g. 3.4M)", ref houseText, 24))
            {
                if (GilFormat.TryParse(houseText, out var parsed)) { Config.GiveawayHousePot = Math.Max(0, parsed); Config.Save(); }
                else if (string.IsNullOrWhiteSpace(houseText)) { Config.GiveawayHousePot = 0; Config.Save(); }
            }
            ImGui.SameLine();
            ImGui.TextColored(Gold, $"Total pot: {GilFormat.Short(Give.TotalPot)} ({Give.TotalPot:N0} gil)");

            // Contributors list.
            GiveawayContribution? removeContrib = null;
            foreach (var c in Config.GiveawayContributions)
            {
                ImGui.PushID(c.Id.ToString());
                ImGui.TextColored(Green, "\u2022");
                ImGui.SameLine();
                ImGui.TextUnformatted($"{c.Name} \u2014 {GilFormat.Short(c.Amount)} ({c.Amount:N0} gil)");
                ImGui.SameLine();
                if (ImGui.SmallButton("x")) removeContrib = c;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove this contributor.");
                ImGui.PopID();
            }
            if (removeContrib != null)
            {
                Config.GiveawayContributions.Remove(removeContrib);
                Config.Save();
            }

            // Add a contributor.
            if (ImGui.Button("Target##contrib"))
            {
                var t = Plugin.GetTargetName();
                if (string.IsNullOrEmpty(t)) SetStatus("No player targeted.", Red);
                else giveawayContribName = t.Replace('\uE05D', '@');
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(SW(170));
            ImGui.InputTextWithHint("##contribname", "Contributor name", ref giveawayContribName, 64);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(SW(120));
            ImGui.InputTextWithHint("##contribamt", "amount (5M)", ref giveawayContribAmount, 24);
            ImGui.SameLine();
            if (ImGui.Button("+ Add##contrib"))
            {
                if (string.IsNullOrWhiteSpace(giveawayContribName))
                    SetStatus("Enter a contributor name first.", Red);
                else if (!GilFormat.TryParse(giveawayContribAmount, out var amt) || amt <= 0)
                    SetStatus("Enter a valid amount (e.g. 5M).", Red);
                else
                {
                    Config.GiveawayContributions.Add(new GiveawayContribution { Name = giveawayContribName, Amount = amt });
                    Config.Save();
                    SetStatus($"Added {giveawayContribName} \u2014 {GilFormat.Short(amt)} to the pot.", Green);
                    giveawayContribName = string.Empty;
                    giveawayContribAmount = string.Empty;
                }
            }
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Separator();
        }

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Mode selection -------------------------------------------
        ImGui.TextColored(Blue, "Winner mode");

        // Manual mode: the giveaway runs elsewhere (e.g. Twitch). No in-game roll
        // capture \u2014 the host just uses the announce line, pot/contributors, and
        // winner log. It takes over the whole mode UI when on.
        var manual = Give.ManualOn;
        if (ImGui.Checkbox("Manual (giveaway run elsewhere, e.g. Twitch)", ref manual))
        {
            Give.Modes = manual ? GiveawayMode.Manual : GiveawayMode.Highest;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("No in-game rolls are captured. Use the announce line, pot/contributor tracking, and tick a winner by hand. Good for giveaways you draw on stream.");

        if (Give.ManualOn)
        {
            WrapText(Grey, "Manual giveaway \u2014 roll capture is off. Track the pot and contributors below, announce it with the line above, and log the winner by hand once you've drawn it.");
        }
        else
        {
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
            WrapText(Green, Give.ManualOn
                ? $"LIVE  {(int)since.TotalMinutes:00}:{since.Seconds:00}  \u2014  manual tracking"
                : $"LIVE  {(int)since.TotalMinutes:00}:{since.Seconds:00}  \u2014  {Give.Count} rolls");
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset##giveaway"))
            ImGui.OpenPopup("##resetgiveaway");
        ImGui.SameLine();
        if (ImGui.Button(showGiveawayHistory ? "Hide history" : $"History ({Give.History.Count})"))
            showGiveawayHistory = !showGiveawayHistory;
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Past giveaways: winners, pot, and contributors. A giveaway is archived here when you Reset.");
        if (ImGui.BeginPopup("##resetgiveaway"))
        {
            WrapText(Red, "Reset for a new giveaway? The current winners, pot, and contributors are saved to History first, then cleared.");
            if (ImGui.Button("Yes, reset"))
            {
                Give.Reset();
                SetStatus("Giveaway archived to history and cleared.", Green);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        // ---- History view ---------------------------------------------
        if (showGiveawayHistory)
        {
            ImGuiHelpers.ScaledDummy(4f);
            if (Give.History.Count == 0)
                WrapText(Grey, "No past giveaways yet. When you Reset a giveaway, it's archived here.");
            else
            {
                const ImGuiTableFlags hflags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                                               | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;
                if (ImGui.BeginTable("##giveawayhistory", 5, hflags, new Vector2(0, SW(200))))
                {
                    ImGui.TableSetupScrollFreeze(0, 1);
                    ImGui.TableSetupColumn("When", ImGuiTableColumnFlags.WidthFixed, SW(110));
                    ImGui.TableSetupColumn("Mode", ImGuiTableColumnFlags.WidthFixed, SW(70));
                    ImGui.TableSetupColumn("Winner(s)", ImGuiTableColumnFlags.WidthStretch, 1.4f);
                    ImGui.TableSetupColumn("Pot", ImGuiTableColumnFlags.WidthFixed, SW(90));
                    ImGui.TableSetupColumn("Contributors", ImGuiTableColumnFlags.WidthStretch, 1.6f);
                    ImGui.TableHeadersRow();
                    foreach (var h in Give.History)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.AlignTextToFramePadding();
                        ImGui.TextColored(Grey, h.When.ToString("MM-dd HH:mm"));
                        ImGui.TableNextColumn();
                        ImGui.TextColored(Blue, h.Mode);
                        ImGui.TableNextColumn();
                        ImGui.TextColored(Gold, h.WinnerSummary);
                        ImGui.TableNextColumn();
                        ImGui.TextColored(Green, GilFormat.Short(h.TotalPot));
                        if (ImGui.IsItemHovered() && h.TotalPot > 0) ImGui.SetTooltip($"{h.TotalPot:N0} gil");
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(h.ContributorSummary);
                    }
                    ImGui.EndTable();
                }
                if (ImGui.SmallButton("Clear history"))
                    ImGui.OpenPopup("##clearghist");
                if (ImGui.BeginPopup("##clearghist"))
                {
                    ImGui.TextColored(Red, "Delete all giveaway history?");
                    if (ImGui.Button("Yes, delete all")) { Give.ClearHistory(); ImGui.CloseCurrentPopup(); }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
                    ImGui.EndPopup();
                }
            }
        }

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Announce line (like Shout/Yell) --------------------------
        ImGui.TextColored(Blue, "Announce the giveaway");
        var annCh = Config.GiveawayAnnounceChannel;
        ImGui.SetNextItemWidth(SW(90));
        if (ImGui.BeginCombo("##giveawaychannel", ChannelLabels[Math.Clamp(annCh, 0, ChannelLabels.Length - 1)]))
        {
            for (var c = 0; c < ChannelLabels.Length; c++)
            {
                if (ImGui.Selectable(ChannelLabels[c], c == annCh))
                {
                    Config.GiveawayAnnounceChannel = c;
                    Config.Save();
                }
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        var annText = Config.GiveawayAnnounceText;
        var sendW = ImGui.CalcTextSize("Send").X + ImGui.GetStyle().FramePadding.X * 2 + SW(16);
        ImGui.SetNextItemWidth(Math.Max(SW(160), ImGui.GetContentRegionAvail().X - sendW));
        if (ImGui.InputTextWithHint("##giveawayann", "e.g. Giveaway now! /random to enter \u2014 highest roll wins!", ref annText, 400))
        {
            Config.GiveawayAnnounceText = annText;
            Config.Save();
        }
        ImGui.SameLine();
        if (ImGui.Button("Send##giveawayann"))
        {
            var ch = (ChatChannel)Math.Clamp(Config.GiveawayAnnounceChannel, 0, ChannelLabels.Length - 1);
            var (ok, message) = ChatSender.SendToChannel(ch, Config.GiveawayAnnounceText);
            SetStatus(ok ? $"Sent to {ChannelLabels[Config.GiveawayAnnounceChannel]}." : message, ok ? Green : Red);
        }

        ImGuiHelpers.ScaledDummy(6f);

        if (Give.ManualOn)
        {
            // No captured rolls in Manual mode \u2014 credit the winner by name.
            ImGui.TextColored(Blue, "Credit a winner");
            if (ImGui.Button("Target##manualwin"))
            {
                var t = Plugin.GetTargetName();
                if (string.IsNullOrEmpty(t)) SetStatus("No player targeted.", Red);
                else giveawayWinnerName = t.Replace('\uE05D', '@');
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(SW(200));
            ImGui.InputTextWithHint("##manualwinname", "Winner name", ref giveawayWinnerName, 64);
            ImGui.SameLine();
            if (ImGui.Button("Log winner"))
            {
                if (string.IsNullOrWhiteSpace(giveawayWinnerName))
                    SetStatus("Enter a winner name first.", Red);
                else
                {
                    Give.CreditWinner(giveawayWinnerName.Replace('@', '\uE05D'),
                        Give.TotalPot > 0 ? $"Manual \u2014 pot {GilFormat.Short(Give.TotalPot)}" : "Manual");
                    SetStatus($"Logged {giveawayWinnerName} as the winner.", Gold);
                    giveawayWinnerName = string.Empty;
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Records this player in the winner log below.");
        }
        else
        {

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
                WrapText(Gold, $"Closest to {Give.ClosestTarget}:  {cl.NameOnly}  rolled {cl.Roll}");
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

        if (ImGui.BeginTable("##giveawaycounted", 5, flags, new Vector2(0, 220)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch, 1.8f);
            ImGui.TableSetupColumn("World", ImGuiTableColumnFlags.WidthStretch, 1.0f);
            ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Out Of", ImGuiTableColumnFlags.WidthFixed, 70);
            ImGui.TableSetupColumn("Winner", ImGuiTableColumnFlags.WidthFixed, SW(70));
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

                // Winner credit: checkmark to log this player as a winner.
                ImGui.TableNextColumn();
                ImGui.PushID(e.FullName);
                var logged = Give.IsLoggedWinner(e.FullName);
                if (ImGui.Checkbox("##win", ref logged))
                {
                    if (logged)
                    {
                        var note = winner ? $"Winner \u2014 rolled {e.Roll}" : $"Credited \u2014 rolled {e.Roll}";
                        Give.CreditWinner(e.FullName, note);
                        SetStatus($"Logged {e.NameOnly} as a winner.", Gold);
                    }
                    else
                    {
                        var w = Give.LoggedWinners.FirstOrDefault(x => x.FullName == e.FullName);
                        if (w != null) Give.RemoveWinner(w);
                    }
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Tick to log this player as a winner (saved in the winner log below).");
                ImGui.PopID();
            }
            ImGui.EndTable();
        }

        if (Give.Count == 0)
            WrapText(Grey, Give.Running ? "Waiting for rolls..." : "Start a giveaway, then have people roll.");

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
        } // end !ManualOn roll-based sections

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        // ---- Winner log -----------------------------------------------
        if (Give.LoggedWinners.Count > 0)
        {
            ImGuiHelpers.ScaledDummy(6f);
            ImGui.TextColored(Blue, "Winner log");
            GiveawayWinner? removeWinner = null;
            foreach (var w in Give.LoggedWinners)
            {
                ImGui.PushID(w.Id.ToString());
                ImGui.TextColored(Gold, $"\u2605 {w.NameOnly}");
                ImGui.SameLine();
                ImGui.TextColored(Grey, $"\u2014 {w.Note}  ({w.When:MM-dd HH:mm})");
                ImGui.SameLine();
                if (ImGui.SmallButton("x")) removeWinner = w;
                ImGui.PopID();
            }
            if (removeWinner != null) Give.RemoveWinner(removeWinner);
        }

        DrawTabStatus("Giveaway Helper");
    }

    private static string WorldOfDisplay(string full)
    {
        var idx = full.IndexOf('\uE05D');
        return idx < 0 ? string.Empty : full[(idx + 1)..];
    }
}

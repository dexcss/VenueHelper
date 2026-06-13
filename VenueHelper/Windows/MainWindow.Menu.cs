using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private string newMenuItemName = string.Empty;
    private string menuServeBuyer = string.Empty;
    private bool menuEditMode = false;
    private string newMenuProfileName = string.Empty;
    private string newMacroLabel = string.Empty;
    private bool clearMacrosConfirm = false;

    private MenuService Menu => Plugin.Menu;

    private void DrawMenuTab()
    {
        currentTab = "Menu Helper";

        DrawTabHeader("Menu Helper", "##export_menu",
            new ExportItem("Sales tonight (item, price, buyer, time)", "menu_sales",
                () => ExportData.MenuSales(Menu.Sales)),
            new ExportItem("Totals per item (sold, revenue)", "menu_totals",
                () => ExportData.MenuTotals(Menu.Sales)));

        // ---- Menu profile selector ------------------------------------
        ImGui.TextColored(Grey, "Menu:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(SW(200));
        if (ImGui.BeginCombo("##menuprofile", Menu.Profile.Name))
        {
            for (var i = 0; i < Menu.Profiles.Count; i++)
            {
                var selected = i == Config.SelectedMenuProfile;
                if (ImGui.Selectable(Menu.Profiles[i].Name, selected)) Menu.SelectProfile(i);
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("+ New menu")) ImGui.OpenPopup("##newmenuprofile");
        if (ImGui.BeginPopup("##newmenuprofile"))
        {
            ImGui.TextColored(Grey, "Name this menu (e.g. the venue):");
            ImGui.SetNextItemWidth(SW(220));
            ImGui.InputTextWithHint("##newmenuname", "Menu name", ref newMenuProfileName, 64);
            if (ImGui.Button("Create") && !string.IsNullOrWhiteSpace(newMenuProfileName))
            {
                Menu.AddProfile(newMenuProfileName);
                newMenuProfileName = string.Empty;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
        if (Menu.Profiles.Count > 1)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
            if (ImGui.SmallButton("Delete menu")) ImGui.OpenPopup("##delmenuprofile");
            ImGui.PopStyleColor();
            if (ImGui.BeginPopup("##delmenuprofile"))
            {
                WrapText(Red, $"Delete the menu \"{Menu.Profile.Name}\" and its items/macros?");
                if (ImGui.Button("Yes, delete"))
                {
                    Menu.RemoveProfile(Menu.Profile);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        ImGuiHelpers.ScaledDummy(4f);

        // ---- Tonight's till -------------------------------------------
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.16f, 0.13f, 0.10f, 1f));
        if (ImGui.BeginChild("##till", new Vector2(0, SW(54)), true))
        {
            ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.5f, 1f), $"Tonight's Till \u2014 {Menu.Profile.Name}");
            ImGui.SameLine(0, 24);
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(Green, $"{Menu.TotalRevenue:N0} gil");
            ImGui.SameLine(0, 16);
            WrapText(Grey, $"across {Menu.TotalSales} order{(Menu.TotalSales == 1 ? "" : "s")}");
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGuiHelpers.ScaledDummy(4f);

        // Mode toggle. In play mode, also show who we're serving to.
        if (ImGui.Button(menuEditMode ? "\u2190 Done editing" : "Edit menu & buttons"))
            menuEditMode = !menuEditMode;
        if (!menuEditMode)
        {
            ImGui.SameLine(0, 24);
            ImGui.TextColored(Grey, "Serving to:");
            ImGui.SameLine();
            if (ImGui.SmallButton("Target"))
            {
                var t = Plugin.GetTargetName();
                if (!string.IsNullOrEmpty(t)) menuServeBuyer = VenueHelper.Logic.VenueCounter.NameOnly(t);
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(SW(180));
            ImGui.InputTextWithHint("##servebuyer", "(optional) guest name", ref menuServeBuyer, 64);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Optional. If filled, the sale is recorded to this guest. Leave blank for an anonymous order.");
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        if (menuEditMode)
            DrawMenuEditor();
        else
            DrawMenuService();

        DrawTabStatus("Menu Helper");
    }

    // The customer-facing "menu": each item is a card you can Serve.
    private void DrawMenuService()
    {
        // Reusable one-click action banks (buttons only here; edited in Edit mode).
        // Macro buttons (full sequences; configured in Edit).
        DrawMacroButtons();

        ImGuiHelpers.ScaledDummy(4f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        if (Menu.Items.Count == 0)
        {
            WrapText(Grey, "No menu items yet. Click \"Edit menu\" to add your drinks and dishes.");
            DrawMenuSalesLog();
            return;
        }

        ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.5f, 1f), "\u2014 The Menu \u2014");
        ImGuiHelpers.ScaledDummy(4f);

        for (var i = 0; i < Menu.Items.Count; i++)
        {
            var item = Menu.Items[i];
            ImGui.PushID(i);

            // Effective steps (migrate legacy single emote for display).
            var effSteps = item.ServeSteps.Where(s => !string.IsNullOrWhiteSpace(s.Command)).ToList();
            var hasSeq = effSteps.Count > 0 || !string.IsNullOrWhiteSpace(item.Emote);
            var previewLines = effSteps.Select(s => ChatSender.ResolveCommand(s.Command)).ToList();
            if (previewLines.Count == 0 && !string.IsNullOrWhiteSpace(item.Emote))
                previewLines.Add(ChatSender.ResolveCommand(item.Emote));

            var cardH = SW(58 + (hasSeq ? 16 * Math.Min(previewLines.Count, 4) + 8 : 0));
            if (ImGui.BeginChild($"##item{i}", new Vector2(0, cardH), true))
            {
                // Name + price, with the Serve button right-aligned.
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(new Vector4(0.95f, 0.9f, 0.75f, 1f), item.Name);
                ImGui.SameLine();
                ImGui.TextColored(Green, $"  {item.Price:N0} gil");

                var serveLabel = "Serve";
                var btnW = ImGui.CalcTextSize(serveLabel).X + ImGui.GetStyle().FramePadding.X * 2 + SW(8);
                ImGui.SameLine();
                var avail = ImGui.GetContentRegionAvail().X;
                if (avail > btnW) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - btnW);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.4f, 0.25f, 1f));
                if (ImGui.Button(serveLabel))
                {
                    var (ok, msg) = Menu.Serve(item, menuServeBuyer);
                    SetStatus(msg, ok ? Green : Red);
                }
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(hasSeq
                        ? "Records the sale and performs this sequence for you:\n" + string.Join("\n", previewLines)
                        : "Records this sale. (No serve sequence \u2014 add emotes in Edit to perform them automatically.)");

                if (hasSeq)
                {
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(520));
                    ImGui.TextColored(Grey, "On serve:  " + string.Join("  \u2192  ", previewLines));
                    ImGui.PopTextWrapPos();
                }
            }
            ImGui.EndChild();
            ImGui.PopID();
            ImGuiHelpers.ScaledDummy(3f);
        }

        DrawMenuSalesLog();
    }

    private void DrawMenuSalesLog()
    {
        ImGuiHelpers.ScaledDummy(8f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.5f, 1f), "Orders tonight");
        ImGui.SameLine(0, 16);
        if (ImGui.SmallButton("Clear orders"))
            ImGui.OpenPopup("##clearsales");
        if (ImGui.BeginPopup("##clearsales"))
        {
            ImGui.TextColored(Red, "Clear all of tonight's orders?");
            if (ImGui.Button("Yes, clear"))
            {
                Menu.ClearSales();
                SetStatus("Orders cleared.", Grey);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        const ImGuiTableFlags flags = ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg
                                      | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp;
        if (ImGui.BeginTable("##saleslog", 5, flags, new Vector2(0, SW(200))))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed, SW(80));
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch, 1.6f);
            ImGui.TableSetupColumn("Price", ImGuiTableColumnFlags.WidthFixed, SW(90));
            ImGui.TableSetupColumn("Guest", ImGuiTableColumnFlags.WidthStretch, 1.4f);
            ImGui.TableSetupColumn("##del", ImGuiTableColumnFlags.WidthFixed, SW(28));
            ImGui.TableHeadersRow();

            MenuSale? toRemove = null;
            var id = 0;
            for (var i = Menu.Sales.Count - 1; i >= 0; i--)
            {
                var s = Menu.Sales[i];
                ImGui.PushID(id++);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextColored(Grey, s.When.ToString("h:mm tt"));
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(s.ItemName);
                ImGui.TableNextColumn();
                ImGui.TextColored(Green, $"{s.Price:N0}");
                ImGui.TableNextColumn();
                ImGui.TextColored(Grey, string.IsNullOrWhiteSpace(s.Buyer) ? "\u2014" : s.Buyer);
                ImGui.TableNextColumn();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) toRemove = s;
                ImGui.PopID();
            }
            ImGui.EndTable();
            if (toRemove != null) Menu.RemoveSale(toRemove);
        }
    }

    // Edit mode: add/edit/remove menu items.
    private void DrawMenuEditor()
    {
        // --- Additional Macros (configured here) -----------------------
        DrawMacroEditor();

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(6f);

        ImGui.TextColored(Blue, "Add an item");
        ImGui.SetNextItemWidth(SW(200));
        ImGui.InputTextWithHint("##newitem", "Item name (e.g. Espresso)", ref newMenuItemName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add item"))
        {
            Menu.AddItem(newMenuItemName);
            newMenuItemName = string.Empty;
        }

        ImGuiHelpers.ScaledDummy(6f);

        MenuItem? toRemove = null;
        for (var i = 0; i < Menu.Items.Count; i++)
        {
            var item = Menu.Items[i];
            ImGui.PushID(i);
            // Migrate a legacy single emote into the steps list once.
            if (item.ServeSteps.Count == 0 && !string.IsNullOrWhiteSpace(item.Emote))
            {
                item.ServeSteps.Add(new ServeStep(item.Emote, 1.0f));
                item.Emote = string.Empty;
                Menu.Save();
            }

            var childH = SW(92 + 26 * Math.Max(1, item.ServeSteps.Count) + 16);
            if (ImGui.BeginChild($"##edit{i}", new Vector2(0, childH), true))
            {
                var name = item.Name;
                ImGui.SetNextItemWidth(SW(220));
                if (ImGui.InputTextWithHint("Name", "Item name", ref name, 64)) { item.Name = name; Menu.Save(); }
                ImGui.SameLine();
                var price = (int)item.Price;
                ImGui.SetNextItemWidth(SW(150));
                if (ImGui.InputInt("Price (gil)", ref price, 100, 1000)) { item.Price = Math.Max(0, price); Menu.Save(); }

                WrapText(Grey, "Serve sequence (this item's macro) \u2014 runs automatically when you press Serve. Each step is a command/emote with a wait (seconds) after it. Plain text becomes /emote; anything starting with / (e.g. /say, /micon, /handover, /trade) is sent as-is.");
                int? stepRemove = null;
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4f, 2f));
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6f, 3f));
                for (var s = 0; s < item.ServeSteps.Count; s++)
                {
                    var step = item.ServeSteps[s];
                    ImGui.PushID($"step{s}");
                    ImGui.TextColored(Grey, $"{s + 1}.");
                    ImGui.SameLine();
                    var cmd = step.Command;
                    // Flex the command box so the trailing controls (wait, Test,
                    // up, X) stay on-screen on narrower windows.
                    var trailing = SW(260);
                    var cmdW = Math.Max(SW(140), ImGui.GetContentRegionAvail().X - trailing);
                    ImGui.SetNextItemWidth(cmdW);
                    if (ImGui.InputTextWithHint("##cmd", "/handover, /say text, or written action", ref cmd, 400))
                    {
                        step.Command = cmd;
                        Menu.Save();
                    }
                    ImGui.SameLine();
                    ImGui.TextColored(Grey, "wait");
                    ImGui.SameLine();
                    var delay = step.DelayAfter;
                    ImGui.SetNextItemWidth(SW(90));
                    if (ImGui.InputFloat("##delay", ref delay, 0.1f, 0.5f, "%.1fs"))
                    {
                        step.DelayAfter = Math.Clamp(delay, 0f, 60f);
                        Menu.Save();
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Test"))
                    {
                        var (ok, msg) = ChatSender.SendRaw(ChatSender.ResolveCommand(step.Command));
                        SetStatus(ok ? "Tested step." : msg, ok ? Green : Red);
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("\u2191") && s > 0)
                    {
                        (item.ServeSteps[s - 1], item.ServeSteps[s]) = (item.ServeSteps[s], item.ServeSteps[s - 1]);
                        Menu.Save();
                    }
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
                    if (ImGui.SmallButton("X")) stepRemove = s;
                    ImGui.PopStyleColor();
                    ImGui.PopID();
                }
                if (stepRemove != null) { item.ServeSteps.RemoveAt(stepRemove.Value); Menu.Save(); }
                ImGui.PopStyleVar(2);

                if (ImGui.SmallButton("+ Add step")) { item.ServeSteps.Add(new ServeStep(string.Empty, 1.0f)); Menu.Save(); }
                ImGui.SameLine(0, 16);
                if (ImGui.SmallButton("+ Add /trade step")) { item.ServeSteps.Add(new ServeStep("/trade", 2.0f)); Menu.Save(); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Opens a trade with your current target when this step runs.");

                ImGui.SameLine(0, 24);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Remove item")) toRemove = item;
                ImGui.PopStyleColor();
            }
            ImGui.EndChild();
            ImGui.PopID();
        }
        if (toRemove != null) Menu.RemoveItem(toRemove);

        if (Menu.Items.Count == 0)
            ImGui.TextColored(Grey, "Add your first item above.");
    }

    // Play view: just the clickable buttons for a bank.
    // Play view: the macro buttons for the active profile.
    private void DrawMacroButtons()
    {
        ImGui.TextColored(new Vector4(0.95f, 0.85f, 0.5f, 1f), "Additional Macros");
        var macros = Menu.Macros;
        if (macros.Count == 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(Grey, "(set these up in Edit)");
            return;
        }
        var shown = 0;
        var style = ImGui.GetStyle();
        var avail = ImGui.GetContentRegionAvail().X;
        var xUsed = 0f;
        for (var i = 0; i < macros.Count; i++)
        {
            var m = macros[i];
            if (string.IsNullOrWhiteSpace(m.Label)) continue;
            ImGui.PushID($"macrobtn{i}");

            // Estimate this button's width (label + frame padding) and wrap to a
            // new line if placing it on the current line would overflow.
            var btnW = ImGui.CalcTextSize(m.Label).X + style.FramePadding.X * 2;
            if (shown > 0)
            {
                var projected = xUsed + style.ItemSpacing.X + btnW;
                if (projected <= avail)
                {
                    ImGui.SameLine();
                    xUsed = projected;
                }
                else
                {
                    xUsed = btnW; // new line
                }
            }
            else
            {
                xUsed = btnW;
            }
            shown++;

            if (ImGui.Button(m.Label))
            {
                var (ok, msg) = Menu.RunMacro(m);
                SetStatus(msg, ok ? Green : Red);
            }
            if (ImGui.IsItemHovered())
            {
                var lines = m.Steps.Where(s => !string.IsNullOrWhiteSpace(s.Command))
                    .Select(s => ChatSender.ResolveCommand(s.Command) + (s.DelayAfter > 0 ? $"  (wait {s.DelayAfter:0.#}s)" : ""));
                var tip = string.Join("\n", lines);
                ImGui.SetTooltip(string.IsNullOrWhiteSpace(tip) ? "(no steps)" : tip);
            }
            ImGui.PopID();
        }
    }

    // Edit view: create/edit/remove macros, each a full step sequence.
    private void DrawMacroEditor()
    {
        ImGui.TextColored(Blue, "Additional Macros");
        if (Menu.Macros.Count > 0)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
            if (ImGui.SmallButton("Clear all macros"))
            {
                clearMacrosConfirm = false;
                ImGui.OpenPopup("##clearmacros");
            }
            ImGui.PopStyleColor();
            if (ImGui.BeginPopup("##clearmacros"))
            {
                if (!clearMacrosConfirm)
                {
                    ImGui.TextColored(Red, "Remove ALL macros from this menu?");
                    if (ImGui.Button("Yes, continue"))
                    {
                        clearMacrosConfirm = true;
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
                }
                else
                {
                    WrapText(Red, "ARE YOU SURE? ALL MACROS ON THIS MENU WILL BE GONE FOREVER.");
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.1f, 0.1f, 1f));
                    if (ImGui.Button("Yes, delete them all"))
                    {
                        Menu.Macros.Clear();
                        Menu.Save();
                        SetStatus("All macros cleared.", Red);
                        clearMacrosConfirm = false;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    if (ImGui.Button("No, keep them"))
                    {
                        clearMacrosConfirm = false;
                        ImGui.CloseCurrentPopup();
                    }
                }
                ImGui.EndPopup();
            }
        }
        WrapText(Grey, "Standalone macro buttons that aren't tied to a menu item (no price, no sale) \u2014 for adverts, menu hand-overs, hourly call-outs, etc. They use the exact same step format as a menu item's serve sequence. (To make a macro fire when an item is served, build it into that item's serve sequence instead.) Each step is a command/emote with a wait after it; plain text becomes /emote, /commands send as-is.");

        MenuMacro? macroRemove = null;
        for (var mi = 0; mi < Menu.Macros.Count; mi++)
        {
            var macro = Menu.Macros[mi];
            ImGui.PushID($"macro{mi}");
            var childH = SW(54 + 26 * Math.Max(1, macro.Steps.Count) + 16);
            if (ImGui.BeginChild($"##macroedit{mi}", new Vector2(0, childH), true))
            {
                var label = macro.Label;
                ImGui.SetNextItemWidth(SW(220));
                if (ImGui.InputTextWithHint("Button label", "e.g. Hand over menu", ref label, 48)) { macro.Label = label; Menu.Save(); }

                int? stepRemove = null;
                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4f, 2f));
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6f, 3f));
                for (var s = 0; s < macro.Steps.Count; s++)
                {
                    var step = macro.Steps[s];
                    ImGui.PushID($"mstep{s}");
                    ImGui.TextColored(Grey, $"{s + 1}.");
                    ImGui.SameLine();
                    var cmd = step.Command;
                    var mTrailing = SW(260);
                    var mCmdW = Math.Max(SW(140), ImGui.GetContentRegionAvail().X - mTrailing);
                    ImGui.SetNextItemWidth(mCmdW);
                    if (ImGui.InputTextWithHint("##cmd", "/micon \"x\" emote, /em ..., /t <t> ..., or written action", ref cmd, 400)) { step.Command = cmd; Menu.Save(); }
                    ImGui.SameLine();
                    ImGui.TextColored(Grey, "wait");
                    ImGui.SameLine();
                    var delay = step.DelayAfter;
                    ImGui.SetNextItemWidth(SW(90));
                    if (ImGui.InputFloat("##delay", ref delay, 0.1f, 0.5f, "%.1fs")) { step.DelayAfter = Math.Clamp(delay, 0f, 60f); Menu.Save(); }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Test")) { var (ok, msg) = ChatSender.SendRaw(ChatSender.ResolveCommand(step.Command)); SetStatus(ok ? "Tested step." : msg, ok ? Green : Red); }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("\u2191") && s > 0) { (macro.Steps[s - 1], macro.Steps[s]) = (macro.Steps[s], macro.Steps[s - 1]); Menu.Save(); }
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
                    if (ImGui.SmallButton("X")) stepRemove = s;
                    ImGui.PopStyleColor();
                    ImGui.PopID();
                }
                if (stepRemove != null) { macro.Steps.RemoveAt(stepRemove.Value); Menu.Save(); }
                ImGui.PopStyleVar(2);

                if (ImGui.SmallButton("+ Add step")) { macro.Steps.Add(new ServeStep(string.Empty, 1.0f)); Menu.Save(); }
                ImGui.SameLine(0, 16);
                if (ImGui.SmallButton("+ Add /trade step")) { macro.Steps.Add(new ServeStep("/trade", 2.0f)); Menu.Save(); }
                ImGui.SameLine(0, 24);
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.45f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Remove macro")) macroRemove = macro;
                ImGui.PopStyleColor();
            }
            ImGui.EndChild();
            ImGui.PopID();
        }
        if (macroRemove != null) Menu.RemoveMacro(macroRemove);

        ImGui.SetNextItemWidth(SW(220));
        ImGui.InputTextWithHint("##newmacro", "New macro button label", ref newMacroLabel, 48);
        ImGui.SameLine();
        if (ImGui.Button("Add macro"))
        {
            if (!string.IsNullOrWhiteSpace(newMacroLabel))
            {
                var m = Menu.AddMacro(newMacroLabel);
                m.Steps.Add(new ServeStep(string.Empty, 1.0f));
                Menu.Save();
                newMacroLabel = string.Empty;
            }
            else SetStatus("Enter a macro button label first.", Red);
        }
    }
}

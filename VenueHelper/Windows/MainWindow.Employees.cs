using Dalamud.Interface.Utility;
using VenueHelper.Data;
using VenueHelper.Logic;

namespace VenueHelper.Windows;

public partial class MainWindow
{
    private string employeeNameInput = string.Empty;
    private static readonly string[] PayModeLabels = { "Hourly", "Flat" };

    private EmployeeService Staff => Plugin.Employees;

    private void DrawEmployeesTab()
    {
        currentTab = "Employees";

        DrawTabHeader("Employees", "##export_employees",
            new ExportItem("Employee pay (name, mode, worked, owed, paid)", "employees",
                () => ExportData.Employees(Staff.Employees, Staff)));

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + SW(560));
        ImGui.TextColored(Grey, "Track your staff and what they're owed. Set each person to hourly (clock in/out) or a flat rate, then tick them off when paid.");
        ImGui.PopTextWrapPos();

        ImGuiHelpers.ScaledDummy(6f);

        // ---- Add employee ---------------------------------------------
        ImGui.SetNextItemWidth(SW(200));
        ImGui.InputTextWithHint("##empname", "Employee name", ref employeeNameInput, 64);
        ImGui.SameLine();
        if (ImGui.Button("+ Add"))
        {
            if (string.IsNullOrWhiteSpace(employeeNameInput))
                SetStatus("Enter a name first.", Red);
            else
            {
                Staff.Add(employeeNameInput);
                SetStatus($"Added {employeeNameInput}.", Green);
                employeeNameInput = string.Empty;
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Target##emp"))
        {
            var t = Plugin.GetTargetName();
            if (string.IsNullOrEmpty(t)) SetStatus("No player targeted.", Red);
            else employeeNameInput = t.Replace('\uE05D', '@');
        }

        ImGuiHelpers.ScaledDummy(4f);

        // ---- Summary --------------------------------------------------
        ImGui.TextColored(new Vector4(0.85f, 0.85f, 0.5f, 1f),
            $"Owed: {Staff.TotalOwed:N0} gil");
        ImGui.SameLine();
        ImGui.TextColored(Green, $"   Paid: {Staff.TotalPaid:N0} gil");
        ImGui.SameLine();
        if (ImGui.SmallButton("Reset shifts"))
            ImGui.OpenPopup("##empresetall");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Clear everyone's worked time and paid status for a new night (keeps the roster and pay settings).");
        if (ImGui.BeginPopup("##empresetall"))
        {
            ImGui.TextColored(Red, "Reset ALL shifts? Clears worked time + paid flags (keeps the roster).");
            if (ImGui.Button("Yes, reset all")) { Staff.ResetAllShifts(); ImGui.CloseCurrentPopup(); }
            ImGui.SameLine();
            if (ImGui.Button("Cancel")) ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(4f);

        // ---- Roster ---------------------------------------------------
        if (Staff.Employees.Count == 0)
        {
            WrapText(Grey, "No employees yet. Add one above.");
            DrawTabStatus("Employees");
            return;
        }

        Employee? removeEmp = null;
        for (var i = 0; i < Staff.Employees.Count; i++)
        {
            var e = Staff.Employees[i];
            ImGui.PushID(e.Id.ToString());

            var paid = e.Paid;
            // Green background tint when paid.
            if (paid) ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.12f, 0.28f, 0.14f, 0.55f));

            var rowH = SW(76);
            if (ImGui.BeginChild($"##emp{i}", new Vector2(0, rowH), true))
            {
                // Name + paid checkbox + remove.
                ImGui.AlignTextToFramePadding();
                var pc = paid;
                if (ImGui.Checkbox("##paid", ref pc))
                {
                    Staff.SetPaid(e, pc);
                    SetStatus(pc ? $"Marked {e.Name} paid." : $"{e.Name} marked unpaid.", pc ? Green : Grey);
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Tick when this employee has been paid.");
                ImGui.SameLine();
                ImGui.TextColored(paid ? Green : new Vector4(0.95f, 0.9f, 0.75f, 1f),
                    $"{e.Name}{(paid ? "  \u2713 PAID" : "")}");

                // Remove button, right-aligned.
                var rmW = ImGui.CalcTextSize("Remove").X + ImGui.GetStyle().FramePadding.X * 2 + SW(6);
                ImGui.SameLine();
                var av = ImGui.GetContentRegionAvail().X;
                if (av > rmW) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + av - rmW);
                if (ImGui.SmallButton("Remove")) removeEmp = e;

                // Pay mode + rate.
                ImGui.SetNextItemWidth(SW(90));
                var mode = (int)e.Mode;
                if (ImGui.BeginCombo("##mode", PayModeLabels[mode]))
                {
                    for (var m = 0; m < PayModeLabels.Length; m++)
                        if (ImGui.Selectable(PayModeLabels[m], m == mode)) { e.Mode = (PayMode)m; Config.Save(); }
                    ImGui.EndCombo();
                }
                ImGui.SameLine();

                if (e.Mode == PayMode.Hourly)
                {
                    ImGui.SetNextItemWidth(SW(120));
                    var rateText = e.HourlyRate == 0 ? string.Empty : e.HourlyRate.ToString();
                    if (ImGui.InputTextWithHint("##hrate", "gil/hour", ref rateText, 24))
                    {
                        if (GilFormat.TryParse(rateText, out var r)) { e.HourlyRate = Math.Max(0, r); Config.Save(); }
                        else if (string.IsNullOrWhiteSpace(rateText)) { e.HourlyRate = 0; Config.Save(); }
                    }
                    ImGui.SameLine();

                    // Clock in/out + worked time.
                    var on = e.IsClockedIn;
                    if (on) ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.2f, 0.2f, 1f));
                    else ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.4f, 0.25f, 1f));
                    if (ImGui.Button(on ? "Clock out" : "Clock in")) Staff.ToggleClock(e);
                    ImGui.PopStyleColor();
                    ImGui.SameLine();

                    var worked = Staff.WorkedSeconds(e);
                    var hrs = worked / 3600;
                    var mins = (worked % 3600) / 60;
                    ImGui.TextColored(on ? Green : Grey, $"{hrs}h {mins}m");
                }
                else
                {
                    ImGui.SetNextItemWidth(SW(140));
                    var flatText = e.FlatRate == 0 ? string.Empty : e.FlatRate.ToString();
                    if (ImGui.InputTextWithHint("##frate", "flat gil (e.g. 500k)", ref flatText, 24))
                    {
                        if (GilFormat.TryParse(flatText, out var r)) { e.FlatRate = Math.Max(0, r); Config.Save(); }
                        else if (string.IsNullOrWhiteSpace(flatText)) { e.FlatRate = 0; Config.Save(); }
                    }
                }

                ImGui.SameLine();
                var owed = Staff.AmountOwed(e);
                ImGui.TextColored(new Vector4(0.85f, 0.85f, 0.5f, 1f), $"  =  {GilFormat.Short(owed)} ({owed:N0} gil)");
            }
            ImGui.EndChild();

            if (paid) ImGui.PopStyleColor();
            ImGui.PopID();
        }

        if (removeEmp != null) Staff.Remove(removeEmp);

        DrawTabStatus("Employees");
    }
}

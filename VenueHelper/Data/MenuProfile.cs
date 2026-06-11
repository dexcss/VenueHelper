namespace VenueHelper.Data;

// A named menu profile (e.g. one per venue): its own menu items, macro
// buttons, and the night's sales.
[Serializable]
public class MenuProfile
{
    public string Name = "Default Menu";
    public List<MenuItem> Items = new();
    public List<MenuMacro> Macros = new();
    public List<MenuSale> Sales = new();   // this menu's orders for the night

    public MenuProfile() { }
    public MenuProfile(string name) => Name = name;
}

// A reusable macro button: a label and a full step sequence (command + wait),
// like a menu item's serve sequence but without a price/sale. For ads,
// menu-handover macros, etc.
[Serializable]
public class MenuMacro
{
    public string Label = string.Empty;
    public List<ServeStep> Steps = new();

    public MenuMacro() { }
    public MenuMacro(string label) => Label = label;
}

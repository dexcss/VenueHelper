using VenueHelper.Data;

namespace VenueHelper.Logic;

// Manages the venue's menu items and the night's sales tally.
public class MenuService
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;

    public MenuService(Plugin plugin) => Plugin = plugin;

    public MenuProfile Profile => Config.ActiveMenuProfile;
    public List<MenuItem> Items => Profile.Items;
    public List<MenuMacro> Macros => Profile.Macros;
    public List<MenuSale> Sales => Profile.Sales;

    // ---- Profiles ------------------------------------------------------
    public List<MenuProfile> Profiles => Config.MenuProfiles;

    public void AddProfile(string name)
    {
        Config.MenuProfiles.Add(new MenuProfile(string.IsNullOrWhiteSpace(name) ? "New Menu" : name.Trim()));
        Config.SelectedMenuProfile = Config.MenuProfiles.Count - 1;
        Config.Save();
    }

    public void SelectProfile(int index)
    {
        if (index >= 0 && index < Config.MenuProfiles.Count)
        {
            Config.SelectedMenuProfile = index;
            Config.Save();
        }
    }

    public void RemoveProfile(MenuProfile profile)
    {
        if (Config.MenuProfiles.Count <= 1) return; // always keep one
        Config.MenuProfiles.Remove(profile);
        if (Config.SelectedMenuProfile >= Config.MenuProfiles.Count)
            Config.SelectedMenuProfile = Config.MenuProfiles.Count - 1;
        Config.Save();
    }

    // ---- Items ---------------------------------------------------------
    public MenuItem AddItem(string name)
    {
        var item = new MenuItem(string.IsNullOrWhiteSpace(name) ? "New Item" : name.Trim());
        // Start with one empty serve step so the sequence is visible and ready to
        // fill in (mirrors how Additional Macros start).
        item.ServeSteps.Add(new ServeStep(string.Empty, 1.0f));
        Profile.Items.Add(item);
        Config.Save();
        return item;
    }

    public void RemoveItem(MenuItem item)
    {
        Profile.Items.Remove(item);
        Config.Save();
    }

    // ---- Macros --------------------------------------------------------
    public MenuMacro AddMacro(string label)
    {
        var m = new MenuMacro(string.IsNullOrWhiteSpace(label) ? "New Macro" : label.Trim());
        Profile.Macros.Add(m);
        Config.Save();
        return m;
    }

    public void RemoveMacro(MenuMacro macro)
    {
        Profile.Macros.Remove(macro);
        Config.Save();
    }

    // Run a macro's step sequence (no sale recorded).
    public (bool ok, string message) RunMacro(MenuMacro macro)
    {
        var steps = macro.Steps.Where(s => !string.IsNullOrWhiteSpace(s.Command)).ToList();
        if (steps.Count == 0) return (false, "This macro has no steps.");
        var scheduled = new List<(Action, float)>();
        foreach (var s in steps)
        {
            var cmd = ChatSender.ResolveCommand(s.Command);
            scheduled.Add((() => ChatSender.SendRaw(cmd), s.DelayAfter));
        }
        Plugin.Scheduler.RunSequence(scheduled);
        return (true, $"Running \"{macro.Label}\" ({steps.Count} step{(steps.Count == 1 ? "" : "s")}).");
    }

    public void Save() => Config.Save();

    // Record a sale, then perform the item's serve sequence \u2014 each step is a
    // command/emote with its own delay-after, like a macro's <wait.N> lines.
    // quantity > 1 logs a SINGLE sale of that many (priced qty x unit), and the
    // serve sequence still runs once (so ordering 3 drinks doesn't triple-spam
    // the emotes).
    public (bool ok, string message) Serve(MenuItem item, string buyer, int quantity = 1)
    {
        var qty = Math.Max(1, quantity);
        Profile.Sales.Add(new MenuSale(item.Name, item.Price, qty, buyer?.Trim() ?? string.Empty));
        Config.Save();

        var steps = item.ServeSteps.Where(s => !string.IsNullOrWhiteSpace(s.Command)).ToList();

        // Backward-compat: migrate a legacy single emote into one step.
        if (steps.Count == 0 && !string.IsNullOrWhiteSpace(item.Emote))
            steps.Add(new ServeStep(item.Emote, 1.0f));

        if (steps.Count > 0)
        {
            var scheduled = new List<(Action, float)>();
            foreach (var s in steps)
            {
                var cmd = ChatSender.ResolveCommand(s.Command); // /x as-is, plain -> /emote
                scheduled.Add((() => ChatSender.SendRaw(cmd), s.DelayAfter));
            }
            Plugin.Scheduler.RunSequence(scheduled);
        }

        var who = string.IsNullOrWhiteSpace(buyer) ? "" : $" to {buyer}";
        var extra = steps.Count > 0 ? $" ({steps.Count} step{(steps.Count == 1 ? "" : "s")} queued)" : "";
        var qtyText = qty > 1 ? $" x{qty} ({item.Price * qty:N0} gil)" : "";
        return (true, $"Served {item.Name}{qtyText}{who}.{extra}");
    }

    public void RemoveSale(MenuSale sale)
    {
        Profile.Sales.Remove(sale);
        Config.Save();
    }

    public void ClearSales()
    {
        Profile.Sales.Clear();
        Config.Save();
    }

    public long TotalRevenue => Profile.Sales.Sum(s => s.Price);
    // Number of orders placed (rows), and total items sold across them.
    public int TotalSales => Profile.Sales.Count;
    public int TotalItemsSold => Profile.Sales.Sum(s => Math.Max(1, s.Quantity));
}

namespace VenueHelper.Data;

// A menu item: a name, a price, and an optional /em flavor macro performed when
// it's served (e.g. "shakes the drink slowly and pours it into a chilled cup").
[Serializable]
public class MenuItem
{
    public string Name = string.Empty;
    public long Price = 0;
    public string Emote = string.Empty;   // legacy single emote (still used/migrated)
    public string Category = string.Empty; // optional grouping (Drinks, Food...)

    // Serve sequence: an ordered list of steps performed when this item is
    // served. Each step is a command/emote plus the delay (seconds) to wait
    // AFTER it before the next step \u2014 like macro <wait.N> lines, but you can
    // put anything: emotes, /say, /micon, /handover, /trade, etc. Plain text
    // with no leading slash becomes /emote text; anything starting with / is
    // sent exactly as typed.
    public List<ServeStep> ServeSteps = new();
    // Legacy single emote (migrated into ServeSteps on first edit).
    public bool OpenTradeOnServe = false;  // legacy; superseded by a /trade step

    public MenuItem() { }
    public MenuItem(string name) => Name = name;
}

// One step of a serve sequence: a command/emote and how long to wait after it.
[Serializable]
public class ServeStep
{
    public string Command = string.Empty;  // "/handover", "/say hi", or "pours a drink"
    public float DelayAfter = 1.0f;          // seconds to wait before the next step

    public ServeStep() { }
    public ServeStep(string command, float delayAfter)
    {
        Command = command;
        DelayAfter = delayAfter;
    }
}

// One recorded sale for the night's tally.
[Serializable]
public class MenuSale
{
    public string ItemName = string.Empty;
    // Price is the TOTAL for this sale (unit price x quantity). Kept as the
    // total so existing saved sales (which had no quantity) stay correct.
    public long Price = 0;
    public string Buyer = string.Empty;    // optional
    public DateTime When = DateTime.Now;
    // How many of the item were bought in this one order. Defaults to 1 so
    // sales saved by older versions deserialize correctly.
    public int Quantity = 1;

    // Per-item price, derived from the total (guards against a 0/absent qty).
    public long UnitPrice => Quantity > 1 ? Price / Quantity : Price;

    public MenuSale() { }
    public MenuSale(string itemName, long price, string buyer)
    {
        ItemName = itemName;
        Price = price;
        Buyer = buyer;
        When = DateTime.Now;
        Quantity = 1;
    }

    public MenuSale(string itemName, long unitPrice, int quantity, string buyer)
    {
        ItemName = itemName;
        Quantity = Math.Max(1, quantity);
        Price = unitPrice * Quantity;
        Buyer = buyer;
        When = DateTime.Now;
    }
}

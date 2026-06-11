namespace VenueHelper.Data;

// A labeled one-click action for the Menu tab. The Label is the button text;
// the Text is what gets sent. Used in two banks: emotes (sent via the emote
// rule \u2014 slash-command as-is, or wrapped in /em) and say lines (sent to /say).
[Serializable]
public class QuickAction
{
    public string Label = string.Empty;
    public string Text = string.Empty;

    public QuickAction() { }
    public QuickAction(string label, string text) { Label = label; Text = text; }
}

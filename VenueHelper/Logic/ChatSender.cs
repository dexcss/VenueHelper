using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace VenueHelper.Logic;

// Sends messages to the game's chat as if the host typed them, for the
// Shout/Yell Helper presets.
//
// Routes text through the game's own chat box entry point
// (UIModule.ProcessChatBoxEntry) via FFXIVClientStructs \u2014 the same path used
// when you press Enter on a typed line. This is unofficial automation: messages
// the game rejects (too long, on cooldown, wrong channel availability) simply
// won't send. Only ever sends host-authored, sanitised text.
public static unsafe class ChatSender
{
    // Hard cap under the client's limit, leaving room for the channel command.
    private const int MaxBytes = 400;

    // Build "/channel message" for the chosen channel, then send.
    public static (bool ok, string message) SendToChannel(ChatChannel channel, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (false, "Message is empty.");

        var prefix = channel switch
        {
            ChatChannel.Say => "/say ",
            ChatChannel.Yell => "/yell ",
            ChatChannel.Shout => "/shout ",
            ChatChannel.Party => "/p ",
            _ => "/say ",
        };

        return Send(prefix + text.Trim());
    }

    private static (bool ok, string message) Send(string message)
    {
        var clean = Sanitise(message);
        if (clean.Length == 0)
            return (false, "Nothing to send after cleaning the text.");

        var bytes = System.Text.Encoding.UTF8.GetByteCount(clean);
        if (bytes > MaxBytes)
            return (false, $"Message too long to send ({bytes} bytes). Trim the text.");

        try
        {
            var uiModule = UIModule.Instance();
            if (uiModule == null)
            {
                Plugin.Log.Error("[Venue Helper] UIModule was null; cannot send chat.");
                return (false, "Couldn't reach the game's chat box. Try again in a moment.");
            }

            using var utf8 = new Utf8String(clean);
            uiModule->ProcessChatBoxEntry(&utf8);
            return (true, "Sent.");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "[Venue Helper] Failed to send chat message.");
            return (false, "Failed to send (see /xllog). The game may have updated.");
        }
    }

    // Removes newlines and other control chars; collapses whitespace.
    private static string Sanitise(string input)
    {
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var c in input)
        {
            if (c == '\n' || c == '\r' || c == '\t')
            {
                sb.Append(' ');
                continue;
            }
            if (char.IsControl(c))
                continue;
            sb.Append(c);
        }

        var collapsed = sb.ToString();
        while (collapsed.Contains("  "))
            collapsed = collapsed.Replace("  ", " ");
        return collapsed.Trim();
    }
}

public enum ChatChannel
{
    Say,
    Yell,
    Shout,
    Party,
}

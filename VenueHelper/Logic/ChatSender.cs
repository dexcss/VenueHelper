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

        var trimmed = text.Trim();

        // If the text is already a slash-command (e.g. /emote ..., /dance,
        // /random), run it as-is rather than wrapping it in the channel command
        // \u2014 otherwise "/say /emote ..." just says the literal text. The /em
        // alias is normalized to /emote for reliability.
        if (trimmed.StartsWith("/"))
        {
            if (trimmed.StartsWith("/em ", StringComparison.OrdinalIgnoreCase))
                trimmed = "/emote " + trimmed[4..];
            return Send(trimmed);
        }

        var prefix = channel switch
        {
            ChatChannel.Say => "/say ",
            ChatChannel.Yell => "/yell ",
            ChatChannel.Shout => "/shout ",
            ChatChannel.Party => "/p ",
            _ => "/say ",
        };

        return Send(prefix + trimmed);
    }

    // What SendEmote would actually send, for UI previews.
    public static string ResolveEmote(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0) return string.Empty;
        if (!trimmed.StartsWith("/")) return "/emote " + trimmed;
        if (trimmed.StartsWith("/em ", StringComparison.OrdinalIgnoreCase))
            return "/emote " + trimmed[4..];
        return trimmed;
    }

    // What will actually be sent, for tooltips. Pass channel=null for the emote
    // rule (plain text -> /emote), or a channel for the say/yell/etc rule
    // (plain text -> /channel, slash-commands run as-is).
    public static string PreviewSend(string text, ChatChannel? channel)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0) return string.Empty;

        if (channel == null)
            return ResolveEmote(trimmed);

        if (trimmed.StartsWith("/"))
        {
            if (trimmed.StartsWith("/em ", StringComparison.OrdinalIgnoreCase))
                return "/emote " + trimmed[4..];
            return trimmed;
        }
        var prefix = channel switch
        {
            ChatChannel.Say => "/say ",
            ChatChannel.Yell => "/yell ",
            ChatChannel.Shout => "/shout ",
            ChatChannel.Party => "/p ",
            _ => "/say ",
        };
        return prefix + trimmed;
    }

    // Resolves a serve-step into the exact command to send. Anything starting
    // with "/" is sent as typed (e.g. /say hi, /micon "x", /handover, /trade);
    // the /em alias is normalized to /emote. Plain text becomes "/emote text".
    public static string ResolveCommand(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0) return string.Empty;
        if (!trimmed.StartsWith("/")) return "/emote " + trimmed;
        if (trimmed.StartsWith("/em ", StringComparison.OrdinalIgnoreCase))
            return "/emote " + trimmed[4..];
        return trimmed;
    }

    // Sends an already-built command string exactly as given (assumed to start
    // with the correct slash-command). Used by the serve scheduler.
    public static (bool ok, string message) SendRaw(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return (false, "Empty command.");
        return Send(command.Trim());
    }

    // Performs an emote. Two cases:
    //  - Plain action text ("farts loudly")     -> sent as "/emote farts loudly"
    //  - An explicit command ("/laugh", "/em x") -> sent as a command (the /em
    //    alias is normalized to /emote for reliability via the chat-box API).
    // This lets default emotes (/laugh, /dance, /sit) play their animation while
    // free-form text still goes through /emote.
    public static (bool ok, string message) SendEmote(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (false, "Emote text is empty.");
        var toSend = ResolveEmote(text);
        return Send(toSend);
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

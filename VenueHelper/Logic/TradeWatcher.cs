using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;

namespace VenueHelper.Logic;

// Detects completed trades using Dalamud's TradeOpen condition flag as the
// open/close signal, then reads the received-gil amount from the trade window's
// array data while the window is still up. Crediting happens on the open->closed
// transition, so we never depend on catching a single exact frame.
//
// Adapted from the Elementalist trade watcher: instead of crediting a roll
// budget, completed trades are credited to the raffle as ticket buy-ins.
public unsafe class TradeWatcher
{
    private readonly Plugin Plugin;

    private bool WasOpen;

    private string SnapshotPartner = string.Empty;
    private uint SnapshotReceiveGil;
    private bool SawBothLocked;

    private const int StateLockedIn = 3;

    public TradeWatcher(Plugin plugin)
    {
        Plugin = plugin;
    }

    public void Update()
    {
        if (!Plugin.Configuration.RaffleAutoTrade)
        {
            ResetSnapshot();
            WasOpen = false;
            return;
        }

        var isOpen = Plugin.Condition[ConditionFlag.TradeOpen];

        if (isOpen)
        {
            CaptureSnapshot();
            WasOpen = true;
            return;
        }

        if (WasOpen)
            OnTradeClosed();

        WasOpen = false;
    }

    private void CaptureSnapshot()
    {
        var numbers = TradeNumberArray.Instance();
        var strings = TradeStringArray.Instance();
        if (numbers == null || strings == null)
            return;

        var partner = ReadPartnerName(strings);
        if (partner != string.Empty)
            SnapshotPartner = partner;

        SnapshotReceiveGil = numbers->ReceiveGilCount;

        if (numbers->SelfState == StateLockedIn && numbers->OtherState == StateLockedIn)
            SawBothLocked = true;
    }

    private void OnTradeClosed()
    {
        if (SawBothLocked && SnapshotReceiveGil > 0 && SnapshotPartner != string.Empty)
            CreditTrade(SnapshotPartner, SnapshotReceiveGil);
        else if (SnapshotReceiveGil > 0 && SnapshotPartner != string.Empty)
            Plugin.Chat.Print($"[Venue Helper] Possible raffle buy-in from {StripWorld(SnapshotPartner)} "
                            + $"for {SnapshotReceiveGil:N0} gil wasn't auto-confirmed \u2014 add manually if it completed.");

        ResetSnapshot();
    }

    private void ResetSnapshot()
    {
        SnapshotPartner = string.Empty;
        SnapshotReceiveGil = 0;
        SawBothLocked = false;
    }

    private string ReadPartnerName(TradeStringArray* strings)
    {
        try
        {
            var raw = strings->TradePartnerName.ToString();
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            foreach (var obj in Plugin.Objects)
            {
                if (obj is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter pc
                    && pc.Name.TextValue == raw
                    && pc.HomeWorld.ValueNullable != null)
                {
                    return $"{raw}\uE05D{pc.HomeWorld.Value.Name}";
                }
            }

            return raw;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void CreditTrade(string partner, uint gil)
    {
        Plugin.Raffle.CreditGil(partner, gil);
        var tickets = Plugin.Configuration.TicketCost > 0 ? gil / Plugin.Configuration.TicketCost : 0;
        Plugin.Chat.Print($"[Venue Helper] Raffle: received {gil:N0} gil from {StripWorld(partner)} (\u2248 {tickets} tickets).");
    }

    private static string StripWorld(string full) => full.Replace("\uE05D", "\uE05D ");
}

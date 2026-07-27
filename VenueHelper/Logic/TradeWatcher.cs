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
    // Latched true once we've credited the current trade, so we never
    // double-credit while the window stays open across multiple frames.
    private bool CreditedThisTrade;

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
        if (Plugin.Panic) return; // master kill switch: don't watch trades
        // Only watch trades when a raffle is actively running AND auto-credit is on.
        if (!Plugin.Configuration.RaffleActive || !Plugin.Configuration.RaffleAutoTrade)
        {
            ResetSnapshot();
            WasOpen = false;
            CreditedThisTrade = false;
            return;
        }

        var isOpen = Plugin.Condition[ConditionFlag.TradeOpen];

        if (isOpen)
        {
            // A fresh open after a close resets the per-trade credit latch.
            if (!WasOpen)
                CreditedThisTrade = false;

            CaptureSnapshot();
            WasOpen = true;

            // Credit as soon as BOTH parties are locked in and there's gil to
            // credit \u2014 don't wait for the close edge. At low FPS a trade can open
            // and close entirely between two Update() calls, so the close edge is
            // unreliable; the "both locked + gil present" state is the moment the
            // trade is actually agreed, and we latch it so we credit only once.
            if (!CreditedThisTrade && SawBothLocked
                && SnapshotReceiveGil > 0 && SnapshotPartner != string.Empty)
            {
                CreditTrade(SnapshotPartner, SnapshotReceiveGil);
                CreditedThisTrade = true;
            }
            return;
        }

        // Window is closed. If we opened but never managed to credit (e.g. we
        // only ever caught frames before both locked), fall back to the close
        // edge so a slow-frame trade still gets flagged/credited.
        if (WasOpen && !CreditedThisTrade)
            OnTradeClosed();

        WasOpen = false;
        CreditedThisTrade = false;
        ResetSnapshot();
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

        var bothLocked = numbers->SelfState == StateLockedIn && numbers->OtherState == StateLockedIn;
        if (bothLocked)
        {
            SawBothLocked = true;
        }
        else if (SawBothLocked)
        {
            // We were locked, now we're not: the previous trade finished (or was
            // amended) and a new agreement is being set up within the same open
            // window. Clear both the "locked" flag and the credit latch so the
            // next lock-in is treated as a fresh trade and credited again. This
            // catches back-to-back trades that never showed a closed window at
            // low FPS.
            SawBothLocked = false;
            CreditedThisTrade = false;
        }
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
        // If a bar game is actively tracking, the trade is a buy-in for it.
        var activeGame = Plugin.BarGames.ActiveTrackingGame;
        if (activeGame != null)
        {
            Plugin.BarGames.CreditTrade(partner, gil);
            var cost = activeGame.EntryCost;
            var plays = cost > 0 ? (int)(gil / cost) : 0;
            Plugin.Chat.Print($"[Venue Helper] {activeGame.Name}: received {gil:N0} gil from {StripWorld(partner)} (\u2248 {plays} play{(plays == 1 ? "" : "s")}).");
            return;
        }

        var ticketCost = Plugin.Configuration.TicketCost;
        var tickets = ticketCost > 0 ? (int)(gil / ticketCost) : 0;
        if (tickets > 0)
        {
            var entry = Plugin.Raffle.GetOrCreate(partner);
            Plugin.Raffle.AddTickets(entry, tickets);
        }
        Plugin.Chat.Print($"[Venue Helper] Raffle: received {gil:N0} gil from {StripWorld(partner)} (\u2248 {tickets} ticket{(tickets == 1 ? "" : "s")}).");
    }

    private static string StripWorld(string full) => full.Replace("\uE05D", "\uE05D ");
}

namespace VenueHelper.Logic;

// Runs a queue of timed actions on the framework tick, so we can fire a
// sequence of chat/emote commands with delays between them (avoiding the
// game's chat spam-block) without ever blocking the game thread.
public class ActionScheduler
{
    private readonly struct Step
    {
        public readonly DateTime FireAt;
        public readonly Action Action;
        public Step(DateTime fireAt, Action action) { FireAt = fireAt; Action = action; }
    }

    private readonly List<Step> pending = new();

    // Queue a sequence: each action runs `gapMs` after the previous one. The
    // first runs immediately (or after `startDelayMs`).
    public void RunSequence(IReadOnlyList<Action> actions, int gapMs, int startDelayMs = 0)
    {
        var t = DateTime.Now.AddMilliseconds(startDelayMs);
        foreach (var a in actions)
        {
            pending.Add(new Step(t, a));
            t = t.AddMilliseconds(gapMs);
        }
    }

    // Queue a sequence where each action carries its own delay-after (seconds).
    // The action fires, then we wait that step's delay before the next.
    public void RunSequence(IReadOnlyList<(Action action, float delayAfterSec)> steps)
    {
        var t = DateTime.Now;
        foreach (var (action, delayAfterSec) in steps)
        {
            pending.Add(new Step(t, action));
            t = t.AddSeconds(Math.Max(0, delayAfterSec));
        }
    }

    public void Clear() => pending.Clear();

    public bool HasPending => pending.Count > 0;

    // Called every framework tick.
    public void Update()
    {
        if (pending.Count == 0) return;
        var now = DateTime.Now;
        // Fire all due steps, oldest first; keep the rest.
        for (var i = 0; i < pending.Count; i++)
        {
            if (pending[i].FireAt <= now)
            {
                var action = pending[i].Action;
                pending.RemoveAt(i);
                i--;
                try { action(); }
                catch (Exception ex) { Plugin.Log.Error(ex, "[Venue Helper] Scheduled action failed."); }
                // Only fire one per tick so commands never bunch up into the
                // same frame (extra protection against spam-block).
                break;
            }
        }
    }
}

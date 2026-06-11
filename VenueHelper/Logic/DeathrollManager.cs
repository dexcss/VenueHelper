using VenueHelper.Data;

namespace VenueHelper.Logic;

// Runs a deathroll tournament bracket. Phase 1: single elimination, local only
// (local only). Builds a randomly-seeded bracket, watches the roll stream
// to resolve each match (roll a 1 = lose, roll a 0 = instant win), and advances
// winners until a champion remains.
//
// Deathroll rules: opener does a plain /random (0-999). Each subsequent roll is
// /random [previous result], i.e. rolling down. A 1 loses; a 0 wins instantly.
public class DeathrollManager
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;

    public DeathrollManager(Plugin plugin) => Plugin = plugin;

    // ---- State (persisted in config for crash protection) ----
    public List<DeathrollPlayer> Players => Config.DeathrollPlayers;
    public List<DeathrollMatch> Matches => Config.DeathrollMatches;
    public bool BracketBuilt
    {
        get => Config.DeathrollBuilt;
        private set { Config.DeathrollBuilt = value; Config.Save(); }
    }
    public BracketKind Kind
    {
        get => (BracketKind)Config.DeathrollKind;
        set { Config.DeathrollKind = (int)value; Config.Save(); }
    }

    // Roll-off range to decide who goes first (0 = plain /random, i.e. 0-999).
    public int RolloffValue
    {
        get => Config.DeathrollRolloffValue;
        set { Config.DeathrollRolloffValue = Math.Max(0, value); Config.Save(); }
    }

    // The match currently being actively refereed (rolls are routed here).
    public Guid? ActiveMatchId
    {
        get => string.IsNullOrEmpty(Config.DeathrollActiveMatch) ? null : Guid.Parse(Config.DeathrollActiveMatch);
        set { Config.DeathrollActiveMatch = value?.ToString() ?? string.Empty; Config.Save(); }
    }

    // ---- Player management (pre-bracket) ----

    public DeathrollPlayer? AddPlayer(string fullName)
    {
        if (BracketBuilt || string.IsNullOrWhiteSpace(fullName))
            return null;
        var normalized = fullName.Trim().Replace('@', '\uE05D');
        if (Players.Any(p => p.FullName == normalized))
            return null;
        var p = new DeathrollPlayer(normalized);
        Players.Add(p);
        Config.Save();
        return p;
    }

    public void RemovePlayer(DeathrollPlayer p)
    {
        if (BracketBuilt) return;
        Players.Remove(p);
        Config.Save();
    }

    public void ClearAll()
    {
        Players.Clear();
        Matches.Clear();
        Config.DeathrollBuilt = false;
        Config.DeathrollActiveMatch = string.Empty;
        Config.Save();
    }

    public DeathrollPlayer? GetPlayer(Guid? id) =>
        id == null ? null : Players.FirstOrDefault(p => p.Id == id);

    // ---- Bracket construction ----

    // Build a single-elimination bracket with randomly shuffled seeding. Byes
    // are handled by padding to the next power of two with empty slots; a player
    // drawn against a bye auto-advances.
    public (bool ok, string message) BuildBracket()
    {
        if (Players.Count < 2)
            return (false, "Need at least 2 players.");
        if (BracketBuilt)
            return (false, "Bracket already built. Reset to rebuild.");

        if (Kind == BracketKind.SingleElimination)
            BuildSingleElim();
        else
            BuildDoubleElim();

        BracketBuilt = true;
        return (true, $"Bracket built for {Players.Count} players.");
    }

    private void BuildSingleElim()
    {
        Matches.Clear();

        // Shuffle players for random seeding.
        var seeded = Players.OrderBy(_ => Guid.NewGuid()).ToList();

        // Next power of two >= count; the difference is the number of byes.
        var size = 1;
        while (size < seeded.Count) size <<= 1;
        var rounds = (int)Math.Log2(size);

        // Round 1: create size/2 matches, fill with players, leave the rest as byes.
        var round1Count = size / 2;
        var first = new List<DeathrollMatch>();
        for (var i = 0; i < round1Count; i++)
        {
            var m = new DeathrollMatch { Round = 1, Position = i, State = MatchState.Pending };
            var aIdx = i * 2;
            var bIdx = i * 2 + 1;
            if (aIdx < seeded.Count) m.PlayerA = seeded[aIdx].Id;
            if (bIdx < seeded.Count) m.PlayerB = seeded[bIdx].Id;
            first.Add(m);
        }

        // Build subsequent rounds (empty matches) and link winner advancement.
        var allRounds = new List<List<DeathrollMatch>> { first };
        var prevCount = round1Count;
        for (var r = 2; r <= rounds; r++)
        {
            var count = prevCount / 2;
            var list = new List<DeathrollMatch>();
            for (var i = 0; i < count; i++)
                list.Add(new DeathrollMatch { Round = r, Position = i, State = MatchState.Pending });
            allRounds.Add(list);
            prevCount = count;
        }

        // Link each match's winner to the appropriate slot in the next round.
        for (var r = 0; r < allRounds.Count - 1; r++)
        {
            var cur = allRounds[r];
            var next = allRounds[r + 1];
            for (var i = 0; i < cur.Count; i++)
            {
                var target = next[i / 2];
                cur[i].WinnerTo = target.Id;
                cur[i].WinnerToSlot = i % 2; // even -> slot A, odd -> slot B
            }
        }

        foreach (var list in allRounds)
            Matches.AddRange(list);

        // Make the final a best-of-3.
        var finalMatch = Matches.OrderByDescending(x => x.Round).FirstOrDefault();
        if (finalMatch != null) finalMatch.BestOf = 3;

        // Resolve byes and set initial readiness.
        foreach (var m in Matches)
            ResolveByesAndReadiness(m);
        SweepByes();

        Config.Save();
    }

    private void BuildDoubleElim()
    {
        Matches.Clear();
        var seeded = Players.OrderBy(_ => Guid.NewGuid()).ToList();

        var size = 1;
        while (size < seeded.Count) size <<= 1;
        var wbRounds = (int)Math.Log2(size);

        // ---- Winners bracket ----
        var wb = new List<List<DeathrollMatch>>();
        var round1Count = size / 2;
        var first = new List<DeathrollMatch>();
        for (var i = 0; i < round1Count; i++)
        {
            var m = new DeathrollMatch { Round = 1, Position = i, State = MatchState.Pending, IsLosersBracket = false };
            var aIdx = i * 2;
            var bIdx = i * 2 + 1;
            if (aIdx < seeded.Count) m.PlayerA = seeded[aIdx].Id;
            if (bIdx < seeded.Count) m.PlayerB = seeded[bIdx].Id;
            first.Add(m);
        }
        wb.Add(first);
        var prev = round1Count;
        for (var r = 2; r <= wbRounds; r++)
        {
            var count = prev / 2;
            var list = new List<DeathrollMatch>();
            for (var i = 0; i < count; i++)
                list.Add(new DeathrollMatch { Round = r, Position = i, State = MatchState.Pending, IsLosersBracket = false });
            wb.Add(list);
            prev = count;
        }
        for (var r = 0; r < wb.Count - 1; r++)
            for (var i = 0; i < wb[r].Count; i++)
            {
                wb[r][i].WinnerTo = wb[r + 1][i / 2].Id;
                wb[r][i].WinnerToSlot = i % 2;
            }

        // ---- Losers bracket ----
        var lb = new List<List<DeathrollMatch>>();
        var lbRoundCount = Math.Max(1, 2 * (wbRounds - 1));
        var sizes = new List<int>();
        var cur = Math.Max(1, round1Count / 2);
        for (var k = 0; k < lbRoundCount; k++)
        {
            sizes.Add(Math.Max(1, cur));
            if (k % 2 == 1) cur = Math.Max(1, cur / 2);
        }
        var lbRoundNum = wbRounds + 1;
        for (var k = 0; k < lbRoundCount; k++)
        {
            var list = new List<DeathrollMatch>();
            for (var i = 0; i < sizes[k]; i++)
                list.Add(new DeathrollMatch { Round = lbRoundNum, Position = i, State = MatchState.Pending, IsLosersBracket = true });
            lb.Add(list);
            lbRoundNum++;
        }
        for (var k = 0; k < lb.Count - 1; k++)
        {
            var curList = lb[k];
            var nextList = lb[k + 1];
            for (var i = 0; i < curList.Count; i++)
            {
                if (nextList.Count == curList.Count)
                {
                    curList[i].WinnerTo = nextList[i].Id;
                    curList[i].WinnerToSlot = 1;
                }
                else
                {
                    curList[i].WinnerTo = nextList[i / 2].Id;
                    curList[i].WinnerToSlot = i % 2;
                }
            }
        }

        // ---- Drop WB losers into the LB ----
        for (var i = 0; i < wb[0].Count; i++)
        {
            var dest = lb[0][i / 2];
            wb[0][i].LoserTo = dest.Id;
            wb[0][i].LoserToSlot = i % 2;
        }
        for (var r = 1; r < wb.Count; r++)
        {
            var lbMinor = lb.ElementAtOrDefault(2 * r - 1);
            if (lbMinor == null) continue;
            for (var i = 0; i < wb[r].Count; i++)
            {
                var dest = lbMinor[Math.Min(i, lbMinor.Count - 1)];
                wb[r][i].LoserTo = dest.Id;
                wb[r][i].LoserToSlot = 0;
            }
        }

        // ---- Grand final ----
        var wbFinal = wb[^1][0];
        var lbFinal = lb[^1][0];
        var grandFinal = new DeathrollMatch { Round = lbRoundNum, Position = 0, State = MatchState.Pending, IsLosersBracket = false, BestOf = 3 };
        wbFinal.WinnerTo = grandFinal.Id; wbFinal.WinnerToSlot = 0;
        lbFinal.WinnerTo = grandFinal.Id; lbFinal.WinnerToSlot = 1;

        foreach (var list in wb) Matches.AddRange(list);
        foreach (var list in lb) Matches.AddRange(list);
        Matches.Add(grandFinal);

        foreach (var m in Matches)
            ResolveByesAndReadiness(m);
        SweepByes();

        Config.Save();
    }

    // If a match has exactly one player (a bye), auto-advance them. Otherwise set
    // Ready when both slots are filled.
    private void ResolveByesAndReadiness(DeathrollMatch m)
    {
        if (m.State == MatchState.Done) return;

        var hasA = m.PlayerA != null;
        var hasB = m.PlayerB != null;

        if (hasA != hasB)
        {
            // One slot filled. It's a bye if the empty slot can never be filled:
            // round 1 (no feeders), or every match feeding this one is already
            // Done (so no further player can arrive).
            var feedersDone = m.Round == 1 || Matches
                .Where(x => x.Id != m.Id && (x.WinnerTo == m.Id || x.LoserTo == m.Id))
                .All(x => x.State == MatchState.Done);
            if (feedersDone)
            {
                var winner = m.PlayerA ?? m.PlayerB;
                m.Winner = winner;
                m.State = MatchState.Done;
                Advance(m);
                return;
            }
        }

        if (hasA && hasB)
            m.State = MatchState.Ready;
    }

    // Push a finished match's winner into its next match slot, then refresh that
    // match's readiness (and cascade byes if needed). In double elimination,
    // also drop the loser into their losers-bracket slot.
    private void Advance(DeathrollMatch m)
    {
        // Winner advances.
        if (m.WinnerTo != null && m.Winner != null)
        {
            var next = Matches.FirstOrDefault(x => x.Id == m.WinnerTo);
            if (next != null)
            {
                if (m.WinnerToSlot == 0) next.PlayerA = m.Winner;
                else next.PlayerB = m.Winner;
                ResolveByesAndReadiness(next);
            }
        }

        // Loser drops (double elimination only).
        if (m.LoserTo != null && m.Loser != null)
        {
            var drop = Matches.FirstOrDefault(x => x.Id == m.LoserTo);
            if (drop != null)
            {
                if (m.LoserToSlot == 0) drop.PlayerA = m.Loser;
                else drop.PlayerB = m.Loser;
                ResolveByesAndReadiness(drop);
            }
        }

        // Sweep: a feeder completing can turn a waiting one-player match into a
        // bye (e.g. the feeder was itself a bye and sent no loser). Re-evaluate
        // pending matches until nothing changes.
        SweepByes();
    }

    private bool sweeping;

    private void SweepByes()
    {
        if (sweeping) return; // re-entrancy guard: Advance can be called from within the sweep
        sweeping = true;
        try
        {
            for (var guard = 0; guard < 64; guard++)
            {
                var changed = false;
                foreach (var x in Matches.Where(x => x.State == MatchState.Pending).ToList())
                {
                    var before = x.State;
                    ResolveByesAndReadiness(x);
                    if (x.State != before) changed = true;
                }
                if (!changed) return;
            }
        }
        finally
        {
            sweeping = false;
        }
    }

    // ---- Running matches ----

    public DeathrollMatch? GetMatch(Guid? id) =>
        id == null ? null : Matches.FirstOrDefault(m => m.Id == id);

    public DeathrollMatch? ActiveMatch => GetMatch(ActiveMatchId);

    public DeathrollMatch? Champion()
    {
        // The final match is the highest round with a single match; champion is its winner.
        var final = Matches.OrderByDescending(m => m.Round).FirstOrDefault();
        return final is { State: MatchState.Done } ? final : null;
    }

    public DeathrollPlayer? ChampionPlayer()
    {
        var f = Champion();
        return f?.Winner != null ? GetPlayer(f.Winner) : null;
    }

    // Start refereeing a match. Begins with the roll-off (who goes first):
    // both players /random the roll-off value, highest goes first, ties re-roll.
    public void StartMatch(DeathrollMatch m)
    {
        if (m.State != MatchState.Ready && m.State != MatchState.InProgress) return;
        m.Rolls.Clear();
        m.CurrentCeiling = 0;
        m.ExpectedRoller = null;
        m.InRolloff = true;
        m.RolloffValueA = -1;
        m.RolloffValueB = -1;
        m.WinsA = 0;
        m.WinsB = 0;
        m.CurrentGame = 1;
        m.State = MatchState.InProgress;
        ActiveMatchId = m.Id;
        Config.Save();
    }

    public void StopMatch()
    {
        ActiveMatchId = null;
        Config.Save();
    }

    // The "out of" value a roll-off roll should show. 0 in config means plain
    // /random (which the game reports as out of 999).
    // A plain /random has no explicit ceiling; FFXIV/deathroll convention treats
    // it as out of 1000 (confirmed against the working DeathRoll plugin).
    private const int PlainRandomOutOf = 1000;
    private int RolloffExpectedOutOf => RolloffValue <= 0 ? PlainRandomOutOf : RolloffValue;

    // Called by the hook for every /random while a match is active. Returns true
    // if the roll belonged to (was consumed by) the active match.
    public bool OnRoll(string fullName, int roll, int outOf)
    {
        var m = ActiveMatch;
        if (m is not { State: MatchState.InProgress }) return false;

        var a = GetPlayer(m.PlayerA);
        var b = GetPlayer(m.PlayerB);
        if (a == null || b == null) return false;

        var roller = MatchPlayer(fullName, a, b);
        if (roller == null) return false; // not one of the two competitors

        var normalizedOut = outOf <= 0 ? PlainRandomOutOf : outOf;

        if (m.InRolloff)
            return HandleRolloffRoll(m, a, b, roller, roll, normalizedOut);

        return HandleDeathrollRoll(m, a, b, roller, roll, normalizedOut);
    }

    // Roll-off: each player rolls once at the configured range. Reject wrong
    // ranges. When both are in, higher goes first; tie -> clear and re-roll.
    private bool HandleRolloffRoll(DeathrollMatch m, DeathrollPlayer a, DeathrollPlayer b, DeathrollPlayer roller, int roll, int outOf)
    {
        // Must roll at the roll-off range.
        if (outOf != RolloffExpectedOutOf)
        {
            Log(m, roller, roll, outOf, $"roll-off must be /random {(RolloffValue <= 0 ? "" : RolloffValue.ToString())}".TrimEnd());
            Config.Save();
            return true;
        }

        var isA = roller.Id == a.Id;
        // Ignore a second roll from the same player until the other has gone.
        if (isA && m.RolloffValueA >= 0) { Log(m, roller, roll, outOf, "already rolled the roll-off"); Config.Save(); return true; }
        if (!isA && m.RolloffValueB >= 0) { Log(m, roller, roll, outOf, "already rolled the roll-off"); Config.Save(); return true; }

        if (isA) m.RolloffValueA = roll; else m.RolloffValueB = roll;
        Log(m, roller, roll, outOf, null); // accepted roll-off roll

        if (m.RolloffValueA >= 0 && m.RolloffValueB >= 0)
        {
            if (m.RolloffValueA == m.RolloffValueB)
            {
                // Tie -> re-roll: clear both and keep waiting.
                m.RolloffValueA = -1;
                m.RolloffValueB = -1;
                Log(m, roller, roll, outOf, "TIE \u2014 both re-roll the roll-off");
            }
            else
            {
                // Higher goes first; that player opens the deathroll.
                var firstIsA = m.RolloffValueA > m.RolloffValueB;
                m.ExpectedRoller = firstIsA ? a.Id : b.Id;
                m.InRolloff = false;
                m.CurrentCeiling = 0; // opener is a plain /random
            }
        }

        Config.Save();
        return true;
    }

    // Deathroll: strict turn order and strict range. Opener must be plain
    // /random (out of 999); each subsequent roll must be /random [previous].
    private bool HandleDeathrollRoll(DeathrollMatch m, DeathrollPlayer a, DeathrollPlayer b, DeathrollPlayer roller, int roll, int outOf)
    {
        // Turn order.
        if (m.ExpectedRoller != null && roller.Id != m.ExpectedRoller)
        {
            Log(m, roller, roll, outOf, "not their turn");
            Config.Save();
            return true;
        }

        // Range: opener must be 999 (plain /random); otherwise must match the ceiling.
        var expectedOut = m.CurrentCeiling == 0 ? PlainRandomOutOf : m.CurrentCeiling;
        if (outOf != expectedOut)
        {
            Log(m, roller, roll, outOf, m.CurrentCeiling == 0
                ? "opener must be a plain /random"
                : $"must be /random {m.CurrentCeiling}");
            Config.Save();
            return true;
        }

        // Accepted roll.
        Log(m, roller, roll, outOf, null);

        if (roll == 0)
            GameWon(m, gameWinner: roller, gameLoser: Other(roller, a, b), a, b);
        else if (roll == 1)
            GameWon(m, gameWinner: Other(roller, a, b), gameLoser: roller, a, b);
        else
        {
            m.CurrentCeiling = roll;
            m.ExpectedRoller = Other(roller, a, b).Id; // pass the turn
        }

        Config.Save();
        return true;
    }

    // A single game finished. For best-of-1 this ends the match. For best-of-N
    // it records a game win and either clinches the match or starts the next
    // game with a fresh roll-off.
    private void GameWon(DeathrollMatch m, DeathrollPlayer gameWinner, DeathrollPlayer gameLoser, DeathrollPlayer a, DeathrollPlayer b)
    {
        if (gameWinner.Id == a.Id) m.WinsA++; else m.WinsB++;

        var needed = m.BestOf / 2 + 1; // 1 for Bo1, 2 for Bo3
        if (m.WinsA >= needed || m.WinsB >= needed)
        {
            var matchWinner = m.WinsA >= needed ? a : b;
            var matchLoser = m.WinsA >= needed ? b : a;
            FinishMatch(m, matchWinner, matchLoser);
            return;
        }

        // Not clinched: start the next game with a new roll-off.
        m.CurrentGame++;
        m.InRolloff = true;
        m.RolloffValueA = -1;
        m.RolloffValueB = -1;
        m.CurrentCeiling = 0;
        m.ExpectedRoller = null;
        // Keep the roll log; a divider is implied by the game counter in the UI.
    }

    private static void Log(DeathrollMatch m, DeathrollPlayer roller, int roll, int outOf, string? rejectReason)
    {
        m.Rolls.Add(new DeathrollRollRecord
        {
            PlayerId = roller.Id,
            PlayerName = roller.NameOnly,
            Roll = roll,
            OutOf = outOf,
            When = DateTime.Now,
            Rejected = rejectReason != null,
            RejectReason = rejectReason ?? string.Empty,
        });
    }

    private void FinishMatch(DeathrollPlayer winner, DeathrollPlayer loser, DeathrollMatch m, string scoresCsv)
    {
        m.Winner = winner.Id;
        m.Loser = loser.Id;
        m.State = MatchState.Done;
        m.ReportedScore = scoresCsv;
        ActiveMatchId = null;
        Advance(m);
    }

    private void FinishMatch(DeathrollMatch m, DeathrollPlayer winner, DeathrollPlayer loser)
    {
        // Build a winner-perspective score string from game wins (e.g. "2-1"),
        // defaulting to 1-0 for best-of-1.
        var wWins = winner.Id == GetPlayer(m.PlayerA)?.Id ? m.WinsA : m.WinsB;
        var lWins = winner.Id == GetPlayer(m.PlayerA)?.Id ? m.WinsB : m.WinsA;
        if (m.BestOf <= 1) { wWins = 1; lWins = 0; }
        FinishMatch(winner, loser, m, $"{wWins}-{lWins}");
    }

    // Manual override: let the host set the winner directly (e.g. a misroll or a
    // forfeit) without relying on auto-detection.
    public void SetWinnerManually(DeathrollMatch m, DeathrollPlayer winner)
    {
        var a = GetPlayer(m.PlayerA);
        var b = GetPlayer(m.PlayerB);
        if (a == null || b == null) return;
        var loser = Other(winner, a, b);
        FinishMatch(m, winner, loser);
        Config.Save();
    }

    private static DeathrollPlayer? MatchPlayer(string fullName, DeathrollPlayer a, DeathrollPlayer b)
    {
        var bare = StripWorld(fullName);
        if (StripWorld(a.FullName).Equals(bare, StringComparison.OrdinalIgnoreCase)) return a;
        if (StripWorld(b.FullName).Equals(bare, StringComparison.OrdinalIgnoreCase)) return b;
        return null;
    }

    private static DeathrollPlayer Other(DeathrollPlayer p, DeathrollPlayer a, DeathrollPlayer b) =>
        p.Id == a.Id ? b : a;

    private static string StripWorld(string full)
    {
        var idx = full.IndexOf('\uE05D');
        return idx < 0 ? full : full[..idx];
    }

    public int TotalRounds => Matches.Count == 0 ? 0 : Matches.Max(m => m.Round);
    public IEnumerable<DeathrollMatch> RoundMatches(int round) =>
        Matches.Where(m => m.Round == round).OrderBy(m => m.Position);
}

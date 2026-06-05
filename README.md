# Venue Helper

An all-in-one Dalamud plugin for FFXIV venue hosts, combining four tools into one tabbed window. Open it with `/venuehelper` (aliases `/vhelp`, `/vh`).

Built from parts of the reference plugins provided:
- **Venue Counter** reuses the object-table scanning approach from **Venue Manager** (the `IPlayerCharacter` / `SubKind == 4` player filter).
- **Raffle Helper** reuses **Elementalist**'s `TradeWatcher` (trade-completion detection via the `TradeOpen` condition flag plus `TradeNumberArray`/`TradeStringArray`).
- **Auction Helper** reuses **Carnival Eorzea**'s target-a-player + note + history + export pattern.
- **Giveaway Helper** reuses **Elementalist**'s `/random` + `/dice` log hook to capture rolls.

---

## Exports (all tabs)

Each tab has an **Export** button in the top-right corner. It opens a small popup with the **format** (TXT by default, plus CSV, PDF, XLSX), the **destination folder** (paste any full path; blank = default plugin folder, remembered between sessions), and a **Save**/**Copy** row for each dataset that tab can export.

- TXT/CSV are plain text (CSV includes a UTF-8 BOM so Excel opens it cleanly).
- XLSX is a real spreadsheet (via ClosedXML), with numeric cells written as numbers.
- PDF is a formatted table (via QuestPDF, Community license).

## Tab 1 — Venue Counter

Two counters share a single per-second scan of everyone rendered around you, so both can run at once. FFXIV only renders ~99 players at a time, so a single snapshot undercounts a packed venue; walking the room with a counter running catches everyone as they stream into render range, keyed by `Name@World` so nobody is double-counted.

- **Temporary Counter** — Start, walk one lap, Stop to freeze a clean headcount.
- **All-Night Counter** — a running tally of unique visitors for the whole night, persisted across relogs. Pause/Resume continues the same total; Reset starts fresh.
- Export the full unique-visitor list (Name, World) in any format.

## Tab 2 — Raffle Helper

Tracks raffle participants by name and world, with automatic trade detection.

- Set the **ticket cost** (gil per ticket); ticket counts are computed from gil paid / cost, with +/- nudges per player.
- **Auto-credit incoming trades** adds received gil to a player's buy-in (toggleable); a manual *Add / Credit Gil* field is the fallback.
- **Add Targeted Player** or type `Name@World`.
- **Assign 1-999** sequentially or **shuffled**, capped at 999 total.
- Two export rows: the **ticket list** (one line per ticket, ideal for https://wheelofnames.com/ ) and the **summary** (one row per player). Both support TXT/CSV/PDF/XLSX.

## Tab 3 — Auction Helper

For auctioning players off (gpose, art, etc.).

- **Add Targeted Player** (or by name); set the **note** and the **Won By** (winning bidder) inline in the table.
- Type the **final sale price**, then hit **Sold** to move it into history.
- The **Won By** field records who won the item, e.g. "Person X won for XYZ gil"; it shows in history and exports.
- **House cut %** (whole numbers) is captured per-sale at finalize time.
- **History & Totals** shows each sale's price, cut %, house cut, and seller payout, plus running totals (total gil through the house, house made, paid out). Export in any format.
- **Reset Active List** and **Clear History** both confirm with an "Are you sure?" prompt.

## Tab 4 — Giveaway Helper

Start a round; only each player's **first** `/random` (or `/dice`) after Start counts toward winners. Every roll is also shown in a verification feed.

- Pick one or more winner modes: **Highest**, **Lowest**, and/or **Closest to** a target number (multi-select — e.g. show highest *and* lowest for a two-prize giveaway).
- Or switch on **Roll until someone hits a number (race)**: every roll counts and the first person to roll the exact target wins (auto-stops on the hit).
- Start / Stop / Reset (Reset confirms first).
- Winner banner(s) update live; the counted-rolls table highlights winners in gold, and the feed marks later rolls as "(later roll)" so you can verify.
- Export the counted rolls in any format.

---

## Crash protection

Everything is saved to disk as you go, so a game crash or relog loses nothing. The all-night visitor count, raffle entries and buy-ins, the active auction list and history, and an in-progress giveaway (its rolls, feed, running state, and mode selection) all persist and come back exactly as they were. Lists are only emptied when you explicitly clear/reset them (each destructive reset asks "Are you sure?" first).

## Building

Standard Dalamud plugin (`Dalamud.NET.Sdk/15.0.0`, `net10.0-windows`). With XIVLauncher/Dalamud installed, open `VenueHelper.sln` or run `dotnet build -c Release`. NuGet restore pulls **ClosedXML** (XLSX) and **QuestPDF** (PDF). The output `VenueHelper.dll` loads as a dev plugin.

---

## Credits & attribution

Venue Helper is built on patterns and code adapted from several open-source FFXIV Dalamud plugins. Huge thanks to their authors:

- **Venue Manager** — object-table player-scanning approach used by the Venue Counter. (AGPL-3.0)
- **Elementalist** — the trade-detection `TradeWatcher` (raffle buy-ins) and the `/random` / `/dice` log hook (Giveaway Helper).
- **Carnival Eorzea** — the target-a-player + note + history + export pattern (Auction Helper).
- **Lalakuza Dice** — additional roll-handling reference.

If you are one of these authors and have concerns about attribution or licensing, please open an issue.

## License

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**, in keeping with the license of Venue Manager, from which it adapts code. See [LICENSE](LICENSE) for the full text.

In short: you're free to use, modify, and redistribute this software, but derivative works must also be AGPL-3.0 and make their source available.

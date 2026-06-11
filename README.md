# Venue Helper

An all-in-one Dalamud plugin for FFXIV venue hosts, combining attendance tracking, raffles, auctions, giveaways, deathroll tournaments, bar games, restaurant menus, and quick announcements into one tabbed window.

Open it with `/venuehelper` (aliases `/vhelp`, `/vh`).

---

## Installation

In-game, open `/xlsettings` → **Experimental** → **Custom Plugin Repositories**, add this URL, and click the **+** then the **save** (floppy) icon:

```
https://raw.githubusercontent.com/dexcss/VenueHelper/main/repo.json
```

Then open `/xlplugins`, search for **Venue Helper**, and install.

---

## Exports

Most tabs have an **Export** button in the top-right corner. It opens a popup with the **format** (TXT, CSV, PDF, or XLSX), a **destination folder** (paste any full path; blank = default plugin folder, remembered between sessions), and **Save**/**Copy** options for each dataset.

- TXT/CSV are plain text (CSV includes a UTF-8 BOM so Excel opens it cleanly).
- XLSX is a real spreadsheet, with numeric cells written as numbers.
- PDF is a formatted table.

---

## Venue Counter

Counts unique visitors. FFXIV only renders ~99 characters at once, so a single snapshot undercounts a packed venue; walking the room with a counter running catches everyone as they stream into render range, keyed by `Name@World` so nobody is double-counted.

- **Temporary (lap) counter** — Start, walk one lap, Stop for a clean headcount.
- **All-night counter** — a running tally of unique visitors for the whole night, persisted across relogs.
- **Multiple venues** — run more than one venue? Add a venue from the dropdown and each keeps its own visitor list and records, switchable at any time.
- **Time in venue (lifetime)** — while the all-night counter runs, it accumulates the total time each visitor has spent in your venue across nights (e.g. *Akari Zanto — 21H 31M*). Click any name for a per-visit breakdown showing when they arrived and left.
- Export the unique-visitor list in any format.

## Raffle Helper

Tracks raffle participants by name and world.

- **Enter ticket counts** directly per player, with +/- adjustments.
- **Free / comp tickets** — give a player tickets that enter the draw but don't add to the pot.
- **Player notes** (e.g. Discord names) — kept in the summary export, never in the public wheel list.
- **House cut %** for split raffles (e.g. 80/20), with a live pot/house/winner summary.
- **Auto-assign numbers 0-999** (sequential or shuffled). If the draw exceeds 1000 tickets, numbers are left blank so you can use an external wheel.
- **Import a name list** from your clipboard (comma or newline separated).
- **Auto-credit trades** — incoming trade gil can be converted to tickets automatically (toggleable).
- Two exports: the **ticket list** (one name per ticket, ideal for [wheelofnames.com](https://wheelofnames.com/), name-only so nothing private leaks) and a **summary** (one row per player, with notes).

## Auction Helper

For auctioning items, gpose, art, mounts, and more.

- **Add a player** by target or name; set the **note** and **Won By** inline.
- **Sold to target** — one click fills the winner with your current target.
- **Record sales**, including negative **"sold to the house"** sales (enter a negative price; no house cut is taken).
- **House cut %** captured per sale.
- **History with date filter** — filter the sale history by date range so you never have to clear it, and export just a window instead of everything.
- **Buyer tracking** — track a buyer across their alts by listing their alias names, then see their total spend across all of them.
- **Import a name list** from your clipboard.

## Giveaway Helper

Start a round; each player's **first** `/random` (or `/dice`) counts.

- Pick one or more winner modes: **Highest**, **Lowest**, and/or **Closest to** a target.
- Or use **exact-match (race)** mode: every roll counts and the first to hit the target wins.
- Every roll is shown for verification; winners are highlighted.
- **Anti-cheat:** only a plain `/random` counts by default — a `/random N` (which would let someone shrink the range) is rejected and flagged in the feed. `/dice` is rejected too unless you enable it in Settings.

## DR Tourny Helper

Run a deathroll tournament bracket in **single or double elimination**.

- Pick single elimination (lose once, you're out) or double elimination (lose twice — a winners bracket, a losers bracket, and a grand final).
- Randomized seeding with byes.
- Automatic roll detection (open with `/random`, roll down each turn; roll a 1 to lose, a 0 to win instantly).
- **Roll-offs** to decide who goes first, strict turn/range validation with rejected-roll logging, and **best-of-3 finals**.
- Manual winner override for edge cases.

## Shout/Yell Helper

Pre-write announcements (start with three, add or remove as many as you like), each with its own channel (Say / Yell / Shout / Party), and fire any of them with one click. Great for repeating venue info, rules, and event calls.

## Bar Game Helper

Build your own roll games and run them live.

- **Configure** the roll (`/random`, `/random N`, or `/dice N`), the win condition, an entry cost, an optional stacking pot, and the prize (a fixed gil amount or a percentage of the pot).
- **Win conditions:** specific number(s), a range, highest, lowest, closest-to, or **survival** — a streak of successes in a row.
- **Survival games** come in three flavours: roll the same number each time, roll higher/lower than a set number, or call higher/lower against your previous roll. Payouts can be **fixed** (reach a streak, win the pot), **tiered** (bank gil for each success past a threshold), or **high score** (longest streak wins the pot). Each player's run shows live, with the leader highlighted.
- **Capture live** — start capturing and the plugin reads trades as buy-ins (one buy-in per entry cost) and reads players' rolls, flagging winners automatically. Add buy-ins manually (paid or free) by target or name, and manually enter a roll for someone who rolled early.
- **Announce** the rules to any chat channel in one click, with the prize shown as the actual gil amount.

## Menu Helper

Run a venue menu like a restaurant.

- **Menu profiles** — keep a separate menu per venue and switch between them; each has its own items, macros, and the night's sales.
- **Items** have a name, price, and a **serve sequence** — a list of emotes/commands, each with its own wait, performed in order when you serve the item (just like a macro's `<wait.N>` lines). Plain text becomes an `/emote`; anything starting with `/` (e.g. `/handover`, `/micon`, `/trade`) is sent as-is.
- **Serving** records the sale (optionally to a named guest) and fires the sequence for you. The "Tonight's Till" banner tracks revenue and orders for the active menu, with an order log and export.
- **Additional Macros** — reusable macro buttons (same multi-step sequences, no price) for adverts, menu hand-overs, and anything else you'd normally keep as a game macro.

---

## Settings

A dedicated tab for global preferences and safety:

- **Default export folder** — set it once and every tab's export starts there.
- **Panic switch** — one toggle that halts all chat-sending and trade-watching, for when something misbehaves mid-event. Fully reversible.
- **Default step delay** — baseline pacing for serve sequences and macros.
- **Rules** — allow `/dice` in giveaways, auto-credit raffle trades, and require confirmation before destructive actions.
- **Backup & Restore** — save everything (venues, menus, games, raffles, auctions, history, settings) to a JSON file you keep, and restore it later.

## Persistence

Everything is saved to disk as you go, so a crash or relog loses nothing — visitor counts, time tracking, raffle entries, auction lists and history, bar games, presets, and in-progress events all persist. Lists are only emptied when you explicitly clear/reset them, and destructive resets ask for confirmation first.

## Building

Standard Dalamud plugin (`Dalamud.NET.Sdk/15.0.0`, `net10.0-windows`, API level 15). With XIVLauncher/Dalamud installed, run `dotnet build -c Release`. The output `VenueHelper.dll` loads as a dev plugin.

## License

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**. See [LICENSE](LICENSE) for the full text.

In short: you're free to use, modify, and redistribute this software, but derivative works must also be AGPL-3.0 and make their source available.

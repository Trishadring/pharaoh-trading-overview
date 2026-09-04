# Trading Overview Requirements

## Purpose

Add live trade information to each good in the Commerce Overseer without changing gameplay or replacing any stock UI values or controls.

## Display

The stock Stored value remains unchanged. The mod adds:

```text
Stored        Status        Input    Trade Volume     Buyers pay   Sellers receive
                                      Ex 600 / 4K
                                      In 300 / 2.5K
```

- Exported is the amount the player sold during the current game year.
- Imported is the amount the player bought during the current game year.
- Each maximum is the combined annual capacity of open, currently active routes for that direction.
- Closed and unopened routes do not contribute to capacity.
- Completed values reset through the game's normal yearly update.
- A trade direction is blank when no open, active route supports it.
- A good with no applicable import or export route has both trade cells blank.
- Values of 1,000 or more use compact notation with at most one decimal, such as `1K` or `1.5K`.

## Behavior

- Refresh values whenever the Commerce Overseer refreshes.
- Do not modify, remove, or reorder the game's existing information or controls.
- Do not modify trade behavior, prices, limits, or balance.
- Reuse the stock row's font and visual styling.
- Display all Commerce column headers at 16 px.
- Work independently and alongside the Bug Fixes and Enhancements Package.
- If data or UI access fails, leave the base UI operational and log a diagnostic warning.

## In-Game Checklist

- [ ] Commerce Overseer opens without errors.
- [ ] Existing Stored numbers and controls are unchanged.
- [ ] Exported and Imported values appear on every good row.
- [ ] Completed values match trade transactions in the current year.
- [ ] Maximums equal the capacities of open, active routes only.
- [ ] Opening a route adds its capacity on the next refresh.
- [ ] A scripted trade shutdown removes its capacity on the next refresh.
- [ ] The game resets completed values at the beginning of a year.
- [ ] Individual goods, such as chariots, use the same unit scaling as Stored.
- [ ] The mod loads with and without `PANEOverseerFixes.dll`.

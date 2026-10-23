# SLICING_README.md — how to slice `battle_ui_sheet.png`

Instructions for Claude (or any tool/dev) cutting the sprite sheet into individual assets.

## Ground rules

- Source file: `battle_ui_sheet.png`, **464 × 352 px**, RGB, pixel art at **1×**.
- Coordinates are `x, y, w, h` in source pixels, origin top-left. Every rect is the tight bounding box of the sprite, including its 1px dark outline.
- **Background is solid `#150D0B` (21,13,11), not transparent.** Before slicing, convert that exact colour to alpha 0 (flood-fill from each rect's corners, not a global replace, so dark pixels inside the art survive). Card and panel corners are deliberately knocked out to this background colour to make a rounded look.
- Never resample. Use nearest-neighbour everywhere. Import with filtering off, no mipmaps, no compression (or lossless only).
- Do not trim or re-pack with auto-trim tools that change rect sizes; the 9-slice margins below assume the sizes listed.
- Name output files exactly as in the "name" column so the UI setup doc (`UI_SETUP.md`) works unchanged.

## Palette (for reference / recolouring)

| role | hex |
|---|---|
| background | #150D0B |
| outline | #1A110D |
| dark / dark2 / brown | #281C15 / #3A2A1E / #543C28 |
| gold dark / gold / gold light | #926426 / #C8943C / #ECC064 |
| cream dark / cream / cream light | #BCA474 / #DECEA4 / #F2E6C6 |
| red dark / red / red light | #5C1610 / #8C2418 / #C44230 |
| orange (damage) / green (heal) | #E28432 / #58B040 |

## Slice table

### Cards

| name | x | y | w | h | notes |
|---|---|---|---|---|---|
| `card_hero` | 8 | 6 | 72 | 90 | Contains baked placeholder content (see "Baked placeholders") |
| `card_enemy` | 90 | 6 | 72 | 90 | Red-trimmed variant, crest on top edge |
| `spell_front` | 172 | 6 | 50 | 70 | Cost gem sits on top-left corner |
| `spell_back` | 230 | 6 | 50 | 70 | |
| `spell_selected` | 285 | 1 | 58 | 76 | Gold glow frame **plus** card; use for hovered/raised card |
| `spell_disabled` | 346 | 6 | 50 | 70 | Greyed out (can't afford / not usable) |

### Hit chance

| name | x | y | w | h |
|---|---|---|---|---|
| `badge_hit_neutral` | 402 | 8 | 56 | 16 |
| `badge_hit_high` | 402 | 28 | 56 | 16 |
| `badge_hit_low` | 402 | 48 | 56 | 16 |
| `reticle` | 414 | 80 | 32 | 32 |

Badges contain baked "50%", "90%", "10%" text (see below).

### Turn slider

| name | x | y | w | h | notes |
|---|---|---|---|---|---|
| `turn_tile_hero_a` | 8 | 120 | 22 | 22 | |
| `turn_tile_enemy_a` | 36 | 120 | 22 | 22 | red frame |
| `turn_tile_hero_b` | 64 | 120 | 22 | 22 | identical art to hero_a |
| `turn_tile_enemy_b` | 92 | 120 | 22 | 22 | identical art to enemy_a |
| `turn_current_hero` | 126 | 106 | 28 | 47 | arch + pointer arrow below it |
| `turn_current_enemy` | 162 | 106 | 28 | 47 | red variant |
| `pointer_hero` | 198 | 120 | 9 | 5 | small down arrow |
| `pointer_enemy` | 212 | 120 | 9 | 5 | |
| `slider_track` | 224 | 121 | 104 | 13 | includes diamond end caps; 9-slice, see below |
| `marker_filled` | 334 | 120 | 13 | 13 | |
| `marker_empty` | 348 | 120 | 13 | 13 | |

### Items and orbs

| name | x | y | w | h | notes |
|---|---|---|---|---|---|
| `slot_item_0` | 12 | 164 | 31 | 31 | shield + "Q" key box |
| `slot_item_1` | 50 | 164 | 31 | 31 | potion + "E" |
| `slot_item_2` | 88 | 164 | 31 | 31 | spark + "F" |
| `slot_item_3` | 126 | 164 | 31 | 31 | boot + "R" |
| `slot_empty` | 164 | 164 | 29 | 29 | no icon, no key box |
| `orb_small` | 204 | 168 | 20 | 20 | red orb with "9" |
| `orb_small_empty` | 230 | 168 | 20 | 20 | |
| `orb_large` | 257 | 165 | 26 | 26 | red orb, no text |
| `icon_heart` | 298 | 173 | 11 | 10 | |
| `icon_sword` | 314 | 173 | 10 | 10 | |
| `icon_shield` | 328 | 173 | 9 | 10 | |
| `icon_boot` | 341 | 173 | 8 | 9 | |
| `swatch_dark` | 358 | 168 | 21 | 21 | |
| `swatch_cream` | 384 | 168 | 21 | 21 | |
| `swatch_red` | 410 | 168 | 21 | 21 | |

Item slots: the key box (Q/E/F/R) is baked in. Treat the whole 31×31 as the sprite and swap only the icon layer if you need different items (draw your own icon centred at slot-local (14,14) on top of `slot_empty`).

### Damage log

| name | x | y | w | h |
|---|---|---|---|---|
| `log_hit` | 8 | 204 | 124 | 38 |
| `log_heal` | 8 | 248 | 124 | 38 |
| `log_miss` | 8 | 292 | 124 | 38 |

Same frame for all three; only the icon and number differ. For dynamic use, slice `log_hit` once and treat it as `log_frame` (see baked placeholders).

### Ribbons, buttons, frames

| name | x | y | w | h | notes |
|---|---|---|---|---|---|
| `ribbon_gold` | 144 | 204 | 64 | 16 | |
| `ribbon_red` | 212 | 204 | 64 | 16 | |
| `ribbon_brown` | 280 | 204 | 64 | 16 | |
| `button_normal` | 144 | 228 | 56 | 16 | 9-slice |
| `button_hover` | 204 | 228 | 56 | 16 | light gold frame |
| `button_pressed` | 264 | 229 | 56 | 16 | drawn 1px lower, same size |
| `button_disabled` | 324 | 228 | 56 | 16 | grey |
| `panel_rivet` | 144 | 254 | 40 | 40 | 9-slice |
| `panel_parchment` | 188 | 254 | 40 | 40 | 9-slice, noisy cream |
| `frame_dark` | 232 | 254 | 30 | 30 | 9-slice |
| `frame_wide` | 266 | 254 | 94 | 24 | 9-slice |
| `frame_small` | 364 | 254 | 24 | 24 | 9-slice |
| `divider` | 268 | 284 | 109 | 9 | horizontal ornament |

### Number font

Glyph size is 6×10 px with a 2px drop shadow; advance is 8px. Three colour rows (cream, orange, green), 10 digits each.

| name | x | y | w | h |
|---|---|---|---|---|
| `digits_cream` | 144 | 304 | 80 | 12 |
| `digits_orange` | 144 | 318 | 80 | 12 |
| `digits_green` | 144 | 332 | 80 | 12 |
| `text_miss` | 232 | 304 | 32 | 12 |
| `glyph_plus_green` | 232 | 318 | 8 | 12 |
| `glyph_x_red` | 244 | 318 | 8 | 12 |

Digit `n` in a row starts at `x = 144 + 8·n`, width 8 (6 glyph + 2 shadow/gap). Build the digits as a bitmap font: cell 8×12, advance 8, baseline at the bottom of the glyph.

## 9-slice margins

Use these insets (left, top, right, bottom) so corners and rivets stay intact:

| asset | margins |
|---|---|
| `button_*` | 5, 5, 5, 5 |
| `panel_rivet` | 9, 9, 9, 9 |
| `panel_parchment` | 3, 3, 3, 3 |
| `frame_dark` | 4, 4, 4, 4 |
| `frame_wide` | 5, 5, 5, 5 |
| `frame_small` | 4, 4, 4, 4 |
| `slider_track` | 8, 6, 8, 6 (stretch horizontally only) |
| `log_*` frame | 9, 9, 9, 9 |

Stretch mode: tile or stretch the centre. The centres are flat-ish, so either is fine. Keep corner pieces at integer scale.

## Baked placeholders (important)

These sprites have example content painted into them. If you want to drive them from game data, either redraw/blank those regions or put a covering layer on top.

| sprite | baked content | region (sprite-local) | what to do |
|---|---|---|---|
| `card_hero`, `card_enemy` | portrait silhouette, HP orb number, two stat numbers, sword and boot icons | portrait disc centre (36,34) r=21; orb centre (36,73) r=9; stats at (10,70) and right-aligned to x=62 | Ask for a "blank" variant, or overdraw with a portrait masked to the disc and re-render numbers on a replacement orb/plate |
| `spell_*` | spiral glyph, text lines, cost number | art window (9,20)–(40,45); cost gem centre (7,7) | Overdraw art window; replace cost digit |
| `badge_*` | "50%", "90%", "10%" and the slashed-circle icon | text starts at local x=20, y=3 | Use as-is for three fixed states, or request a blank badge |
| `log_*` | portrait, icon, number | portrait centre (17,19) r=10; icon at x=38; number at x=54–62 | Treat as frame and overdraw |
| `turn_tile_*`, `turn_current_*` | silhouette portrait | tile centre (11,11) r=7; arch centre (14,22) r=9 | Overdraw with unit portrait |

Because of this, the recommended workflow is: slice for layout and prototyping now, and ask for a **blank-frame variant of the sheet** (no portraits, numbers or text) before final art/data binding. Text and numbers should be drawn by the engine at runtime using `digits_*` or your own pixel font.

## Output checklist

1. All sprites exported as PNG with transparency, same pixel size as the table.
2. Nearest-neighbour filtering, no compression.
3. A JSON/atlas file listing each name with `{x, y, w, h}` and 9-slice margins where applicable.
4. Verify by compositing slices back onto `#150D0B` at their original coordinates; the result should be pixel-identical to the source sheet except for knocked-out background pixels.
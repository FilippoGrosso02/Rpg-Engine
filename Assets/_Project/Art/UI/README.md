# UI asset pack (pixel style)

Copy the `UI` folder to `Assets/_Project/Art/UI/`. The importer in `Editor/` sets Point filtering, no compression, Full Rect meshes, pixels-per-unit (3x scale) and the 9-slice borders automatically.

All art is drawn at 1x pixel size, so use Image Type **Sliced** for anything listed below and **Simple** (Preserve Aspect) for the rest. Keep the Canvas Scaler at 1920x1080.

## Folders
- `Core/` panels, buttons (5 states), cursor, list highlight, dividers, title banner, scrollbar, toggle, slider, tooltip
- `Bars/` frame + background + fills for HP, MP, break, XP, low-HP flash, enemy bar, CP pips
- `Battle/` portrait and turn-chip frames, turn marker, nameplate, status slot, loot slot, target ring/arrow, banner plates, level-up badge
- `Battle/Numbers/` damage digits (`dmg_normal_`, `dmg_crit_`, `dmg_heal_` 0-9), `dmg_heal_plus`, `dmg_miss`
- `Battle/Sprites/` test bat and rapier
- `Icons/Status|Element|Command|Stat/` 10x10 icons
- `Decor/` gargoyle, corner, stud, diamond, ring
- `Dice/` d6, d10 (ivory, red), d20

## Layering a bar
`bar_bg` (Sliced) -> fill (`bar_hp_fill`, Image Type Filled, Horizontal) -> `bar_frame` (Sliced, hollow in the middle) on top.
Enemy bar: `enemy_bar_bg`, `enemy_bar_fill`, `enemy_bar_frame`.

## Button states
Button component, Transition = Sprite Swap: Normal `btn_normal`, Highlighted `btn_highlighted`, Pressed `btn_pressed`, Selected `btn_selected`, Disabled `btn_disabled`.

## Not included
Banner text ("Victory", "Round 3") and item names use your TMP font on top of the `banner_plate_*` sprites. Portraits and item icons still need your own art.

## 9-slice borders (left, bottom, right, top)
| Sprite | L | B | R | T |
|---|---|---|---|---|
| `panel_frame` | 10 | 10 | 10 | 10 |
| `panel_light` | 6 | 6 | 6 | 6 |
| `panel_dark` | 6 | 6 | 6 | 6 |
| `tooltip_box` | 6 | 6 | 6 | 6 |
| `btn_normal` | 6 | 6 | 6 | 6 |
| `btn_highlighted` | 6 | 6 | 6 | 6 |
| `btn_pressed` | 6 | 6 | 6 | 6 |
| `btn_selected` | 6 | 6 | 6 | 6 |
| `btn_disabled` | 6 | 6 | 6 | 6 |
| `list_highlight` | 4 | 4 | 4 | 4 |
| `divider_h` | 8 | 0 | 8 | 0 |
| `divider_v` | 0 | 8 | 0 | 8 |
| `banner_title` | 14 | 6 | 14 | 6 |
| `banner_plate_victory` | 16 | 8 | 16 | 8 |
| `banner_plate_defeat` | 16 | 8 | 16 | 8 |
| `banner_plate_round` | 16 | 8 | 16 | 8 |
| `scroll_track` | 2 | 2 | 2 | 2 |
| `scroll_handle` | 2 | 2 | 2 | 2 |
| `slider_track` | 3 | 3 | 3 | 3 |
| `bar_frame` | 3 | 3 | 3 | 3 |
| `bar_bg` | 1 | 1 | 1 | 1 |
| `bar_hp_fill` | 1 | 1 | 1 | 1 |
| `bar_mp_fill` | 1 | 1 | 1 | 1 |
| `bar_break_fill` | 1 | 1 | 1 | 1 |
| `bar_xp_fill` | 1 | 1 | 1 | 1 |
| `bar_hp_low_fill` | 1 | 1 | 1 | 1 |
| `enemy_bar_frame` | 2 | 2 | 2 | 2 |
| `enemy_bar_bg` | 1 | 1 | 1 | 1 |
| `portrait_frame` | 5 | 5 | 5 | 5 |
| `turn_chip_frame` | 4 | 4 | 4 | 4 |
| `turn_chip_frame_current` | 4 | 4 | 4 | 4 |
| `turn_strip_bg` | 5 | 5 | 5 | 5 |
| `enemy_nameplate` | 4 | 4 | 4 | 4 |
| `status_slot_frame` | 3 | 3 | 3 | 3 |
| `loot_slot_frame` | 4 | 4 | 4 | 4 |

# AHUG — PeekVPN design tokens

AHUG is the PeekVPN visual contract. Every spacing, radius, type size, stroke, and color
in the Avalonia UI must come from a named token — never from a one-off number in a view.

The four layers:

| Layer | Meaning | Lives in |
| --- | --- | --- |
| **A**tomic | Raw scale values (`Space.Sm` = 8, `Radius.Md` = 6) | `Styles/Tokens.axaml` |
| **H**ierarchical | Semantic intent (`Brush.Background.Card`, `Brush.Accent.Primary`) | `Styles/Colors.axaml`, `Styles/Brushes.axaml` |
| **U**sage | Component recipes that only reference A + H | `Styles/Controls.axaml` |
| **G**uidelines | This file — when to use which token |

Do not add a new pixel value until it exists as a token here and in `Tokens.axaml`.

---

## Theme

PeekVPN uses a **warm cream** palette: paper backgrounds, honey gold accents, and
soft green when the tunnel is up. Default appearance is **Light**. Dark is the same
system with brown-void surfaces, not a neon inversion.

| Role | Light | Dark | Use |
| --- | --- | --- | --- |
| App canvas | `#F5F1EA` | `#181412` | Window and page background |
| Surface | `#FFFCF7` | `#211B18` | Recessed wells |
| Card | `#FFFFFF` | `#2B2420` | Panels |
| Primary text | `#21170F` | `#F7EEE5` | Copy |
| Gold | `#F2B928` / `#E8C15A` | `#E8C15A` | Primary CTA, brand |
| Success | `#299B61` | `#72C995` | Connected |
| Warning | `#E0A800` | `#F0C45E` | Paused |
| Danger | `#C83F36` | `#FF9A89` | Disconnected / error |

---

## Atomic — spacing

4px base grid. Named like a compact product scale, not a numeric ladder.

| Token | px | Use |
| --- | --- | --- |
| `Space.None` | 0 | Collapse a gap |
| `Space.Xxs` | 2 | Hairline stacks (label on value) |
| `Space.Xs` | 4 | Icon-to-label, tight lists |
| `Space.Sm` | 8 | Default inner gap, nav stack, card content |
| `Space.Md` | 12 | Card padding, section gaps |
| `Space.Lg` | 16 | Shell gutters, setting rows |
| `Space.Xl` | 20 | Title bar inset, page header offset |
| `Space.Xxl` | 24 | Page section stacks |
| `Space.Xxxl` | 32 | Settings nav-to-content |
| `Space.Huge` | 48 | Rare layout breathing room |

Insets (padding) alias the same scale: `Inset.Sm` = 8 on all sides.
Squish insets (`Inset.Md.Squish` = 16,10) are for buttons and search fields.

XAML:

```xml
<StackPanel Spacing="{StaticResource Space.Sm}">
<Border Padding="{StaticResource Inset.Md}">
```

---

## Atomic — radius

Geometry is **soft**. Cards use `Radius.Card` (22). CTAs are pills
(`Radius.Cta`). Prefer these semantic radii over the raw 4–8px steps.

| Token | px | Use |
| --- | --- | --- |
| `Radius.None` | 0 | Hairline rules |
| `Radius.Xs` | 2 | Micro marks, pause bars |
| `Radius.Sm` | 4 | Tight chips |
| `Radius.Md` | 6 | Compact wells |
| `Radius.Lg` | 8 | Ghost icons |
| `Radius.Xl` / `Radius.Nav` | 12 | Nav, logo, list items |
| `Radius.Xxl` | 16 | Shell chrome |
| `Radius.Search` | 18 | Search field |
| `Radius.Sidebar` | 20 | Nav rail |
| `Radius.Card` | 22 | Cards |
| `Radius.Cta` / `Radius.Full` | 9999 | Pills, dots, avatars |

```xml
<Border CornerRadius="{StaticResource Radius.Lg}">
```

---

## Atomic — type

| Token | px | Use |
| --- | --- | --- |
| `Type.Micro` | 10 | Latency, country code fallback |
| `Type.Caption` | 11 | Muted helper, map subtitle |
| `Type.Label` | 12 | Section labels, badges |
| `Type.Body` | 13 | Buttons, status |
| `Type.BodyLg` | 14 | Card titles, chrome |
| `Type.TitleSm` | 15 | Connection headline |
| `Type.Title` | 18 | Page section headers |
| `Type.TitleLg` | 22 | Profile name |
| `Type.Display` | 28 | Page titles |

Body face: Inter (`AppFontFamily`). Technical figures (ping, latency, IDs):
`MonoFontFamily` (Cascadia Mono / Consolas).

Classes: `PageTitle`, `SectionTitle`, `Caption`, `Mono`.

---

## Atomic — size & stroke

| Token | px | Use |
| --- | --- | --- |
| `Size.Dot` | 8 | Status LED |
| `Size.Icon.Sm` | 28 | Flag chip |
| `Size.Icon.Md` | 36 | Icon action |
| `Size.Icon.Lg` | 40 | Logo, connection flag |
| `Size.Icon.Xl` | 44 | Sidebar nav |
| `Size.TitleBar` | 60 | Window chrome |
| `Size.Sidebar` | 64 | Nav column |
| `Stroke.Hairline` | 1 | Card border |
| `Stroke.Accent` | 1.5 | Outline CTA, live flag ring |

---

## Hierarchical — color roles

Always consume **brushes** in XAML (`Brush.*`), not raw hex. `Color.*` exists so C#
(map markers) and documentation share the same names.

| Brush | Role |
| --- | --- |
| `Background.App` | Window canvas |
| `Background.Surface` | Recessed list wells |
| `Background.Card` | Panels |
| `Background.Sidebar` | Nav rail |
| `Background.NavActive` | Selected nav / row hover |
| `Background.Search` | Search field |
| `Background.ButtonDark` | Inverted banner |
| `Text.Primary` / `Secondary` / `Muted` | Copy hierarchy |
| `Text.OnAccent` | Text on cyan CTA |
| `Text.OnDark` | Text on inverted banner |
| `Accent.Primary` / `PrimaryHover` | Connect, brand mark |
| `Status.Success` / `Warning` / `Danger` / `Neutral` | Connection states |
| `Server.*` | Server row state fills and dashes |
| `Map.Land` / `Water` / `Marker` | World map |

---

## Usage — components

`Styles/Controls.axaml` is the only place component geometry is defined.

| Class | Tokens |
| --- | --- |
| `Button.PrimaryCta` | gold fill, `Radius.Cta`, `Inset.Lg.Squish` |
| `Button.OutlineCta` | card hairline, transparent fill, pill |
| `Button.NavIcon` | `Size.Icon.Xl`, `Radius.Nav` |
| `Border.Card` | `Radius.Card`, `Inset.Card` |
| `TextBox.SearchBox` | `Radius.Search`, `Inset.Search` |

Views should apply classes and layout tokens. They must not invent a 22px corner
or a 14px gap.

---

## Guidelines

1. **Warm cream.** Surfaces stay paper/beige; gold is punctuation, not the whole UI.
2. **Gold means action.** Use it for connect and brand — not for error.
3. **Green means tunneled.** Connected rows, map pulse, success badges.
4. **Soft over sharp.** New radii default to `Radius.Card` / `Radius.Cta` / `Radius.Nav`.
5. **4px grid.** If a value is not in the space scale, pick the nearest token instead
   of adding `7` or `18`.
6. **Theme through brushes.** Hardcoded hex in a view or Skia draw call is a bug;
   resolve `Color.*` / `Brush.*` instead.

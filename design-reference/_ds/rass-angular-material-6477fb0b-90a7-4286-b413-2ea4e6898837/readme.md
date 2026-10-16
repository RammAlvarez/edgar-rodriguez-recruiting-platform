# RASS Angular Material — design system

RASS builds back-office software: admin panels, CRMs and operational dashboards used all day by internal staff, plus occasional customer-facing and mobile screens. This design system is **Angular Material 3 (Material Design 3 as implemented by Angular Material v19+) with the prebuilt "Azure & Blue" theme** (`azure-blue`). Every design made with it must be implementable with stock Angular Material / CDK components and `mat.theme()` — no custom components unless Angular Material has no equivalent.

Brand expression comes from color, typography and shape tokens. We do not restyle components.

## Sources
- Brief supplied by the user (theme, density, components, a11y and naming rules) — the source of truth.
- Angular Material palettes, copied verbatim: `angular/components` → `src/material/core/theming/_palettes.scss` (`$azure-palette`, `$blue-palette`, error patch). https://github.com/angular/components/blob/main/src/material/core/theming/_palettes.scss
- Angular Material theming guide (system variables `--mat-sys-*`, `mat.theme`, `theme-overrides`): https://github.com/angular/components/blob/main/guides/theming.md · https://material.angular.dev/guide/system-variables
- Fonts: Roboto, Roboto Mono and Material Symbols Outlined from Google Fonts (self-hosted in `assets/fonts/`).
- **No logo, product code, Figma or screenshots were provided.** Wherever a mark would go we render "RASS" in Roboto. The UI kit is a reference composition, not a recreation of a real screen.

## Implementing in Angular
```scss
@use '@angular/material' as mat;
html {
  color-scheme: light dark;
  @include mat.theme((
    color: (primary: mat.$azure-palette, tertiary: mat.$blue-palette),
    typography: Roboto,
    density: -1,
  ));
}
body { background: var(--mat-sys-surface); color: var(--mat-sys-on-surface); }
.dense { @include mat.theme((density: -2)); }   // tables, filter bars, desktop toolbars
.comfortable { @include mat.theme((density: 0)); } // mobile-first / customer-facing
```
`tokens/colors.css` mirrors the exact `--mat-sys-*` values this produces (light-dark()), so HTML prototypes and the Angular app share one vocabulary.

---

## CONTENT FUNDAMENTALS
- **Language:** UI copy is Spanish or English depending on the product (sample content in this system is Spanish, Spain). Component, token, class and variant names are **always English** (`matButton="tonal"`, `--mat-sys-primary`).
- **Voice:** plain, operational, calm. Staff use these tools for hours; copy should state what is there and what will happen. No marketing tone, no exclamation marks, no jokes in errors.
- **Person:** address the user as *tú* implicitly via imperatives ("Guarda los cambios", "Introduce un email válido") or neutral infinitives on buttons ("Guardar", "Exportar"). Avoid "nosotros" except in system notices ("Usaremos este correo para avisos"). English: imperative verbs, second person ("Enter a valid email").
- **Casing:** sentence case everywhere — buttons, titles, tabs, menu items ("Nuevo cliente", "Datos fiscales"). Never ALL CAPS except the datepicker month label Angular renders.
- **Buttons:** verb (+ object): "Guardar", "Crear cliente", "Eliminar". Dialog actions: dismissive first ("Cancelar"), confirming last, named for the action, not "Aceptar".
- **Errors** (`mat-error`): say what's wrong and how to fix it in one line: "El NIF debe tener 9 caracteres". **Hints** (`mat-hint`): format or purpose: "Con prefijo internacional", "DD/MM/AAAA".
- **Confirmations:** question title + consequence body: "¿Eliminar cliente?" / "Se eliminará «Ferretería Ruiz S.L.» … no se puede deshacer."
- **Snackbars:** past-tense fact, ≤ 6 words, optional single action: "Cliente guardado · Deshacer".
- **Empty states:** what's missing + where it will appear: "Sin facturas este año. Las facturas emitidas aparecerán aquí."
- **Numbers & dates:** locale-formatted (es-ES: `84.320,00 €`, `07/10/2026`); right-aligned tabular figures in tables; IDs (NIF, IBAN, order no.) may use Roboto Mono.
- **Emoji:** never. **Unicode as icons:** never — use Material Symbols.

## VISUAL FOUNDATIONS
- **Overall vibe:** stock Material 3, cool and quiet. Neutral surfaces, azure primary used sparingly for the one main action and selection, density tuned for information-heavy screens.
- **Color:** M3 color roles only — never raw hex in designs or annotations. Primary `--mat-sys-primary` (azure 40 `#005CBB` light / azure 80 `#ABC7FF` dark) for filled buttons, focus, links, active tab, checked controls. Secondary-container for selection (nav item, filter chip, selected option, button toggle). Tertiary (blue) only for accents such as data-viz series. Error for validation and destructive actions. Light and dark come from the same roles via `color-scheme: light dark`.
- **Surfaces / elevation:** hierarchy is expressed by tone, not shadow: page `surface`, sidenav `surface-container-low`, menus/select panels `surface-container`, dialogs/datepicker `surface-container-high`, filled field/filled card `surface-container-highest`. Shadows (`--mat-sys-level1–3`) only on things that float: menus, dialogs, snackbars, FABs, raised cards, expansion panels.
- **Backgrounds:** flat solid surfaces. No images, gradients, textures, patterns or illustrations in app chrome. No full-bleed imagery.
- **Typography:** Roboto, M3 type scale (`--mat-sys-display-*` … `--mat-sys-label-small`). Default text is body-medium (14/20). Page titles headline-small (24/32); card titles title-large; table headers and tabs title-small; buttons/chips/nav label-large. Weights only 400 and 500.
- **Shape:** M3 corner tokens: 4px fields/menus/snackbar/tooltip, 8px chips, 12px cards and expansion panels, 16px FAB/datepicker/sidenav edge, 28px dialogs and bottom sheets, full for buttons, nav items, toggle groups, badges.
- **Borders:** 1px `outline` for field and outlined-button borders; 1px `outline-variant` for dividers, table rows, outlined cards. Focused field = 2px primary.
- **Cards:** `mat-card` raised (surface-container-low + level1), outlined (surface + outline-variant border, the RASS default inside pages), filled (surface-container-highest). 12px radius, 16px padding. No colored side borders.
- **Spacing:** 4px grid — 4, 8, 12, 16, 24, 32, 48. 24px page padding and gaps between major regions on desktop, 16px inside cards and between fields, 8px between buttons.
- **Density:** default -1 (buttons 36, fields 52, rows 48). -2 for data-heavy desktop views (tables, filters, toolbars). 0 for mobile-first / customer-facing screens. Touch targets ≥ 48×48 (40×40 allowed at -2 on desktop-only screens).
- **States (M3 state layers):** an overlay of the content color — hover 8%, focus 12% plus a 3px `--mat-sys-secondary` focus ring (strong focus indicators), pressed 12%, dragged 16%. Disabled = on-surface at 12% (container) / 38% (content). Selected uses secondary-container and a check / filled icon, never color alone. Error uses error color **and** an icon/message.
- **Hover:** state layer only; filled/tonal buttons also gain level1. No color shifts, no scaling.
- **Press:** state layer + Angular ripple. No shrink.
- **Motion:** M3 easing (`--rass-easing-standard`, emphasized decelerate for enter, accelerate for exit), 150–300ms for most UI; Angular's built-in component animations only. No bounces, no decorative animation. Respect reduced motion.
- **Transparency & blur:** only the 32% scrim behind dialogs, modal sidenav and bottom sheets. No glass/blur.
- **Imagery:** none in chrome. If product imagery appears (e.g. product photos in a catalogue), show it in a `mat-card-image` at 16:9 or 1:1, neutral and unfiltered.
- **Layout:** `mat-sidenav-container` shell: fixed sidenav (280px, side mode) ≥1280px; collapsed icon-only sidenav at Medium; modal (`over`) sidenav or bottom nav below 960px. `mat-toolbar` at top of content (surface; surface-container when scrolled). Content max-width 1440px. Page header = headline-small title + primary action on the right.
- **Breakpoints:** CDK `Breakpoints` — XSmall <600, Small 600–959, Medium 960–1279, Large 1280–1919, XLarge ≥1920.
- **Accessibility:** WCAG 2.1 AA contrast in both schemes (role pairs guarantee it — always pair `X` with `on-X`). Icon-only buttons need `aria-label` + `matTooltip`. Status uses icon + text.

## ICONOGRAPHY
- **System:** Material Symbols **Outlined**, the Angular Material default glyph set (`<mat-icon fontSet="material-symbols-outlined">`). Self-hosted variable font in `assets/fonts/MaterialSymbolsOutlined.woff2` (axes: FILL, wght, GRAD, opsz); class `.material-symbols-outlined` / `.rass-icon` in `tokens/base.css`.
- **Defaults:** weight 400, FILL 0, grade 0, optical size 24. **FILL 1 only for selected / active** (active nav item, selected tab icon, toggled icon button).
- **Sizes:** 24px default; 18px inside buttons, chips, table cells and sort headers; 36px for large FAB and empty states.
- **Color:** on-surface-variant for standalone icons; inherits the content color inside buttons/chips; primary or error only when conveying state, always alongside text.
- **No** custom SVG icons, PNG icons, emoji or unicode glyphs as icons. No logo or illustrations were provided; don't invent them.
- React prototypes use `<Icon name="search" />`; plain HTML uses `<span class="rass-icon">search</span>`.

---

## Index
- `styles.css` — entry point; `@import`s only.
- `tokens/` — `fonts.css`, `colors.css` (ref palettes + `--mat-sys-*` roles), `typography.css`, `shape.css`, `elevation.css`, `spacing.css` (grid + breakpoints), `density.css` (`[data-density]` 0/-1/-2), `motion.css` (easing, durations, state-layer opacities, focus ring), `base.css` (document defaults, icon class, state-layer helpers).
- `components/` — React recreations of Angular Material components, each with `.jsx`, `.d.ts`, `.prompt.md`, CSS and a card.
- `guidelines/` — foundation specimen cards (colors, type, shape, elevation, spacing, density, states, breakpoints, motion).
- `ui_kits/back-office/` — reference admin/CRM app (see its README).
- `assets/fonts/` — Roboto, Roboto Mono, Material Symbols Outlined (woff2).
- `thumbnail.html`, `SKILL.md`.

## Components
Each maps 1:1 to Angular Material; the Angular selector is in each `.d.ts`.
- **Actions** (`components/button`): Button (`matButton` filled / tonal / outlined / text / elevated), IconButton (`mat-icon-button`), Fab (`mat-fab`, `mat-mini-fab`, extended), ButtonToggleGroup (`mat-button-toggle-group`)
- **Forms** (`components/form-field`): FormField (`mat-form-field appearance="outline"` + `matInput`), Select (`mat-select`), Autocomplete (`matAutocomplete`), Datepicker (`mat-datepicker`, also exports Calendar)
- **Selection** (`components/selection`, `chips`, `slider`): Checkbox, Radio + RadioGroup, SlideToggle, Chip + ChipSet (assist / filter / input), Slider
- **Data** (`components/table`, `list`, `card`, `badge`, `expansion`): Table (`mat-table` + `matSort`), Paginator, List + ListItem, Card, Badge, ExpansionPanel + Accordion
- **Navigation** (`components/tabs`, `stepper`, `toolbar`, `sidenav`, `menu`): Tabs, Stepper, Toolbar, Sidenav + NavItem + NavSection, Menu
- **Feedback** (`components/overlay`, `progress`): Dialog, BottomSheet, Snackbar, Tooltip, ProgressBar + ProgressSpinner
- **Foundations** (`components/icon`): Icon, Divider

### Intentional additions
- **Icon** — wrapper for the Material Symbols font (`mat-icon` equivalent) so prototypes get correct FILL/opsz.
- **Sidenav `collapsed`** — icon-only "rail" pattern built on `mat-sidenav` with a narrow width; Angular Material has no navigation-rail component.
- **`state` prop** on interactive components — forces hover/focus/pressed for specimens and annotations; not a runtime API.
- **Button `color="error"`** — destructive actions; in Angular implement with a scoped `mat.theme-overrides` (primary → error roles), not a custom component.

## Caveats
- Component visuals are hand-built React/CSS approximations of Angular Material's M3 styles, matched to `--mat-sys-*` tokens; small pixel differences from the real MDC-based components are expected. The Angular implementation is always the reference.
- Some Angular component tokens (e.g. dialog container color, tab indicator height) were set from M3 spec knowledge, not read from Angular source.

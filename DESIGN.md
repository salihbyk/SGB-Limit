---
name: SGB Limit
description: Native Windows quota companion with Dynamic Island, taskbar and floating presentations.
colors:
  island-black: "#080A0D"
  island-stroke: "#2B2F34"
  island-hover: "#1E2226"
  charcoal: "#13181B"
  raised: "#1D2529"
  sage: "#A4DCBD"
  reading-white: "#F1F5FC"
  metadata-gray: "#AFBACF"
  quota-track: "#343E52"
  claude-peach: "#F0AE83"
  codex-mint: "#75DEC7"
  antigravity-blue: "#8CC2F0"
  cursor-violet: "#C096F5"
  grok-silver: "#D4D4D8"
  trae-green: "#5BE4AD"
  provider-cyan: "#6CC7E6"
  caution-amber: "#F5B03F"
  exhausted-red: "#FF6B5E"
typography:
  settings-title: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "27px", fontWeight: 600}
  quota-reading: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "20px", fontWeight: 600}
  panel-title: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "19px", fontWeight: 600}
  section-title: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "14px", fontWeight: 600}
  body: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "13px", fontWeight: 400}
  advice: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "11.5px", fontWeight: 400}
  gauge-reading: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "12px", fontWeight: 600}
  detail: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "10.5px", fontWeight: 400}
  floating-caption: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "8.5px", fontWeight: 400}
  island-reading: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "17px", fontWeight: 400}
  island-metadata: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "9.5px", fontWeight: 400}
  amount-detail: {fontFamily: "Segoe UI Variable Text, Segoe UI", fontSize: "10px", fontWeight: 400}
rounded:
  control: "6px"
  choice: "7px"
  tile: "8px"
  plan-badge: "4px"
  island-top: "0px 0px 14px 14px"
  island-bottom: "14px 14px 0px 0px"
  island-free: "18px"
  compact-panel: "10px"
  floating-circle: "107px"
spacing:
  compact: "4px"
  related: "8px"
  controls: "10px"
  detail-inset: "18px"
  section-gap: "20px"
  settings-inset: "22px"
components:
  dynamic-collapsed: {backgroundColor: "{colors.island-black}", textColor: "{colors.reading-white}", rounded: "{rounded.island-top}", padding: "8px 13px"}
  dynamic-expanded: {backgroundColor: "{colors.island-black}", textColor: "{colors.reading-white}", rounded: "{rounded.island-top}", padding: "13px 17px 14px"}
  floating-orb: {backgroundColor: "{colors.charcoal}", textColor: "{colors.reading-white}", rounded: "{rounded.floating-circle}", padding: "6px", width: "214px", height: "214px"}
  taskbar-identity: {textColor: "{colors.reading-white}"}
  account-row: {textColor: "{colors.reading-white}", rounded: "{rounded.tile}", height: "42px"}
  plan-badge: {backgroundColor: "{colors.raised}", textColor: "{colors.sage}", rounded: "{rounded.plan-badge}", padding: "1px 6px"}
  tab-selected: {backgroundColor: "{colors.raised}", textColor: "{colors.sage}", rounded: "{rounded.tile}", height: "36px"}
  choice-button: {backgroundColor: "{colors.raised}", textColor: "{colors.reading-white}", rounded: "{rounded.choice}", height: "34px"}
  save-button: {backgroundColor: "transparent", textColor: "{colors.sage}", rounded: "{rounded.control}", padding: "0px 10px"}
  icon-button: {backgroundColor: "transparent", textColor: "{colors.reading-white}", rounded: "{rounded.control}", size: "32px"}
---

# Design System: SGB Limit

## Overview

Version 2.3 settings use a compact 532-DIP sidebar and three view subtabs. Preview cards use fresh synthetic production renderings. Signal shows three/five ascending bars for quota availability; numeric mode remains independent. Dynamic Island's selected left/right edge has zero gap, including while expanded. Antigravity last-reading values use amber text and an explicit original timestamp. Existing dark/sage palette, Save semantics, keyboard focus and safe unavailable states continue.

**Creative North Star: "A floating quota companion"**

SGB Pulse is a small native Windows instrument. Each product keeps its own real quota; selecting an account reveals its limits, resets and conditional advice. The user specified the macOS-like horizontal black Dynamic Island and retained Windows taskbar and floating gauges as separate presentations. Native source and labeled captures establish this extension of the existing Operate system; no new-world concept roll or external concept seed is claimed.

Dynamic Island is the default presentation: provider marks and limits at rest, then full names, separate quota windows and optional resets on hover or keyboard focus. Windows taskbar defaults to Identity with marks, rings, visible names and limits; Rings, Ribbon, Dial and Flow remain alternatives. Floating gauges offer circular Halo, Orbit and Comet, with Orbit selected initially within that mode. All three lead to the same compact account panel with inline reported plan badges and preserve the established charcoal, sage and native text language.

**Key Characteristics:**
- A black expanding Dynamic Island, Windows taskbar instrument or draggable floating gauges.
- Charcoal panels, sage actions and provider-colored quota marks, lines and arcs.
- One selected account's details, explicit Save and conditional forecasts.
- Quiet idle states, optional hover motion and honest unavailable readings.

## Colors

The palette combines a near-black signature shell with the incumbent charcoal panel system; provider hues identify the source while warning colors describe actual pressure.

### Primary
Sage identifies the SGB grip, actions, selected settings choices and advice headings. It is the interaction accent, not a service identity.

### Secondary
Claude Peach, Codex Mint, Antigravity Blue, Cursor Violet, Grok Silver, TRAE Green and Provider Cyan identify products alongside their names or authored geometric marks. Reuse the existing `Palette.Hue` mapping. A colored mark identifies an application; it does not certify a paid plan or a readable live quota.

### Tertiary
Caution Amber and Exhausted Red indicate quota pressure. `Palette.Fill` uses amber at twenty percent or less remaining or hot pace; red at five percent or less remaining or exhausted pace. Compact account-row numbers additionally turn red at ninety-five percent used. Preserve the actual contextual thresholds.

### Neutral
Island Black carries Dynamic Island, Island Stroke traces its one-unit boundary and Island Hover supplies its shared hover/focus fill. Charcoal carries floating gauges and the dark detail panel. Raised separates floating-gauge hover and selected-tab surfaces. Reading White carries primary text; Metadata Gray carries context and unavailable readings. Quota Track shows an unfilled ring or limit line. Floating gauges and panels use the palette's translucent hairline.

**The Independent Products Rule.** A shared email never merges product quota, identity or forecast.

**The Truth Before Color Rule.** Stale, expired or absent current readings use an em dash in the island and compact list. Details explain the state; a missing value never becomes a convincing live zero.

Dynamic Island and floating gauges always use the dark palette. The saved theme preference applies to detail panels, menus and hover surfaces. The taskbar instrument follows the Windows system taskbar theme. Light-mode counterparts remain defined in `Theme.cs`; the dark frontmatter does not replace those contextual mappings.

## Typography

Segoe UI Variable Text with Segoe UI fallback supports native Windows text. Portable px tokens represent WPF device-independent units; Windows DPI controls physical scaling. There is no custom tracking or global imposed line height.

The panel title is quieter than the settings title. Compact row readings remain prominent without dashboard-sized numerals. Expanded Dynamic Island readings use the island-reading role; its provider names use normal eleven-and-a-half-unit text, window labels use ten units and reset captions use nine-and-a-half units. Collapsed readings use eleven units. Floating provider percentages use the gauge-reading role beneath twenty-one-unit marks; the central pulse grip has an eight-and-a-half-unit used/remaining caption. Dynamic Island uses the authored pulse mark, adding a thirteen-unit semibold SGB Pulse label when expanded. Compact plan badges use ten-unit normal text; taskbar Identity names use eleven-and-a-half-unit semibold text.

**The Captioned Number Rule.** Keep product identity and usage mode discoverable for every reading: collapsed Dynamic Island uses marks plus tooltips and automation names; its expansion and the detail panel expose names and quota labels. Numerical values never animate to simulate consumption.

## Layout

Dynamic Island measures its content rather than using a fixed overall width or height. Cache both natural dimensions before assigning either window dimension: WPF remeasures synchronously when width changes. Collapsed provider columns are forty-nine units wide with eight-unit trailing gaps; expanded columns are 102 units wide with twelve-unit gaps. The expanded header sits fourteen units above the limits caption. The 3/5/8-account setting is an upper bound: work-area width and Windows DPI can reduce the visible count, with a +N action opening the complete list. Small, standard and large scales are 0.85, 1 and 1.15; the renderer clamps to that range for both island modes.

All three floating styles use the same circular composition: a 200-unit square canvas, six-unit inset and one-unit boundary yield a 214-unit circle at standard scale. Up to six provider marks and percentages sit at equal angles on a fifty-nine-unit radius around the central pulse grip. Each provider owns a separate outer quota sector at an eighty-nine-unit radius, with a twelve-degree gap between sectors. Additional accounts use a +N action beneath the grip; the circle does not grow with account count. No connected accounts shows Hesap bul.

The compact panel is at most 410 units wide, clamped to work-area width minus twenty-four. Its content starts eighteen units from the sides and fourteen from the top. Account rows have the recorded fixed height and show their reported plan badge before expansion; one selected row expands details directly beneath it. Settings independently uses at most 520 units, with twenty-two-unit horizontal and twenty-unit top insets. Choice controls wrap.

Placement for Dynamic Island and floating gauges supports upper/lower left, center and right, plus free dragging. Both leave eighteen units at horizontal edges. Top-docked Dynamic Island touches the physical top edge at y=0; bottom docking touches the work-area bottom. Floating presets retain fourteen units above or eighteen below the work area. Saved free positions are clamped into the work area. Repositioning on size changes keeps the selected edge or center anchor stable during expansion. Taskbar mode uses available native taskbar space, shrinking names first, then showing all accounts as icons and percentages before using +N; if no item fits without covering Windows buttons, the widget hides and the tray remains.

**The Reveal One Rule.** Expand one account's detail and advice section at a time. Do not restore the superseded card grid or broad multi-quota desk as the default view.

**The Three Presentations Rule.** Dynamic Island, Windows taskbar and floating gauges are independent, supported choices. Do not make a style within one mode stand in for another mode.

## Elevation & Depth

Tonal surfaces and thin boundaries establish separation. Dynamic Island and floating gauges have no decorative shadow or glass effect. Floating-gauge hover and keyboard focus reveal Raised inside the rounded hit region; Dynamic Island uses its dedicated hover fill. Detail panels retain native Windows backdrop handling where supported and an opaque palette surface fallback; native window behavior is not a reusable CSS shadow token.

**The Quiet Idle Rule.** Dynamic Island animates shell width and height over 350 milliseconds with a finite critically damped spring and overlapping content fade/scale; departure starts a 240-millisecond collapse delay. Keyboard focus also expands it, focus within prevents delayed collapse and Escape collapses it. With reduced motion or motion disabled, the state changes without the size animation. Comet's decorative point travels only during hover: ten updates per second, three seconds per cycle. Its timer stops on leave, hide, refresh or close. The floating-gauge hover opacity transition lasts 180 milliseconds. Motion requires the saved preference and Windows animation permission; quota values stay unchanged and neither island runs continuous decorative motion at idle.

## Shapes

Dynamic Island is an edge-attached black shell. At the top, both states have square upper corners, fourteen-unit lower corners and a one-unit boundary on the sides and bottom only. Bottom docking mirrors those corners and omits the bottom boundary. Free placement uses eighteen-unit corners and a complete boundary. Halo, Orbit and Comet all use the floating-circle token and a circular outer quota track. Halo and Comet use a two-and-a-half-unit continuous stroke; Orbit uses rounded dots with dash pattern 0.1/2.1. The Comet point sits five units outside the sector radius.

Small control radii contrast with the instrument silhouettes. Account and indicator tiles use the tile token and a Reading White selected outline; the compact panel uses its own corner token. Rounded gauge hit regions follow the circular forms without introducing separate decorative cards. Provider marks are authored vector geometry: spark, prompt, star, slashed ring, pointer and brackets, with a dedicated TRAE mark; they are not typographic logo substitutes or copied official logos.

## Components

- **Dynamic Island:** at rest, the pulse grip sits beside provider marks and binding-limit values. Hover expansion is optional; keyboard focus expands independently. Expansion shows full product names with ellipsis/tooltips where needed, provider marks and separate reported main windows, including Binding when distinct: at most three windows per account. Reported amount details appear as muted island metadata beneath their quota; TRAE uses the concise Dönem label while its tooltip preserves the full window name. The two-unit binding-limit line and optional binding reset follow them. Unavailable accounts show an em dash or an explicit reading state. Select an account for all its limits and advice; Tümü aç and +N open the complete compact list, and Ayarlar opens settings. Drag the pulse grip to save a free position; double-click it to open the panel.
- **Windows taskbar instrument:** Identity is the default: provider marks sit inside thin quota rings, with visible names and a second line of limit values. Rings, Ribbon, Dial and Flow remain alternatives. Names, short-period limits, long-period limits and reset timer are separate options. Width fitting keeps all accounts through full, 88-percent and icon-plus-binding-percent densities before using +N. Compact mode uses clear nineteen-unit unframed marks and ten-and-a-half-unit percentages. Missing readings stay unavailable. Flow motion decorates a fixed reading only during permitted hover.
- **Floating gauges:** drag the central pulse grip to save a free position; double-click it to open the panel; select a provider mark/percentage to open its product. Halo, Orbit and Comet share the same circular geometry, separate real quota sectors and a central used/remaining caption. Full product identity remains in each provider's tooltip and accessible action name; its tooltip also includes the reported plan. Orbit is the initial style within Floating, not the default presentation mode.
- **Compact account row:** provider mark and identity at left, percentage or em dash at right, binding quota or connection state beneath. A raised plan badge sits beside the name before expansion, using ten-unit text, four-unit corners and a maximum text width of 135 units. Preserve the reported plan such as Pro or Max; strip only a redundant provider-name prefix. Unknown plans read Plan ?. A stale snapshot's tooltip explicitly says Son bilinen plan; the badge never proves freshness or current allowance. Selection reveals reported windows, resets and advice. Reported amount details use muted amount-detail text below their quota, preserving any supplied units instead of inferring a different allowance. Email is optional.
- **Usage advice:** sage heading and normal text under selected-account limits. Forecasts depend on reported timing and sufficient elapsed usage, and explicitly remain conditional. An unknown period start never produces an invented exhaustion date.
- **Settings navigation:** compact Görünüm, Hesaplar and Sistem sidebar. Görünüm uses three progressive steps: presentation, related indicator options, placement and appearance. Choosing a presentation immediately enters its options. Back preserves the draft. İleri advances; only final Kaydet applies it.
- **Presentation and indicator options:** full-width presentation tiles with explanatory captions select Dynamic Island, taskbar or floating gauges. Mode-specific options open as the next step; floating style tiles include gauge previews. The selected outline communicates selection rather than decorative card framing.
- **Settings switches and choices:** labels and hints explicitly receive the text palette. Appearance, placement, motion, discovery and system choices remain drafted until Save.
- **Save and recovery:** persistent Kaydet and an unsaved-state footer. Failure remains visible; back discards the draft. The compact panel's used/remaining control is an immediate preference, separate from the settings draft.
- **Icon buttons:** small native pin, close, refresh and settings controls retain hover and keyboard focus treatments. The pin uses authored vector geometry.

The ground-truth implementations are `DynamicIsland.cs`, `FloatingIsland.cs`, `FloatingOrb.cs`, `Settings.cs`, `PulseSettings.cs`, `PulseCompact.cs`, `Theme.cs`, `WidgetRenderer.cs`, `TaskbarWidget.cs`, `Strings.cs`, `ProviderMark.cs` and the shared native control styles. `PulseOverview.cs` supplies shared text/mark helpers; its older broad card composition does not define the default panel. Labeled native captures in `docs/proof/2.2` document the implementation. Shown-window pointer tests verify expansion at the top anchor and collapse after moving the test window to Free to cause pointer departure; offscreen state changes alone do not verify hover. Sidecar HTML/CSS examples are portable illustrations with synthetic values, not WPF runtime or live-quota evidence.

## Do's and Don'ts

### Do:
- Do keep quota, usage mode, product identity and freshness semantics together.
- Do preserve all three presentation choices and use Dynamic Island as the new-install default.
- Do keep Dynamic Island compact at rest and reveal names, separate limits and optional resets on expansion.
- Do keep Dynamic Island attached to its selected edge and verify hover on the shown native window.
- Do keep every floating style circular and each provider's quota sector separate; use +N for overflow.
- Do restore names when there is space; keep all accounts as icons and percentages before reducing the shown provider count.
- Do show reported plan badges before account expansion, with Plan ? for unknown and last-known labeling for stale metadata.
- Do preserve placement, explicit account opt-outs and settings drafts until Save succeeds.
- Do gate optional motion on the relevant hover/focus state, the saved preference and Windows animation permission.
- Do label synthetic captures and keep live-account evidence outside distributable assets.

### Don't:
- Don't combine Cursor and Grok allowances because their email or branding matches.
- Don't show unavailable quota as zero or infer quota from a receipt.
- Don't turn Orbit's initial floating style into the default presentation or remove the supported taskbar choice.
- Don't restore the superseded card grid or broad account desk as the default detail surface.
- Don't infer a paid plan or live quota from a discovered application, colored mark or catalog entry.
- Don't forecast without the real timing and usage evidence required by the advice engine.
- Don't animate numerical consumption or run decorative motion continuously at idle.

Version 2.4 adds vertical left/right top/center/bottom docks with upright provider stacks. Expansion grows inward and preserves the original hover footprint. Signal uses the panel binding quota and follows the displayed used/remaining percentage, with partial equal slices instead of rounding tiny values into full bars. Identified historical Antigravity percentages carry an amber asterisk and read-time tooltip, never a fresh-data claim.

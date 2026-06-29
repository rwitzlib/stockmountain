
# 🎨 Software Design Artifact — *“Cyber-Ops Dashboard UI”*

## Overall Theme
- Dark mode, high-contrast, terminal-inspired.
- Minimal colors: black/charcoal background, gray/white text, neon accents (red, green, cyan, yellow).
- Dense information layout, but structured grids prevent clutter.
- Mix of **dataviz** (charts, globe, histograms, radar), **tables**, and **logs**.

---

## Typography
- Monospace or near-monospace font.
- Small caps for section headers.
- Accent colors on keywords (`SUCCESS`, `FAILED`, `WARNING`) and IDs.
- Time/date formats: `DD/MM/YYYY HH:mm`, `UTC`.

---

## Layout Patterns
1. **Dashboard Panels**
   - Left/right sidebars, main center charts/logs.
   - Grid of cards: each card is compact, boxed, and separated with thin lines.
   - Consistent padding and spacing for data cells.

2. **Tables**
   - Flat, grid-like with alternating row tones.
   - Left: IDs or identifiers; Right: values or activity.

3. **Charts**
   - Minimal axes, grid lines faint.
   - Line charts for trends, radial/radar for status, globe for geospatial targets.

4. **Logs / Streams**
   - Scrollable text areas.
   - Prefixed with symbols (`>`, `#`, `::`) for terminal feel.
   - Highlighted tokens in neon.

---

## Interaction Elements
- Toggle buttons (Day / Week / Month).
- “Details / Join Mission” style CTAs → minimal bordered buttons.
- Expand/collapse panels (`RULES`, `SEQUENCE MGMT`) with chevron indicators.
- “Play / Pause / Stop” with iconography resembling control panels.

---

## Color Usage
- **Primary**: Neutral gray/white for text.
- **Secondary**: Neon highlights:
  - Green → success/active agents.
  - Red → errors/failures.
  - Yellow → warnings.
  - Cyan → neutral accents (links, network activity).
- Background gradients very subtle → mostly flat.

---

## Motion / Feedback
- Hover → subtle glow/border highlight.
- Charts animate linearly (progressive reveal, pulsing dots).
- Logs auto-scroll with latest entries.

---

## Implementation Notes (React.js)
- Use **Tailwind CSS** for atomic styling, extended with custom dark palette + neon colors.
- Dataviz: `recharts` (line/bar), `react-globe.gl` (3D globe), `react-sigma` (network/radar).
- Logs: styled `<pre>` with auto-scroll + syntax highlighting.
- State handling: each panel is modular React component:
  - `<AgentTable />`, `<MissionOverviewChart />`, `<ActivityLog />`, `<EncryptedChat />`.
- Keep **grid-based layout** via CSS Grid / Flexbox.

---

⚡ **Key Principle**: Treat the UI like a **spy ops terminal crossed with a financial trading dashboard** → efficient, data-dense, but stylish with cyberpunk polish.

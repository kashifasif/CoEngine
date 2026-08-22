---
name: Technical Precision
colors:
  surface: '#0b1326'
  surface-dim: '#0b1326'
  surface-bright: '#31394d'
  surface-container-lowest: '#060e20'
  surface-container-low: '#131b2e'
  surface-container: '#171f33'
  surface-container-high: '#222a3d'
  surface-container-highest: '#2d3449'
  on-surface: '#dae2fd'
  on-surface-variant: '#c7c4d7'
  inverse-surface: '#dae2fd'
  inverse-on-surface: '#283044'
  outline: '#908fa0'
  outline-variant: '#464554'
  surface-tint: '#c0c1ff'
  primary: '#c0c1ff'
  on-primary: '#1000a9'
  primary-container: '#8083ff'
  on-primary-container: '#0d0096'
  inverse-primary: '#494bd6'
  secondary: '#4edea3'
  on-secondary: '#003824'
  secondary-container: '#00a572'
  on-secondary-container: '#00311f'
  tertiary: '#ffb2b7'
  on-tertiary: '#67001b'
  tertiary-container: '#ff516a'
  on-tertiary-container: '#5b0017'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#e1e0ff'
  primary-fixed-dim: '#c0c1ff'
  on-primary-fixed: '#07006c'
  on-primary-fixed-variant: '#2f2ebe'
  secondary-fixed: '#6ffbbe'
  secondary-fixed-dim: '#4edea3'
  on-secondary-fixed: '#002113'
  on-secondary-fixed-variant: '#005236'
  tertiary-fixed: '#ffdadb'
  tertiary-fixed-dim: '#ffb2b7'
  on-tertiary-fixed: '#40000d'
  on-tertiary-fixed-variant: '#92002a'
  background: '#0b1326'
  on-background: '#dae2fd'
  surface-variant: '#2d3449'
typography:
  display-lg:
    fontFamily: Hanken Grotesk
    fontSize: 48px
    fontWeight: '700'
    lineHeight: 56px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Hanken Grotesk
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  headline-sm:
    fontFamily: Hanken Grotesk
    fontSize: 18px
    fontWeight: '600'
    lineHeight: 24px
  body-lg:
    fontFamily: Hanken Grotesk
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-sm:
    fontFamily: Hanken Grotesk
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  code-md:
    fontFamily: JetBrains Mono
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  code-sm:
    fontFamily: JetBrains Mono
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
  label-caps:
    fontFamily: JetBrains Mono
    fontSize: 11px
    fontWeight: '700'
    lineHeight: 16px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  unit: 4px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 40px
  container-max: 1440px
  gutter: 20px
---

## Brand & Style
The design system is engineered for high-density information architecture and technical rigor. It targets developers, systems engineers, and technical product managers who require an environment that minimizes cognitive load while maximizing data visibility.

The style is a hybrid of **Minimalism** and **Modern Corporate**, drawing functional cues from Integrated Development Environments (IDEs). It prioritizes utility and structural clarity over decorative elements. The visual language uses "intentional density"—grouping related technical data tightly while using generous outer margins to define distinct work zones. The emotional response is one of efficiency, reliability, and "calm control" amidst complex technical specifications.

## Colors
The palette is rooted in a "Deep Space" dark mode to reduce eye strain during long technical review sessions. 

- **Primary (Indigo):** Reserved for high-intent actions, primary buttons, and active selection states.
- **Secondary (Emerald):** Used for "Success" states, "Approved" status, and AI-generated insights.
- **Tertiary (Rose):** Dedicated to destructive actions, critical blockers, or high-priority alerts.
- **Neutral/Slate:** A tiered grey scale used for structural borders and surface elevations.

Surface colors follow a logical nesting: the base background is the darkest, with each functional layer (cards, sidebars, modals) becoming progressively lighter to indicate proximity to the user.

## Typography
This design system employs a dual-font strategy to differentiate between "Narrative" and "Technical" content.

- **Hanken Grotesk:** Used for the primary UI, navigation, and documentation prose. It provides a contemporary, sharp feel that balances the technical nature of the tool with approachable readability.
- **JetBrains Mono:** Used for all "Scope Tags," technical metadata, code snippets, and status indicators. The monospaced nature ensures that alphanumeric IDs and version numbers align vertically, aiding quick scanning.

**Hierarchy Note:** Avoid using font sizes below 12px for technical data. Use the `label-caps` style for section headers within sidebars to maintain a structured, IDE-like sidebar layout.

## Layout & Spacing
The layout uses a **Fluid Grid** for the main content area, flanked by fixed-width functional rails (Sidebar: 280px, Inspector: 320px). 

- **Density:** Components use a tight 4px base unit. For data-heavy tables or lists, use `sm` (8px) padding. For marketing or landing pages, scale to `lg` (24px).
- **Breakpoints:** 
  - *Desktop (1280px+):* 3-column layout (Navigation / Main Editor / Metadata).
  - *Tablet (768px - 1279px):* 2-column layout (Navigation hidden in hamburger / Main Editor / Metadata drawer).
  - *Mobile (<768px):* Single column stack. Technical specs should horizontally scroll if they exceed the viewport; do not wrap code-like content.

## Elevation & Depth
Depth is communicated through **Tonal Layers** and **Low-Contrast Outlines** rather than heavy shadows.

- **Level 0 (Base):** Deep Slate background (#020617).
- **Level 1 (Cards/Sidebar):** Raised surface (#0F172A) with a 1px solid border (#1E293B).
- **Level 2 (Popovers/Modals):** Lighter surface (#1E293B) with a subtle "Indigo Tint" glow (0px 4px 20px rgba(99, 102, 241, 0.1)).

This approach creates a flat, professional "dashboard" feel that prevents the UI from feeling cluttered when many elements are on screen simultaneously.

## Shapes
The design system utilizes **Soft** (4px) roundedness to maintain a precise, engineered appearance.

- **Standard Elements:** 4px radius for buttons, input fields, and cards.
- **Scope Tags & Badges:** 2px radius or sharp corners to distinguish them as "Technical Metadata."
- **AI Brainstorming Bubble:** Use a slightly larger 8px radius (`rounded-lg`) to differentiate the human/AI conversation flow from the rigid technical specification grid.

## Components
- **Buttons:** Primary buttons use a solid Indigo fill. Secondary buttons use a ghost style with an Indigo border. All buttons use 14px Medium Hanken Grotesk.
- **Status Badges:** Use JetBrains Mono 12px. Backgrounds are low-opacity versions of the status color (e.g., Success is Emerald at 10% opacity) with a solid 1px border.
- **Project Cards:** Feature a top-accent border (2px) using the project's primary color. Content is left-aligned with technical metadata (version, owner) pushed to the bottom right in `code-sm` style.
- **Input Fields:** Darker than the surface level, with a 1px border that glows Indigo on focus. Use JetBrains Mono for the text input to accommodate technical strings/IDs.
- **AI Chat Interface:** Floating or docked panel using a blurred background (Glassmorphism) to distinguish it as an "assistant layer" sitting above the static specifications.
- **Scope Tags:** Compact, monospaced tags with no background, only a thin 1px border and a small leading icon representing the data type (e.g., ⚙️ for config, 🔗 for link).
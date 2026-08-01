import {
  Accordion,
  ActionIcon,
  Badge,
  Button,
  Card,
  createTheme,
  Drawer,
  Menu,
  Modal,
  Notification,
  SegmentedControl,
  Tooltip,
  type MantineColorsTuple,
} from '@mantine/core';
import {
  IconAlertTriangle,
  IconBan,
  IconBrandTelegram,
  IconBrandWhatsapp,
  IconCircleCheck,
  IconCircleDot,
  IconClockCheck,
  IconMail,
  IconPlayerPause,
  IconProgress,
  type TablerIcon,
} from '@tabler/icons-react';
import type { NotificationChannel, WorkItemStatus } from './api/types';

/**
 * The brand ramp. Deliberately the same indigo-blue as the application mark's gradient start
 * (#4c6ef5, see public/favicon.svg), so the icon in the browser tab and the primary button on the
 * page read as one product.
 */
const everdue: MantineColorsTuple = [
  '#eef2ff',
  '#dde3f7',
  '#b6c3ec',
  '#8da1e2',
  '#6b84d9',
  '#5572d4',
  '#4a69d3',
  '#3b57bb',
  '#334ea8',
  '#284094',
];

export const theme = createTheme({
  primaryColor: 'everdue',
  primaryShade: { light: 6, dark: 5 },
  colors: { everdue },
  defaultRadius: 'md',

  /** The mark's gradient, available to any component as `variant="gradient"`. */
  defaultGradient: { from: 'everdue.6', to: 'teal.7', deg: 135 },

  /**
   * Picks black or white text on a filled surface by its luminance, which is what makes a yellow or
   * lime badge readable without hand-picking a text colour at every call site.
   */
  autoContrast: true,

  fontFamily:
    'system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, "Noto Sans", sans-serif',
  headings: { fontWeight: '650' },
  cursorType: 'pointer',

  /*
   * House style, set once. Everything here is presentation: motion on the things that respond to a
   * pointer, slightly softer corners, and overlays that separate a dialog from the page behind it.
   */
  components: {
    Card: Card.extend({ defaultProps: { radius: 'lg' } }),
    Button: Button.extend({ defaultProps: { radius: 'md' } }),
    ActionIcon: ActionIcon.extend({ defaultProps: { radius: 'md' } }),
    Badge: Badge.extend({ defaultProps: { radius: 'sm' } }),
    SegmentedControl: SegmentedControl.extend({ defaultProps: { radius: 'md' } }),
    Accordion: Accordion.extend({ defaultProps: { radius: 'md' } }),
    Notification: Notification.extend({ defaultProps: { radius: 'md' } }),

    Tooltip: Tooltip.extend({
      defaultProps: { withArrow: true, openDelay: 200, transitionProps: { transition: 'pop', duration: 120 } },
    }),
    Menu: Menu.extend({
      defaultProps: { shadow: 'md', transitionProps: { transition: 'pop-top-right', duration: 120 } },
    }),
    Modal: Modal.extend({
      defaultProps: {
        radius: 'lg',
        overlayProps: { blur: 2, backgroundOpacity: 0.5 },
        transitionProps: { transition: 'pop', duration: 150 },
      },
    }),
    Drawer: Drawer.extend({
      defaultProps: { overlayProps: { blur: 2, backgroundOpacity: 0.5 } },
    }),
  },
});

/** One colour per status, used identically by badges, board columns and report chips. */
export const STATUS_COLOR: Record<WorkItemStatus, string> = {
  Open: 'blue',
  InProgress: 'indigo',
  OnHold: 'orange',
  Missed: 'red',
  Completed: 'teal',
  CompletedLate: 'lime',
  Cancelled: 'gray',
};

/**
 * One glyph per status, beside the colour. Two channels rather than one: the badge still reads as
 * itself in a screenshot printed in grey, and for the eight percent of men who would otherwise be
 * asked to tell orange from red.
 */
export const STATUS_ICON: Record<WorkItemStatus, TablerIcon> = {
  Open: IconCircleDot,
  InProgress: IconProgress,
  OnHold: IconPlayerPause,
  Missed: IconAlertTriangle,
  Completed: IconCircleCheck,
  CompletedLate: IconClockCheck,
  Cancelled: IconBan,
};

/** One glyph per delivery channel, for the settings forms and the delivery-health table. */
export const CHANNEL_ICON: Record<NotificationChannel, TablerIcon> = {
  Email: IconMail,
  Telegram: IconBrandTelegram,
  WhatsApp: IconBrandWhatsapp,
};

/**
 * One colour per navigation section, so a screen's header, its icon and its group in the navbar all
 * agree on where you are. The sections read as a progression: your work, then what went wrong, then
 * what keeps happening, then the machinery behind it.
 */
export const SECTION_COLOR = {
  work: 'everdue',
  reports: 'orange',
  insights: 'grape',
  admin: 'teal',
} as const;

export type SectionId = keyof typeof SECTION_COLOR;

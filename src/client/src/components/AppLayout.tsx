import {
  Accordion,
  ActionIcon,
  AppShell,
  Avatar,
  Badge,
  Burger,
  Group,
  Menu,
  NavLink,
  ScrollArea,
  Text,
  ThemeIcon,
  Tooltip,
  UnstyledButton,
} from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconChevronDown, IconFlask, IconHelp, IconLogout, IconUser } from '@tabler/icons-react';
import { useEffect, useState, type CSSProperties, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { activeNav, NAV_SECTIONS, type NavSection } from '../lib/navigation';
import { routes } from '../lib/routes';
import { SECTION_COLOR } from '../theme';
import { useSession } from '../features/auth/session';
import { NotificationBell } from '../features/notifications/NotificationBell';
import { BrandMark } from './BrandMark';
import { ColorSchemeToggle } from './ColorSchemeToggle';
import { LegalNotice } from './LegalNotice';

export function AppLayout({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const [opened, { toggle, close }] = useDisclosure(false);
  const location = useLocation();
  const navigate = useNavigate();
  const { user, isAdmin, signOut } = useSession();

  const active = activeNav(location.pathname);

  // Reports are admin-only for now — the simplest rule that fits, revisited with usage. The sections
  // themselves say who may see them; this only filters.
  const sections = NAV_SECTIONS.filter((section) => isAdmin || !section.adminOnly);

  /*
   * Four groups of links, of which one is nine long, is a wall. So they collapse, and the one you are
   * in is the one that is open — including after a drill-through from a report into the work list,
   * which is what the effect below is for. Which groups are open is presentation state and stays in
   * the component: it is not worth a round trip or a stored preference.
   */
  const [expanded, setExpanded] = useState<string[]>(() => [active?.section ?? 'work']);

  useEffect(() => {
    const section = active?.section;
    if (section) setExpanded((current) => (current.includes(section) ? current : [...current, section]));
  }, [active?.section]);

  return (
    <AppShell
      header={{ height: 56 }}
      navbar={{ width: 260, breakpoint: 'sm', collapsed: { mobile: !opened } }}
      padding="md"
    >
      <AppShell.Header>
        <Group h="100%" px="md" justify="space-between">
          <Group gap="sm">
            <Burger opened={opened} onClick={toggle} hiddenFrom="sm" size="sm" />

            {/* The mark, then the name in the mark's own gradient: one identity, not two. */}
            <Group gap={8} wrap="nowrap">
              <BrandMark size={26} />
              <Text
                fw={800}
                size="lg"
                variant="gradient"
                gradient={{ from: 'everdue.6', to: 'teal.7', deg: 135 }}
                style={{ letterSpacing: '-0.01em' }}
              >
                {t('common.appName')}
              </Text>
            </Group>

            {user?.tenant.name && (
              <Badge variant="light" color="everdue" radius="sm" visibleFrom="sm">
                {user.tenant.name}
              </Badge>
            )}

            {/* Seeded history is indistinguishable from real history by design — that is what makes the demo
                convincing — so the only thing stopping somebody putting real work into it is this badge.
                Shown to every role, including the members who never reach the settings screen. */}
            {user?.tenant.demoMode && (
              <Tooltip label={t('demo.bannerHint')}>
                <Badge variant="filled" color="orange" radius="sm" leftSection={<IconFlask size={12} />}>
                  {t('demo.badge')}
                </Badge>
              </Tooltip>
            )}
          </Group>

          <Group gap="xs">
            {/* The manual, one click from every screen — where somebody stuck actually looks. */}
            <Tooltip label={t('nav.help')}>
              <ActionIcon
                component={Link}
                to={routes.help}
                variant="subtle"
                color="gray"
                size="lg"
                aria-label={t('nav.help')}
              >
                <IconHelp size={20} />
              </ActionIcon>
            </Tooltip>

            <NotificationBell />

            <Menu position="bottom-end" withinPortal>
              <Menu.Target>
                <UnstyledButton>
                  <Group gap={8} wrap="nowrap">
                    <Avatar size={30} radius="xl" color="everdue" name={user?.displayName} />
                    <Text size="sm" fw={500} visibleFrom="xs">
                      {user?.displayName}
                    </Text>
                    <IconChevronDown size={14} />
                  </Group>
                </UnstyledButton>
              </Menu.Target>

              <Menu.Dropdown>
                <Menu.Label>{user?.email}</Menu.Label>
                <Menu.Item leftSection={<IconUser size={16} />} onClick={() => navigate(routes.profile)}>
                  {t('nav.profile')}
                </Menu.Item>
                <Menu.Item leftSection={<IconHelp size={16} />} onClick={() => navigate(routes.help)}>
                  {t('nav.help')}
                </Menu.Item>

                <Menu.Divider />
                <Menu.Label>{t('theme.label')}</Menu.Label>
                <Menu.Item component="div" closeMenuOnClick={false} px="xs">
                  <ColorSchemeToggle />
                </Menu.Item>

                <Menu.Divider />
                <Menu.Item
                  color="red"
                  leftSection={<IconLogout size={16} />}
                  onClick={async () => {
                    await signOut();
                    navigate(routes.login);
                  }}
                >
                  {t('nav.logout')}
                </Menu.Item>
              </Menu.Dropdown>
            </Menu>
          </Group>
        </Group>
      </AppShell.Header>

      <AppShell.Navbar p="xs">
        <ScrollArea type="scroll">
          <Accordion
            multiple
            value={expanded}
            onChange={setExpanded}
            chevronPosition="right"
            variant="filled"
            styles={{
              content: { padding: '0 0 var(--mantine-spacing-xs) 0' },
              control: { paddingInline: 'var(--mantine-spacing-xs)' },
              item: { border: 'none' },
            }}
          >
            {sections.map((section) => (
              <NavSectionPanel key={section.id} section={section} activeTo={active?.to} onNavigate={close} />
            ))}
          </Accordion>
        </ScrollArea>
      </AppShell.Navbar>

      <AppShell.Main>
        {children}

        {/* Required by the AGPL, and the honest place for it: visible in the running program, not only in a file. */}
        <LegalNotice />
      </AppShell.Main>
    </AppShell>
  );
}

function NavSectionPanel({
  section,
  activeTo,
  onNavigate,
}: {
  section: NavSection;
  activeTo: string | undefined;
  onNavigate: () => void;
}) {
  const { t } = useTranslation();
  const colour = SECTION_COLOR[section.id];
  const SectionIcon = section.icon;

  return (
    <Accordion.Item value={section.id}>
      <Accordion.Control>
        <Group gap="xs" wrap="nowrap">
          <ThemeIcon size="sm" radius="sm" variant="light" color={colour}>
            <SectionIcon size={14} />
          </ThemeIcon>
          <Text size="xs" tt="uppercase" fw={700} c="dimmed">
            {t(section.labelKey)}
          </Text>
        </Group>
      </Accordion.Control>

      <Accordion.Panel>
        {/* The section's colour reaches the links through a custom property; app.css paints the
            active link's accent bar with it. */}
        <div style={{ ['--nav-accent' as string]: `var(--mantine-color-${colour}-filled)` } as CSSProperties}>
          {section.items.map((item) => {
            const ItemIcon = item.icon;

            return (
              <NavLink
                key={item.to}
                component={Link}
                to={item.to}
                label={t(item.labelKey)}
                color={colour}
                variant="light"
                active={activeTo === item.to}
                leftSection={<ItemIcon size={18} />}
                onClick={onNavigate}
              />
            );
          })}
        </div>
      </Accordion.Panel>
    </Accordion.Item>
  );
}

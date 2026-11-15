import { ActionIcon, Kbd, Loader, Modal, Text, TextInput, ThemeIcon, UnstyledButton } from '@mantine/core';
import { useDebouncedValue, useHotkeys } from '@mantine/hooks';
import { IconArrowRight, IconSearch } from '@tabler/icons-react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useEffect, useState, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import type { WorkItem } from '../../api/types';
import { api } from '../../lib/api';
import { formatDueDate } from '../../lib/format';
import { keys } from '../../lib/queryKeys';
import { routes } from '../../lib/routes';
import { excerptAround, markMatches, searchTerms } from '../../lib/searchText';
import { STATUS_COLOR, STATUS_ICON } from '../../theme';
import { WorkItemDrawer } from '../workitems/WorkItemDrawer';

const LIMIT = 8;

export function QuickSearch() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [opened, setOpened] = useState(false);
  const [text, setText] = useState('');
  const [active, setActive] = useState(0);
  const [openId, setOpenId] = useState<string | null>(null);

  const [query] = useDebouncedValue(text.trim(), 160);
  const terms = searchTerms(query);
  const searchable = terms.some((term) => term.length >= 2);

  const results = useQuery({
    queryKey: keys.workItems.search(query),
    queryFn: () => api.workItems.search(query, LIMIT),
    enabled: opened && searchable,
    placeholderData: keepPreviousData,
  });

  const items = searchable ? (results.data ?? []) : [];

  // What is on screen still belongs to an earlier query until the debounce and the fetch catch up.
  const settled = text.trim() === query && !results.isPlaceholderData;

  useHotkeys([
    ['mod+K', () => setOpened(true)],
    ['/', () => setOpened(true)],
    // On a Spanish keyboard the slash is Shift+7.
    ['shift+/', () => setOpened(true)],
  ]);

  useEffect(() => setActive(0), [query]);

  const close = () => {
    setOpened(false);
    setText('');
  };

  const openItem = (item: WorkItem) => {
    close();
    setOpenId(item.id);
  };

  const seeAll = () => {
    close();
    navigate(`${routes.work}?search=${encodeURIComponent(query)}`);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    // The row after the last result is "see all in the list".
    const rows = items.length + (items.length > 0 ? 1 : 0);
    if (rows === 0) return;

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setActive((current) => (current + 1) % rows);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setActive((current) => (current - 1 + rows) % rows);
    } else if (event.key === 'Enter' && settled) {
      event.preventDefault();
      if (active < items.length) openItem(items[active]);
      else seeAll();
    }
  };

  return (
    <>
      <UnstyledButton
        className="everdue-search-trigger"
        visibleFrom="sm"
        onClick={() => setOpened(true)}
        aria-label={t('search.open')}
      >
        <IconSearch size={16} />
        <Text size="sm" span>
          {t('search.placeholder')}
        </Text>
        <Kbd size="xs">{/Mac|iPhone|iPad/.test(navigator.userAgent) ? '⌘K' : 'Ctrl K'}</Kbd>
      </UnstyledButton>

      <ActionIcon
        hiddenFrom="sm"
        variant="subtle"
        color="gray"
        size="lg"
        aria-label={t('search.open')}
        onClick={() => setOpened(true)}
      >
        <IconSearch size={20} />
      </ActionIcon>

      <Modal
        opened={opened}
        onClose={close}
        withCloseButton={false}
        size={640}
        padding={0}
        yOffset="12vh"
        transitionProps={{ transition: 'fade-down', duration: 140 }}
      >
        <TextInput
          data-autofocus
          variant="unstyled"
          size="lg"
          className="everdue-search-input"
          placeholder={t('search.placeholder')}
          aria-label={t('search.open')}
          leftSection={results.isFetching ? <Loader size={18} /> : <IconSearch size={20} />}
          value={text}
          onChange={(event) => setText(event.currentTarget.value)}
          onKeyDown={onKeyDown}
        />

        <div className="everdue-search-results" role="listbox">
          {items.map((item, index) => (
            <SearchHit
              key={item.id}
              item={item}
              terms={terms}
              active={index === active}
              onHover={() => setActive(index)}
              onOpen={() => openItem(item)}
            />
          ))}

          {items.length > 0 && (
            <UnstyledButton
              className="everdue-search-hit everdue-search-all"
              data-active={active === items.length || undefined}
              onMouseEnter={() => setActive(items.length)}
              onClick={seeAll}
            >
              <Text size="sm" fw={600}>
                {t('search.seeAll')}
              </Text>
              <IconArrowRight size={16} />
            </UnstyledButton>
          )}

          {items.length === 0 && (
            <Text size="sm" c="dimmed" p="md">
              {!searchable
                ? t('search.hint')
                : results.isFetching
                  ? t('common.loading')
                  : t('search.empty', { query })}
            </Text>
          )}
        </div>
      </Modal>

      <WorkItemDrawer id={openId} onClose={() => setOpenId(null)} />
    </>
  );
}

function SearchHit({
  item,
  terms,
  active,
  onHover,
  onOpen,
}: {
  item: WorkItem;
  terms: string[];
  active: boolean;
  onHover: () => void;
  onOpen: () => void;
}) {
  const { t } = useTranslation();
  const Icon = STATUS_ICON[item.status];
  const excerpt = item.description ? excerptAround(item.description, terms) : null;

  return (
    <UnstyledButton
      className="everdue-search-hit"
      role="option"
      aria-selected={active}
      data-active={active || undefined}
      onMouseEnter={onHover}
      onClick={onOpen}
    >
      <ThemeIcon variant="light" color={STATUS_COLOR[item.status]} size={30} radius="md" title={t(`status.${item.status}`)}>
        <Icon size={16} />
      </ThemeIcon>

      <div className="everdue-search-hit-body">
        <Text size="sm" fw={600} lineClamp={1}>
          <Marked text={item.title} terms={terms} />
        </Text>

        {excerpt && (
          <Text size="xs" c="dimmed" lineClamp={1}>
            <Marked text={excerpt} terms={terms} />
          </Text>
        )}

        <Text size="xs" c="dimmed" lineClamp={1}>
          <Marked text={[item.entityName, item.ownerDisplayName].filter(Boolean).join(', ')} terms={terms} />
        </Text>
      </div>

      <div className="everdue-search-hit-side">
        <Text size="xs" fw={600} c={STATUS_COLOR[item.status]}>
          {t(`status.${item.status}`)}
        </Text>
        <Text size="xs" c={item.isOverdue ? 'red' : 'dimmed'}>
          {formatDueDate(item.dueDate, item.responsibilityId != null)}
        </Text>
      </div>
    </UnstyledButton>
  );
}

function Marked({ text, terms }: { text: string; terms: string[] }) {
  return (
    <>
      {markMatches(text, terms).map((part, index) =>
        part.hit ? <mark key={index}>{part.text}</mark> : <span key={index}>{part.text}</span>,
      )}
    </>
  );
}

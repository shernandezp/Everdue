import { Badge, Button, Card, Group, Loader, ScrollArea, SimpleGrid, Stack, Text, ThemeIcon } from '@mantine/core';
import { DndContext, PointerSensor, useDroppable, useSensor, useSensors, type DragEndEvent } from '@dnd-kit/core';
import { IconPlus } from '@tabler/icons-react';
import { useMemo, useState, type CSSProperties } from 'react';
import { useTranslation } from 'react-i18next';
import { notifications } from '@mantine/notifications';
import type { WorkItem, WorkItemStatus } from '../../api/types';
import { STATUS_COLOR, STATUS_ICON } from '../../theme';
import { PageHeader } from '../../components/PageHeader';
import { UserPicker } from '../../components/pickers';
import { useSession } from '../auth/session';
import { HoldDialog } from '../workitems/HoldDialog';
import { NewTaskModal } from '../workitems/NewTaskModal';
import { WorkItemDrawer } from '../workitems/WorkItemDrawer';
import { useWorkItemActions, useWorkItems } from '../workitems/hooks';
import { BOARD_COLUMNS, columnOf, resolveDrop, type BoardColumnId } from './boardColumns';
import { WorkItemCard } from './WorkItemCard';

/**
 * The status each column stands for, so a column's rule, its glyph and the badges of the cards in it
 * all take their colour from the one table in theme.ts rather than from five literals here.
 */
const COLUMN_STATUS: Record<BoardColumnId, WorkItemStatus> = {
  open: 'Open',
  inProgress: 'InProgress',
  onHold: 'OnHold',
  missed: 'Missed',
  done: 'Completed',
};

export function BoardPage() {
  const { t } = useTranslation();
  const { user } = useSession();

  // Defaults to your own work; clearing it shows the whole team, and picking a person answers the
  // manager's question — what is this person doing, and what is still queued for them.
  const [ownerId, setOwnerId] = useState<string | null>(user?.id ?? null);
  const [openId, setOpenId] = useState<string | null>(null);
  const [newTaskOpen, setNewTaskOpen] = useState(false);
  const [holdTarget, setHoldTarget] = useState<string | null>(null);

  const board = useWorkItems({
    view: 'board',
    pageSize: 100,
    ...(ownerId ? { ownerId } : {}),
  });

  const actions = useWorkItemActions();

  // Which card's action is in flight, so that card — and only that card — can say so. Without this
  // a tap on Complete gave no sign until the refetch landed, which reads as a broken button.
  const pendingId = actions.isPending ? (actions.variables?.id ?? null) : null;

  // A pointer sensor with a small activation distance so a tap opens the card and a drag moves it.
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));

  const grouped = useMemo(() => {
    const buckets: Record<BoardColumnId, WorkItem[]> = {
      open: [],
      inProgress: [],
      onHold: [],
      missed: [],
      done: [],
    };
    for (const item of board.data?.items ?? []) {
      buckets[columnOf(item)].push(item);
    }

    // Work to do reads oldest-first (the server already orders by due date); work already done
    // reads newest-first, because the only question about it is "what just happened".
    buckets.done.sort((a, b) => (b.completedAt ?? '').localeCompare(a.completedAt ?? ''));

    return buckets;
  }, [board.data]);

  const onDragEnd = (event: DragEndEvent) => {
    const target = event.over?.id as BoardColumnId | undefined;
    const id = event.active.id as string;
    if (!target) return;

    const item = (board.data?.items ?? []).find((candidate) => candidate.id === id);
    if (!item) return;

    const outcome = resolveDrop(item.status, target);

    switch (outcome.kind) {
      case 'start':
        actions.mutate({ id, action: { kind: 'start' } });
        break;
      case 'complete':
        actions.mutate({ id, action: { kind: 'complete' } });
        break;
      case 'reopen':
        actions.mutate({ id, action: { kind: 'reopen' } });
        break;
      case 'hold':
        // The reason is mandatory, so the drop opens the dialog rather than guessing one.
        setHoldTarget(id);
        break;
      case 'rejected':
        notifications.show({ color: 'red', message: t('board.dropRejected'), autoClose: 4000 });
        break;
      case 'noop':
        break;
    }
  };

  return (
    <>
      <PageHeader
        title={t('board.title')}
        actions={
          <Group gap="sm" align="flex-end">
            <UserPicker
              label={t('board.showing')}
              placeholder={t('board.everyone')}
              clearable
              value={ownerId}
              onChange={setOwnerId}
              w={200}
            />
            <Button leftSection={<IconPlus size={16} />} onClick={() => setNewTaskOpen(true)}>
              {t('board.newTask')}
            </Button>
          </Group>
        }
      />

      {board.isLoading && <Loader />}

      {/*
        The board fetches one page. Saying so beats silently under-counting: the column badges count
        the cards on screen, and a manager reading "Open: 37" must be able to trust it.
      */}
      {board.data && board.data.totalCount > board.data.items.length && (
        <Text size="xs" c="orange" mb="xs">
          {t('board.truncated', { shown: board.data.items.length, total: board.data.totalCount })}
        </Text>
      )}

      <DndContext sensors={sensors} onDragEnd={onDragEnd}>
        <SimpleGrid cols={{ base: 1, sm: 2, lg: 3, xl: 5 }} spacing="sm">
          {BOARD_COLUMNS.map((column) => (
            <Column key={column} id={column} items={grouped[column]}>
              {grouped[column].map((item) => (
                <WorkItemCard
                  key={item.id}
                  item={item}
                  busy={pendingId === item.id}
                  onOpen={() => setOpenId(item.id)}
                  onStart={() => actions.mutate({ id: item.id, action: { kind: 'start' } })}
                  onHold={() => setHoldTarget(item.id)}
                  onComplete={() => actions.mutate({ id: item.id, action: { kind: 'complete' } })}
                  onReopen={() => actions.mutate({ id: item.id, action: { kind: 'reopen' } })}
                />
              ))}
            </Column>
          ))}
        </SimpleGrid>
      </DndContext>

      <HoldDialog
        opened={holdTarget !== null}
        onClose={() => setHoldTarget(null)}
        busy={actions.isPending}
        onConfirm={(reason, text) => {
          if (holdTarget) {
            actions.mutate({ id: holdTarget, action: { kind: 'hold', reason, text } });
          }
        }}
      />

      <NewTaskModal opened={newTaskOpen} onClose={() => setNewTaskOpen(false)} />
      <WorkItemDrawer id={openId} onClose={() => setOpenId(null)} />
    </>
  );
}

function Column({ id, items, children }: { id: BoardColumnId; items: WorkItem[]; children: React.ReactNode }) {
  const { t } = useTranslation();
  const { setNodeRef, isOver } = useDroppable({ id });

  const colour = STATUS_COLOR[COLUMN_STATUS[id]];
  const Icon = STATUS_ICON[COLUMN_STATUS[id]];

  return (
    <Card
      ref={setNodeRef}
      withBorder
      padding="xs"
      className="everdue-column"
      // While a card is over the column, the column says so in its own colour rather than in grey.
      bg={isOver ? `var(--mantine-color-${colour}-light)` : undefined}
      style={
        {
          minHeight: 160,
          '--everdue-accent': `var(--mantine-color-${colour}-6)`,
        } as CSSProperties
      }
    >
      <Group justify="space-between" mb="xs">
        <Group gap={6}>
          <ThemeIcon size="sm" radius="sm" variant="light" color={colour}>
            <Icon size={14} />
          </ThemeIcon>
          <Text fw={600} size="sm">
            {t(`board.${id}`)}
          </Text>
          <Badge size="sm" variant="light" color={colour}>
            {items.length}
          </Badge>
        </Group>
        {id === 'done' && (
          <Text size="xs" c="dimmed">
            {t('board.doneHint')}
          </Text>
        )}
      </Group>

      <ScrollArea.Autosize mah={{ base: 'none', lg: 'calc(100vh - 230px)' }} type="auto">
        <Stack gap="xs">
          {items.length === 0 ? (
            <Text size="xs" c="dimmed">
              {t('board.empty')}
            </Text>
          ) : (
            children
          )}
        </Stack>
      </ScrollArea.Autosize>
    </Card>
  );
}

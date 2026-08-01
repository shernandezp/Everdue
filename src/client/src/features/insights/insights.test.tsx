import { MantineProvider } from '@mantine/core';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import type { BucketAxis, BucketPoint, ChronicResponsibility, ConcentrationRow, StripPoint } from '../../api/types';
import { ChronicDelayCard } from './ChronicDelayCard';
import { ComplianceStrip } from './ComplianceStrip';
import { seriesFor, toStackedSeries } from './concentrationSeries';
import { RateCell } from './RateCell';
import { judgedRates, TrendSparkline } from './TrendSparkline';

vi.mock('../../lib/api', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../lib/api')>();

  return {
    ...actual,
    api: {
      ...actual.api,
      insights: {
        ...actual.api.insights,
        chronic: vi.fn(),
      },
    },
  };
});

const { api } = await import('../../lib/api');

function wrap(node: React.ReactNode) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  return render(
    <MantineProvider>
      <QueryClientProvider client={client}>
        <MemoryRouter>{node}</MemoryRouter>
      </QueryClientProvider>
    </MantineProvider>,
  );
}

function strip(overrides: Partial<StripPoint> = {}): StripPoint {
  return {
    workItemId: crypto.randomUUID(),
    label: 'W29',
    periodStart: '2026-07-13',
    status: 'Completed',
    holdReason: null,
    periodConcluded: true,
    ...overrides,
  };
}

describe('RateCell', () => {
  /** A percentage never travels alone: the volume it rests on is always beside it. */
  it('shows the rate together with the pair it came from', () => {
    wrap(<RateCell rate={0.8667} suppressed={false} onTime={26} concluded={30} />);

    expect(screen.getByText('87%')).toBeInTheDocument();
    expect(screen.getByText('· 26/30')).toBeInTheDocument();
  });

  it('withholds a rate the server judged too thin, and still shows the count', () => {
    wrap(<RateCell rate={null} suppressed onTime={1} concluded={3} />);

    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getByText('· 1/3')).toBeInTheDocument();
    expect(screen.queryByText(/33%/)).not.toBeInTheDocument();
  });

  it('does not imply failure when nothing was due', () => {
    wrap(<RateCell rate={null} suppressed={false} onTime={0} concluded={0} />);

    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.queryByText(/0%/)).not.toBeInTheDocument();
  });
});

describe('ComplianceStrip', () => {
  it('renders one chip per period, marked by status', () => {
    wrap(
      <ComplianceStrip
        points={[
          strip({ label: 'W29', status: 'Completed' }),
          strip({ label: 'W30', status: 'Missed' }),
          strip({ label: 'W31', status: 'OnHold', holdReason: 'WaitingCustomer', periodConcluded: false }),
        ]}
      />,
    );

    expect(screen.getByText(/✅ W29/)).toBeInTheDocument();
    expect(screen.getByText(/❌ W30/)).toBeInTheDocument();
    expect(screen.getByText(/⏸ W31/)).toBeInTheDocument();
  });

  it('renders nothing rather than a placeholder when there are no periods', () => {
    const { container } = wrap(<ComplianceStrip points={[]} />);

    expect(container.querySelectorAll('.mantine-Badge-root')).toHaveLength(0);
  });

  /** Naming a period is only useful if you can go and look at it. */
  it('opens the occurrence behind the chip that was clicked', async () => {
    const onOpen = vi.fn();
    const point = strip({ label: 'W30', status: 'Missed' });

    wrap(<ComplianceStrip points={[point]} onOpen={onOpen} />);
    await userEvent.click(screen.getByText(/❌ W30/));

    expect(onOpen).toHaveBeenCalledWith(point.workItemId);
  });
});

describe('TrendSparkline', () => {
  const point = (rate: number | null, key: string): BucketPoint => ({
    key,
    label: key,
    start: '2026-07-01',
    partial: false,
    onTime: rate === null ? 0 : 1,
    late: 0,
    missed: 0,
    rate,
  });

  /** A week where nothing was due is not a week of total failure; it is not a data point at all. */
  it('plots only the periods that were judged', () => {
    expect(judgedRates([point(1, 'a'), point(null, 'b'), point(0.5, 'c')])).toEqual([100, 50]);
  });

  it('says so rather than drawing a line through one point', () => {
    wrap(<TrendSparkline points={[point(1, 'a'), point(null, 'b')]} />);

    expect(screen.getByText('—')).toBeInTheDocument();
  });
});

describe('concentration chart data', () => {
  const buckets: BucketAxis[] = [
    { key: '2026-05', label: '2026-05', start: '2026-05-01', partial: false },
    { key: '2026-06', label: '2026-06', start: '2026-06-01', partial: false },
    { key: '2026-07', label: '2026-07', start: '2026-07-01', partial: true },
  ];

  const rows: ConcentrationRow[] = [
    {
      entityId: '44444444-4444-4444-4444-444444444444',
      entityName: 'Acme',
      entityType: 'Customer',
      total: 5,
      points: [
        { bucketKey: '2026-05', occurrences: 2, oneOffs: 1, total: 3 },
        { bucketKey: '2026-07', occurrences: 2, oneOffs: 0, total: 2 },
      ],
      drillThrough: { workItemQuery: {} },
    },
  ];

  /** A month with no completions is a zero, never a gap: the chart must not skip it. */
  it('emits a dense point for every bucket the server sent', () => {
    const series = toStackedSeries(buckets, rows);

    expect(series).toEqual([
      { label: '2026-05', Acme: 3 },
      { label: '2026-06', Acme: 0 },
      { label: '2026-07', Acme: 2 },
    ]);
  });

  it('gives every entity its own colour, capped at the palette', () => {
    expect(seriesFor(rows)).toEqual([{ name: 'Acme', color: 'everdue.6' }]);

    const many = Array.from({ length: 12 }, (_, index) => ({ ...rows[0], entityName: `Entity ${index}` }));
    expect(seriesFor(many)).toHaveLength(8);
  });
});

describe('ChronicDelayCard', () => {
  function chronic(overrides: Partial<ChronicResponsibility> = {}): ChronicResponsibility {
    return {
      responsibilityId: '33333333-3333-3333-3333-333333333333',
      title: 'Weekly client call',
      ownerName: 'Ana',
      entityName: 'Acme',
      missed: 3,
      evaluated: 8,
      lastMissedPeriodStart: '2026-07-20',
      drillThrough: { workItemQuery: {} },
      ...overrides,
    };
  }

  it('leads with how often, out of how many', async () => {
    vi.mocked(api.insights.chronic).mockResolvedValue([chronic()]);

    wrap(<ChronicDelayCard />);

    expect(await screen.findByText('Weekly client call')).toBeInTheDocument();
    expect(screen.getByText('3 of 8 missed')).toBeInTheDocument();
    expect(screen.getByText(/Ana · Acme/)).toBeInTheDocument();
  });

  it('says nothing is chronic rather than showing an empty list', async () => {
    vi.mocked(api.insights.chronic).mockResolvedValue([]);

    wrap(<ChronicDelayCard />);

    await waitFor(() => expect(screen.getByText(/nothing is chronically delayed/i)).toBeInTheDocument());
  });
});

import { Group, Text, Tooltip } from '@mantine/core';
import { useTranslation } from 'react-i18next';

/**
 * A rate never appears on its own: the volume it rests on is beside it, and a rate the server withheld
 * as too thin shows as a dash rather than as a number nobody should act on. 95% of 200 is not 100% of 3,
 * and this component is where that is enforced for every screen.
 */
export function RateCell({
  rate,
  suppressed,
  onTime,
  concluded,
}: {
  rate: number | null;
  suppressed: boolean;
  onTime: number;
  concluded: number;
}) {
  const { t } = useTranslation();
  const pair = `${onTime}/${concluded}`;

  const value =
    rate === null ? (
      <Tooltip label={suppressed ? t('insights.rateWithheld') : t('insights.rateNothingDue')} withArrow>
        <Text span fw={600} c="dimmed">
          —
        </Text>
      </Tooltip>
    ) : (
      <Text span fw={600}>
        {Math.round(rate * 100)}%
      </Text>
    );

  return (
    <Group gap={6} justify="flex-end" wrap="nowrap">
      {value}
      <Text span size="xs" c="dimmed">
        · {pair}
      </Text>
    </Group>
  );
}

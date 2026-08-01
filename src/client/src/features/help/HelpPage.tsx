import { Alert, Card, Grid, Group, Loader, NavLink, ScrollArea, Text, ThemeIcon } from '@mantine/core';
import { IconAlertTriangle, IconBook, IconHelp } from '@tabler/icons-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useParams } from 'react-router-dom';
import { PageHeader } from '../../components/PageHeader';
import { routes } from '../../lib/routes';
import { HELP_TOPICS, helpLanguage, loadArticle } from './content/manifest';
import { Markdown } from './markdown';

/**
 * The user manual, shipped inside the product.
 *
 * The articles are markdown files under `content/{language}/`, imported one at a time, so the manual
 * costs nothing until somebody opens it and an install with no internet access still has its
 * documentation. The language follows the interface: a Spanish account reads the Spanish manual
 * without choosing anything.
 */
export function HelpPage() {
  const { t, i18n } = useTranslation();
  const { slug } = useParams<{ slug: string }>();

  const language = helpLanguage(i18n.language);
  const topic = HELP_TOPICS.find((candidate) => candidate.slug === slug) ?? HELP_TOPICS[0];

  const [article, setArticle] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;

    setArticle(null);
    setFailed(false);

    loadArticle(language, topic.slug)
      .then((text) => {
        if (cancelled) return;
        if (text === null) setFailed(true);
        else setArticle(text);
      })
      .catch(() => {
        if (!cancelled) setFailed(true);
      });

    return () => {
      cancelled = true;
    };
  }, [language, topic.slug]);

  // A long article is read top-down; arriving at one scrolled halfway down is disorienting.
  useEffect(() => {
    window.scrollTo({ top: 0 });
  }, [topic.slug]);

  return (
    <>
      <PageHeader title={t('help.title')} description={t('help.description')} icon={IconHelp} color="everdue" />

      <Grid gap="md" align="flex-start">
        <Grid.Col span={{ base: 12, sm: 5, md: 4, lg: 3 }}>
          <Card withBorder padding="xs">
            <Group gap="xs" px="xs" pb="xs">
              <ThemeIcon size="sm" radius="sm" variant="light" color="everdue">
                <IconBook size={14} />
              </ThemeIcon>
              <Text size="xs" tt="uppercase" fw={700} c="dimmed">
                {t('help.topics')}
              </Text>
            </Group>

            <ScrollArea.Autosize mah={{ base: 'none', sm: 'calc(100vh - 220px)' }} type="auto">
              {HELP_TOPICS.map((entry) => {
                const TopicIcon = entry.icon;

                return (
                  <NavLink
                    key={entry.slug}
                    component={Link}
                    to={routes.helpTopic(entry.slug)}
                    label={entry.title[language]}
                    leftSection={<TopicIcon size={18} />}
                    active={entry.slug === topic.slug}
                    variant="light"
                    color="everdue"
                  />
                );
              })}
            </ScrollArea.Autosize>
          </Card>
        </Grid.Col>

        <Grid.Col span={{ base: 12, sm: 7, md: 8, lg: 9 }}>
          <Card withBorder padding="lg">
            {failed && (
              <Alert color="orange" icon={<IconAlertTriangle size={16} />}>
                {t('help.unavailable')}
              </Alert>
            )}

            {!failed && article === null && <Loader size="sm" />}

            {article !== null && <Markdown source={article} />}
          </Card>
        </Grid.Col>
      </Grid>
    </>
  );
}

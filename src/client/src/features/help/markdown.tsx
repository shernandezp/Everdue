import { Anchor, Blockquote, Code, Divider, List, Table, Text, Title } from '@mantine/core';
import { Fragment, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { routes } from '../../lib/routes';

/**
 * The manual's markdown, rendered as Mantine components.
 *
 * Hand-written rather than a dependency for two reasons. It emits **React elements, never HTML**, so
 * nothing is ever injected with `dangerouslySetInnerHTML` and the Content-Security-Policy posture is
 * untouched; and the whole feature is one screen reading files we write ourselves, which is not worth
 * 40 kB of general-purpose parser plus its plugin chain.
 *
 * It therefore supports exactly what the manual uses — headings, paragraphs, bold, italic, inline
 * code, links, bullet and numbered lists, blockquotes, fenced code, tables and rules — and nothing
 * else. If an article needs something outside that list, extend this file rather than reaching for
 * raw HTML, which the renderer deliberately does not understand.
 */
export function Markdown({ source }: { source: string }) {
  return <>{parse(source.split(/\r?\n/))}</>;
}

// ── Block level ───────────────────────────────────────────────────────────────────────────────────

function parse(lines: string[]): ReactNode[] {
  const blocks: ReactNode[] = [];
  let index = 0;
  let key = 0;

  const next = () => key++;

  while (index < lines.length) {
    const line = lines[index];

    if (line.trim() === '') {
      index += 1;
      continue;
    }

    // Fenced code.
    if (line.startsWith('```')) {
      const body: string[] = [];
      index += 1;
      while (index < lines.length && !lines[index].startsWith('```')) {
        body.push(lines[index]);
        index += 1;
      }
      index += 1; // the closing fence

      blocks.push(
        <Code key={next()} block my="md">
          {body.join('\n')}
        </Code>,
      );
      continue;
    }

    // Horizontal rule.
    if (/^(-{3,}|\*{3,})$/.test(line.trim())) {
      blocks.push(<Divider key={next()} my="lg" />);
      index += 1;
      continue;
    }

    // Heading.
    const heading = line.match(/^(#{1,4})\s+(.*)$/);
    if (heading) {
      const order = heading[1].length as 1 | 2 | 3 | 4;
      blocks.push(
        <Title key={next()} order={order} mt={order === 1 ? 0 : 'xl'} mb="sm">
          {inline(heading[2])}
        </Title>,
      );
      index += 1;
      continue;
    }

    // Table: a run of lines starting with a pipe, whose second line is the alignment row.
    if (line.startsWith('|') && /^\|[\s:|-]+\|$/.test(lines[index + 1] ?? '')) {
      const head = cells(line);
      const rows: string[][] = [];
      index += 2;

      while (index < lines.length && lines[index].startsWith('|')) {
        rows.push(cells(lines[index]));
        index += 1;
      }

      blocks.push(
        <Table.ScrollContainer key={next()} minWidth={420} my="md">
          <Table striped withTableBorder highlightOnHover>
            <Table.Thead>
              <Table.Tr>
                {head.map((cell, column) => (
                  <Table.Th key={column}>{inline(cell)}</Table.Th>
                ))}
              </Table.Tr>
            </Table.Thead>
            <Table.Tbody>
              {rows.map((row, rowIndex) => (
                <Table.Tr key={rowIndex}>
                  {row.map((cell, column) => (
                    <Table.Td key={column}>{inline(cell)}</Table.Td>
                  ))}
                </Table.Tr>
              ))}
            </Table.Tbody>
          </Table>
        </Table.ScrollContainer>,
      );
      continue;
    }

    // Blockquote.
    if (line.startsWith('>')) {
      const body: string[] = [];
      while (index < lines.length && lines[index].startsWith('>')) {
        body.push(lines[index].replace(/^>\s?/, ''));
        index += 1;
      }

      blocks.push(
        <Blockquote key={next()} color="everdue" my="md" p="md">
          <Text size="sm">{inline(body.join(' '))}</Text>
        </Blockquote>,
      );
      continue;
    }

    // Lists. A wrapped continuation line is indented, and joins the item above it.
    const bullet = line.match(/^[-*]\s+(.*)$/);
    const ordered = line.match(/^\d+\.\s+(.*)$/);

    if (bullet || ordered) {
      const items: string[] = [];
      const isOrdered = Boolean(ordered);

      while (index < lines.length) {
        const current = lines[index];
        const item = isOrdered ? current.match(/^\d+\.\s+(.*)$/) : current.match(/^[-*]\s+(.*)$/);

        if (item) {
          items.push(item[1]);
          index += 1;
          continue;
        }

        if (/^\s+\S/.test(current) && items.length > 0) {
          items[items.length - 1] += ` ${current.trim()}`;
          index += 1;
          continue;
        }

        break;
      }

      blocks.push(
        <List key={next()} type={isOrdered ? 'ordered' : 'unordered'} spacing="xs" my="md" withPadding>
          {items.map((item, itemIndex) => (
            <List.Item key={itemIndex}>{inline(item)}</List.Item>
          ))}
        </List>,
      );
      continue;
    }

    // Paragraph: everything up to the next blank line or block opener.
    const paragraph: string[] = [];
    while (index < lines.length && lines[index].trim() !== '' && !opensBlock(lines[index])) {
      paragraph.push(lines[index].trim());
      index += 1;
    }

    blocks.push(
      <Text key={next()} my="md">
        {inline(paragraph.join(' '))}
      </Text>,
    );
  }

  return blocks;
}

function opensBlock(line: string): boolean {
  return (
    line.startsWith('#') ||
    line.startsWith('>') ||
    line.startsWith('|') ||
    line.startsWith('```') ||
    /^[-*]\s+/.test(line) ||
    /^\d+\.\s+/.test(line) ||
    /^(-{3,}|\*{3,})$/.test(line.trim())
  );
}

function cells(row: string): string[] {
  return row
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map((cell) => cell.trim());
}

// ── Inline level ──────────────────────────────────────────────────────────────────────────────────

/** `code` first, so nothing inside a code span is mistaken for emphasis or a link. */
const INLINE = /(`[^`]+`)|(\[[^\]]+\]\([^)]+\))|(\*\*[^*]+\*\*)|(\*[^*]+\*)|(_[^_]+_)/;

function inline(text: string, depth = 0): ReactNode {
  const match = INLINE.exec(text);

  // Depth is a guard, not a feature: emphasis inside emphasis is not something the manual does.
  if (!match || depth > 4) return text;

  const before = text.slice(0, match.index);
  const after = text.slice(match.index + match[0].length);
  const token = match[0];

  let element: ReactNode;

  if (token.startsWith('`')) {
    element = <Code>{token.slice(1, -1)}</Code>;
  } else if (token.startsWith('[')) {
    const [, label, href] = token.match(/^\[([^\]]+)\]\(([^)]+)\)$/)!;
    element = <MarkdownLink href={href}>{inline(label, depth + 1)}</MarkdownLink>;
  } else if (token.startsWith('**')) {
    element = <strong>{inline(token.slice(2, -2), depth + 1)}</strong>;
  } else {
    element = <em>{inline(token.slice(1, -1), depth + 1)}</em>;
  }

  return (
    <>
      {before}
      {element}
      <Fragment>{inline(after, depth)}</Fragment>
    </>
  );
}

/**
 * A link in an article is either external, or the slug of another article — the manual cross-links
 * itself by file name (`[Responsibilities](07-responsibilities)`), so an author never has to know the
 * route. Any anchor is dropped: the renderer emits no heading ids to jump to.
 */
function MarkdownLink({ href, children }: { href: string; children: ReactNode }) {
  if (/^(https?:|mailto:)/i.test(href)) {
    return (
      <Anchor href={href} target="_blank" rel="noreferrer noopener">
        {children}
      </Anchor>
    );
  }

  const slug = href.replace(/^\.\//, '').replace(/\.md$/, '').split('#')[0];

  if (slug === '') return <>{children}</>;

  return (
    <Anchor component={Link} to={routes.helpTopic(slug)}>
      {children}
    </Anchor>
  );
}

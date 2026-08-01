import { MantineProvider } from '@mantine/core';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { HELP_TOPICS, helpLanguage, loadArticle } from './content/manifest';
import { HelpPage } from './HelpPage';
import { Markdown } from './markdown';

function wrap(node: React.ReactNode, path = '/help') {
  return render(
    <MantineProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/help" element={node} />
          <Route path="/help/:slug" element={node} />
        </Routes>
      </MemoryRouter>
    </MantineProvider>,
  );
}

describe('the markdown renderer', () => {
  /** It emits React elements, never HTML — which is what keeps the CSP posture untouched. */
  it('renders the constructs the manual actually uses', () => {
    wrap(
      <Markdown
        source={[
          '# Title',
          '',
          'A paragraph with **bold**, *italic* and `code`.',
          '',
          '| Column | Meaning |',
          '| --- | --- |',
          '| Open | Waiting for you |',
          '',
          '- first bullet',
          '- second bullet',
          '',
          '1. first step',
          '2. second step',
          '',
          '> A quoted aside.',
        ].join('\n')}
      />,
    );

    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Title');
    expect(screen.getByText('bold').tagName).toBe('STRONG');
    expect(screen.getByText('italic').tagName).toBe('EM');
    expect(screen.getByText('Waiting for you')).toBeInTheDocument();
    expect(screen.getByText('second bullet')).toBeInTheDocument();
    expect(screen.getByText('second step')).toBeInTheDocument();
    expect(screen.getByText('A quoted aside.')).toBeInTheDocument();
  });

  /** Articles cross-link by file name, so an author never has to know the route. */
  it('turns a bare slug into a link to that topic, and leaves an external link alone', () => {
    wrap(<Markdown source={'See [responsibilities](07-responsibilities) and [the site](https://example.com).'} />);

    expect(screen.getByRole('link', { name: 'responsibilities' })).toHaveAttribute(
      'href',
      '/help/07-responsibilities',
    );

    const external = screen.getByRole('link', { name: 'the site' });
    expect(external).toHaveAttribute('href', 'https://example.com');
    expect(external).toHaveAttribute('target', '_blank');
  });
});

describe('the manual', () => {
  it('has every topic in both languages', async () => {
    for (const topic of HELP_TOPICS) {
      for (const language of ['en', 'es'] as const) {
        const article = await loadArticle(language, topic.slug);

        expect(article, `${language}/${topic.slug}.md`).not.toBeNull();
        expect(article!.startsWith('# '), `${language}/${topic.slug}.md starts with a title`).toBe(true);
      }
    }
  });

  it('follows the interface language rather than asking', () => {
    expect(helpLanguage('en')).toBe('en');
    expect(helpLanguage('en-GB')).toBe('en');
    expect(helpLanguage('es')).toBe('es');
    expect(helpLanguage(undefined)).toBe('es');
  });
});

describe('HelpPage', () => {
  it('opens the first topic when no slug is given', async () => {
    wrap(<HelpPage />);

    expect(await screen.findByRole('heading', { level: 1, name: /getting started/i })).toBeInTheDocument();
  });

  it('opens the topic in the URL', async () => {
    wrap(<HelpPage />, '/help/07-responsibilities');

    await waitFor(() =>
      expect(screen.getByRole('heading', { level: 1, name: /responsibilities/i })).toBeInTheDocument(),
    );
  });
});

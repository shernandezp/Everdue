import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';
import i18n from '../i18n';

// Mantine components read these; jsdom implements neither.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  }),
});

Object.defineProperty(window, 'ResizeObserver', {
  writable: true,
  value: class {
    observe() {}
    unobserve() {}
    disconnect() {}
  },
});

// Mantine's autosizing Textarea re-measures when webfonts finish loading; jsdom has no font manager.
Object.defineProperty(document, 'fonts', {
  writable: true,
  value: {
    ready: Promise.resolve(),
    addEventListener: () => {},
    removeEventListener: () => {},
  },
});

window.HTMLElement.prototype.scrollIntoView = () => {};

// jsdom has no layout, so it implements neither of these and warns loudly on every call.
window.scrollTo = () => {};

// Assertions read English labels; the Spanish half is covered by the i18n key-parity check.
void i18n.changeLanguage('en');

afterEach(cleanup);

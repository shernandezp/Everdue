import { describe, expect, it } from 'vitest';
import { activeNav, NAV_SECTIONS } from './navigation';
import { isSettingsTab, routes, settingsTabLink } from './routes';

/**
 * The administration section was deliberately shrunk to six entries: the integrator surfaces —
 * custom fields, API keys, webhooks — are tabs of Settings, not navigation destinations. This pins
 * that shape so they do not quietly grow back, and pins the redirect targets the old routes use.
 */
describe('admin navigation', () => {
  const admin = NAV_SECTIONS.find((section) => section.id === 'admin')!;

  it('offers exactly the six admin destinations', () => {
    expect(admin.items.map((item) => item.to)).toEqual([
      routes.responsibilities,
      routes.departments,
      routes.users,
      routes.import,
      routes.settings,
      routes.channels,
    ]);
  });

  it('still resolves notification channels ahead of settings, its path prefix', () => {
    expect(activeNav(routes.channels)?.to).toBe(routes.channels);
    expect(activeNav(routes.settings)?.to).toBe(routes.settings);
  });

  it('files the old integrator paths under settings, where their redirects land', () => {
    for (const legacy of [routes.entityFields, routes.apiKeys, routes.webhooks]) {
      expect(activeNav(legacy)?.to).toBe(routes.settings);
    }
  });
});

describe('settings tab links', () => {
  it('keeps the default tab out of the URL and names the rest', () => {
    expect(settingsTabLink('general')).toBe('/settings');
    expect(settingsTabLink('custom-fields')).toBe('/settings?tab=custom-fields');
    expect(settingsTabLink('api-keys')).toBe('/settings?tab=api-keys');
    expect(settingsTabLink('webhooks')).toBe('/settings?tab=webhooks');
  });

  it('treats anything unrecognized as not a tab, so the page falls back to general', () => {
    expect(isSettingsTab('webhooks')).toBe(true);
    expect(isSettingsTab('channels')).toBe(false);
    expect(isSettingsTab(null)).toBe(false);
  });
});

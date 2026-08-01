import { Center, Loader } from '@mantine/core';
import { lazy, Suspense, type ComponentType, type ReactElement } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';

import { AppLayout } from './components/AppLayout';
import { routePatterns, routes, settingsTabLink } from './lib/routes';
import { useSession } from './features/auth/session';
import { LoginPage } from './features/auth/LoginPage';
import { ChangePasswordPage } from './features/auth/ChangePasswordPage';
import { ExternalLoginCompletePage } from './features/auth/ExternalLoginCompletePage';
import { ProfilePage } from './features/auth/ProfilePage';
import { BoardPage } from './features/board/BoardPage';
import { WorkListPage } from './features/workitems/WorkListPage';
import { EntitiesPage } from './features/entities/EntitiesPage';

/** Named-export module → lazy route component. */
function lazyPage<T>(load: () => Promise<T>, pick: (module: T) => ComponentType) {
  return lazy(() => load().then((module) => ({ default: pick(module) })));
}

/*
 * The screens staff open every day ship in the entry bundle; everything else loads on first visit.
 * The practical effect is that the chart library and the admin surfaces stop weighing down the
 * board on a phone — the entry chunk is what a deskless user actually downloads.
 */
const DashboardPage = lazyPage(() => import('./features/reports/DashboardPage'), (m) => m.DashboardPage);
const EntityHealthPage = lazyPage(() => import('./features/reports/EntityHealthPage'), (m) => m.EntityHealthPage);
const NeglectPage = lazyPage(() => import('./features/reports/NeglectPage'), (m) => m.NeglectPage);
const BlockedByEntityPage = lazyPage(() => import('./features/reports/BlockedByEntityPage'), (m) => m.BlockedByEntityPage);
const EntityTimelinePage = lazyPage(() => import('./features/reports/EntityTimelinePage'), (m) => m.EntityTimelinePage);
const CompliancePage = lazyPage(() => import('./features/insights/CompliancePage'), (m) => m.CompliancePage);
const ConcentrationPage = lazyPage(() => import('./features/insights/ConcentrationPage'), (m) => m.ConcentrationPage);
const HoldAgingPage = lazyPage(() => import('./features/insights/HoldAgingPage'), (m) => m.HoldAgingPage);
const ReliabilityPage = lazyPage(() => import('./features/insights/ReliabilityPage'), (m) => m.ReliabilityPage);
const ResponsibilityCompliancePage = lazyPage(
  () => import('./features/insights/ResponsibilityCompliancePage'),
  (m) => m.ResponsibilityCompliancePage,
);
const ResponsibilitiesPage = lazyPage(
  () => import('./features/responsibilities/ResponsibilitiesPage'),
  (m) => m.ResponsibilitiesPage,
);
const DepartmentsPage = lazyPage(() => import('./features/entities/DepartmentsPage'), (m) => m.DepartmentsPage);
const UsersPage = lazyPage(() => import('./features/admin/UsersPage'), (m) => m.UsersPage);
const SettingsPage = lazyPage(() => import('./features/admin/SettingsPage'), (m) => m.SettingsPage);
const ChannelsPage = lazyPage(() => import('./features/admin/channels/ChannelsPage'), (m) => m.ChannelsPage);
const ImportPage = lazyPage(() => import('./features/imports/ImportPage'), (m) => m.ImportPage);
const HelpPage = lazyPage(() => import('./features/help/HelpPage'), (m) => m.HelpPage);

function Loading() {
  return (
    <Center h="100vh">
      <Loader />
    </Center>
  );
}

/**
 * Route gating mirrors the server's: it is a convenience, not a security boundary — every one of
 * these endpoints refuses the same requests on its own.
 */
function Protected({ children, adminOnly = false }: { children: ReactElement; adminOnly?: boolean }) {
  const { user, isLoading, isAdmin, mustChangePassword } = useSession();
  const location = useLocation();

  if (isLoading) return <Loading />;
  if (!user) return <Navigate to={routes.login} state={{ from: location.pathname }} replace />;

  // A forced password change blocks everything else, exactly as the API does.
  if (mustChangePassword) return <Navigate to={routes.changePassword} replace />;

  if (adminOnly && !isAdmin) return <Navigate to={routes.board} replace />;

  return <AppLayout>{children}</AppLayout>;
}

export function App() {
  const { user, isLoading, mustChangePassword } = useSession();

  if (isLoading) return <Loading />;

  return (
    <Suspense fallback={<Loading />}>
      <Routes>
      <Route path={routes.login} element={user ? <Navigate to={routes.board} replace /> : <LoginPage />} />

      {/* Where an external sign-in lands. See the page for why it is not the board directly. */}
      <Route path={routes.loginComplete} element={<ExternalLoginCompletePage />} />

      <Route
        path={routes.changePassword}
        element={!user ? <Navigate to={routes.login} replace /> : <ChangePasswordPage forced={mustChangePassword} />}
      />

      <Route path={routes.board} element={<Protected><BoardPage /></Protected>} />
      <Route path={routes.work} element={<Protected><WorkListPage /></Protected>} />
      <Route path={routes.entities} element={<Protected><EntitiesPage /></Protected>} />
      <Route path={routePatterns.entityTimeline} element={<Protected><EntityTimelinePage /></Protected>} />
      <Route path={routes.profile} element={<Protected><ProfilePage /></Protected>} />

      <Route path={routes.dashboard} element={<Protected adminOnly><DashboardPage /></Protected>} />
      <Route path={routes.entityHealth} element={<Protected adminOnly><EntityHealthPage /></Protected>} />
      <Route path={routes.neglect} element={<Protected adminOnly><NeglectPage /></Protected>} />
      <Route path={routes.blocked} element={<Protected adminOnly><BlockedByEntityPage /></Protected>} />

      {/* Insights. Administrators only — per-person numbers are management information, not a scoreboard. */}
      <Route path={routes.compliance} element={<Protected adminOnly><CompliancePage /></Protected>} />
      <Route
        path={routePatterns.responsibilityCompliance}
        element={<Protected adminOnly><ResponsibilityCompliancePage /></Protected>}
      />
      <Route path={routes.reliability} element={<Protected adminOnly><ReliabilityPage /></Protected>} />
      <Route path={routes.concentration} element={<Protected adminOnly><ConcentrationPage /></Protected>} />
      <Route path={routes.holdAging} element={<Protected adminOnly><HoldAgingPage /></Protected>} />

      <Route path={routes.responsibilities} element={<Protected adminOnly><ResponsibilitiesPage /></Protected>} />
      <Route path={routes.departments} element={<Protected adminOnly><DepartmentsPage /></Protected>} />
      <Route path={routes.users} element={<Protected adminOnly><UsersPage /></Protected>} />
      <Route path={routes.settings} element={<Protected adminOnly><SettingsPage /></Protected>} />
      <Route path={routes.channels} element={<Protected adminOnly><ChannelsPage /></Protected>} />

      {/* The import wizard. */}
      <Route path={routes.import} element={<Protected adminOnly><ImportPage /></Protected>} />

      {/* The integrator screens are Settings tabs now; their old paths keep working as redirects. */}
      <Route path={routes.entityFields} element={<Navigate to={settingsTabLink('custom-fields')} replace />} />
      <Route path={routes.apiKeys} element={<Navigate to={settingsTabLink('api-keys')} replace />} />
      <Route path={routes.webhooks} element={<Navigate to={settingsTabLink('webhooks')} replace />} />

      {/* The manual. Open to anybody signed in — a member needs it more than an administrator does. */}
      <Route path={routes.help} element={<Protected><HelpPage /></Protected>} />
      <Route path={routePatterns.helpTopic} element={<Protected><HelpPage /></Protected>} />

      <Route path={routePatterns.catchAll} element={<Navigate to={routes.board} replace />} />
      </Routes>
    </Suspense>
  );
}

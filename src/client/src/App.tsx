import { Center, Loader } from '@mantine/core';
import type { ReactElement } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';

import { AppLayout } from './components/AppLayout';
import { routePatterns, routes } from './lib/routes';
import { useSession } from './features/auth/session';
import { LoginPage } from './features/auth/LoginPage';
import { ChangePasswordPage } from './features/auth/ChangePasswordPage';
import { ExternalLoginCompletePage } from './features/auth/ExternalLoginCompletePage';
import { ProfilePage } from './features/auth/ProfilePage';
import { BoardPage } from './features/board/BoardPage';
import { WorkListPage } from './features/workitems/WorkListPage';
import { DashboardPage } from './features/reports/DashboardPage';
import { EntityHealthPage } from './features/reports/EntityHealthPage';
import { NeglectPage } from './features/reports/NeglectPage';
import { BlockedByEntityPage } from './features/reports/BlockedByEntityPage';
import { EntityTimelinePage } from './features/reports/EntityTimelinePage';
import { CompliancePage } from './features/insights/CompliancePage';
import { ConcentrationPage } from './features/insights/ConcentrationPage';
import { HoldAgingPage } from './features/insights/HoldAgingPage';
import { ReliabilityPage } from './features/insights/ReliabilityPage';
import { ResponsibilityCompliancePage } from './features/insights/ResponsibilityCompliancePage';
import { ResponsibilitiesPage } from './features/responsibilities/ResponsibilitiesPage';
import { EntitiesPage } from './features/entities/EntitiesPage';
import { DepartmentsPage } from './features/entities/DepartmentsPage';
import { UsersPage } from './features/admin/UsersPage';
import { SettingsPage } from './features/admin/SettingsPage';
import { ChannelsPage } from './features/admin/channels/ChannelsPage';
import { ApiKeysPage } from './features/admin/apikeys/ApiKeysPage';
import { EntityFieldDefsPage } from './features/admin/entityfields/EntityFieldDefsPage';
import { WebhooksPage } from './features/admin/webhooks/WebhooksPage';
import { ImportPage } from './features/imports/ImportPage';
import { HelpPage } from './features/help/HelpPage';

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

      {/* v2.5. The import wizard, the custom-field definitions, and the two public-API screens. */}
      <Route path={routes.import} element={<Protected adminOnly><ImportPage /></Protected>} />
      <Route path={routes.entityFields} element={<Protected adminOnly><EntityFieldDefsPage /></Protected>} />
      <Route path={routes.apiKeys} element={<Protected adminOnly><ApiKeysPage /></Protected>} />
      <Route path={routes.webhooks} element={<Protected adminOnly><WebhooksPage /></Protected>} />

      {/* The manual. Open to anybody signed in — a member needs it more than an administrator does. */}
      <Route path={routes.help} element={<Protected><HelpPage /></Protected>} />
      <Route path={routePatterns.helpTopic} element={<Protected><HelpPage /></Protected>} />

      <Route path={routePatterns.catchAll} element={<Navigate to={routes.board} replace />} />
    </Routes>
  );
}

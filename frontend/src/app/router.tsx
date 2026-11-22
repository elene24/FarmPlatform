import { createBrowserRouter } from 'react-router'
import { GuestOnly, RequireAuth } from '../features/auth/RouteGuards'
import { ForgotPasswordPage } from '../pages/auth/ForgotPasswordPage'
import { LoginPage } from '../pages/auth/LoginPage'
import { RegisterPage } from '../pages/auth/RegisterPage'
import { ResetPasswordPage } from '../pages/auth/ResetPasswordPage'
import { VerifyEmailPage } from '../pages/auth/VerifyEmailPage'
import { DashboardPage } from '../pages/DashboardPage'
import { LandingPage } from '../pages/LandingPage'
import { NotFoundPage } from '../pages/NotFoundPage'
import { PlaceholderPage } from '../pages/PlaceholderPage'
import { AppLayout } from './AppLayout'

export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <LandingPage /> },
      { path: 'verify-email', element: <VerifyEmailPage /> },
      { path: 'reset-password', element: <ResetPasswordPage /> },
      { path: 'terms', element: <PlaceholderPage name="Terms and conditions" /> },

      // Only for visitors who are not signed in.
      {
        element: <GuestOnly />,
        children: [
          { path: 'login', element: <LoginPage /> },
          { path: 'register', element: <RegisterPage /> },
          { path: 'forgot-password', element: <ForgotPasswordPage /> },
        ],
      },

      // Signed-in users only. Category and profile pages are built in Phase 3.
      {
        element: <RequireAuth />,
        children: [
          { path: 'dashboard', element: <DashboardPage /> },
          { path: 'register/categories', element: <PlaceholderPage name="Category selection" /> },
          { path: 'register/picker', element: <PlaceholderPage name="Picker registration" /> },
          { path: 'register/driver', element: <PlaceholderPage name="Driver registration" /> },
          { path: 'register/orchard', element: <PlaceholderPage name="Orchard registration" /> },
          { path: 'profile', element: <PlaceholderPage name="Profile" /> },
          { path: 'settings', element: <PlaceholderPage name="Account settings" /> },
        ],
      },

      { path: '*', element: <NotFoundPage /> },
    ],
  },
])

import { Navigate, createBrowserRouter } from 'react-router'
import { GuestOnly, RequireAuth } from '../features/auth/RouteGuards'
import { CategoriesStep } from '../features/registration/CategoriesStep'
import { DoneStep } from '../features/registration/DoneStep'
import { OrchardsStep } from '../features/registration/OrchardsStep'
import { PersonalStep } from '../features/registration/PersonalStep'
import { RegistrationLayout } from '../features/registration/RegistrationLayout'
import { ReviewStep } from '../features/registration/ReviewStep'
import { VehiclesStep } from '../features/registration/VehiclesStep'
import { ForgotPasswordPage } from '../pages/auth/ForgotPasswordPage'
import { LoginPage } from '../pages/auth/LoginPage'
import { RegisterPage } from '../pages/auth/RegisterPage'
import { ResetPasswordPage } from '../pages/auth/ResetPasswordPage'
import { VerifyEmailPage } from '../pages/auth/VerifyEmailPage'
import { DashboardPage } from '../pages/DashboardPage'
import { LandingPage } from '../pages/LandingPage'
import { NotFoundPage } from '../pages/NotFoundPage'
import { PlaceholderPage } from '../pages/PlaceholderPage'
import { ProfilePage } from '../pages/ProfilePage'
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
          // Registration steps 2-5 share wizard state through RegistrationLayout.
          {
            path: 'register',
            element: <RegistrationLayout />,
            children: [
              { path: 'categories', element: <CategoriesStep /> },
              { path: 'personal', element: <PersonalStep /> },
              { path: 'picker', element: <Navigate to="/register/personal" replace /> },
              { path: 'driver', element: <VehiclesStep /> },
              { path: 'orchard', element: <OrchardsStep /> },
              { path: 'review', element: <ReviewStep /> },
              { path: 'done', element: <DoneStep /> },
            ],
          },
          { path: 'profile', element: <ProfilePage /> },
          { path: 'settings', element: <PlaceholderPage name="Account settings" /> },
        ],
      },

      { path: '*', element: <NotFoundPage /> },
    ],
  },
])

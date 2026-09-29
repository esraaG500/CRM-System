import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth-guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/auth/login-page').then(m => m.LoginPage) },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then(m => m.Shell),
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./features/dashboard/dashboard-page').then(m => m.DashboardPage) },
      { path: 'tickets', loadComponent: () => import('./features/tickets/ticket-list-page').then(m => m.TicketListPage) },
      { path: 'tickets/:ticketId', loadComponent: () => import('./features/tickets/ticket-detail-page').then(m => m.TicketDetailPage) },
      { path: 'customers', loadComponent: () => import('./features/customers/customer-list-page').then(m => m.CustomerListPage) },
      { path: 'customers/:customerId', loadComponent: () => import('./features/customers/customer-profile-page').then(m => m.CustomerProfilePage) },
    ],
  },
  { path: '**', redirectTo: '' },
];

// Typed models mirroring specs/001-support-crm-mvp/contracts/openapi.yaml.
// Replace with the generated client (npm run generate:api) once the generator is wired in CI.

export type Language = 'ar' | 'en';
export type Channel = 'Email' | 'WhatsApp' | 'Sms' | 'LiveChat' | 'WebForm' | 'Portal' | 'Phone' | 'Api';
export type TicketStatus = 'New' | 'Open' | 'InProgress' | 'PendingCustomer' | 'Resolved' | 'Closed';
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type CustomerType = 'Individual' | 'Company';

export const TICKET_STATUSES: readonly TicketStatus[] = ['New', 'Open', 'InProgress', 'PendingCustomer', 'Resolved', 'Closed'];
export const TICKET_PRIORITIES: readonly TicketPriority[] = ['Urgent', 'High', 'Medium', 'Low'];
export const CHANNELS: readonly Channel[] = ['Phone', 'Email', 'WhatsApp', 'Sms', 'LiveChat', 'WebForm', 'Portal', 'Api'];

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface UserRef { id: string; fullName: string; }
export interface NamedRef { id: string; nameEn: string; nameAr: string; }

export interface CurrentUser {
  id: string;
  fullName: string;
  email: string;
  userType: string;
  roles: string[];
  permissions: string[];
  departmentIds: string[];
  branchId: string | null;
  preferredLanguage: Language;
}

export interface AuthResult { accessToken: string; expiresIn: number; user: CurrentUser; }

export interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

// Customers ------------------------------------------------------------------

export interface Address { line1: string | null; city: string | null; country: string | null; }

export interface CustomerUpsert {
  type: CustomerType;
  name: string;
  primaryEmail: string | null;
  primaryPhone: string | null;
  address: Address | null;
  preferredLanguage: Language;
  branchId: string | null;
  erpReference: string | null;
  version: string | null;
}

export interface ContactPerson {
  id: string;
  name: string;
  email: string | null;
  phone: string | null;
  jobTitle: string | null;
  isPrimary: boolean;
}

export type ContactPersonUpsert = Omit<ContactPerson, 'id'>;

export interface Customer extends Omit<CustomerUpsert, 'version'> {
  id: string;
  referenceNumber: string;
  needsReview: boolean;
  contacts: ContactPerson[];
  createdAt: string;
  version: string;
}

export interface CustomerSummary {
  id: string;
  referenceNumber: string;
  type: CustomerType;
  name: string;
  primaryEmail: string | null;
  primaryPhone: string | null;
  openTicketCount: number;
  needsReview: boolean;
}

export interface Note { id: string; body: string; author: UserRef; createdAt: string; }

export interface TimelineItem {
  kind: 'Ticket' | 'Message' | 'Note';
  occurredAt: string;
  title: string;
  excerpt: string;
  channel: Channel | null;
  ticketId: string | null;
  actor: UserRef | null;
}

// Tickets --------------------------------------------------------------------

export interface CustomerRef { id: string; name: string; referenceNumber: string; }

export interface TicketSummary {
  id: string;
  referenceNumber: string;
  subject: string;
  status: TicketStatus;
  priority: TicketPriority;
  channel: Channel;
  customer: CustomerRef;
  assignee: UserRef | null;
  category: NamedRef | null;
  department: NamedRef | null;
  escalationLevel: number;
  firstRespondedAt: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface Ticket extends TicketSummary {
  description: string;
  contactPerson: ContactPerson | null;
  branch: NamedRef | null;
  resolvedAt: string | null;
  closedAt: string | null;
  allowedTransitions: TicketStatus[];
  version: string;
}

export interface TicketCreate {
  subject: string;
  description: string;
  customerId: string;
  contactPersonId: string | null;
  channel: Channel;
  categoryId: string;
  priority: TicketPriority;
  departmentId: string | null;
  assigneeId: string | null;
}

export interface TicketHistoryEntry {
  timestamp: string;
  user: UserRef | null;
  changeType: 'Created' | 'FieldChanged' | 'Assigned' | 'StatusChanged' | 'Escalated' | 'SlaBreached' | 'Merged' | 'Reopened';
  field: string | null;
  oldValue: string | null;
  newValue: string | null;
  reason: string | null;
}

export interface Message {
  id: string;
  ticketId: string;
  direction: 'Inbound' | 'Outbound';
  isInternal: boolean;
  channel: Channel;
  body: string;
  sender: { kind: 'Agent' | 'Customer' | 'System'; id: string | null; name: string };
  mentions: UserRef[];
  deliveryStatus: 'Pending' | 'Sent' | 'Delivered' | 'Read' | 'Failed' | null;
  deliveryError: string | null;
  createdAt: string;
}

export interface TicketListFilter {
  q?: string;
  status?: TicketStatus[];
  priority?: TicketPriority[];
  departmentId?: string;
  categoryId?: string;
  assigneeId?: string;
  customerId?: string;
  sort?: string;
  page?: number;
  pageSize?: number;
}

// Dashboard & lookups -------------------------------------------------------

export interface AgentDashboard {
  countsByStatus: Record<TicketStatus, number>;
  myOpenCount: number;
  assigned: TicketSummary[];
  awaitingFirstResponse: TicketSummary[];
  queueUnassignedCount: number;
  unassignedQueue: TicketSummary[];
  newToday: number;
  resolvedToday: number;
  team: {
    byStatus: Record<TicketStatus, number>;
    byPriority: Record<TicketPriority, number>;
    openByAgent: { agentId: string; fullName: string; openTickets: number }[];
  } | null;
}

export interface Lookups {
  departments: NamedRef[];
  branches: NamedRef[];
  categories: (NamedRef & { departmentId: string | null })[];
  agents: { id: string; fullName: string; isAvailable: boolean; departmentIds: string[] }[];
}

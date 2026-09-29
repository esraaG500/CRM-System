import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AgentDashboard, ContactPerson, ContactPersonUpsert, Customer, CustomerSummary, CustomerType, CustomerUpsert,
  Lookups, Message, Note, PagedResult, Ticket, TicketCreate, TicketHistoryEntry, TicketListFilter,
  TicketPriority, TicketStatus, TicketSummary, TimelineItem,
} from './models';

const API = '/api/v1';

function params(values: Record<string, string | number | boolean | readonly string[] | null | undefined>): HttpParams {
  let p = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value === null || value === undefined || value === '') continue;
    if (Array.isArray(value)) {
      for (const v of value) p = p.append(key, v);
    } else {
      p = p.set(key, String(value));
    }
  }
  return p;
}

/** Thin typed wrapper over the CRM REST API (contract: contracts/openapi.yaml). */
@Injectable({ providedIn: 'root' })
export class CrmApi {
  private readonly http = inject(HttpClient);

  // Customers
  searchCustomers(q: string, page: number, pageSize: number, type?: CustomerType | null): Observable<PagedResult<CustomerSummary>> {
    return this.http.get<PagedResult<CustomerSummary>>(`${API}/customers`, { params: params({ q, page, pageSize, type }) });
  }
  getCustomer(id: string): Observable<Customer> { return this.http.get<Customer>(`${API}/customers/${id}`); }
  createCustomer(body: CustomerUpsert): Observable<Customer> { return this.http.post<Customer>(`${API}/customers`, body); }
  updateCustomer(id: string, body: CustomerUpsert): Observable<Customer> { return this.http.put<Customer>(`${API}/customers/${id}`, body); }
  deleteCustomer(id: string): Observable<void> { return this.http.delete<void>(`${API}/customers/${id}`); }
  addContact(customerId: string, body: ContactPersonUpsert): Observable<ContactPerson> {
    return this.http.post<ContactPerson>(`${API}/customers/${customerId}/contacts`, body);
  }
  updateContact(customerId: string, contactId: string, body: ContactPersonUpsert): Observable<ContactPerson> {
    return this.http.put<ContactPerson>(`${API}/customers/${customerId}/contacts/${contactId}`, body);
  }
  removeContact(customerId: string, contactId: string): Observable<void> {
    return this.http.delete<void>(`${API}/customers/${customerId}/contacts/${contactId}`);
  }
  timeline(customerId: string, page: number, pageSize = 25): Observable<PagedResult<TimelineItem>> {
    return this.http.get<PagedResult<TimelineItem>>(`${API}/customers/${customerId}/timeline`, { params: params({ page, pageSize }) });
  }
  addNote(customerId: string, body: string): Observable<Note> {
    return this.http.post<Note>(`${API}/customers/${customerId}/notes`, { body });
  }

  // Tickets
  listTickets(f: TicketListFilter): Observable<PagedResult<TicketSummary>> {
    return this.http.get<PagedResult<TicketSummary>>(`${API}/tickets`, {
      params: params({ ...f, status: f.status, priority: f.priority }),
    });
  }
  getTicket(id: string): Observable<Ticket> { return this.http.get<Ticket>(`${API}/tickets/${id}`); }
  createTicket(body: TicketCreate): Observable<Ticket> { return this.http.post<Ticket>(`${API}/tickets`, body); }
  updateTicket(id: string, body: { subject?: string; categoryId?: string; priority?: TicketPriority; departmentId?: string; version: string }): Observable<Ticket> {
    return this.http.patch<Ticket>(`${API}/tickets/${id}`, body);
  }
  assign(id: string, assigneeId: string | null, version: string, departmentId?: string): Observable<Ticket> {
    return this.http.post<Ticket>(`${API}/tickets/${id}/assign`, { assigneeId, departmentId, version });
  }
  take(id: string): Observable<Ticket> { return this.http.post<Ticket>(`${API}/tickets/${id}/take`, {}); }
  changeStatus(id: string, status: TicketStatus, version: string, reason?: string): Observable<Ticket> {
    return this.http.post<Ticket>(`${API}/tickets/${id}/status`, { status, reason, version });
  }
  escalate(id: string, reason: string, version: string): Observable<Ticket> {
    return this.http.post<Ticket>(`${API}/tickets/${id}/escalate`, { reason, version });
  }
  history(id: string): Observable<TicketHistoryEntry[]> { return this.http.get<TicketHistoryEntry[]>(`${API}/tickets/${id}/history`); }
  messages(id: string): Observable<Message[]> { return this.http.get<Message[]>(`${API}/tickets/${id}/messages`); }
  reply(id: string, body: string, setStatus?: TicketStatus): Observable<Message> {
    return this.http.post<Message>(`${API}/tickets/${id}/messages`, { body, setStatus });
  }
  addInternalNote(id: string, body: string, mentionUserIds: string[]): Observable<Message> {
    return this.http.post<Message>(`${API}/tickets/${id}/notes`, { body, mentionUserIds });
  }

  // Dashboard & lookups
  dashboard(): Observable<AgentDashboard> { return this.http.get<AgentDashboard>(`${API}/dashboard/agent`); }
  lookups(): Observable<Lookups> { return this.http.get<Lookups>(`${API}/lookups`); }
}

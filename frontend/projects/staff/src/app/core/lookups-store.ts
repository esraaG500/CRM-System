import { Injectable, inject, signal } from '@angular/core';
import { Observable, of, shareReplay, tap } from 'rxjs';
import { CrmApi } from './api/crm-api';
import { Lookups } from './api/models';

/** Reference data (departments, categories, agents) loaded once per session. */
@Injectable({ providedIn: 'root' })
export class LookupsStore {
  private readonly api = inject(CrmApi);
  private readonly _lookups = signal<Lookups | null>(null);
  private request: Observable<Lookups> | null = null;

  readonly lookups = this._lookups.asReadonly();

  load(): Observable<Lookups> {
    const current = this._lookups();
    if (current) return of(current);
    this.request ??= this.api.lookups().pipe(tap(l => this._lookups.set(l)), shareReplay(1));
    return this.request;
  }

  agentName(id: string | null | undefined): string {
    return this._lookups()?.agents.find(a => a.id === id)?.fullName ?? '';
  }
}

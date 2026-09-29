import { Injectable, inject } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';

/**
 * Counts pages visited inside the app (sign-in excluded) so Back can tell whether stepping back
 * stays in the app or would leave it (for example when a ticket link was opened in a new tab).
 */
@Injectable({ providedIn: 'root' })
export class NavigationHistory {
  private visited = 0;

  constructor() {
    inject(Router).events
      .pipe(filter((e): e is NavigationEnd => e instanceof NavigationEnd))
      .subscribe(e => {
        if (!e.urlAfterRedirects.startsWith('/login')) this.visited++;
      });
  }

  /** True when there is an earlier in-app page in this tab's history. */
  get canGoBack(): boolean {
    return this.visited > 1;
  }

  /** Called after stepping back so a second Back keeps the count honest. */
  wentBack(): void {
    // Popping the current entry triggers another NavigationEnd for the previous page; remove both.
    this.visited = Math.max(0, this.visited - 2);
  }
}

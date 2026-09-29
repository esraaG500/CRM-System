import { BidiModule } from '@angular/cdk/bidi';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LanguageService } from './core/i18n/language';
import { NavigationHistory } from './core/navigation-history';

@Component({
  selector: 'crm-root',
  imports: [RouterOutlet, BidiModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // [dir] gives every Material component (and overlays opened from them) the current direction.
  template: `<div class="root" [dir]="language.direction()"><router-outlet /></div>`,
  styles: `.root { min-height: 100%; }`,
})
export class App {
  protected readonly language = inject(LanguageService);

  constructor() {
    // Start counting in-app navigations from the first page load.
    inject(NavigationHistory);
  }
}

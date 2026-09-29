import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from '../core/i18n/language';

const UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 31_536_000], ['month', 2_592_000], ['week', 604_800], ['day', 86_400], ['hour', 3_600], ['minute', 60],
];

/** "3 hours ago" / "منذ ٣ ساعات" in the current UI language. Impure so it follows language switches. */
@Pipe({ name: 'crmAgo', pure: false })
export class AgoPipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  transform(value: string | null | undefined): string {
    if (!value) return '';
    const seconds = (new Date(value).getTime() - Date.now()) / 1000;
    const format = new Intl.RelativeTimeFormat(this.language.lang(), { numeric: 'auto' });
    for (const [unit, size] of UNITS) {
      if (Math.abs(seconds) >= size) return format.format(Math.round(seconds / size), unit);
    }
    return format.format(0, 'minute');
  }
}

/** Absolute date/time in the current UI language (Gregorian calendar, Latin digits for consistency with references). */
@Pipe({ name: 'crmDate', pure: false })
export class DatePipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  transform(value: string | null | undefined, withTime = true): string {
    if (!value) return '';
    const locale = this.language.lang() === 'ar' ? 'ar-SA-u-ca-gregory-nu-latn' : 'en-GB';
    return new Intl.DateTimeFormat(locale, withTime ? { dateStyle: 'medium', timeStyle: 'short' } : { dateStyle: 'medium' })
      .format(new Date(value));
  }
}

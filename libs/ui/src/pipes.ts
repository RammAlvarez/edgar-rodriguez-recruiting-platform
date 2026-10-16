import { Pipe, type PipeTransform } from '@angular/core';
import { type DateKey, type Decimal, formatDateKey, formatMoney, formatTimestamp } from '@platform/shared';

/** `{{ line.totalNet | money }}` → "$ 1,234.56" (string-only, truncated to cents). */
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  transform(value: Decimal | null | undefined, currency = false): string {
    return formatMoney(value, { currency });
  }
}

/** `{{ period.endDate | dateKey }}` → "17/09/2026". */
@Pipe({ name: 'dateKey' })
export class DateKeyPipe implements PipeTransform {
  transform(value: DateKey | null | undefined): string {
    return formatDateKey(value);
  }
}

/** `{{ user.lastLoginAt | timestamp }}` → "16/10/2026 01:43" in America/Monterrey. */
@Pipe({ name: 'timestamp' })
export class TimestampPipe implements PipeTransform {
  transform(value: Date | null | undefined, timeZone?: string): string {
    return formatTimestamp(value, timeZone);
  }
}

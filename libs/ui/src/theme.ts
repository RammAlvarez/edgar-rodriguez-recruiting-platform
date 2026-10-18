import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, Injectable, signal } from '@angular/core';
import { MatButtonToggle, MatButtonToggleGroup } from '@angular/material/button-toggle';
import { MatIcon } from '@angular/material/icon';

export type ThemeMode = 'light' | 'dark' | 'auto';

export const THEME_MODES: readonly { readonly value: ThemeMode; readonly label: string; readonly icon: string }[] = [
  { value: 'light', label: 'Claro', icon: 'light_mode' },
  { value: 'dark', label: 'Oscuro', icon: 'dark_mode' },
  { value: 'auto', label: 'Automático', icon: 'brightness_auto' },
];

const KEY = 'platform.theme';

/**
 * Light by default; «Automático» follows the operating system. The choice is kept per device and applied as the
 * `color-scheme` of the page, which the Material system tokens (`light-dark()`) follow.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly root = inject(DOCUMENT).documentElement;
  private readonly modeSignal = signal<ThemeMode>(this.read());
  readonly mode = this.modeSignal.asReadonly();

  constructor() {
    this.apply(this.modeSignal());
  }

  set(mode: ThemeMode): void {
    this.modeSignal.set(mode);
    this.apply(mode);
    try {
      localStorage.setItem(KEY, mode);
    } catch {
      // Storage can be blocked; the choice then lasts until the page closes.
    }
  }

  private read(): ThemeMode {
    try {
      const v = localStorage.getItem(KEY);
      return v === 'dark' || v === 'auto' ? v : 'light';
    } catch {
      return 'light';
    }
  }

  private apply(mode: ThemeMode): void {
    this.root.style.colorScheme = mode === 'auto' ? 'light dark' : mode;
  }
}

/** «Tema» selector: Claro, Oscuro or Automático. */
@Component({
  selector: 'ui-theme-picker',
  imports: [MatButtonToggleGroup, MatButtonToggle, MatIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <mat-button-toggle-group aria-label="Tema" [value]="theme.mode()" (change)="theme.set($event.value)">
      @for (m of modes; track m.value) {
        <mat-button-toggle [value]="m.value">
          <mat-icon fontSet="material-symbols-outlined">{{ m.icon }}</mat-icon>
          {{ m.label }}
        </mat-button-toggle>
      }
    </mat-button-toggle-group>
  `,
})
export class ThemePicker {
  readonly theme = inject(ThemeService);
  readonly modes = THEME_MODES;
}

import { effect, inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

const STORAGE_KEY = 'mep-theme';
const DARK_CLASS = 'dark-theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _isDarkMode = signal<boolean>(false);
  private readonly _platformId = inject(PLATFORM_ID);

  readonly isDarkMode = this._isDarkMode.asReadonly();

  constructor() {
    if (isPlatformBrowser(this._platformId)) {
      const stored = localStorage.getItem(STORAGE_KEY);
      this._isDarkMode.set(stored === 'dark');
    }

    effect(() => {
      if (isPlatformBrowser(this._platformId)) {
        if (this._isDarkMode()) {
          document.body.classList.add(DARK_CLASS);
          localStorage.setItem(STORAGE_KEY, 'dark');
        } else {
          document.body.classList.remove(DARK_CLASS);
          localStorage.setItem(STORAGE_KEY, 'light');
        }
      }
    });
  }

  toggleTheme(): void {
    this._isDarkMode.set(!this._isDarkMode());
  }
}

import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

import { environment } from '../../environments/environment';
import { SupportedLocale, TRANSLATIONS, TranslationDictionary } from '../i18n/translations';

@Injectable({ providedIn: 'root' })
export class TranslationService {
  private readonly defaultLocale: SupportedLocale = environment.defaultLocale as SupportedLocale;
  private readonly supportedLocales = new Set(environment.supportedLocales as SupportedLocale[]);
  private readonly localeSubject = new BehaviorSubject<SupportedLocale>(this.normalizeLocale(this.defaultLocale));

  readonly locale$ = this.localeSubject.asObservable();

  constructor() {}

  translate(key: string, params?: Record<string, unknown>): string {
    const locale = this.localeSubject.getValue();
    const dict = this.resolveDictionary(locale);
    const template = dict[key] ?? key;

    if (!params) {
      return template;
    }

    return template.replace(/{{\s*(\w+)\s*}}/g, (match, paramKey) => {
      const value = params[paramKey];
      return value !== undefined && value !== null ? String(value) : match;
    });
  }

  setLocale(locale: string): void {
    const normalized = this.normalizeLocale(locale as SupportedLocale);
    if (normalized !== this.localeSubject.getValue()) {
      this.localeSubject.next(normalized);
    }
  }

  getCurrentLocale(): SupportedLocale {
    return this.localeSubject.getValue();
  }

  private normalizeLocale(locale: SupportedLocale): SupportedLocale {
    if (!locale) {
      return this.defaultLocale;
    }

    if (this.supportedLocales.has(locale)) {
      return locale;
    }

    const fallback = environment.supportedLocales.find((item) => item.split('-')[0] === locale.split('-')[0]);
    if (fallback && this.supportedLocales.has(fallback as SupportedLocale)) {
      return fallback as SupportedLocale;
    }

    return this.defaultLocale;
  }

  private resolveDictionary(locale: SupportedLocale): TranslationDictionary {
    return TRANSLATIONS[locale] ?? TRANSLATIONS[this.defaultLocale];
  }
}

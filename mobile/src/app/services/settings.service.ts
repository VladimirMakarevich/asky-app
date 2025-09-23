import { Injectable } from '@angular/core';
import { Preferences } from '@capacitor/preferences';
import { BehaviorSubject, Observable } from 'rxjs';

import { environment } from '../../environments/environment';
import { AppSettings, DEFAULT_SETTINGS } from '../models/settings.model';
import { AnalyticsService } from './analytics.service';
import { TranslationService } from './translation.service';

const STORAGE_KEY = 'asky_app_settings';

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly subject = new BehaviorSubject<AppSettings>(this.mergeDefaults(DEFAULT_SETTINGS));
  private readonly ready: Promise<void>;

  readonly settings$: Observable<AppSettings> = this.subject.asObservable();

  constructor(private readonly analytics: AnalyticsService, private readonly translation: TranslationService) {
    this.ready = this.restore();
    this.translation.setLocale(this.subject.getValue().locale);
  }

  async ensureReady(): Promise<void> {
    await this.ready;
  }

  get value(): AppSettings {
    return this.subject.getValue();
  }

  async update(partial: Partial<AppSettings>): Promise<void> {
    await this.ensureReady();
    const next = this.mergeDefaults({ ...this.value, ...partial });
    this.subject.next(next);
    await Preferences.set({ key: STORAGE_KEY, value: JSON.stringify(next) });
    this.analytics.setEnabled(next.analyticsEnabled);
    this.translation.setLocale(next.locale);
  }

  private async restore(): Promise<void> {
    const stored = await Preferences.get({ key: STORAGE_KEY });
    if (stored.value) {
      try {
        const parsed = JSON.parse(stored.value) as AppSettings;
        const merged = this.mergeDefaults(parsed);
        this.subject.next(merged);
        this.analytics.setEnabled(merged.analyticsEnabled);
        this.translation.setLocale(merged.locale);
        return;
      } catch (error) {
        console.warn('Unable to parse stored settings. Falling back to defaults.', error);
      }
    }

    const defaults = this.mergeDefaults(DEFAULT_SETTINGS);
    this.subject.next(defaults);
    this.analytics.setEnabled(defaults.analyticsEnabled);
    this.translation.setLocale(defaults.locale);
  }

  private mergeDefaults(settings: AppSettings): AppSettings {
    return {
      ...DEFAULT_SETTINGS,
      ...settings,
      locale: settings.locale ?? environment.defaultLocale,
      analyticsEnabled: settings.analyticsEnabled ?? environment.analytics.enabled,
      autoStart: settings.autoStart ?? environment.autoStartListening
    };
  }
}

import { Injectable } from '@angular/core';
import { FirebaseAnalytics } from '@capacitor-firebase/analytics';
import { Capacitor } from '@capacitor/core';

import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private enabled = environment.analytics.enabled;

  setEnabled(enabled: boolean): void {
    this.enabled = enabled;
    if (Capacitor.isPluginAvailable('FirebaseAnalytics')) {
      void FirebaseAnalytics.setCollectionEnabled({ enabled }).catch(() => undefined);
    }
  }

  async logEvent(name: string, params?: Record<string, unknown>): Promise<void> {
    if (!this.enabled) {
      return;
    }

    if (!Capacitor.isPluginAvailable('FirebaseAnalytics')) {
      if (!environment.production) {
        console.debug('[analytics]', name, params ?? {});
      }
      return;
    }

    await FirebaseAnalytics.logEvent({
      name,
      params: params ?? {}
    }).catch((error) => {
      if (!environment.production) {
        console.warn('Failed to log analytics event', error);
      }
    });
  }
}

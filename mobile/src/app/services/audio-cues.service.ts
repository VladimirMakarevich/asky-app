import { Injectable } from '@angular/core';
import { NativeAudio } from '@capacitor-community/native-audio';
import { Capacitor } from '@capacitor/core';

const SOUND_IDS = {
  start: 'asky_start',
  stop: 'asky_stop',
  success: 'asky_success'
} as const;

@Injectable({ providedIn: 'root' })
export class AudioCuesService {
  private initialized = false;

  async preload(): Promise<void> {
    if (this.initialized || !Capacitor.isPluginAvailable('NativeAudio')) {
      this.initialized = true;
      return;
    }

    await Promise.allSettled([
      NativeAudio.preload({ assetId: SOUND_IDS.start, assetPath: 'audio/start.wav' }),
      NativeAudio.preload({ assetId: SOUND_IDS.stop, assetPath: 'audio/stop.wav' }),
      NativeAudio.preload({ assetId: SOUND_IDS.success, assetPath: 'audio/success.wav' })
    ]);

    this.initialized = true;
  }

  async play(sound: keyof typeof SOUND_IDS): Promise<void> {
    if (!this.initialized) {
      await this.preload();
    }

    if (!Capacitor.isPluginAvailable('NativeAudio')) {
      return;
    }

    await NativeAudio.play({ assetId: SOUND_IDS[sound] }).catch(() => undefined);
  }
}

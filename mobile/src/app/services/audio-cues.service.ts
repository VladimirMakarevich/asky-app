import { Injectable } from '@angular/core';

const SOUND_FILES: Record<'start' | 'stop' | 'success', string> = {
  start: 'assets/audio/start.wav',
  stop: 'assets/audio/stop.wav',
  success: 'assets/audio/success.wav'
};

@Injectable({ providedIn: 'root' })
export class AudioCuesService {
  private initialized = false;
  private readonly sounds = new Map<keyof typeof SOUND_FILES, HTMLAudioElement>();

  async preload(): Promise<void> {
    if (this.initialized) {
      return;
    }

    Object.entries(SOUND_FILES).forEach(([key, src]) => {
      const audio = new Audio(src);
      audio.load();
      this.sounds.set(key as keyof typeof SOUND_FILES, audio);
    });

    this.initialized = true;
  }

  async play(sound: keyof typeof SOUND_FILES): Promise<void> {
    if (!this.initialized) {
      await this.preload();
    }

    const audio = this.sounds.get(sound);
    if (!audio) {
      return;
    }

    try {
      audio.currentTime = 0;
      await audio.play();
    } catch (error) {
      console.debug('Audio cue playback failed', error);
    }
  }
}

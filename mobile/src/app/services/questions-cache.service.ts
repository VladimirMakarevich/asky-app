import { Injectable } from '@angular/core';
import { Preferences } from '@capacitor/preferences';

const STORAGE_KEY = 'asky_asked_recently';
const MAX_RECENT = 50;

@Injectable({ providedIn: 'root' })
export class QuestionsCacheService {
  private asked = new Set<string>();
  private readonly ready: Promise<void>;

  constructor() {
    this.ready = this.restore();
  }

  async ensureReady(): Promise<void> {
    await this.ready;
  }

  async markAsked(text: string): Promise<void> {
    if (!text) {
      return;
    }
    await this.ensureReady();
    this.asked.delete(text);
    this.asked.add(text);
    while (this.asked.size > MAX_RECENT) {
      const first = this.asked.values().next();
      if (!first.done) {
        this.asked.delete(first.value);
      } else {
        break;
      }
    }
    await this.persist();
  }

  async unmark(text: string): Promise<void> {
    if (!text) {
      return;
    }
    await this.ensureReady();
    if (this.asked.delete(text)) {
      await this.persist();
    }
  }

  async isAsked(text: string): Promise<boolean> {
    await this.ensureReady();
    return this.asked.has(text);
  }

  async getAsked(): Promise<string[]> {
    await this.ensureReady();
    return Array.from(this.asked.values());
  }

  private async restore(): Promise<void> {
    const stored = await Preferences.get({ key: STORAGE_KEY });
    if (stored.value) {
      try {
        const parsed = JSON.parse(stored.value) as string[];
        this.asked = new Set(parsed);
      } catch (error) {
        console.warn('Failed to restore asked_recently cache', error);
        this.asked.clear();
      }
    }
  }

  private async persist(): Promise<void> {
    await Preferences.set({ key: STORAGE_KEY, value: JSON.stringify(Array.from(this.asked.values())) });
  }
}

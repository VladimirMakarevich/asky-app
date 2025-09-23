import { ChangeDetectorRef, DestroyRef, Pipe, PipeTransform } from '@angular/core';
import { Subscription } from 'rxjs';

import { TranslationService } from '../services/translation.service';

@Pipe({
  name: 'translate',
  standalone: true,
  pure: false
})
export class TranslatePipe implements PipeTransform {
  private latestValue: string | null = null;
  private key?: string;
  private params?: Record<string, unknown>;
  private subscription: Subscription;

  constructor(private readonly translation: TranslationService, cdr: ChangeDetectorRef, destroyRef: DestroyRef) {
    this.subscription = this.translation.locale$.subscribe(() => {
      this.updateValue();
      cdr.markForCheck();
    });

    destroyRef.onDestroy(() => {
      this.subscription.unsubscribe();
    });
  }

  transform(key: string, params?: Record<string, unknown>): string {
    this.key = key;
    this.params = params;
    this.updateValue();
    return this.latestValue ?? key;
  }

  private updateValue(): void {
    if (!this.key) {
      return;
    }
    this.latestValue = this.translation.translate(this.key, this.params);
  }
}

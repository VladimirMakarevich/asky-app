import { NgFor, NgIf } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import {
  IonBackButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonItem,
  IonLabel,
  IonList,
  IonSelect,
  IonSelectOption,
  IonTitle,
  IonToggle,
  IonToolbar
} from '@ionic/angular/standalone';
import { debounceTime } from 'rxjs/operators';

import { TranslatePipe } from '../../pipes/translate.pipe';

import { environment } from '../../../environments/environment';
import { AppSettings } from '../../models/settings.model';
import { SettingsService } from '../../services/settings.service';

type SettingsFormControls = {
  locale: FormControl<string>;
  model: FormControl<string>;
  autoStart: FormControl<boolean>;
  sendFeedback: FormControl<boolean>;
  analyticsEnabled: FormControl<boolean>;
};

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    NgFor,
    NgIf,
    ReactiveFormsModule,
    IonBackButton,
    IonButtons,
    IonContent,
    IonHeader,
    IonItem,
    IonLabel,
    IonList,
    IonSelect,
    IonSelectOption,
    IonTitle,
    IonToggle,
    IonToolbar,
    TranslatePipe
  ],
  templateUrl: './settings.page.html',
  styleUrls: ['./settings.page.scss']
})
export class SettingsPage implements OnInit {
  readonly locales = environment.supportedLocales;
  readonly models = ['general', 'detailed', 'meeting'];
  form!: FormGroup<SettingsFormControls>;

  constructor(private readonly fb: FormBuilder, private readonly settings: SettingsService) {}

  async ngOnInit(): Promise<void> {
    await this.settings.ensureReady();
    const current = this.settings.value;

    this.form = this.fb.group<SettingsFormControls>({
      locale: this.fb.nonNullable.control(current.locale),
      model: this.fb.nonNullable.control(current.model),
      autoStart: this.fb.nonNullable.control(current.autoStart),
      sendFeedback: this.fb.nonNullable.control(current.sendFeedback),
      analyticsEnabled: this.fb.nonNullable.control(current.analyticsEnabled)
    });

    this.form.valueChanges.pipe(debounceTime(150)).subscribe((value: Partial<AppSettings>) => {
      void this.settings.update(value);
    });
  }
}

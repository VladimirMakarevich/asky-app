import { AsyncPipe, NgFor, NgIf } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule
} from '@angular/forms';
import {
  IonBackButton,
  IonButton,
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

import { environment } from '../../environments/environment';
import { AppSettings } from '../../models/settings.model';
import { SettingsService } from '../../services/settings.service';

type SettingsForm = FormGroup<{
  locale: FormControl<string>;
  model: FormControl<string>;
  autoStart: FormControl<boolean>;
  sendFeedback: FormControl<boolean>;
  analyticsEnabled: FormControl<boolean>;
}>;

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    AsyncPipe,
    NgFor,
    NgIf,
    ReactiveFormsModule,
    IonBackButton,
    IonButton,
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
  ],
  templateUrl: './settings.page.html',
  styleUrls: ['./settings.page.scss']
})
export class SettingsPage implements OnInit {
  readonly locales = environment.supportedLocales;
  readonly models = ['general', 'detailed', 'meeting'];
  form!: SettingsForm;

  constructor(private readonly fb: FormBuilder, private readonly settings: SettingsService) {}

  async ngOnInit(): Promise<void> {
    await this.settings.ensureReady();
    this.form = this.fb.nonNullable.group({
      locale: this.fb.control(this.settings.value.locale),
      model: this.fb.control(this.settings.value.model),
      autoStart: this.fb.control(this.settings.value.autoStart),
      sendFeedback: this.fb.control(this.settings.value.sendFeedback),
      analyticsEnabled: this.fb.control(this.settings.value.analyticsEnabled)
    });

    this.form.valueChanges.pipe(debounceTime(150)).subscribe((value: Partial<AppSettings>) => {
      void this.settings.update(value);
    });
  }
}

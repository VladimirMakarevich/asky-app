import { AsyncPipe, NgClass, NgFor, NgIf, SlicePipe } from '@angular/common';
import { Component, OnDestroy, OnInit } from '@angular/core';
import {
  IonBadge,
  IonButton,
  IonButtons,
  IonCard,
  IonCardContent,
  IonChip,
  IonContent,
  IonFab,
  IonFabButton,
  IonHeader,
  IonIcon,
  IonItem,
  IonLabel,
  IonList,
  IonNote,
  IonProgressBar,
  IonRefresher,
  IonRefresherContent,
  IonSpinner,
  IonTitle,
  IonToolbar
} from '@ionic/angular/standalone';
import { RefresherCustomEvent } from '@ionic/angular';
import { RouterLink } from '@angular/router';
import { addIcons } from 'ionicons';
import { chatbubbleEllipses, play, settingsOutline, stop, syncOutline } from 'ionicons/icons';
import { Observable } from 'rxjs';
import { TranslatePipe } from '../../pipes/translate.pipe';

import { ListenFacadeService } from '../../services/listen-facade.service';
import { ListenViewState } from '../../state/listen.state';

addIcons({ play, stop, syncOutline, settingsOutline, chatbubbleEllipses });

@Component({
  selector: 'app-listen',
  standalone: true,
  imports: [
    AsyncPipe,
    NgClass,
    NgFor,
    NgIf,
    SlicePipe,
    IonBadge,
    IonButton,
    IonButtons,
    IonCard,
    IonCardContent,
    IonChip,
    IonContent,
    IonFab,
    IonFabButton,
    IonHeader,
    IonIcon,
    IonItem,
    IonLabel,
    IonList,
    IonNote,
    IonProgressBar,
    IonRefresher,
    IonRefresherContent,
    IonSpinner,
    IonTitle,
    IonToolbar,
    RouterLink,
    TranslatePipe
  ],
  templateUrl: './listen.page.html',
  styleUrls: ['./listen.page.scss']
})
export class ListenPage implements OnInit, OnDestroy {
  readonly state$: Observable<ListenViewState> = this.facade.viewState$;

  constructor(protected readonly facade: ListenFacadeService) {}

  async ngOnInit(): Promise<void> {
    await this.facade.initialize();
  }

  ngOnDestroy(): void {
    this.facade.clearError();
  }

  async onToggleRecording(state: ListenViewState): Promise<void> {
    if (state.recorder === 'recording' || state.recorder === 'starting') {
      await this.facade.stopListening();
    } else {
      await this.facade.startListening();
    }
  }

  async onRefresh(event: RefresherCustomEvent): Promise<void> {
    try {
      await this.facade.refreshConnection();
    } finally {
      event.detail.complete();
    }
  }

  async onGenerateQuestions(): Promise<void> {
    await this.facade.requestQuestions();
  }

  async markQuestionAsked(id: string): Promise<void> {
    await this.facade.markQuestionAsked(id);
  }

  async unmarkQuestion(id: string): Promise<void> {
    await this.facade.unmarkQuestion(id);
  }

  toggleQuestionsExpanded(): void {
    this.facade.toggleQuestionsExpanded();
  }
}

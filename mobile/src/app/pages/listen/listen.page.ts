import { AsyncPipe, NgClass, NgFor, NgIf, NgStyle, SlicePipe } from '@angular/common';
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
  IonMenuButton,
  IonNote,
  IonProgressBar,
  IonRefresher,
  IonRefresherContent,
  IonSkeletonText,
  IonSpinner,
  IonText,
  IonTitle,
  IonToolbar
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { play, stop, syncOutline, settingsOutline, chatbubbleEllipses } from 'ionicons/icons';
import { Observable } from 'rxjs';
import { RouterLink } from '@angular/router';

import { ListenFacadeService } from '../../services/listen-facade.service';
import { ListenViewState } from '../../state/listen.state';

addIcons({ play, stop, syncOutline, settingsOutline, chatbubbleEllipses });

@Component({
  selector: 'app-listen',
  standalone: true,
  imports: [
    AsyncPipe,
    DatePipe,
    NgClass,
    NgFor,
    NgIf,
    NgStyle,
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
    IonMenuButton,
    IonNote,
    IonProgressBar,
    IonRefresher,
    IonRefresherContent,
    IonSkeletonText,
    IonSpinner,
    IonText,
    IonTitle,
    IonToolbar,
    RouterLink
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

  async onRefresh(event: CustomEvent): Promise<void> {
    try {
      await this.facade.refreshConnection();
    } finally {
      event.target.complete();
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

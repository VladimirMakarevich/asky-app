import { Injectable, OnDestroy } from '@angular/core';
import { Subscription, BehaviorSubject, Observable, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { v4 as uuid } from 'uuid';

import { environment } from '../../environments/environment';
import { ErrorEvent, FinalTranscriptDto, PartialTranscriptDto, QuestionItem, QuestionItemDto, TranscriptItem } from '../models/hub.models';
import { AppSettings } from '../models/settings.model';
import { INITIAL_LISTEN_STATE, ListenViewState } from '../state/listen.state';
import { AnalyticsService } from './analytics.service';
import { AudioRecorderService } from './audio-recorder.service';
import { QuestionsCacheService } from './questions-cache.service';
import { RealtimeService } from './realtime.service';
import { SettingsService } from './settings.service';

const MAX_TRANSCRIPTS = 100;

@Injectable({ providedIn: 'root' })
export class ListenFacadeService implements OnDestroy {
  private readonly destroy$ = new Subject<void>();
  private readonly state = new BehaviorSubject<ListenViewState>(INITIAL_LISTEN_STATE);
  private framesSub?: Subscription;

  readonly viewState$: Observable<ListenViewState> = this.state.asObservable();

  constructor(
    private readonly audioRecorder: AudioRecorderService,
    private readonly realtime: RealtimeService,
    private readonly questionsCache: QuestionsCacheService,
    private readonly settings: SettingsService,
    private readonly analytics: AnalyticsService
  ) {
    this.audioRecorder.state$.pipe(takeUntil(this.destroy$)).subscribe((recorderState) => {
      this.patch({ recorder: recorderState });
    });

    this.realtime.connected$.pipe(takeUntil(this.destroy$)).subscribe((connected) => {
      this.patch({ connection: connected ? 'connected' : 'disconnected' });
    });

    this.realtime.partial$.pipe(takeUntil(this.destroy$)).subscribe((partial) => this.handlePartial(partial));
    this.realtime.final$.pipe(takeUntil(this.destroy$)).subscribe((final) => this.handleFinal(final));
    this.realtime.questions$.pipe(takeUntil(this.destroy$)).subscribe((questions) => this.handleQuestions(questions));
    this.realtime.errors$.pipe(takeUntil(this.destroy$)).subscribe((error) => this.handleError(error));
  }

  async initialize(): Promise<void> {
    await Promise.all([this.settings.ensureReady(), this.questionsCache.ensureReady()]);
    const askedRecently = await this.questionsCache.getAsked();
    this.patch({ askedRecently });

    await this.realtime.connect();

    const currentSettings = this.settings.value;
    if (currentSettings.autoStart || environment.autoStartListening) {
      await this.startListening(true);
    }
  }

  async refreshConnection(): Promise<void> {
    await this.realtime.disconnect();
    await this.realtime.connect();
  }

  async startListening(auto = false): Promise<void> {
    if (this.state.getValue().recorder !== 'idle') {
      return;
    }

    await this.realtime.connect();
    this.framesSub?.unsubscribe();
    this.framesSub = this.audioRecorder.frames$.subscribe((frame) => this.realtime.enqueueFrame(frame));
    await this.audioRecorder.start();
    this.analytics.logEvent('listen_start', { auto });
  }

  async stopListening(): Promise<void> {
    this.framesSub?.unsubscribe();
    this.framesSub = undefined;
    await this.audioRecorder.stop();
    await this.realtime.stopStream();
    this.patch({ partial: undefined });
    this.analytics.logEvent('listen_stop');
  }

  async requestQuestions(): Promise<void> {
    this.patch({ pendingQuestions: true, error: null });
    try {
      await this.realtime.requestQuestions();
      this.analytics.logEvent('questions_requested');
    } catch (error) {
      this.handleError({
        reason: 'QuestionsRequestFailed',
        details: (error as Error).message
      });
    } finally {
      this.patch({ pendingQuestions: false });
    }
  }

  async markQuestionAsked(questionId: string): Promise<void> {
    const state = this.state.getValue();
    const question = state.questions.find((q) => q.id === questionId);
    if (!question) {
      return;
    }

    await this.questionsCache.markAsked(question.text);
    const askedRecently = await this.questionsCache.getAsked();
    this.patch({ askedRecently, questions: this.flagAsked(state.questions, askedRecently) });
    this.analytics.logEvent('question_marked', { questionId });
  }

  async unmarkQuestion(questionId: string): Promise<void> {
    const state = this.state.getValue();
    const question = state.questions.find((q) => q.id === questionId);
    if (!question) {
      return;
    }

    await this.questionsCache.unmark(question.text);
    const askedRecently = await this.questionsCache.getAsked();
    this.patch({ askedRecently, questions: this.flagAsked(state.questions, askedRecently) });
    this.analytics.logEvent('question_unmarked', { questionId });
  }

  toggleQuestionsExpanded(): void {
    const current = this.state.getValue();
    this.patch({ showAllQuestions: !current.showAllQuestions });
  }

  clearError(): void {
    this.patch({ error: null });
  }

  async updateSettings(settings: Partial<AppSettings>): Promise<void> {
    await this.settings.update(settings);
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.framesSub?.unsubscribe();
  }

  private handlePartial(partial: PartialTranscriptDto): void {
    const item: TranscriptItem = {
      id: `partial-${partial.offset}`,
      text: partial.text,
      type: 'partial',
      offset: partial.offset,
      duration: partial.duration
    };

    this.patch({ partial: item });
  }

  private handleFinal(final: FinalTranscriptDto): void {
    const transcript: TranscriptItem = {
      id: uuid(),
      text: final.text,
      type: 'final',
      offset: final.offset,
      duration: final.duration,
      facts: final.facts ?? undefined
    };

    const transcripts = [...this.state.getValue().transcripts, transcript].slice(-MAX_TRANSCRIPTS);
    this.patch({ transcripts, partial: undefined });
  }

  private handleQuestions(items: QuestionItemDto[]): void {
    const askedRecently = this.state.getValue().askedRecently;
    const questions = items.map<QuestionItem>((item) => ({
      ...item,
      id: uuid(),
      askedRecently: askedRecently.includes(item.text)
    }));
    const shouldExpand = questions.length <= 3;
    this.patch({
      questions,
      pendingQuestions: false,
      showAllQuestions: shouldExpand
    });
  }

  private handleError(error: ErrorEvent): void {
    this.patch({ error });
    this.analytics.logEvent('listen_error', error);
  }

  private patch(patch: Partial<ListenViewState>): void {
    this.state.next({ ...this.state.getValue(), ...patch });
  }

  private flagAsked(questions: QuestionItem[], askedRecently: string[]): QuestionItem[] {
    const askedSet = new Set(askedRecently);
    return questions.map((item) => ({
      ...item,
      askedRecently: askedSet.has(item.text)
    }));
  }
}

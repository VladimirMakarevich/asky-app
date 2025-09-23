import { Injectable, NgZone } from '@angular/core';
import { HttpTransportType, HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject } from 'rxjs';

import { environment } from '../../environments/environment';
import {
  AudioFrameDto,
  ErrorEvent,
  FinalTranscriptDto,
  PartialTranscriptDto,
  QuestionItemDto,
  QuestionsEnvelope,
  SessionEvent
} from '../models/hub.models';
import { toBase64 } from '../utils/buffer.utils';
import { AnalyticsService } from './analytics.service';

const MAX_IN_FLIGHT_FRAMES = 6;
const MAX_BUFFER_SIZE = 256;
const RETRY_DELAY_MS = 25;

@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private connection?: HubConnection;
  private readonly connectedSubject = new BehaviorSubject<boolean>(false);
  private readonly sessionSubject = new Subject<SessionEvent>();
  private readonly partialSubject = new Subject<PartialTranscriptDto>();
  private readonly finalSubject = new Subject<FinalTranscriptDto>();
  private readonly questionsSubject = new Subject<QuestionItemDto[]>();
  private readonly errorSubject = new Subject<ErrorEvent>();
  private readonly frameQueue: AudioFrameDto[] = [];
  private flushing = false;
  private inFlight = 0;
  private readonly useMock = environment.useMockBackend;
  private readonly mockEngine?: MockRealtimeEngine;

  readonly connected$ = this.connectedSubject.asObservable();
  readonly session$ = this.sessionSubject.asObservable();
  readonly partial$ = this.partialSubject.asObservable();
  readonly final$ = this.finalSubject.asObservable();
  readonly questions$ = this.questionsSubject.asObservable();
  readonly errors$ = this.errorSubject.asObservable();

  constructor(private readonly zone: NgZone, private readonly analytics: AnalyticsService) {
    if (environment.useMockBackend) {
      this.mockEngine = new MockRealtimeEngine({
        emitSession: (payload) => this.zone.run(() => this.sessionSubject.next(payload)),
        emitPartial: (payload) => this.zone.run(() => this.partialSubject.next(payload)),
        emitFinal: (payload) => this.zone.run(() => this.finalSubject.next(payload)),
        emitQuestions: (payload) => this.zone.run(() => this.questionsSubject.next(payload)),
        emitError: (payload) => this.zone.run(() => this.errorSubject.next(payload))
      });
    }
  }

  get isConnected(): boolean {
    return this.connectedSubject.getValue();
  }

  async connect(): Promise<void> {
    if (this.useMock) {
      this.mockEngine?.connect();
      this.analytics.logEvent('connection_started', { transport: 'mock' });
      this.zone.run(() => this.connectedSubject.next(true));
      return;
    }

    if (this.connection?.state === HubConnectionState.Connected) {
      return;
    }

    if (!this.connection) {
      this.connection = new HubConnectionBuilder()
        .withUrl(`${environment.apiBaseUrl}${environment.signalRHubPath}`, {
          transport: HttpTransportType.WebSockets,
          skipNegotiation: true
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(environment.production ? LogLevel.Warning : LogLevel.Information)
        .build();

      this.registerHandlers();
    }

    if (this.connection.state === HubConnectionState.Connecting) {
      return;
    }

    await this.connection.start();
    this.analytics.logEvent('connection_started', { transport: 'websocket' });
    this.zone.run(() => {
      this.connectedSubject.next(true);
    });
    void this.flushQueue();
  }

  async disconnect(): Promise<void> {
    if (this.useMock) {
      this.mockEngine?.disconnect();
      this.analytics.logEvent('connection_closed');
      this.zone.run(() => this.connectedSubject.next(false));
      return;
    }

    if (!this.connection) {
      return;
    }

    try {
      await this.connection.stop();
    } finally {
      this.analytics.logEvent('connection_closed');
      this.zone.run(() => {
        this.connectedSubject.next(false);
      });
    }
  }

  enqueueFrame(frame: AudioFrameDto): void {
    if (this.useMock) {
      this.mockEngine?.enqueueFrame(frame);
      return;
    }
    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      return;
    }
    if (this.frameQueue.length >= MAX_BUFFER_SIZE) {
      this.frameQueue.shift();
    }
    this.frameQueue.push(frame);
    void this.flushQueue();
  }

  async stopStream(): Promise<void> {
    if (this.useMock) {
      this.mockEngine?.stop();
      this.analytics.logEvent('stop_stream');
      return;
    }

    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      return;
    }

    await this.connection.invoke('StopStream');
    this.analytics.logEvent('stop_stream');
  }

  async requestQuestions(options?: { topic?: string; preferredStyle?: string; forceRefresh?: boolean }): Promise<void> {
    if (this.useMock) {
      await this.mockEngine?.requestQuestions(options ?? {});
      this.analytics.logEvent('request_questions', options ?? {});
      return;
    }

    if (!this.connection || this.connection.state !== HubConnectionState.Connected) {
      throw new Error('Realtime connection is not established');
    }

    await this.connection.invoke('GenerateQuestions', options ?? {});
    this.analytics.logEvent('request_questions', options ?? {});
  }

  private async flushQueue(): Promise<void> {
    if (this.useMock) {
      return;
    }

    if (this.flushing || !this.connection || this.connection.state !== HubConnectionState.Connected) {
      return;
    }

    this.flushing = true;

    while (this.connection.state === HubConnectionState.Connected && this.frameQueue.length > 0) {
      if (this.inFlight >= MAX_IN_FLIGHT_FRAMES) {
        await this.delay(RETRY_DELAY_MS);
        continue;
      }

      const frame = this.frameQueue.shift();
      if (!frame) {
        break;
      }

      try {
        this.inFlight += 1;
        await this.connection.invoke('SendAudioFrame', {
          sequence: frame.sequence,
          timestamp: frame.timestamp,
          payload: toBase64(frame.payload)
        });
      } catch (error) {
        this.errorSubject.next({ reason: 'SendAudioFrameFailed', details: (error as Error).message });
        this.frameQueue.unshift(frame);
        await this.delay(RETRY_DELAY_MS);
      } finally {
        this.inFlight = Math.max(0, this.inFlight - 1);
      }
    }

    this.flushing = false;

    if (this.frameQueue.length > 0 && this.connection?.state === HubConnectionState.Connected) {
      void this.flushQueue();
    }
  }

  private registerHandlers(): void {
    if (!this.connection) {
      return;
    }

    this.connection.on('Session', (payload: SessionEvent) => {
      this.zone.run(() => {
        this.sessionSubject.next(payload);
      });
    });

    this.connection.on('Partial', (payload: PartialTranscriptDto) => {
      this.zone.run(() => this.partialSubject.next(payload));
    });

    this.connection.on('Final', (payload: FinalTranscriptDto) => {
      this.zone.run(() => this.finalSubject.next(payload));
    });

    this.connection.on('Questions', (payload: QuestionsEnvelope) => {
      const items = payload?.data ?? [];
      this.zone.run(() => this.questionsSubject.next(items));
    });

    this.connection.on('Error', (payload: ErrorEvent) => {
      this.zone.run(() => this.errorSubject.next(payload));
    });

    this.connection.onclose(() => {
      this.analytics.logEvent('connection_lost');
      this.zone.run(() => {
        this.connectedSubject.next(false);
      });
    });

    this.connection.onreconnected(() => {
      this.analytics.logEvent('connection_resumed');
      this.zone.run(() => {
        this.connectedSubject.next(true);
      });
      void this.flushQueue();
    });
  }

  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}

interface MockEmitter {
  emitSession(payload: SessionEvent): void;
  emitPartial(payload: PartialTranscriptDto): void;
  emitFinal(payload: FinalTranscriptDto): void;
  emitQuestions(payload: QuestionItemDto[]): void;
  emitError(payload: ErrorEvent): void;
}

class MockRealtimeEngine {
  private connected = false;
  private frameCounter = 0;

  constructor(private readonly emitter: MockEmitter) {}

  connect(): void {
    if (this.connected) {
      return;
    }
    this.connected = true;
    setTimeout(() => {
      this.emitter.emitSession({ state: 'started' });
    }, 50);
  }

  disconnect(): void {
    this.connected = false;
    this.frameCounter = 0;
  }

  enqueueFrame(_frame: AudioFrameDto): void {
    if (!this.connected) {
      return;
    }
    this.frameCounter += 1;
    if (this.frameCounter % 4 === 0) {
      this.emitter.emitPartial({
        text: `Черновой транскрипт #${this.frameCounter / 4}`,
        offset: this.frameCounter * 200,
        duration: 2000
      });
    }

    if (this.frameCounter % 8 === 0) {
      const index = this.frameCounter / 8;
      this.emitter.emitFinal({
        text: `Финальная реплика #${index}`,
        offset: this.frameCounter * 200,
        duration: 2500,
        facts: index % 2 === 0 ? ['highlight', 'next-step'] : undefined
      });
    }
  }

  stop(): void {
    this.frameCounter = 0;
  }

  async requestQuestions(_options: { topic?: string; preferredStyle?: string; forceRefresh?: boolean }): Promise<void> {
    if (!this.connected) {
      throw new Error('Mock connection is not established');
    }

    await new Promise((resolve) => setTimeout(resolve, 150));
    this.emitter.emitQuestions([
      { text: 'Что самое важное из услышанного?', tags: ['insight'] },
      { text: 'Какие действия запланированы дальше?', tags: ['action'] },
      { text: 'Нужна ли кому-то поддержка?', tags: ['support'] },
      { text: 'Какие риски стоит учесть?', tags: ['risk'] }
    ]);
  }
}

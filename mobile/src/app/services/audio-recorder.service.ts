import { Injectable, NgZone } from '@angular/core';
import { BehaviorSubject, Observable, Subject } from 'rxjs';

import { AudioFrameDto } from '../models/hub.models';
import { AUDIO_CONSTANTS, createFrameAccumulator, downsampleTo16k, getTimestamp, mixDownToMono } from '../utils/audio.utils';
import { environment } from '../../environments/environment';
import { PermissionsService } from './permissions.service';
import { AudioCuesService } from './audio-cues.service';

export type RecorderState = 'idle' | 'starting' | 'recording';

@Injectable({ providedIn: 'root' })
export class AudioRecorderService {
  private audioContext?: AudioContext;
  private processor?: ScriptProcessorNode;
  private source?: MediaStreamAudioSourceNode;
  private sink?: GainNode;
  private stream?: MediaStream;
  private mockTimer?: ReturnType<typeof setInterval>;
  private sequence = 0;
  private startedAt = 0;
  private readonly frameAccumulator = createFrameAccumulator(AUDIO_CONSTANTS.frameSamples);
  private readonly framesSubject = new Subject<AudioFrameDto>();
  private readonly stateSubject = new BehaviorSubject<RecorderState>('idle');

  readonly frames$: Observable<AudioFrameDto> = this.framesSubject.asObservable();
  readonly state$: Observable<RecorderState> = this.stateSubject.asObservable();

  constructor(
    private readonly permissions: PermissionsService,
    private readonly audioCues: AudioCuesService,
    private readonly zone: NgZone
  ) {}

  get state(): RecorderState {
    return this.stateSubject.getValue();
  }

  async start(): Promise<void> {
    if (this.state !== 'idle') {
      return;
    }

    this.stateSubject.next('starting');

    if (environment.useMockBackend) {
      this.startedAt = getTimestamp();
      this.sequence = 0;
      this.stateSubject.next('recording');
      this.mockTimer = setInterval(() => {
        const payload = new ArrayBuffer(AUDIO_CONSTANTS.frameSamples * 2);
        this.framesSubject.next({
          sequence: this.sequence++,
          timestamp: getTimestamp() - this.startedAt,
          payload
        });
      }, AUDIO_CONSTANTS.frameDurationMs);
      await this.audioCues.play('start');
      return;
    }

    const granted = await this.permissions.ensureMicrophonePermission();
    if (!granted) {
      this.stateSubject.next('idle');
      throw new Error('Microphone permission is required to start listening.');
    }

    try {
      this.audioContext = new AudioContext({ sampleRate: 48000 });
      await this.audioContext.resume();

      this.stream = await navigator.mediaDevices.getUserMedia({
        audio: {
          channelCount: 1,
          sampleRate: this.audioContext.sampleRate,
          noiseSuppression: true,
          echoCancellation: true
        },
        video: false
      });

      this.source = this.audioContext.createMediaStreamSource(this.stream);
      this.processor = this.audioContext.createScriptProcessor(4096, this.source.channelCount, 1);
      this.sink = this.audioContext.createGain();
      this.sink.gain.value = 0;

      this.processor.onaudioprocess = (event) => {
        this.zone.runOutsideAngular(() => {
          if (this.state !== 'recording') {
            return;
          }

          const channels: Float32Array[] = [];
          for (let i = 0; i < event.inputBuffer.numberOfChannels; i += 1) {
            channels.push(event.inputBuffer.getChannelData(i).slice());
          }

          const mono = mixDownToMono(channels);
          const downsampled = downsampleTo16k(mono, this.audioContext!.sampleRate);
          const frames = this.frameAccumulator.append(downsampled);

          for (const frame of frames) {
            const timestamp = getTimestamp() - this.startedAt;
            this.framesSubject.next({
              sequence: this.sequence,
              timestamp,
              payload: frame.payload
            });
            this.sequence += 1;
          }
        });
      };

      this.source.connect(this.processor);
      this.processor.connect(this.sink);
      this.sink.connect(this.audioContext.destination);

      this.frameAccumulator.reset();
      this.sequence = 0;
      this.startedAt = getTimestamp();
      this.stateSubject.next('recording');

      await this.audioCues.play('start');
    } catch (error) {
      await this.stop();
      throw error;
    }
  }

  async stop(): Promise<void> {
    if (this.state === 'idle') {
      return;
    }

    this.stateSubject.next('idle');
    this.frameAccumulator.reset();

    if (this.mockTimer) {
      clearInterval(this.mockTimer);
      this.mockTimer = undefined;
    }

    if (this.processor) {
      this.processor.disconnect();
      this.processor.onaudioprocess = null;
      this.processor = undefined;
    }

    if (this.source) {
      this.source.disconnect();
      this.source = undefined;
    }

    if (this.sink) {
      this.sink.disconnect();
      this.sink = undefined;
    }

    if (this.stream) {
      this.stream.getTracks().forEach((track) => track.stop());
      this.stream = undefined;
    }

    if (this.audioContext) {
      await this.audioContext.close();
      this.audioContext = undefined;
    }

    this.sequence = 0;
    this.startedAt = 0;

    await this.audioCues.play('stop');
  }
}

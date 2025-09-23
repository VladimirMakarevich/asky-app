export interface AudioFrameDto {
  sequence: number;
  timestamp: number;
  payload: ArrayBuffer;
}

export interface PartialTranscriptDto {
  text: string;
  offset: number;
  duration: number;
}

export interface FinalTranscriptDto {
  text: string;
  offset: number;
  duration: number;
  facts?: ReadonlyArray<string> | null;
}

export interface QuestionItemDto {
  text: string;
  tags?: ReadonlyArray<string> | null;
  confidence?: number | null;
  novelty?: number | null;
}

export interface QuestionsEnvelope {
  data: QuestionItemDto[];
}

export interface SessionEvent {
  state: 'started' | 'stopped';
}

export interface ErrorEvent {
  reason: string;
  details?: string;
}

export interface QuestionItem extends QuestionItemDto {
  id: string;
  askedRecently?: boolean;
}

export interface TranscriptItem {
  id: string;
  text: string;
  type: 'partial' | 'final';
  offset: number;
  duration: number;
  facts?: ReadonlyArray<string> | null;
}

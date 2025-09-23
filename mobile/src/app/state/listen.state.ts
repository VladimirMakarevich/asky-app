import { ErrorEvent, QuestionItem, TranscriptItem } from '../models/hub.models';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected';

export interface ListenViewState {
  connection: ConnectionState;
  recorder: 'idle' | 'starting' | 'recording';
  transcripts: TranscriptItem[];
  partial?: TranscriptItem;
  questions: QuestionItem[];
  showAllQuestions: boolean;
  pendingQuestions: boolean;
  error?: ErrorEvent | null;
  askedRecently: ReadonlyArray<string>;
}

export const INITIAL_LISTEN_STATE: ListenViewState = {
  connection: 'disconnected',
  recorder: 'idle',
  transcripts: [],
  questions: [],
  showAllQuestions: false,
  pendingQuestions: false,
  askedRecently: []
};

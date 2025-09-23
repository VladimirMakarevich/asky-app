export interface ListeningSettings {
  locale: string;
  model: string;
  autoStart: boolean;
  sendFeedback: boolean;
}

export interface AppSettings extends ListeningSettings {
  analyticsEnabled: boolean;
}

export const DEFAULT_SETTINGS: AppSettings = {
  locale: 'en-US',
  model: 'general',
  autoStart: false,
  sendFeedback: true,
  analyticsEnabled: false
};

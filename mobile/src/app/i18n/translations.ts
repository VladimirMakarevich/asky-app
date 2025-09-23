export type SupportedLocale = 'en-US' | 'ru-RU';

export type TranslationDictionary = Record<string, string>;

export const TRANSLATIONS: Record<SupportedLocale, TranslationDictionary> = {
  'en-US': {
    'listen.title': 'Listen',
    'listen.status.connected': 'Connected',
    'listen.status.waiting': 'Waiting',
    'listen.partial.title': 'Recognizing…',
    'listen.empty': 'No transcripts yet. Tap “Start” to begin.',
    'listen.final.title': 'Final fragment',
    'listen.final.factsPrefix': 'Facts:',
    'listen.questions.title': 'Recommended questions',
    'listen.questions.button': 'Get questions',
    'listen.questions.ask': 'Ask',
    'listen.questions.hide': 'Hide',
    'listen.questions.showMore': 'Show more',
    'listen.questions.showLess': 'Show less',
    'listen.questions.empty': 'Request questions to get ideas for continuing the conversation.',
    'listen.questions.countLabel': '{{count}} questions',
    'listen.error.heading': 'Error',
    'listen.error.dismiss': 'Dismiss',

    'settings.title': 'Settings',
    'settings.locale': 'Recognition language',
    'settings.model': 'Model',
    'settings.autoStart': 'Auto start listening',
    'settings.sendFeedback': 'Send feedback',
    'settings.analytics': 'Analytics (Firebase)'
  },
  'ru-RU': {
    'listen.title': 'Слушать',
    'listen.status.connected': 'Подключено',
    'listen.status.waiting': 'Ожидает',
    'listen.partial.title': 'Распознаём…',
    'listen.empty': 'Пока нет транскриптов. Нажмите «Старт», чтобы начать.',
    'listen.final.title': 'Финальный фрагмент',
    'listen.final.factsPrefix': 'Факты:',
    'listen.questions.title': 'Рекомендованные вопросы',
    'listen.questions.button': 'Получить вопросы',
    'listen.questions.ask': 'Задать',
    'listen.questions.hide': 'Скрыть',
    'listen.questions.showMore': 'Показать ещё',
    'listen.questions.showLess': 'Показать меньше',
    'listen.questions.empty': 'Запросите вопросы, чтобы получить идеи для продолжения разговора.',
    'listen.questions.countLabel': '{{count}} вопросов',
    'listen.error.heading': 'Ошибка',
    'listen.error.dismiss': 'Скрыть',

    'settings.title': 'Настройки',
    'settings.locale': 'Язык распознавания',
    'settings.model': 'Модель',
    'settings.autoStart': 'Автостарт прослушивания',
    'settings.sendFeedback': 'Отправлять фидбек',
    'settings.analytics': 'Аналитика (Firebase)'
  }
};

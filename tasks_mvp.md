# MVP Implementation Task List

## Cloud
- Создать ресурсную группу и рабочие среды (dev/stage/prod) с единым наименованием.
- Развернуть Azure Speech Service, получить ключи/endpoint, настроить quota-лимиты.
- Создать Azure SignalR Service (Serverless или Standard), включить приватные endpoints и connection string.
- Подготовить App Service / Container Apps для .NET бэкенда, настроить managed identity.
- Создать Azure Key Vault; поместить секреты Speech/LLM, разрешить доступ бэкенду по managed identity.
- Настроить Application Insights + Log Analytics workspace; подключить к бэкенду и SignalR.
- Выпустить TLS-сертификат (Azure Front Door/App Gateway), привязать к домену, включить HTTPS only.
- Сформировать Terraform/Bicep шаблон для перечисленных ресурсов, настроить GitHub Actions deployment.
- Настроить alerts: доступность Hub, задержка LLM, стоимость Speech.

### Cloud infra: запуск
1. Установите Azure CLI 2.52+ и выполните `az login`, выбрав нужный tenant.
2. Выберите рабочую подписку: `az account set --subscription <SUBSCRIPTION_ID>`.
3. Запустите deployment шаблона ресурсных групп: `az deployment sub create --template-file infra/resource-groups.bicep --location westeurope --parameters baseName=asky`.
4. При необходимости измените регион/набор окружений через параметр `environmentConfigs` (пример в файле `infra/resource-groups.bicep`).
5. Проверьте созданные группы: `az group list --query "[?starts_with(name,'asky-')].{name:name,location:location}" -o table`.

## Backend API (.NET 9 + SignalR)
- Скелет ASP.NET Core проекта с SignalR Hub `/hubs/asr`, конфигурация DI и middleware.
- Реализовать методы Hub: `SendAudioFrame`, `StopStream`, `GenerateQuestions`, отправку `Partial/Final` событий.
- Интегрировать Azure Speech SDK (PushAudioInputStream) с очередью входящих PCM-фреймов и backpressure.
- Реализовать хранение контекста: скользящее окно транскриптов, роллинг-сводка, `asked_recently`, известные факты.
- Добавить конвейер Summarizer для обновления `rolling_summary` при финальных распознаваниях.
- Инкапсулировать обращение к LLM-сервису: HTTP-клиент, схема запроса/ответа, ретраи, тайм-ауты.
- Встроить фоллбек генератора (шаблон 4W1H) на случай недоступности LLM.
- Добавить псевдонимизацию PII в контексте перед отправкой в LLM (конфигурируемая опция).
- Реализовать конфигурацию лимитов: частота `SendAudioFrame`, `GenerateQuestions`, ограничения payload.
- Настроить структурированное логирование, метрики (latency ASR/LLM, ошибки), экспорт в Application Insights.
- Написать интеграционные тесты Hub (SignalR тестовый клиент) и модульные тесты для контекстного менеджера.

## Mobile (Ionic + Angular + Capacitor)
- Инициализировать Ionic Angular 19 проект на последних релизах Ionic/Capacitor, подключить Capacitor Native Audio/Microphone плагины.
- Реализовать экран «Слушать»: запрос разрешений, управление состоянием записи, отображение индикатора.
- Создать аудио-пайплайн: захват PCM 48k, ресемплинг до 16k mono, нарезка на 20–40 мс и упаковка в PCM16.
- Реализовать SignalR-клиент: подключение, `SendAudioFrame` с очередью и подтверждением, обработка backpressure.
- Отобразить partial/final транскрипты в UI с обновлением скользящего контекста.
- Добавить кнопку «Получить вопросы», вызов `GenerateQuestions`, обработка ответов/ошибок.
- Спроектировать список вопросов: топ-3 на экране, остальные под «Показать ещё», действия «Задать/Скрыть».
- Реализовать локальное кэширование `asked_recently` и синхронизацию с сервером.
- Добавить настройки: выбор языка/модели, автостарт прослушивания, отправка фида.
- Настроить аналитики (AppCenter/Firebase) и передачу ключевых событий (старт/стоп, запрос вопросов).
- Подготовить end-to-end smoke-тесты (E2E) с mock-сервером для проверки потоков «Слушать» и «Получить вопросы».

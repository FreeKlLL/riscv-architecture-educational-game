# Adventure Mode Foundation — этап 1

Ветка: `workflow`. В рамках этапа добавлены только новые файлы Adventure, тесты и документация. Существующие сцены, prefab, DialogueGraph, процессоры, MainMenu, LevelManager, BaseLevelRegisseur, Classic Mode и незакоммиченные изменения не редактировались. Git использовался только для чтения ветки, статуса и diff.

## Классы и файлы

Все runtime-типы находятся в namespace `RiscVAdventure.Adventure`, отдельная сборка `Adventure.Foundation`.

| Файл | Назначение |
| --- | --- |
| `Assets/Scripts/Adventure/AdventureTopics.cs` | Постоянные строковые TopicIds, TopicDefinition, расширяемый TopicCatalog и проверка значений |
| `Assets/Scripts/Adventure/Knowledge.cs` | TopicKnowledge, IUtcClock / SystemUtcClock, KnowledgeDecaySettings, UTC-сериализация и забывание |
| `Assets/Scripts/Adventure/AdventureProfile.cs` | AdventureProfile, ChapterProgress, LessonChoice, PracticeEvidence, PracticeRecord, KnowledgeEvaluation, IKnowledgeEvaluator |
| `Assets/Scripts/Adventure/AdventureProfileStore.cs` | Версионированные JSON, атомарная запись, резервные копии, восстановление, SaveLoadIssue |
| `Assets/Scripts/Adventure/AdventureProfileService.cs` | API профилей и сохранение изменений прогресса |
| `Assets/Scripts/Adventure/AdventureRuntime.cs` | Автозагрузка перед первой сценой приложения, доступ к сервису и отдельному каталогу сохранений |
| `Assets/Scripts/Adventure/Adventure.Foundation.asmdef` | Изолированная runtime-сборка |
| `Assets/Tests/Adventure/EditMode/AdventureFoundationTests.cs` | 44 изолированных EditMode-тестовых случая Adventure |
| `Assets/Tests/Adventure/EditMode/AdventureTestExecution.cs` | Запуск новых и существующих EditMode-тестов через штатный Unity TestRunner API / MCP |
| `Assets/Tests/Adventure/EditMode/Adventure.Foundation.Tests.asmdef` | Editor-only тестовая сборка, без изменений существующей Tests.asmdef |

Unity создал `.meta` для новых скриптов, сборок и каталогов. Отчёты: [JSON](adventure-foundation-test-results.json), [NUnit XML](adventure-foundation-test-results.xml). Этот документ также является новым файлом.

## Профили и API

```csharp
using RiscVAdventure.Adventure;

var profiles = AdventureRuntime.Profiles;
var player = profiles.CreateProfile("Charlie"); // GUID в формате N, постоянный после перезапуска
profiles.SelectProfile(player.Id);
profiles.RenameProfile(player.Id, "Charlie II");

// Запись результата диагностики; сама диагностика здесь не выполняется.
profiles.RecordAssessment(player.Id, TopicIds.Registers, TopicIds.Registers, 80);
profiles.RecordLessonChoice(player.Id, TopicIds.Registers, LessonChoice.SkipLesson);
profiles.CompleteLesson(player.Id, TopicIds.Registers);
profiles.CompleteTopic(player.Id, TopicIds.Registers);

profiles.RefreshKnowledge(player.Id);
profiles.SaveProfile(player.Id); // Обычно не нужен: изменения сохраняются автоматически.
bool completed = profiles.HasCompletedAdventure(player.Id);

profiles.DeleteProfile(player.Id, confirmDeletion: false); // false, без изменений
// Только явное подтверждение разрешает удаление:
// profiles.DeleteProfile(player.Id, confirmDeletion: true);
```

- `Profiles`, `ActiveProfile`, `ActiveProfileId`, `GetProfile(id)` предоставляют состояние. Коллекции и свойства модели доступны только для чтения; изменения проходят через сервис.
- Первый созданный профиль становится активным. Выбор и переименование сохраняются. После удаления активного выбирается первый оставшийся профиль; после удаления последнего активного профиля нет.
- Имена обрезаются по краям и не могут быть пустыми. Одинаковые имена допустимы: идентичность задаётся GUID, а не именем.
- Удаление затрагивает только файл выбранного профиля и его backup. Отмена не меняет файлы. Удалённые профили не восстанавливаются из оставленной `.bak`.
- `LoadIssues` содержит пути и причины пропуска/восстановления повреждённых документов. Неудачная мутация откатывает состояние, доступное через сервис, и пробрасывает исключение. После исключения следует заново получить объект через `GetProfile`.
- API синхронный, предназначен для главного потока Unity и одного процесса-писателя. Межпроцессная синхронизация / облачное объединение не входят в этап.

## Модель знаний и будущий Regisseur

Восемь обязательных тем: `registers`, `multiplexer`, `alu`, `memory`, `extender`, `register-file`, `eintaktprozessor`, `mehrtaktprozessor`.

Каждая TopicKnowledge хранит TopicId, CurrentKnowledge, Mastery, IsAssessed, IsLessonCompleted, IsTopicCompleted, LastPracticeUtc и LastDecayCalculationUtc. Числа ограничиваются диапазоном 0–100; NaN и Infinity отвергаются. Начальные 0% с `IsAssessed=false` означают **неизвестный уровень**, а не доказанное отсутствие знаний. Результат диагностики 0% отличается флагом `IsAssessed=true`.

`CompleteLesson` выставляет IsLessonCompleted и применяет `max(50, старое значение)` независимо к CurrentKnowledge и Mastery. Уже достигнутые 80% не снижаются. Завершение урока не подменяет диагностику. Повторное подтверждение уже завершённого урока без новых начислений не сбрасывает таймер забывания.

ChapterProgress хранит собственный статус диагностики и решение `Undecided / TakeLesson / SkipLesson / Review`. Первые восемь глав имеют ID соответствующей темы и изначально не диагностированы, включая первую главу. Новые главы можно зарегистрировать через `RecordAssessment(profileId, chapterId, topicId, score)`. Пропуск урока до диагностики этой главы запрещён; порог «высоких знаний» и выбор маршрута определит будущий Regisseur.

Практика записывается через `RecordPractice(profileId, evidence, evaluator)`:

- PracticeEvidence передаёт тему, ID упражнения, источник контента (например, `classic` или `adventure`), сложность, число ошибок и факт успешности.
- IKnowledgeEvaluator получает эти данные и текущее состояние после расчёта забывания; возвращает KnowledgeEvaluation с CurrentKnowledge, Mastery и признаком подтверждённой диагностики. Конкретная формула оценивания намеренно не навязана фундаментом.
- Результат обновляет знания, история сохраняет сложность / ошибки / источник / UTC. Успешная практика обновляет LastPracticeUtc и опорный уровень забывания. Неудачная практика сама по себе не меняет время последней успешной практики и не сбрасывает таймер.
- Повторение старой темы использует тот же API; существующие упражнения могут быть указаны ссылочным ID без изменения Classic Mode.
- `CompleteTopic` — явное решение будущей системы прохождения. `HasCompletedAdventure` проверяет завершение всех обязательных тем каталога. Снижение знаний не сбрасывает завершение темы или Adventure.

Для Pipelineprozessor достаточно расширить TopicCatalog новой TopicDefinition. Загрузка добавляет недостающие темы и главы; неизвестные текущему каталогу темы и их прогресс сохраняются. Необязательные темы не препятствуют завершению Adventure. Строковые TopicId не зависят от порядка enum.

## Сохранения

Каталог по умолчанию:

```text
Application.persistentDataPath/AdventureMode/profiles/
    index.json
    index.json.bak
    <profile-guid-N>.json
    <profile-guid-N>.json.bak
```

Ни `UnlockedLevelIndex`, ни другие ключи PlayerPrefs не используются. При старте `AdventureRuntime` автоматически загружает существующие профили; профиль по умолчанию не создаётся. В EditMode-тестах этот bootstrap не вызывается, сервис получает отдельный временный каталог. Недоступность Adventure-каталога при запуске отражается в `StartupError` и предупреждении, не останавливая запуск Classic-сцен.

Индекс содержит только версию и активный ID:

```json
{
  "schemaVersion": 1,
  "activeProfileId": "e35078b45164428592099ebfe4c1105b"
}
```

Схема файла профиля (показаны по одной записи массивов):

```json
{
  "schemaVersion": 1,
  "profile": {
    "id": "e35078b45164428592099ebfe4c1105b",
    "displayName": "Charlie",
    "createdUtc": "2026-01-01T00:00:00.0000000Z",
    "topics": [{
      "topicId": "registers",
      "currentKnowledge": 80.0,
      "mastery": 80.0,
      "isAssessed": true,
      "isLessonCompleted": true,
      "isTopicCompleted": true,
      "lastPracticeUtc": "2026-01-01T00:00:00.0000000Z",
      "lastDecayCalculationUtc": "2026-01-01T00:00:00.0000000Z",
      "decayBaselineUtc": "2026-01-01T00:00:00.0000000Z",
      "decayBaselineKnowledge": 80.0
    }],
    "chapters": [{
      "chapterId": "registers",
      "topicId": "registers",
      "isDiagnosticCompleted": true,
      "lessonChoice": 2
    }],
    "practiceHistory": [{
      "topicId": "registers",
      "exerciseId": "register-exercise-1",
      "contentSource": "classic",
      "difficulty": 2,
      "errorCount": 0,
      "isSuccessful": true,
      "recordedUtc": "2026-01-01T00:00:00.0000000Z"
    }]
  }
}
```

JSON записывается через JsonUtility в UTF-8. Неустановленные UTC-строки могут сериализоваться как пустые строки; публичные свойства возвращают для них null. Enum-значения сохраняются численно. Добавление новых несовместимых значений/полей должно сопровождаться новой версией и миграцией.

Запись: уникальный временный файл в том же каталоге → запись всех байтов → `Flush(true)` → `File.Replace` с сохранением предыдущей `.bak`; для первого сохранения — `File.Move`. При ошибке старый основной файл не удаляется, временный убирается, исключение передаётся вызывающему коду. Если файловая система не поддерживает File.Replace, операция завершается ошибкой; небезопасного удаления основного файла ради fallback нет. Реально проверено на файловой системе текущего Windows Editor; другие целевые платформы требуют отдельной проверки.

Файлы профилей обнаруживаются независимо от индекса. Повреждённый основной документ восстанавливается из валидной `.bak`, либо исключается из списка с LoadIssue; другие профили остаются доступны. Автозагрузка не перезаписывает повреждённый файл даже при восстановлении из backup. При следующем явном изменении восстановленного профиля записывается актуальный валидный документ. Неоконченные `.tmp` игнорируются.

Версия 1 — первый формат, миграций более старых форматов пока нет. Документ с будущей версией не открывается и не заменяется старой резервной копией. Будущая версия индекса запрещает создание, выбор и удаление профилей, которые перезаписали бы этот индекс. Атомарность обеспечивается для каждого файла; индекс и несколько профилей не образуют общую транзакцию. После прерывания между ними сканирование профилей и проверка активного ID восстанавливают доступ к валидным профилям.

## Забывание и часы

KnowledgeDecaySettings по умолчанию: 7 полных дней на шаг, 1 процентный пункт за шаг, предел 15 пунктов. Настройки внедряются в конструктор AdventureProfileService, отдельно от модели.

При подтверждении знаний сохраняются `B = max(CurrentKnowledge, Mastery)` и опорное UTC-время `A`. Диагностика и новый урок создают опору; успешная практика обновляет её. Максимум с Mastery не позволяет новому уроку превратить уже забытые знания в очередную пониженную опору и накопить более 15 пунктов временного снижения.

```text
T = max(часы.UtcNow, CreatedUtc, все сохранённые LastDecayCalculationUtc профиля)
D = max(0, floor((T - A) / 24 часа))
S = floor(D / FullDaysPerStep)
L = min(MaximumLoss, S * PercentagePointsPerStep)
CurrentKnowledge' = min(CurrentKnowledge, clamp(B - L, 0, 100))
LastDecayCalculationUtc = T
```

Для B=80: через 6 дней — 80; через 7 — 79; через 70 — 70; через 105 и более — не ниже 65 из-за времени. Более низкая оценка за практику может существовать отдельно; забывание не повышает её автоматически.

- Расчёт применяется только к оценённым или изученным темам.
- Mastery, IsTopicCompleted и IsLessonCompleted от времени не меняются.
- Опора A не заменяется временем каждой загрузки: неполные недели не теряются, а повторная загрузка не вычитает потерю второй раз.
- При откате системных часов сохранённая временная граница не уменьшается; новые подтверждения и записи практики тоже используют её. Это действует при переходе к другой теме того же профиля.
- Сильный перевод часов вперёд ограничен максимумом потери. После обратного перевода забывание консервативно ждёт достижения сохранённой границы. Прохождение тем при этом не блокируется и не откатывается.
- Завершение не вычисляется из текущего процента: игрок не обязан бесконечно перепроходить предыдущие главы из-за забывания.

## Фактические проверки

Через Unity MCP выполнены AssetDatabase.Refresh, проверка recompile_status, запуск штатного Unity TestRunner API и чтение Unity Console. Финальный запуск: **135 Passed, 0 Failed, 0 Skipped, 0 Inconclusive** — 44 новых случая Adventure и все 91 существующий EditMode-тест проекта.

Покрыты все 15 пунктов задания: несколько профилей, round-trip, активный профиль, повреждения, Unknown/Assessed zero, минимум 50, сохранение 80, 6/7/70 дней, предел 15, повторная загрузка, завершение, часы и диапазон 0–100. Дополнительно проверены backup, временные файлы, ошибки записи / откат памяти, подтверждение удаления, будущие версии, новые темы, практика со сложностью и ошибками, частичные недели и повторные уроки.

Каждый тест использует новый `Path.GetTempPath()/RiscVAdventureFoundationTests/<guid>` и удаляет только его в TearDown. Реальные профили и PlayerPrefs тестами не изменялись. Новые сборки и тесты не требуют обновления пакетов.

Команда MCP для воспроизведения после компиляции в Edit Mode:

```csharp
RiscVAdventure.Tests.Adventure.AdventureTestExecution.RunAllEditMode();
```

Ошибок компиляции нет. Финальная Console: **0 ошибок, 0 предупреждений**. Unity оставлена вне Play Mode, активна MainMenu. SHA-256 1795 существовавших файлов совпали со снимком до реализации; существующие незакоммиченные изменения сохранены. Новых изменений в существующих файлах нет.

Не проводились PlayMode-прохождение Classic, перезапуск отдельного собранного приложения, Android/WebGL-проверка или имитация аппаратного отключения питания. Перезапуск хранилища проверен созданием нового сервиса и чтением реальных временных JSON-файлов.

## Следующий этап

UI профилей, фактическая диагностика перед главами, конкретная политика оценивания / пороги высокого знания, адаптивный выбор упражнений, связывание событий существующих уровней с Adventure и сам Adventure Regisseur. В этом этапе они не реализованы. При необходимости далее добавляются миграции форматов и платформенная проверка файловой системы; Classic-сохранения остаются независимыми.

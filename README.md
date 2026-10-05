# Money Runner

> Мобильный раннер: собирай деньги, обходи бутылки и разбогатей настолько, чтобы открыть все двери на финише.

<p align="left">
<img width="30%" alt="Скриншот 1" src="https://github.com/user-attachments/assets/6e0f5643-9f2c-41e6-aee4-3f25721a98f4" />
<img width="30%" alt="Скриншот 2" src="https://github.com/user-attachments/assets/0ba549b7-6658-40ac-9fe8-a85d7faabd0f" />
<img width="30%" alt="Скриншот 3" src="https://github.com/user-attachments/assets/4c3167c4-b4bd-404a-869c-bbba6ff4d089" />
</p>

<!-- Перетащите сюда видео геймплея -->

## Об игре

Персонаж сам бежит по дороге, игрок только смещает его влево и вправо. Купюры добавляют деньги, бутылки отнимают. От суммы зависит статус персонажа (Бедный → Средний → Богатый → Миллионер), и вместе со статусом меняется его модель. В конце уровня стоит ряд дверей с порогами: чем вы богаче, тем больше дверей откроется.

- **Статусы богатства** с полосой прогресса над персонажем и сменой модели.
- **Чекпоинты:** на жёлтой полосе поднимается флаг, прогресс сохраняется, а объекты-заглушки получают настоящие материалы.
- **Финиш:** двери открываются по порогам богатства, персонаж танцует, летит конфетти.
- **Награда:** забрать деньги или умножить их. На экране победы для этого качается стрелка множителя.
- **Отклик на подбор:** всплывающий текст «+10 $» / «−20 $», частицы и звук.
- **Уровни** из списка в ScriptableObject, прогресс сохраняется.

## Управление

- Перетаскивание мышью или пальцем — смещение влево и вправо.
- `A` / `D` или стрелки — то же самое с клавиатуры.

## Стек

Unity 2022.3.62f1 · C# · URP · uGUI + TextMeshPro · ProBuilder

## Архитектура

Код лежит в `Assets/Scripts`:

| Папка | Что внутри |
|---|---|
| `Core` | `GameManager` (состояния Ready → Playing → Won), `SceneSingleton<T>`, утилиты пути, `RoundRobinPool<T>` |
| `Player` | `PlayerController` (бег по точкам пути, наклон в поворотах), `SteeringInput`, анимация, смена модели |
| `Gameplay/Collectibles` | Базовый `Collectible` и наследники `MoneyPickup` и `BottlePickup` |
| `Gameplay/Economy` | `ScoreManager`, `WealthTracker`, `WealthConfig` (пороги статусов), `RewardCalculator` |
| `Gameplay/Checkpoints` | `SaveZone`, `CheckpointManager`, `ProgressSave` (слот сохранения привязан к сцене и уровню), `FlagRaiser` |
| `Gameplay/Finish` | `DoorsController`, `Door`, `FinishController`, `FinishCelebration` (процедурный танец) |
| `Levels` | `LevelManager`, `LevelsList`, `Level` |
| `Feedback` | `AudioManager` с пулом AudioSource, `PickupFeedback`, `FloatingText` |
| `UI` | HUD, экраны победы и финиша, счётчик денег с анимацией |
| `Editor` | Инструменты настройки сцены (см. ниже) |

Решения:
- **Геймплей не знает про эффекты.** `PickupFeedback` подписывается на `Collectible.OnCollected` и `WealthTracker.OnStatusChanged`.
- **Нет аллокаций в игровом цикле:** эффекты берутся из пулов, созданных в `Awake`, а `FloatingText` форматирует числа через `TMP SetText`.
- **`SteeringInput` — обычный класс, а не MonoBehaviour.** Он ничего не знает про дорогу и только сообщает смещение за кадр.
- **Экраны результата работают при `Time.timeScale = 0`,** потому что используют unscaled time.

## Инструменты редактора

Меню **Tools → Runner**. Все команды можно запускать повторно: ничего не дублируется, ручные правки сохраняются.

| Пункт | Что делает |
|---|---|
| Setup Scene | Готовит сцену и префаб уровня |
| Setup Collectibles | Настраивает купюры и бутылки, создаёт `WealthConfig` |
| Setup Checkpoints And Doors | Находит чекпоинты и двери по цвету материала и вешает компоненты |
| Setup HUD / Setup Win Panel | Собирает HUD и экран победы |
| Setup Feedback | Создаёт частицы, всплывающий текст и звук |
| Setup Player Visuals | Анимация бега и камера |
| Fix Tags And Layers / Fix Broken UI Sprites | Чинит теги, слои и битые спрайты |

## Запуск

1. Откройте проект в Unity 2022.3.62f1.
2. Откройте сцену `Assets/Scenes/SampleScene.unity` и нажмите Play.

## Автор

Василий · [GitHub](https://github.com/Vasia33131) · Telegram [@Vasiliyg4](https://t.me/Vasiliyg4)

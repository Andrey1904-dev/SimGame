// Life From Zero — UI/Runtime/UiSettings.cs (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// НАСТРОЙКИ ЭКРАНА «НАСТРОЙКИ» И ЧЕСТНОЕ ПОВЕДЕНИЕ КНОПОК ПАУЗЫ.
//
// Зачем: в меню паузы есть кнопки «Сохранить», «Загрузить», «Настройки»,
// «Управление», «Главное меню», «Выход». Если у игры нет системы сохранений,
// эти кнопки НЕ ДОЛЖНЫ делать вид, что что-то сохранили. Правило простое:
//   доступно только то, для чего в сцене есть исполнитель (IDisposable-провайдер);
//   недоступная кнопка в UI неактивна (PauseMenuScreen уже это умеет).
//
// Что реально работает здесь и сейчас: настройки звука (AudioListener.volume)
// и полный экран — обоими управляет сам Unity, выдумывать нечего.

using System;
using UnityEngine;
using LifeFromZero.UI.Core;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Настройки, которые UI умеет применять сам (звук, полный экран).</summary>
    public sealed class UiSettings
    {
        private const string VolumeKey = "lfz.ui.volume";
        private const float DefaultVolume = 0.8f;

        private float _volume = DefaultVolume;
        private bool _fullscreen;

        /// <summary>Громкость 0..1. Применяется сразу.</summary>
        public float Volume
        {
            get { return _volume; }
            set
            {
                _volume = Mathf.Clamp01(value);
                AudioListener.volume = _volume;
                PlayerPrefs.SetFloat(VolumeKey, _volume);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Полный экран (в редакторе Unity это безопасно, в окне — нет).</summary>
        public bool Fullscreen
        {
            get { return _fullscreen; }
            set
            {
                _fullscreen = value;
                Screen.fullScreen = value;
            }
        }

        /// <summary>Загрузить сохранённые значения и применить их.</summary>
        public void Load()
        {
            _volume = PlayerPrefs.HasKey(VolumeKey)
                ? Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume))
                : DefaultVolume;
            AudioListener.volume = _volume;
            _fullscreen = Screen.fullScreen;
        }

        /// <summary>Шаговое изменение громкости (±0.1), как на кнопках в меню.</summary>
        public float StepVolume(float delta)
        {
            Volume = _volume + delta;
            return _volume;
        }
    }

    /// <summary>
    /// Исполнители для кнопок паузы, которых нет в UI. Игру они не подменяют:
    /// если провайдер не назначен, кнопка недоступна, и игрок видит честное состояние.
    /// </summary>
    public sealed class UiMenuActions : MonoBehaviour
    {
        [Header("Исполнители (пусто — кнопка будет недоступна)")]
        [Tooltip("Сохранение игры: компонент, реализующий IUiGameAction.")]
        [SerializeField] private MonoBehaviour saveHandler;

        [Tooltip("Загрузка игры: компонент, реализующий IUiGameAction.")]
        [SerializeField] private MonoBehaviour loadHandler;

        [Tooltip("Выход в главное меню: компонент, реализующий IUiGameAction.")]
        [SerializeField] private MonoBehaviour mainMenuHandler;

        private readonly UiSettings _settings = new UiSettings();

        /// <summary>Настройки звука/экрана (кнопки «Настройки»).</summary>
        public UiSettings Settings => _settings;

        private void Awake() => _settings.Load();

        /// <summary>Доступно ли действие меню.</summary>
        public bool IsAvailable(string actionId, bool hasControlsPage)
        {
            switch (actionId)
            {
                case PauseActions.Save: return saveHandler is IUiGameAction;
                case PauseActions.Load: return loadHandler is IUiGameAction;
                case PauseActions.MainMenu: return mainMenuHandler is IUiGameAction;
                case PauseActions.Settings: return true;
                case PauseActions.Controls: return hasControlsPage;
                case PauseActions.Quit: return true;
                default: return false;
            }
        }

        /// <summary>
        /// Выполнить действие. Возвращает текст для игрока (null — без сообщения)
        /// и признак «сделано». Никогда не рапортует об успехе без исполнителя.
        /// </summary>
        public bool TryExecute(string actionId, out string message)
        {
            message = null;
            switch (actionId)
            {
                case PauseActions.Save:
                    return RunHandler(saveHandler, "Игра сохранена.", "Сохранение игры не подключено.", out message);
                case PauseActions.Load:
                    return RunHandler(loadHandler, "Игра загружена.", "Загрузка игры не подключена.", out message);
                case PauseActions.MainMenu:
                    return RunHandler(mainMenuHandler, null, "Главное меню не подключено.", out message);
                case PauseActions.Settings:
                    message = "Громкость: " + Mathf.RoundToInt(_settings.Volume * 100f) + "%. " +
                              "Клавиши изменения — в разделе «Управление».";
                    return true;
                case PauseActions.Quit:
                    message = "Выход отключён в редакторе — используйте кнопку Stop.";
                    return false;
                default:
                    message = "Действие недоступно.";
                    return false;
            }
        }

        private static bool RunHandler(MonoBehaviour handler, string success, string failure, out string message)
        {
            var action = handler as IUiGameAction;
            if (action == null)
            {
                message = failure;
                return false;
            }

            try
            {
                if (!action.Run(out message))
                {
                    if (string.IsNullOrEmpty(message)) message = failure;
                    return false;
                }
                if (string.IsNullOrEmpty(message)) message = success;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[UiMenuActions] Исполнитель действия выбросил исключение: " + exception.Message);
                message = failure;
                return false;
            }
        }
    }

    /// <summary>
    /// Исполнитель действия паузы (сохранение/загрузка/главное меню). Реализуется
    /// системой игры; UI только вызывает и показывает то, что она сообщила.
    /// </summary>
    public interface IUiGameAction
    {
        bool Run(out string message);
    }
}

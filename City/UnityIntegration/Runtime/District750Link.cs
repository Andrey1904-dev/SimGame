// Life From Zero — City/UnityIntegration/Runtime/District750Link.cs (ТЗ #039)
// Namespace: LifeFromZero.Integration
//
// Пассивный мост «игра → District750». Сам по себе НИЧЕГО не делает:
// нет Update, нет Update-опроса клавиш, нет триггеров и таймеров. Ждёт вызова
// из существующей системы переходов проекта (дверь, портал, меню, кнопка).
//
// Как подключить (делает Codex после Tools → Life From Zero → Integrate Into Game):
//   1. найти в стартовой сцене существующий переход на улицу/дверь/портал;
//   2. в его UnityEvent (OnClick / OnInteract / OnTriggerEnter → «+»)
//      перетащить объект, на котором висит District750Link;
//   3. выбрать метод District750Link.OpenDistrict.
// Если в проекте нет перехода на улицу — сначала решает владелец игры, где он должен быть.
//
// Возврат в стартовую сцену — ReturnToStart(), если существующая система переходов
// это поддерживает. Компонент не хранит состояние и не мешает уже работающим переходам.
//
// ВНИМАНИЕ: в среде репозитория Unity нет; проверено только статическим разбором.

using UnityEngine;
using UnityEngine.SceneManagement;

namespace LifeFromZero.Integration
{
    public sealed class District750Link : MonoBehaviour
    {
        [Tooltip("Имя сцены района District750 (без .unity).")]
        [SerializeField] private string districtSceneName = "District750";

        [Tooltip("Сцена, в которую вернуться по ReturnToStart(). Пусто — возврата нет.")]
        [SerializeField] private string returnSceneName = "";

        [Tooltip("Single — район заменяет текущую сцену; Additive — добавляется поверх (нужна своя камера).")]
        [SerializeField] private LoadSceneMode loadMode = LoadSceneMode.Single;

        /// <summary>Открыть District750. Вызывается из существующего перехода (дверь/портал/меню).</summary>
        public void OpenDistrict()
        {
            LoadByName(districtSceneName);
        }

        /// <summary>Вернуться в стартовую сцену. Ничего не делает, если имя не задано.</summary>
        public void ReturnToStart()
        {
            LoadByName(returnSceneName);
        }

        private void LoadByName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[District750Link] имя сцены не задано — переход отменён.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[District750Link] сцена '" + sceneName + "' недоступна: добавьте её в Build Settings / Build Profile. " +
                               "Переход отменён, текущая сцена не изменена.");
                return;
            }

            if (loadMode == LoadSceneMode.Additive)
                SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            else
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}

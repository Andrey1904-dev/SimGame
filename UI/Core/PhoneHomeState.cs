// Life From Zero — UI/Core (ТЗ №045)
// Namespace: LifeFromZero.UI.Core
//
// ЗАЧЕМ ЭТОТ ФАЙЛ (регрессия после реального Play Mode).
//
// После нажатия P телефон открывался, но на рабочем столе было пусто, а игрок
// (и интегратор) читали подсказку «Приложения не подключены. Регистрация:
// PhoneAppRegistry.CreateDefault()». Подсказка ВРАЛА: плитки телефона приходят
// не из PhoneAppRegistry, а из PhoneSnapshot.Apps, который собирает IPhoneDataSource
// (PhoneFacadeUiBridge / DeliveryServiceBridge). Регистрация приложений в модуле
// Phone сама по себе рабочий стол не заполняет.
//
// Здесь одно место, где решается, что показать на рабочем столе:
//   * есть данные  → плитки как есть;
//   * нет источника → заглушки (все приложения неактивны) + честное сообщение;
//   * источник есть, но список пуст → заглушки + другое сообщение.
//
// Модуль без UnityEngine.

using System;
using System.Collections.Generic;

namespace LifeFromZero.UI.Core
{
    /// <summary>Что показывать на рабочем столе телефона.</summary>
    public sealed class PhoneHomeContent
    {
        public PhoneHomeContent(IReadOnlyList<PhoneAppTile> tiles, string notice, bool placeholder)
        {
            Tiles = tiles ?? Array.Empty<PhoneAppTile>();
            Notice = notice;
            Placeholder = placeholder;
        }

        /// <summary>Плитки в порядке рабочего стола.</summary>
        public IReadOnlyList<PhoneAppTile> Tiles { get; }

        /// <summary>Текст для игрока (null — сообщения нет).</summary>
        public string Notice { get; }

        /// <summary>true — показаны неактивные заглушки вместо настоящих данных.</summary>
        public bool Placeholder { get; }

        public bool IsEmpty => Tiles.Count == 0;
    }

    /// <summary>Правило «что на рабочем столе», отдельно от uGUI.</summary>
    public static class PhoneHomeState
    {
        /// <summary>Источник данных телефона в сцене не найден.</summary>
        public const string NoDataSourceNotice = "Телефон не подключён к данным игры.";

        /// <summary>Источник есть, но приложений в снимке нет.</summary>
        public const string EmptySourceNotice = "Источник данных не вернул ни одного приложения.";

        /// <summary>
        /// Данные не пришли (нет IPhoneDataSource) — плитки-заглушки, чтобы телефон
        /// не выглядел «сломанным экраном», но ни одно приложение не активно:
        /// обещать работу, которой нет, нельзя.
        /// </summary>
        public static PhoneHomeContent Resolve(IReadOnlyList<PhoneAppTile> apps, bool dataSourceConnected)
        {
            List<PhoneAppTile> ordered = PhoneAppIds.Sort(apps);
            if (ordered.Count > 0)
                return new PhoneHomeContent(ordered, null, false);

            return new PhoneHomeContent(
                PhoneAppCatalog.BuildPlaceholder(),
                dataSourceConnected ? EmptySourceNotice : NoDataSourceNotice,
                true);
        }
    }
}

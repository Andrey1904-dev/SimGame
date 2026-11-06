// Life From Zero — UI/Core (ТЗ №042)
// Namespace: LifeFromZero.UI.Core
//
// Заводская привязка карты и контрольные точки.
//
// ПОЧЕМУ ЗДЕСЬ ДВЕ РАЗНЫЕ ПРИВЯЗКИ
// ---------------------------------
// `Map.PNG` (утверждённый референс) — это НЕ ортогональный план, а наклонный
// (перспективный) рендер: решение «сверху», но с углом, из-за которого дальние
// кварталы сжаты, а ближние растянуты. Вдобавок раскладка на картинке не совпадает
// с District750 по составу объектов: на картинке «Банк» стоит у входа «ТЦ Горизонт»,
// а в раскладке — на улице H300 (390;380); «Мэрия» на картинке — на площади центра,
// в раскладке — (330;430); количества домов в кварталах тоже различаются
// (север 4/5, юг 3/4).
//
// Поэтому:
//   * <see cref="District750Ortho"/> — рабочая привязка для карты, построенной из
//     РЕАЛЬНЫХ данных District750 (дороги, дома, точки из QuarterPlan). Маркеры,
//     дома и маршрут лежат точно.
//   * <see cref="District750Perspective"/> — привязка к самому `Map.PNG`, посчитанная
//     МНК по 18 контрольным точкам. Она НЕ точна: остаточная ошибка ~69 px по
//     картинке ≈ 44 м по земле в медиане (см. <see cref="MeasuredReferenceResidualOkCount"/>
//     и UI/MAP_CALIBRATION_REPORT.md). Использовать её можно только как справочный
//     слой поверх картинки, и расхождение нельзя «замазывать» сдвигом маркеров.
//
// Контрольные точки взяты по центрам подписей/зданий на картинке и по данным
// генератора District750 — таблица одинаковая в C# и в отчёте.

using System;
using System.Collections.Generic;

namespace LifeFromZero.UI.Core
{
    /// <summary>Контрольная точка: мировая координата District750 и её координата на картинке.</summary>
    public readonly struct MapTiePoint
    {
        public MapTiePoint(string id, float worldX, float worldZ, float u, float v)
        {
            Id = id;
            World = new MapPoint(worldX, worldZ);
            Uv = new MapUv(u, v);
        }

        public string Id { get; }

        public MapPoint World { get; }

        public MapUv Uv { get; }
    }

    /// <summary>Результат честной проверки привязки картинки к раскладке.</summary>
    public readonly struct MapCalibrationAudit
    {
        public MapCalibrationAudit(
            int okCount,
            int totalCount,
            float medianErrorPixels,
            float maxErrorPixels,
            float pixelsPerMeter,
            string worstId,
            float worstErrorPixels)
        {
            OkCount = okCount;
            TotalCount = totalCount;
            MedianErrorPixels = medianErrorPixels;
            MaxErrorPixels = maxErrorPixels;
            PixelsPerMeter = pixelsPerMeter;
            WorstId = worstId;
            WorstErrorPixels = worstErrorPixels;
        }

        /// <summary>Сколько точек уложилось в допуск <see cref="MapCalibrationDefaults.TieTolerancePixels"/>.</summary>
        public int OkCount { get; }

        public int TotalCount { get; }

        public float MedianErrorPixels { get; }

        public float MaxErrorPixels { get; }

        public float PixelsPerMeter { get; }

        public string WorstId { get; }

        public float WorstErrorPixels { get; }

        /// <summary>Средняя ошибка в метрах (пересчёт по PixelsPerMeter).</summary>
        public float MedianErrorMeters =>
            PixelsPerMeter <= 0f ? float.PositiveInfinity : MedianErrorPixels / PixelsPerMeter;

        public bool IsAcceptable => MaxErrorPixels <= MapCalibrationDefaults.TieTolerancePixels;

        public override string ToString() =>
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "в допуске {0}/{1}; медиана {2:0.0} px (~{3:0.0} м); максимум {4:0.0} px ({5})",
                OkCount, TotalCount, MedianErrorPixels, MedianErrorMeters, MaxErrorPixels, WorstId);
    }

    /// <summary>Заводские параметры привязки и контрольные точки District750.</summary>
    public static class MapCalibrationDefaults
    {
        /// <summary>Допуск привязки, пиксели картинки-референса.</summary>
        public const float TieTolerancePixels = 12f;

        /// <summary>Сторона района District750, метры.</summary>
        public const float DistrictSize = 750f;

        /// <summary>Полезная область `Map.PNG` в пикселях (без левой легенды и рамки).</summary>
        public const int ReferenceRegionX = 318;
        public const int ReferenceRegionY = 60;
        public const int ReferenceRegionWidth = 1172;
        public const int ReferenceRegionHeight = 892;

        /// <summary>
        /// Масштаб референса: 1172 px на 750 м по горизонтали. Нужен только для перевода
        /// «пиксели картинки → метры» в отчётах и диагностике.
        /// </summary>
        public const float ReferencePixelsPerMeter = ReferenceRegionWidth / DistrictSize;

        /// <summary>
        /// Гомография (row-major, h8 = 1) «мир → картинка», МНК по 18 точкам.
        /// Значения получены вне Unity и проверяются тестом
        /// <c>MapCalibrationTests.PerspectiveFitIsMeasured</c>: тест НЕ требует точности,
        /// он фиксирует измеренную ошибку, чтобы её нельзя было молча «улучшить».
        /// </summary>
        public static readonly double[] ReferenceHomography =
        {
            0.0005439307, -0.0003331341, 0.192336912,
            -0.0001771764, 0.0003954528, 0.1348213427,
            -0.0003018408, -0.0007818894, 1.0,
        };

        /// <summary>Рабочая привязка для карты, построенной из данных District750.</summary>
        public static MapCalibration District750Ortho() => MapCalibration.District750Ortho(DistrictSize);

        /// <summary>Привязка к `Map.PNG` (с перспективой). Точность ограничена — см. отчёт.</summary>
        public static MapCalibration District750Perspective() =>
            new MapCalibration(
                DistrictSize,
                DistrictSize,
                0f,
                0f,
                0f,
                false,
                ReferenceHomography,
                0f,
                0f);

        /// <summary>Контрольные точки: мир District750 ↔ UV полезной области `Map.PNG`.</summary>
        public static IReadOnlyList<MapTiePoint> TiePoints { get; } = new List<MapTiePoint>
        {
            new MapTiePoint("gas-02 (АЗС-2)", 85f, 680f, 0.10418f, 0.91470f),
            new MapTiePoint("gas-03 (АЗС-3)", 688f, 530f, 0.92741f, 0.60494f),
            new MapTiePoint("gas-01 (АЗС-1)", 462f, 66f, 0.61230f, 0.07407f),
            new MapTiePoint("gas-04 (АЗС-4)", 85f, 395f, 0.10675f, 0.33782f),
            new MapTiePoint("com-warehouse (Склад)", 85f, 535f, 0.10504f, 0.64646f),
            new MapTiePoint("com-mall (ТЦ Горизонт)", 345f, 535f, 0.52007f, 0.58923f),
            new MapTiePoint("com-cafe (Кафе)", 190f, 425f, 0.26132f, 0.47587f),
            new MapTiePoint("com-restaurant (Ресторан)", 190f, 390f, 0.26046f, 0.41414f),
            new MapTiePoint("com-clothing (Одежда)", 190f, 355f, 0.26217f, 0.33446f),
            new MapTiePoint("pvz-01 (ПВЗ)", 85f, 316f, 0.11102f, 0.40853f),
            new MapTiePoint("com-bank (Банк)", 390f, 380f, 0.36806f, 0.38608f),
            new MapTiePoint("com-business (Бизнес-центр)", 470f, 428f, 0.58839f, 0.45567f),
            new MapTiePoint("com-police (Полиция)", 470f, 378f, 0.58241f, 0.39169f),
            new MapTiePoint("com-hospital (Больница)", 470f, 333f, 0.58326f, 0.32997f),
            new MapTiePoint("com-city-hall (Мэрия)", 330f, 430f, 0.36806f, 0.32884f),
            new MapTiePoint("com-carservice (Автосервис)", 535f, 535f, 0.71990f, 0.60045f),
            new MapTiePoint("com-tuning (Тюнинг)", 600f, 535f, 0.82750f, 0.60157f),
            new MapTiePoint("player.home (Квартира №67 / bld-n3)", 210f, 630f, 0.32622f, 0.68799f),
        }.AsReadOnly();

        /// <summary>Предельно допустимая медианная ошибка привязки к картинке, метры.</summary>
        public const float MaxAcceptableReferenceErrorMeters = 10f;

        /// <summary>
        /// Измеряет, насколько привязка объясняет контрольные точки. Ничего не подгоняет:
        /// возвращает фактические ошибки в пикселях картинки.
        /// </summary>
        public static MapCalibrationAudit Audit(MapCalibration calibration, IReadOnlyList<MapTiePoint> ties = null)
        {
            if (calibration == null) throw new ArgumentNullException(nameof(calibration));
            IReadOnlyList<MapTiePoint> points = ties ?? TiePoints;

            var errors = new List<float>(points.Count);
            float maxError = 0f;
            string worstId = string.Empty;
            int ok = 0;
            for (int i = 0; i < points.Count; i++)
            {
                MapTiePoint tie = points[i];
                MapUv projected = calibration.WorldToMap(tie.World);
                float du = (projected.U - tie.Uv.U) * ReferenceRegionWidth;
                float dv = (projected.V - tie.Uv.V) * ReferenceRegionHeight;
                float error = (float)Math.Sqrt(du * du + dv * dv);
                errors.Add(error);
                if (error <= TieTolerancePixels) ok++;
                if (error > maxError)
                {
                    maxError = error;
                    worstId = tie.Id;
                }
            }

            errors.Sort();
            float median = errors.Count == 0
                ? 0f
                : errors.Count % 2 == 1
                    ? errors[errors.Count / 2]
                    : (errors[errors.Count / 2 - 1] + errors[errors.Count / 2]) * 0.5f;

            return new MapCalibrationAudit(ok, points.Count, median, maxError, ReferencePixelsPerMeter, worstId, maxError);
        }

        /// <summary>
        /// Строит привязку-подобие по контрольным точкам (для диагностики и тестов:
        /// показывает, что даже ортогональная модель не описывает картинку).
        /// </summary>
        public static MapCalibration FitReferenceSimilarity(out float rmsError)
        {
            IReadOnlyList<MapTiePoint> ties = TiePoints;
            var world = new MapPoint[ties.Count];
            var uv = new MapUv[ties.Count];
            for (int i = 0; i < ties.Count; i++)
            {
                world[i] = ties[i].World;
                uv[i] = ties[i].Uv;
            }
            return MapCalibration.FitSimilarity(world, uv, out rmsError);
        }

        /// <summary>Строит гомографию по контрольным точкам и возвращает её привязку.</summary>
        public static MapCalibration FitReferencePerspective()
        {
            IReadOnlyList<MapTiePoint> ties = TiePoints;
            var world = new MapPoint[ties.Count];
            var uv = new MapUv[ties.Count];
            for (int i = 0; i < ties.Count; i++)
            {
                world[i] = ties[i].World;
                uv[i] = ties[i].Uv;
            }
            double[] h = MapCalibration.SolvePerspective(world, uv);
            if (h == null) return null;
            return new MapCalibration(DistrictSize, DistrictSize, 0f, 0f, 0f, false, h, 0f, 0f);
        }
    }
}

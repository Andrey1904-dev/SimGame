// Life From Zero — UI/Runtime (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// Процедурные иконки и скруглённые подложки.
//
// Зачем процедурные: в репозитории нет PNG-иконок, а тянуть сторонние пакеты
// запрещено (§17). Иконки рисуются SDF-функциями в текстуру 64×64 один раз и
// кэшируются: повторное открытие окон не создаёт новые текстуры (§17).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Набор иконок интерфейса.</summary>
    public enum UiIconKind
    {
        None = 0,

        // Меню паузы
        Play, Save, Load, Settings, Controls, Home, Power, Crown,

        // Заголовки блоков
        Calendar, Clock, Target, CheckEmpty, CheckDone, Chart, Grid,

        // Статусы
        Heart, ForkKnife, Bolt, Smile, Coins,

        // Быстрые ссылки
        Phone, Bag, Pin, Tasks,

        // Телефон
        Message, ContactsBook, Wallet, Box, BookOpen, Megaphone, Graduation, Image,
        Back, Send, Plus, Mic, Video, CallEnd, CallIncoming, CallOutgoing,
        Star, Lock, Camera, Flashlight, Route, Cross, Shield, Wrench, Diamond, Mug, Fork,

        // Карта/HUD
        Compass, Player, Order, Flag, Trash, Center, ZoomIn, ZoomOut,
    }

    /// <summary>Кэш спрайтов и подложек. Один экземпляр на игру, ресурсы освобождаются.</summary>
    public sealed class UiSpriteCache
    {
        private const int IconSize = 64;

        private readonly Dictionary<UiIconKind, Sprite> _icons = new Dictionary<UiIconKind, Sprite>();
        private readonly Dictionary<int, Sprite> _rounded = new Dictionary<int, Sprite>();
        private readonly Dictionary<int, Sprite> _gradients = new Dictionary<int, Sprite>();
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

        /// <summary>Иконка заданного вида (белая маска; цвет задаётся компонентом Image).</summary>
        public Sprite Icon(UiIconKind kind)
        {
            Sprite sprite;
            if (_icons.TryGetValue(kind, out sprite) && sprite != null) return sprite;

            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            texture.name = "UI icon " + kind;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
            {
                float v = (y + 0.5f) / IconSize;
                for (int x = 0; x < IconSize; x++)
                {
                    float u = (x + 0.5f) / IconSize;
                    float distance = IconSdf.Evaluate(kind, u, v);
                    byte alpha = (byte)(Mathf.Clamp01(0.5f - distance * IconSize) * 255f);
                    pixels[y * IconSize + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            sprite = Sprite.Create(texture, new Rect(0, 0, IconSize, IconSize), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect);
            sprite.name = "UI icon " + kind;
            _icons[kind] = sprite;
            _owned.Add(texture);
            _owned.Add(sprite);
            return sprite;
        }

        /// <summary>Скруглённый прямоугольник (9-slice). Радиус в пикселях текстуры.</summary>
        public Sprite Rounded(int radius)
        {
            radius = Mathf.Clamp(radius, 1, 48);
            Sprite sprite;
            if (_rounded.TryGetValue(radius, out sprite) && sprite != null) return sprite;

            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "UI rounded " + radius;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, 0f, x - (size - radius) + 0.5f);
                    float dy = Mathf.Max(radius - y - 0.5f, 0f, y - (size - radius) + 0.5f);
                    byte alpha = (byte)(Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            sprite.name = "UI rounded " + radius;
            _rounded[radius] = sprite;
            _owned.Add(texture);
            _owned.Add(sprite);
            return sprite;
        }

        /// <summary>Вертикальный градиент 1×64 (фон экрана телефона).</summary>
        public Sprite VerticalGradient(int steps)
        {
            steps = Mathf.Clamp(steps, 2, 256);
            Sprite sprite;
            if (_gradients.TryGetValue(steps, out sprite) && sprite != null) return sprite;

            var texture = new Texture2D(1, steps, TextureFormat.RGBA32, false);
            texture.name = "UI gradient " + steps;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[steps];
            for (int i = 0; i < steps; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, 1, steps), new Vector2(0.5f, 0.5f), 100f);
            _gradients[steps] = sprite;
            _owned.Add(texture);
            _owned.Add(sprite);
            return sprite;
        }

        /// <summary>Круг (для аватаров и маркеров).</summary>
        public Sprite Circle(int size = 64) => Rounded(size / 2);

        public void Dispose()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null) UnityEngine.Object.Destroy(_owned[i]);
            }
            _owned.Clear();
            _icons.Clear();
            _rounded.Clear();
            _gradients.Clear();
        }
    }

    /// <summary>SDF-описание иконок: 0 — внутри, &gt;0 — снаружи (в долях стороны).</summary>
    internal static class IconSdf
    {
        private const float Stroke = 0.052f;

        public static float Evaluate(UiIconKind kind, float x, float y)
        {
            switch (kind)
            {
                case UiIconKind.Play:
                    return Triangle(x, y, 0.30f, 0.18f, 0.30f, 0.82f, 0.80f, 0.50f);

                case UiIconKind.Save:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.50f, 0.30f, 0.32f, 0.05f, Stroke),
                        Mathf.Min(
                            BoxOutline(x, y, 0.46f, 0.68f, 0.16f, 0.11f, 0.02f, Stroke * 0.85f),
                            BoxOutline(x, y, 0.50f, 0.30f, 0.20f, 0.12f, 0.02f, Stroke * 0.85f)));

                case UiIconKind.Load:
                    return Mathf.Min(
                        Mathf.Min(
                            Seg(x, y, 0.22f, 0.46f, 0.22f, 0.22f, Stroke),
                            Mathf.Min(Seg(x, y, 0.22f, 0.22f, 0.78f, 0.22f, Stroke), Seg(x, y, 0.78f, 0.22f, 0.78f, 0.46f, Stroke))),
                        Mathf.Min(
                            Seg(x, y, 0.50f, 0.34f, 0.50f, 0.80f, Stroke),
                            Mathf.Min(Seg(x, y, 0.33f, 0.62f, 0.50f, 0.80f, Stroke), Seg(x, y, 0.67f, 0.62f, 0.50f, 0.80f, Stroke))));

                case UiIconKind.Settings:
                {
                    float outer = Ring(x, y, 0.50f, 0.50f, 0.21f, Stroke * 1.1f);
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI * 0.25f;
                        outer = Mathf.Min(outer, Circle(x, y, 0.50f + Mathf.Cos(angle) * 0.30f,
                            0.50f + Mathf.Sin(angle) * 0.30f, 0.055f));
                    }
                    return Mathf.Max(outer, -Circle(x, y, 0.50f, 0.50f, 0.10f));
                }

                case UiIconKind.Controls:
                {
                    float d = BoxOutline(x, y, 0.50f, 0.48f, 0.36f, 0.24f, 0.06f, Stroke);
                    for (int column = 0; column < 4; column++)
                    {
                        float keyX = 0.29f + column * 0.14f;
                        d = Mathf.Min(d, BoxFill(x, y, keyX, 0.58f, 0.032f, 0.032f));
                        if (column == 0 || column == 2) d = Mathf.Min(d, BoxFill(x, y, keyX, 0.40f, 0.032f, 0.032f));
                    }
                    return Mathf.Min(d, Seg(x, y, 0.55f, 0.40f, 0.66f, 0.40f, Stroke * 0.9f));
                }

                case UiIconKind.Home:
                    return Mathf.Min(
                        Mathf.Min(Seg(x, y, 0.16f, 0.52f, 0.50f, 0.82f, Stroke), Seg(x, y, 0.50f, 0.82f, 0.84f, 0.52f, Stroke)),
                        Mathf.Min(
                            Mathf.Min(Seg(x, y, 0.26f, 0.54f, 0.26f, 0.18f, Stroke), Seg(x, y, 0.74f, 0.54f, 0.74f, 0.18f, Stroke)),
                            Mathf.Min(Seg(x, y, 0.26f, 0.18f, 0.74f, 0.18f, Stroke),
                                BoxOutline(x, y, 0.50f, 0.32f, 0.08f, 0.14f, 0.01f, Stroke * 0.85f))));

                case UiIconKind.Power:
                {
                    float ring = Ring(x, y, 0.50f, 0.46f, 0.28f, Stroke);
                    if (y > 0.58f && Mathf.Abs(x - 0.50f) < 0.16f) ring = 1f;
                    return Mathf.Min(ring, Seg(x, y, 0.50f, 0.48f, 0.50f, 0.84f, Stroke));
                }

                case UiIconKind.Crown:
                    return Mathf.Min(
                        Seg(x, y, 0.14f, 0.30f, 0.86f, 0.30f, Stroke),
                        Mathf.Min(
                            Mathf.Min(Seg(x, y, 0.14f, 0.30f, 0.10f, 0.80f, Stroke), Seg(x, y, 0.10f, 0.80f, 0.36f, 0.50f, Stroke)),
                            Mathf.Min(
                                Mathf.Min(Seg(x, y, 0.36f, 0.50f, 0.50f, 0.86f, Stroke), Seg(x, y, 0.50f, 0.86f, 0.64f, 0.50f, Stroke)),
                                Mathf.Min(Seg(x, y, 0.64f, 0.50f, 0.90f, 0.80f, Stroke), Seg(x, y, 0.90f, 0.80f, 0.86f, 0.30f, Stroke)))));

                case UiIconKind.Calendar:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.46f, 0.32f, 0.28f, 0.05f, Stroke),
                        Mathf.Min(
                            Seg(x, y, 0.18f, 0.58f, 0.82f, 0.58f, Stroke * 0.9f),
                            Mathf.Min(Seg(x, y, 0.34f, 0.70f, 0.34f, 0.84f, Stroke), Seg(x, y, 0.66f, 0.70f, 0.66f, 0.84f, Stroke))));

                case UiIconKind.Clock:
                    return Mathf.Min(
                        Ring(x, y, 0.50f, 0.50f, 0.33f, Stroke),
                        Mathf.Min(Seg(x, y, 0.50f, 0.50f, 0.50f, 0.74f, Stroke), Seg(x, y, 0.50f, 0.50f, 0.68f, 0.40f, Stroke)));

                case UiIconKind.Target:
                    return Mathf.Min(
                        Mathf.Min(Ring(x, y, 0.48f, 0.48f, 0.30f, Stroke), Ring(x, y, 0.48f, 0.48f, 0.15f, Stroke * 0.9f)),
                        Mathf.Min(Circle(x, y, 0.48f, 0.48f, 0.045f), Seg(x, y, 0.48f, 0.48f, 0.82f, 0.82f, Stroke)));

                case UiIconKind.CheckEmpty:
                    return BoxOutline(x, y, 0.50f, 0.50f, 0.32f, 0.32f, 0.07f, Stroke);

                case UiIconKind.CheckDone:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.50f, 0.32f, 0.32f, 0.07f, Stroke),
                        Mathf.Min(Seg(x, y, 0.30f, 0.50f, 0.45f, 0.34f, Stroke * 1.1f), Seg(x, y, 0.45f, 0.34f, 0.72f, 0.66f, Stroke * 1.1f)));

                case UiIconKind.Chart:
                    return Mathf.Min(
                        BoxFill(x, y, 0.24f, 0.32f, 0.055f, 0.14f),
                        Mathf.Min(BoxFill(x, y, 0.50f, 0.44f, 0.055f, 0.24f), BoxFill(x, y, 0.76f, 0.54f, 0.055f, 0.32f)));

                case UiIconKind.Grid:
                    return Mathf.Min(
                        Mathf.Min(BoxOutline(x, y, 0.34f, 0.66f, 0.13f, 0.13f, 0.03f, Stroke), BoxOutline(x, y, 0.66f, 0.66f, 0.13f, 0.13f, 0.03f, Stroke)),
                        Mathf.Min(BoxOutline(x, y, 0.34f, 0.34f, 0.13f, 0.13f, 0.03f, Stroke), BoxOutline(x, y, 0.66f, 0.34f, 0.13f, 0.13f, 0.03f, Stroke)));

                case UiIconKind.Heart:
                {
                    float lobes = Mathf.Min(Circle(x, y, 0.36f, 0.60f, 0.17f), Circle(x, y, 0.64f, 0.60f, 0.17f));
                    float body = Triangle(x, y, 0.20f, 0.58f, 0.80f, 0.58f, 0.50f, 0.16f);
                    return Mathf.Min(lobes, body);
                }

                case UiIconKind.ForkKnife:
                {
                    float fork = Mathf.Min(
                        Seg(x, y, 0.30f, 0.14f, 0.30f, 0.86f, Stroke),
                        Mathf.Min(Seg(x, y, 0.20f, 0.86f, 0.20f, 0.60f, Stroke * 0.75f),
                            Mathf.Min(Seg(x, y, 0.40f, 0.86f, 0.40f, 0.60f, Stroke * 0.75f),
                                Seg(x, y, 0.20f, 0.60f, 0.40f, 0.60f, Stroke * 0.75f))));
                    float knife = Mathf.Min(Seg(x, y, 0.70f, 0.14f, 0.70f, 0.86f, Stroke),
                        BoxFill(x, y, 0.64f, 0.66f, 0.055f, 0.16f));
                    return Mathf.Min(fork, knife);
                }

                case UiIconKind.Fork:
                    return Mathf.Min(
                        Seg(x, y, 0.40f, 0.10f, 0.40f, 0.88f, Stroke),
                        Mathf.Min(Seg(x, y, 0.26f, 0.88f, 0.26f, 0.60f, Stroke * 0.8f),
                            Mathf.Min(Seg(x, y, 0.54f, 0.88f, 0.54f, 0.60f, Stroke * 0.8f),
                                Seg(x, y, 0.26f, 0.60f, 0.54f, 0.60f, Stroke * 0.8f))));

                case UiIconKind.Bolt:
                    return Mathf.Min(
                        Triangle(x, y, 0.58f, 0.86f, 0.24f, 0.44f, 0.52f, 0.44f),
                        Triangle(x, y, 0.42f, 0.14f, 0.50f, 0.52f, 0.78f, 0.52f));

                case UiIconKind.Smile:
                {
                    float face = Ring(x, y, 0.50f, 0.50f, 0.34f, Stroke);
                    float eyes = Mathf.Min(Circle(x, y, 0.38f, 0.60f, 0.045f), Circle(x, y, 0.62f, 0.60f, 0.045f));
                    float smile = Ring(x, y, 0.50f, 0.50f, 0.19f, Stroke);
                    if (y > 0.47f) smile = 1f;
                    return Mathf.Max(face, -Mathf.Min(eyes, smile));
                }

                case UiIconKind.Coins:
                    return Mathf.Min(Ring(x, y, 0.42f, 0.40f, 0.21f, Stroke), Ring(x, y, 0.58f, 0.60f, 0.21f, Stroke));

                case UiIconKind.Phone:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.50f, 0.22f, 0.34f, 0.05f, Stroke),
                        Mathf.Min(Seg(x, y, 0.44f, 0.76f, 0.56f, 0.76f, Stroke * 0.8f),
                            Circle(x, y, 0.50f, 0.26f, 0.035f)));

                case UiIconKind.Bag:
                {
                    float body = BoxOutline(x, y, 0.50f, 0.41f, 0.28f, 0.21f, 0.04f, Stroke);
                    float handle = Ring(x, y, 0.50f, 0.63f, 0.12f, Stroke);
                    if (y < 0.62f) handle = 1f;
                    return Mathf.Min(body, handle);
                }

                case UiIconKind.Box:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.50f, 0.28f, 0.28f, 0.04f, Stroke),
                        Mathf.Min(Seg(x, y, 0.22f, 0.50f, 0.78f, 0.50f, Stroke * 0.9f),
                            Seg(x, y, 0.50f, 0.50f, 0.50f, 0.78f, Stroke * 0.9f)));

                case UiIconKind.Pin:
                case UiIconKind.Flag:
                    return Mathf.Min(
                        Mathf.Min(Ring(x, y, 0.50f, 0.60f, 0.22f, Stroke), Circle(x, y, 0.50f, 0.60f, 0.07f)),
                        Mathf.Min(Seg(x, y, 0.31f, 0.50f, 0.50f, 0.16f, Stroke), Seg(x, y, 0.69f, 0.50f, 0.50f, 0.16f, Stroke)));

                case UiIconKind.Tasks:
                    return Mathf.Min(
                        Mathf.Min(BoxOutline(x, y, 0.24f, 0.66f, 0.07f, 0.07f, 0.01f, Stroke * 0.85f), Seg(x, y, 0.40f, 0.66f, 0.82f, 0.66f, Stroke)),
                        Mathf.Min(BoxOutline(x, y, 0.24f, 0.34f, 0.07f, 0.07f, 0.01f, Stroke * 0.85f), Seg(x, y, 0.40f, 0.34f, 0.82f, 0.34f, Stroke)));

                case UiIconKind.Message:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.53f, 0.32f, 0.24f, 0.07f, Stroke),
                        Mathf.Min(
                            Mathf.Min(Seg(x, y, 0.36f, 0.30f, 0.28f, 0.16f, Stroke), Seg(x, y, 0.36f, 0.30f, 0.48f, 0.30f, Stroke)),
                            Mathf.Min(Seg(x, y, 0.36f, 0.60f, 0.64f, 0.60f, Stroke * 0.85f), Seg(x, y, 0.36f, 0.47f, 0.58f, 0.47f, Stroke * 0.85f))));

                case UiIconKind.ContactsBook:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.54f, 0.50f, 0.24f, 0.30f, 0.04f, Stroke),
                        Mathf.Min(
                            Circle(x, y, 0.54f, 0.58f, 0.09f),
                            Mathf.Min(Seg(x, y, 0.38f, 0.36f, 0.70f, 0.36f, Stroke * 0.9f),
                                Mathf.Min(Seg(x, y, 0.24f, 0.70f, 0.24f, 0.30f, Stroke * 0.9f),
                                    Mathf.Min(Seg(x, y, 0.14f, 0.72f, 0.24f, 0.72f, Stroke * 0.8f),
                                        Seg(x, y, 0.14f, 0.30f, 0.24f, 0.30f, Stroke * 0.8f))))));

                case UiIconKind.Wallet:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.50f, 0.30f, 0.22f, 0.05f, Stroke),
                        Mathf.Min(Seg(x, y, 0.20f, 0.60f, 0.80f, 0.60f, Stroke * 0.8f),
                            Mathf.Min(BoxFill(x, y, 0.68f, 0.44f, 0.07f, 0.05f),
                                Seg(x, y, 0.20f, 0.34f, 0.50f, 0.34f, Stroke * 0.8f))));

                case UiIconKind.BookOpen:
                    return Mathf.Min(
                        Mathf.Min(Seg(x, y, 0.50f, 0.24f, 0.50f, 0.74f, Stroke),
                            Mathf.Min(Seg(x, y, 0.50f, 0.74f, 0.18f, 0.84f, Stroke), Seg(x, y, 0.18f, 0.84f, 0.18f, 0.34f, Stroke))),
                        Mathf.Min(Seg(x, y, 0.18f, 0.34f, 0.50f, 0.24f, Stroke),
                            Mathf.Min(Seg(x, y, 0.50f, 0.74f, 0.82f, 0.84f, Stroke),
                                Mathf.Min(Seg(x, y, 0.82f, 0.84f, 0.82f, 0.34f, Stroke), Seg(x, y, 0.82f, 0.34f, 0.50f, 0.24f, Stroke)))));

                case UiIconKind.Megaphone:
                    return Mathf.Min(
                        Triangle(x, y, 0.22f, 0.36f, 0.22f, 0.64f, 0.68f, 0.82f),
                        Mathf.Min(
                            Seg(x, y, 0.68f, 0.82f, 0.68f, 0.18f, Stroke),
                            Mathf.Min(Seg(x, y, 0.68f, 0.18f, 0.22f, 0.36f, Stroke),
                                Seg(x, y, 0.34f, 0.34f, 0.34f, 0.10f, Stroke))));

                case UiIconKind.Graduation:
                    return Mathf.Min(
                        Triangle(x, y, 0.50f, 0.82f, 0.10f, 0.58f, 0.50f, 0.34f),
                        Mathf.Min(
                            Triangle(x, y, 0.50f, 0.82f, 0.90f, 0.58f, 0.50f, 0.34f),
                            Mathf.Min(Seg(x, y, 0.28f, 0.44f, 0.28f, 0.26f, Stroke * 0.9f),
                                Seg(x, y, 0.72f, 0.44f, 0.72f, 0.26f, Stroke * 0.9f))));

                case UiIconKind.Image:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.50f, 0.32f, 0.26f, 0.05f, Stroke),
                        Mathf.Min(Circle(x, y, 0.38f, 0.62f, 0.055f),
                            Seg(x, y, 0.20f, 0.28f, 0.44f, 0.52f, Stroke * 0.9f)));

                case UiIconKind.Back:
                    return Mathf.Min(
                        Seg(x, y, 0.34f, 0.78f, 0.62f, 0.50f, Stroke),
                        Mathf.Min(Seg(x, y, 0.62f, 0.50f, 0.34f, 0.22f, Stroke),
                            Seg(x, y, 0.36f, 0.50f, 0.80f, 0.50f, Stroke)));

                case UiIconKind.Send:
                    return Mathf.Min(
                        Seg(x, y, 0.16f, 0.50f, 0.86f, 0.84f, Stroke),
                        Mathf.Min(Seg(x, y, 0.86f, 0.84f, 0.60f, 0.16f, Stroke),
                            Seg(x, y, 0.60f, 0.16f, 0.16f, 0.50f, Stroke)));

                case UiIconKind.Plus:
                    return Mathf.Min(Seg(x, y, 0.50f, 0.24f, 0.50f, 0.76f, Stroke), Seg(x, y, 0.24f, 0.50f, 0.76f, 0.50f, Stroke));

                case UiIconKind.Cross:
                    return Mathf.Min(Seg(x, y, 0.30f, 0.70f, 0.70f, 0.30f, Stroke), Seg(x, y, 0.30f, 0.30f, 0.70f, 0.70f, Stroke));

                case UiIconKind.Mic:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.60f, 0.10f, 0.18f, 0.10f, Stroke),
                        Mathf.Min(Ring(x, y, 0.50f, 0.54f, 0.24f, Stroke * 0.85f),
                            Mathf.Min(Seg(x, y, 0.50f, 0.30f, 0.50f, 0.18f, Stroke),
                                Seg(x, y, 0.38f, 0.18f, 0.62f, 0.18f, Stroke))));

                case UiIconKind.Video:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.44f, 0.50f, 0.24f, 0.18f, 0.04f, Stroke),
                        Triangle(x, y, 0.66f, 0.58f, 0.86f, 0.70f, 0.86f, 0.30f));

                case UiIconKind.CallEnd:
                case UiIconKind.CallIncoming:
                case UiIconKind.CallOutgoing:
                {
                    // Трубка: дуга с двумя «ушками».
                    float handset = Ring(x, y, 0.50f, 0.56f, 0.30f, Stroke * 1.05f);
                    if (y > 0.56f) handset = 1f;
                    float ends = Mathf.Min(
                        Mathf.Max(BoxFill(x, y, 0.20f, 0.56f, 0.075f, 0.075f), 0f),
                        Mathf.Max(BoxFill(x, y, 0.80f, 0.56f, 0.075f, 0.075f), 0f));
                    float result = Mathf.Min(handset, ends);
                    if (kind == UiIconKind.CallEnd)
                        result = Mathf.Min(result, Seg(x, y, 0.50f, 0.78f, 0.50f, 0.90f, Stroke * 0.9f));
                    return result;
                }

                case UiIconKind.Star:
                {
                    float best = Circle(x, y, 0.50f, 0.52f, 0.30f);
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = -Mathf.PI * 0.5f + i * Mathf.PI * 0.4f;
                        best = Mathf.Min(best, Seg(x, y, 0.50f, 0.52f,
                            0.50f + Mathf.Cos(angle) * 0.42f, 0.52f + Mathf.Sin(angle) * 0.42f, Stroke * 0.55f));
                    }
                    return best;
                }

                case UiIconKind.Lock:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.36f, 0.24f, 0.18f, 0.04f, Stroke),
                        Mathf.Max(Ring(x, y, 0.50f, 0.60f, 0.15f, Stroke), 0.60f - y > 0f ? -1f : 1f));

                case UiIconKind.Camera:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.46f, 0.30f, 0.20f, 0.05f, Stroke),
                        Mathf.Min(Ring(x, y, 0.50f, 0.46f, 0.12f, Stroke * 0.9f),
                            BoxFill(x, y, 0.50f, 0.74f, 0.10f, 0.05f)));

                case UiIconKind.Flashlight:
                    return Mathf.Min(
                        Seg(x, y, 0.50f, 0.34f, 0.50f, 0.14f, Stroke * 1.2f),
                        Mathf.Min(Triangle(x, y, 0.34f, 0.36f, 0.66f, 0.36f, 0.50f, 0.86f),
                            Seg(x, y, 0.34f, 0.36f, 0.66f, 0.36f, Stroke)));

                case UiIconKind.Route:
                    return Mathf.Min(
                        Mathf.Min(Seg(x, y, 0.26f, 0.24f, 0.26f, 0.62f, Stroke), Seg(x, y, 0.26f, 0.62f, 0.70f, 0.62f, Stroke)),
                        Mathf.Min(Seg(x, y, 0.70f, 0.62f, 0.70f, 0.80f, Stroke), Circle(x, y, 0.70f, 0.86f, 0.07f)));

                case UiIconKind.Shield:
                    return Mathf.Min(
                        Mathf.Min(Seg(x, y, 0.24f, 0.78f, 0.24f, 0.46f, Stroke), Seg(x, y, 0.76f, 0.78f, 0.76f, 0.46f, Stroke)),
                        Mathf.Min(Seg(x, y, 0.24f, 0.78f, 0.50f, 0.14f, Stroke),
                            Mathf.Min(Seg(x, y, 0.76f, 0.78f, 0.50f, 0.14f, Stroke),
                                Seg(x, y, 0.24f, 0.46f, 0.76f, 0.46f, Stroke * 0.9f))));

                case UiIconKind.Wrench:
                    return Mathf.Min(
                        Seg(x, y, 0.30f, 0.22f, 0.70f, 0.66f, Stroke),
                        Mathf.Max(Ring(x, y, 0.74f, 0.74f, 0.15f, Stroke), 0.74f - 0.15f - y));

                case UiIconKind.Diamond:
                    return Mathf.Min(
                        Mathf.Min(Seg(x, y, 0.50f, 0.14f, 0.18f, 0.44f, Stroke), Seg(x, y, 0.50f, 0.14f, 0.82f, 0.44f, Stroke)),
                        Mathf.Min(Seg(x, y, 0.18f, 0.44f, 0.50f, 0.86f, Stroke), Seg(x, y, 0.82f, 0.44f, 0.50f, 0.86f, Stroke)));

                case UiIconKind.Mug:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.44f, 0.48f, 0.20f, 0.22f, 0.04f, Stroke),
                        Mathf.Max(Ring(x, y, 0.74f, 0.50f, 0.13f, Stroke * 0.9f), 0.5f - y));

                case UiIconKind.Compass:
                    return Mathf.Min(
                        Ring(x, y, 0.50f, 0.50f, 0.34f, Stroke),
                        Triangle(x, y, 0.50f, 0.76f, 0.40f, 0.40f, 0.60f, 0.40f));

                case UiIconKind.Player:
                    return Mathf.Min(Circle(x, y, 0.50f, 0.62f, 0.14f), Triangle(x, y, 0.24f, 0.18f, 0.76f, 0.18f, 0.50f, 0.52f));

                case UiIconKind.Order:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.48f, 0.24f, 0.24f, 0.04f, Stroke),
                        Mathf.Min(Seg(x, y, 0.32f, 0.62f, 0.68f, 0.62f, Stroke * 0.85f),
                            Seg(x, y, 0.50f, 0.62f, 0.50f, 0.72f, Stroke * 0.85f)));

                case UiIconKind.Trash:
                    return Mathf.Min(
                        BoxOutline(x, y, 0.50f, 0.40f, 0.18f, 0.22f, 0.03f, Stroke),
                        Mathf.Min(Seg(x, y, 0.24f, 0.74f, 0.76f, 0.74f, Stroke),
                            Seg(x, y, 0.42f, 0.84f, 0.58f, 0.84f, Stroke)));

                case UiIconKind.Center:
                    return Mathf.Min(
                        Mathf.Min(Ring(x, y, 0.50f, 0.50f, 0.20f, Stroke), Circle(x, y, 0.50f, 0.50f, 0.05f)),
                        Mathf.Min(Seg(x, y, 0.50f, 0.94f, 0.50f, 0.78f, Stroke), Seg(x, y, 0.50f, 0.22f, 0.50f, 0.06f, Stroke)));

                case UiIconKind.ZoomIn:
                    return Mathf.Min(
                        Mathf.Min(Ring(x, y, 0.44f, 0.56f, 0.24f, Stroke), Seg(x, y, 0.26f, 0.32f, 0.10f, 0.10f, Stroke)),
                        Mathf.Min(Seg(x, y, 0.32f, 0.56f, 0.56f, 0.56f, Stroke * 0.9f), Seg(x, y, 0.44f, 0.44f, 0.44f, 0.68f, Stroke * 0.9f)));

                case UiIconKind.ZoomOut:
                    return Mathf.Min(
                        Mathf.Min(Ring(x, y, 0.44f, 0.56f, 0.24f, Stroke), Seg(x, y, 0.26f, 0.32f, 0.10f, 0.10f, Stroke)),
                        Seg(x, y, 0.32f, 0.56f, 0.56f, 0.56f, Stroke * 0.9f));

                default:
                    return 1f;
            }
        }

        private static float Circle(float x, float y, float cx, float cy, float r) =>
            Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        private static float Ring(float x, float y, float cx, float cy, float r, float halfWidth) =>
            Mathf.Abs(Circle(x, y, cx, cy, r)) - halfWidth;

        private static float BoxFill(float x, float y, float cx, float cy, float hx, float hy, float r = 0.01f)
        {
            float dx = Mathf.Abs(x - cx) - hx + r;
            float dy = Mathf.Abs(y - cy) - hy + r;
            return Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f))
                   + Mathf.Min(Mathf.Max(dx, dy), 0f) - r;
        }

        private static float BoxOutline(float x, float y, float cx, float cy, float hx, float hy, float r, float halfWidth) =>
            Mathf.Abs(BoxFill(x, y, cx, cy, hx, hy, r)) - halfWidth;

        private static float Seg(float px, float py, float ax, float ay, float bx, float by, float r)
        {
            float vx = bx - ax, vy = by - ay;
            float wx = px - ax, wy = py - ay;
            float t = Mathf.Clamp01((wx * vx + wy * vy) / (vx * vx + vy * vy + 1e-6f));
            float dx = wx - vx * t, dy = wy - vy * t;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        private static float Triangle(float px, float py, float x0, float y0, float x1, float y1, float x2, float y2)
        {
            float d0 = Seg(px, py, x0, y0, x1, y1, 0f);
            float d1 = Seg(px, py, x1, y1, x2, y2, 0f);
            float d2 = Seg(px, py, x2, y2, x0, y0, 0f);
            float minEdge = Mathf.Min(d0, Mathf.Min(d1, d2));
            float c0 = (x1 - x0) * (py - y0) - (y1 - y0) * (px - x0);
            float c1 = (x2 - x1) * (py - y1) - (y2 - y1) * (px - x1);
            float c2 = (x0 - x2) * (py - y2) - (y0 - y2) * (px - x2);
            bool inside = (c0 >= 0f && c1 >= 0f && c2 >= 0f) || (c0 <= 0f && c1 <= 0f && c2 <= 0f);
            return inside ? -minEdge : minEdge;
        }
    }
}

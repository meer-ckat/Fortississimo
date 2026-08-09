using System;
using System.Collections.Generic;
using UnityEngine;

namespace IMGUI
{
    public static class GUITween
    {
        private static readonly Dictionary<GUIItem, Action<GUIItem, float>> running = new();
        private static readonly Dictionary<GUIItem, Action<GUIItem, float>> fading = new();

        private static void ApplyPos(GUIItem item, Vector2 pos)
        {
            if (item is GUIGroup)
                item.SetRect(new Rect(pos, item.Size));
            else
                item.SetPos(pos);
        }

        public static void MoveTo(
            this GUIItem item,
            Vector2 to,
            float duration,
            float delay = 0f,
            Func<float, float> ease = null,
            Action onComplete = null)
        {
            if (item == null) return;

            KillMove(item);

            ease ??= TweenHelper.EaseOutSine;

            float time = 0f;
            bool begun = false;
            Vector2 from = default;
            Action<GUIItem, float> handler = null;

            handler = (gui, dt) =>
            {
                time += dt;
                float local = time - delay;

                if (local < 0f)
                    return;

                // DrawItemLocal이 Draw 중에 Rect를 잠시 밀어둔 상태에서
                // MoveTo가 호출되면(버튼 클릭 등) from이 어긋난 좌표로 고정된다.
                // 첫 적용 프레임(Update)에 잡아서 진짜 위치를 사용한다.
                if (!begun)
                {
                    begun = true;
                    from = gui.Pos;
                }

                float x = duration > 0f
                    ? TweenHelper.XCalculator(local, 0f, duration)
                    : 1f;

                ApplyPos(gui, Vector2.LerpUnclamped(from, to, ease(x)));

                if (x < 1f)
                    return;

                ApplyPos(gui, to);

                gui.whenTick -= handler;
                running.Remove(gui);

                onComplete?.Invoke();
            };

            item.whenTick += handler;
            running[item] = handler;
        }

        public static void MoveIn(
            this GUIItem item,
            Vector2 offset,
            float duration,
            float delay = 0f,
            Func<float, float> ease = null,
            Action onComplete = null)
        {
            if (item == null) return;

            Vector2 target = item.Pos;

            ApplyPos(item, target + offset);
            item.MoveTo(target, duration, delay, ease, onComplete);
        }

        public static void MoveOut(
            this GUIItem item,
            Vector2 offset,
            float duration,
            float delay = 0f,
            Func<float, float> ease = null,
            Action onComplete = null)
        {
            if (item == null) return;

            item.MoveTo(item.Pos + offset, duration, delay, ease, onComplete);
        }

        public static void FadeTo(
            this GUIItem item,
            float to,
            float duration,
            float delay = 0f,
            Func<float, float> ease = null,
            Action onComplete = null)
        {
            if (item == null) return;

            KillFade(item);

            float from = item.Opacity;
            float time = 0f;

            to = Mathf.Clamp01(to);
            ease ??= TweenHelper.EaseInOutSine;

            Action<GUIItem, float> handler = null;

            handler = (gui, dt) =>
            {
                time += dt;
                float local = time - delay;

                if (local < 0f)
                    return;

                float x = duration > 0f
                    ? TweenHelper.XCalculator(local, 0f, duration)
                    : 1f;

                gui.Opacity = Mathf.LerpUnclamped(from, to, ease(x));

                if (x < 1f)
                    return;

                gui.Opacity = to;

                gui.whenTick -= handler;
                fading.Remove(gui);

                onComplete?.Invoke();
            };

            item.whenTick += handler;
            fading[item] = handler;
        }

        public static void FadeIn(
            this GUIItem item,
            float duration,
            float delay = 0f,
            Func<float, float> ease = null,
            Action onComplete = null)
        {
            if (item == null) return;

            item.Opacity = 0f;
            item.FadeTo(1f, duration, delay, ease, onComplete);
        }

        public static void FadeOut(
            this GUIItem item,
            float duration,
            float delay = 0f,
            Func<float, float> ease = null,
            Action onComplete = null)
        {
            item?.FadeTo(0f, duration, delay, ease, onComplete);
        }

        private static void KillMove(GUIItem item)
        {
            if (!running.TryGetValue(item, out Action<GUIItem, float> handler))
                return;

            item.whenTick -= handler;
            running.Remove(item);
        }

        private static void KillFade(GUIItem item)
        {
            if (!fading.TryGetValue(item, out Action<GUIItem, float> handler))
                return;

            item.whenTick -= handler;
            fading.Remove(item);
        }

        public static void Kill(GUIItem item)
        {
            if (item == null) return;

            KillMove(item);
            KillFade(item);
        }

        public static void Wave(
            IReadOnlyList<GUIItem> items,
            Vector2 offset,
            float duration,
            float step,
            Func<float, float> ease = null,
            bool inward = true,
            bool reverse = false,
            Action onAllComplete = null)
        {
            if (items == null || items.Count == 0)
            {
                onAllComplete?.Invoke();
                return;
            }

            int total = items.Count;
            int done = 0;

            Action tally = onAllComplete == null
                ? null
                : () =>
                {
                    done++;

                    if (done >= total)
                        onAllComplete();
                };

            for (int i = 0; i < total; i++)
            {
                GUIItem item = items[i];

                if (item == null)
                {
                    tally?.Invoke();
                    continue;
                }

                float delay = (reverse ? total - i - 1 : i) * step;

                if (inward)
                    item.MoveIn(offset, duration, delay, ease, tally);
                else
                    item.MoveOut(offset, duration, delay, ease, tally);
            }
        }
    }
}
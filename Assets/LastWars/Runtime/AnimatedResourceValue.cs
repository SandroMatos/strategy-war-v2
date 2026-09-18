using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastWars.Client
{
    // One animation per counter. A new target replaces the previous animation.
    public sealed class AnimatedResourceValue : MonoBehaviour
    {
        public float Duration = .65f;
        TMP_Text label;
        RectTransform fill;
        Image fillImage;
        Color normal, full;
        long shown, target, capacity;
        double start;
        float elapsed;
        bool initialized, animating;
        public long DisplayedValue => shown;
        public bool IsAnimating => animating;

        public void Initialize(TMP_Text text, RectTransform bar, Color normalColor, Color fullColor)
        {
            label = text; fill = bar; fillImage = bar.GetComponent<Image>();
            normal = normalColor; full = fullColor;
        }

        public void SetValue(long value, long maximum)
        {
            value = Math.Max(0, value); capacity = Math.Max(0, maximum);
            if (!initialized || !isActiveAndEnabled || Duration <= 0)
            {
                initialized = true; animating = false; shown = target = value;
            }
            else if (value != target)
            {
                // Kill the previous tween and restart from the number currently on screen.
                animating = false;
                start = shown; target = value; elapsed = 0;
                animating = shown != target;
            }
            Draw();
        }

        void Update()
        {
            if (!animating) return;
            elapsed += Time.unscaledDeltaTime;
            double t = Mathf.Clamp01(elapsed / Mathf.Max(.001f, Duration));
            double eased = 1 - Math.Pow(1 - t, 3);
            shown = t >= 1 ? target : (long)Math.Round(start + ((double)target - start) * eased);
            if (t >= 1) animating = false;
            Draw();
        }

        void OnDisable()
        {
            animating = false;
            if (initialized) { shown = target; Draw(); }
        }

        void Draw()
        {
            if (label == null || fill == null) return;
            label.text = capacity > 0 ? $"{shown:N0} / {capacity:N0}" : $"{shown:N0} / â€”";
            fill.anchorMax = new Vector2(capacity > 0 ? Mathf.Clamp01((float)((double)shown / capacity)) : 0, 1);
            fillImage.color = capacity > 0 && shown >= capacity ? full : normal;
        }
    }
}

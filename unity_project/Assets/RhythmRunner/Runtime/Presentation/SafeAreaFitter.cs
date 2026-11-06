using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect lastSafeArea;
        Vector2Int lastScreen;

        void OnEnable() { Apply(); }
        void Update()
        {
            if (lastSafeArea != Screen.safeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height) Apply();
        }

        void Apply()
        {
            Rect safe = Screen.safeArea;
            lastSafeArea = safe;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            RectTransform rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}

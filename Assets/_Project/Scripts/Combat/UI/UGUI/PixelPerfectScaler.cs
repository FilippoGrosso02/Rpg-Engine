using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// Keeps the 480x270 HUD at an INTEGER scale (x1, x2, x3...) so the pixel art never shimmers: the canvas scale factor is the
    /// largest whole number that fits, and any leftover window area is letterboxed (the 3D scene simply shows around the UI).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
    public class PixelPerfectScaler : MonoBehaviour
    {
        [SerializeField] private float referenceWidth = UiFactory.ScreenWidth;
        [SerializeField] private float referenceHeight = UiFactory.ScreenHeight;
        [Tooltip("The 480x270 RectTransform that holds the whole HUD; it is centred in the canvas.")]
        [SerializeField] private RectTransform uiRoot;

        private Canvas _canvas;
        private CanvasScaler _scaler;
        private int _lastWidth = -1;
        private int _lastHeight = -1;

        public int Scale { get; private set; } = 1;

        /// <summary>The largest integer scale at which the reference screen fits inside <paramref name="width"/> x <paramref name="height"/> (never below 1).</summary>
        public static int ComputeScale(float width, float height, float referenceWidth, float referenceHeight)
        {
            return Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(width / referenceWidth, height / referenceHeight)));
        }

        public void SetUiRoot(RectTransform root)
        {
            uiRoot = root;
        }

        private void OnEnable()
        {
            Apply(force: true);
        }

        private void Update()
        {
            Apply(force: false);
        }

        private void Apply(bool force)
        {
            if (!force && Screen.width == _lastWidth && Screen.height == _lastHeight)
            {
                return;
            }

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            _canvas = _canvas != null ? _canvas : GetComponent<Canvas>();
            _scaler = _scaler != null ? _scaler : GetComponent<CanvasScaler>();

            Scale = ComputeScale(Screen.width, Screen.height, referenceWidth, referenceHeight);
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _scaler.scaleFactor = Scale;

            if (uiRoot != null)
            {
                // Centre the 480x270 screen in the canvas, but snap its top-left corner to a whole SCREEN pixel: with an odd
                // leftover margin a plain centre could land on half a pixel and make every sprite shimmer.
                float canvasWidth = Screen.width / (float)Scale;
                float canvasHeight = Screen.height / (float)Scale;
                float left = Mathf.Floor((canvasWidth - referenceWidth) * 0.5f * Scale) / Scale;
                float top = Mathf.Floor((canvasHeight - referenceHeight) * 0.5f * Scale) / Scale;

                uiRoot.anchorMin = uiRoot.anchorMax = new Vector2(0f, 1f);
                uiRoot.pivot = new Vector2(0f, 1f);
                uiRoot.anchoredPosition = new Vector2(left, -top);
                uiRoot.sizeDelta = new Vector2(referenceWidth, referenceHeight);
            }
        }
    }
}

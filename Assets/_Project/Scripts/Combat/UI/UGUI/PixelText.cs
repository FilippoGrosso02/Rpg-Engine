using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// UI text in the 5x7 <see cref="PixelFont"/>: each character is one quad, optionally with a 1-pixel drop shadow. Never takes
    /// mouse input. Upper-cases everything (the font has no lower case), supports "\n", and positions glyphs on whole canvas units
    /// so it stays crisp under the integer-scaled HUD canvas.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PixelText : MaskableGraphic
    {
        public enum Align
        {
            Left,
            Center,
            Right
        }

        [SerializeField] private string _text = "";
        [SerializeField] private Align _align = Align.Left;
        [SerializeField] private bool _shadow = true;
        [SerializeField] private Color _shadowColor = new Color(0.102f, 0.067f, 0.051f, 1f);

        public string text
        {
            get => _text;
            set
            {
                value ??= "";
                if (_text == value)
                {
                    return;
                }

                _text = value;
                SetVerticesDirty();
            }
        }

        public Align alignment
        {
            get => _align;
            set
            {
                _align = value;
                SetVerticesDirty();
            }
        }

        public bool shadow
        {
            get => _shadow;
            set
            {
                _shadow = value;
                SetVerticesDirty();
            }
        }

        public Color shadowColor
        {
            get => _shadowColor;
            set
            {
                _shadowColor = value;
                SetVerticesDirty();
            }
        }

        public override Texture mainTexture => PixelFont.Texture;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }

        /// <summary>Width in canvas units of the widest line.</summary>
        public int MeasureWidth()
        {
            int widest = 0;
            foreach (var line in (_text ?? "").Split('\n'))
            {
                widest = Mathf.Max(widest, PixelFont.MeasureLine(line));
            }

            return widest;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (string.IsNullOrEmpty(_text))
            {
                return;
            }

            var rect = rectTransform.rect;
            var lines = _text.Split('\n');
            float blockHeight = lines.Length * PixelFont.LineHeight - (PixelFont.LineHeight - PixelFont.GlyphHeight);
            float top = Mathf.Round(rect.center.y + blockHeight * 0.5f);

            for (int pass = _shadow ? 0 : 1; pass < 2; pass++)
            {
                bool isShadow = pass == 0;
                var tint = isShadow ? new Color(_shadowColor.r, _shadowColor.g, _shadowColor.b, _shadowColor.a * color.a) : color;
                float offsetX = isShadow ? 1f : 0f;
                float offsetY = isShadow ? -1f : 0f;

                for (int l = 0; l < lines.Length; l++)
                {
                    string line = lines[l];
                    int width = PixelFont.MeasureLine(line);
                    float left = _align == Align.Left ? rect.xMin
                        : _align == Align.Center ? rect.center.x - width * 0.5f
                        : rect.xMax - width;
                    left = Mathf.Round(left);
                    float lineTop = top - l * PixelFont.LineHeight;

                    for (int i = 0; i < line.Length; i++)
                    {
                        if (!PixelFont.TryGetUv(line[i], out var uv))
                        {
                            continue;
                        }

                        float x = left + i * PixelFont.Advance + offsetX;
                        float y = lineTop - PixelFont.GlyphHeight + offsetY;
                        AddQuad(vh, x, y, x + PixelFont.GlyphWidth, y + PixelFont.GlyphHeight, uv, tint);
                    }
                }
            }
        }

        private static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Rect uv, Color tint)
        {
            int start = vh.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = tint;

            vertex.position = new Vector3(x0, y0);
            vertex.uv0 = new Vector2(uv.xMin, uv.yMin);
            vh.AddVert(vertex);
            vertex.position = new Vector3(x0, y1);
            vertex.uv0 = new Vector2(uv.xMin, uv.yMax);
            vh.AddVert(vertex);
            vertex.position = new Vector3(x1, y1);
            vertex.uv0 = new Vector2(uv.xMax, uv.yMax);
            vh.AddVert(vertex);
            vertex.position = new Vector3(x1, y0);
            vertex.uv0 = new Vector2(uv.xMax, uv.yMin);
            vh.AddVert(vertex);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}

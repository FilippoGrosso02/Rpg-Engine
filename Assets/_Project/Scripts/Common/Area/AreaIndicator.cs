using UnityEngine;

namespace Game.Common.Area
{
    /// <summary>
    /// Reusable ground area marker (move range, attack range, cones, lines...). Set a shape, a colour and where it sits, and the
    /// depth-projected shader paints it onto whatever geometry lies within its height window, so it follows stairs and slopes.
    /// Change <see cref="shape"/> (or call <see cref="SetRange"/>) at any time; it redraws on the next Show/Apply.
    /// </summary>
    [DisallowMultipleComponent]
    public class AreaIndicator : MonoBehaviour
    {
        const string DefaultMaterialPath = "Common/AreaIndicator";

        [Tooltip("Optional. Falls back to Resources/Common/AreaIndicator.")]
        public Material material;

        public AreaShape shape = AreaShape.Circle(3f);

        [Header("Look")]
        public Color fillColor = new Color(0.3f, 0.9f, 1f, 0.15f);
        public Color edgeColor = new Color(0.3f, 0.9f, 1f, 0.9f);
        [Min(0.005f)] public float edgeWidth = 0.08f;

        [Tooltip("Dashed edge: length of each dash in world units. 0 = solid line.")]
        [Min(0f)] public float dashLength = 0f;
        [Min(0f)] public float dashGap = 0f;

        [Header("Height window (around the origin's height)")]
        [Min(0.05f)] public float heightAbove = 1.5f;
        [Min(0.05f)] public float heightBelow = 1.5f;
        [Min(0f)] public float heightFade = 0.3f;

        static readonly int FillColorId = Shader.PropertyToID("_FillColor");
        static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int RangeId = Shader.PropertyToID("_Range");
        static readonly int InnerRadiusId = Shader.PropertyToID("_InnerRadius");
        static readonly int HalfAngleId = Shader.PropertyToID("_HalfAngle");
        static readonly int WidthId = Shader.PropertyToID("_Width");
        static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        static readonly int HeightAboveId = Shader.PropertyToID("_HeightAbove");
        static readonly int HeightBelowId = Shader.PropertyToID("_HeightBelow");
        static readonly int HeightFadeId = Shader.PropertyToID("_HeightFade");
        static readonly int DashLengthId = Shader.PropertyToID("_DashLength");
        static readonly int DashGapId = Shader.PropertyToID("_DashGap");
        static readonly int WorldToLocalId = Shader.PropertyToID("_AreaWorldToLocal");

        Transform _volume;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;

        public bool IsVisible => _volume != null && _volume.gameObject.activeSelf;

        /// <summary>Shows the area at <paramref name="origin"/>, facing <paramref name="forward"/> (flattened; zero keeps the current facing).</summary>
        public void Show(Vector3 origin, Vector3 forward)
        {
            forward.y = 0f;
            transform.position = origin;
            if (forward.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            }

            Apply();
            _volume.gameObject.SetActive(true);
        }

        /// <summary>Convenience for a plain circle of the given radius.</summary>
        public void ShowCircle(Vector3 center, float radius)
        {
            shape = AreaShape.Circle(radius, shape.kind == AreaShapeKind.Circle ? shape.innerRadius : 0f);
            Show(center, transform.forward);
        }

        /// <summary>Changes the main size of the current shape (radius, cone length or line length) and redraws if visible.</summary>
        public void SetRange(float range)
        {
            shape.range = Mathf.Max(0f, range);
            if (IsVisible) Apply();
        }

        public void Hide()
        {
            if (_volume != null) _volume.gameObject.SetActive(false);
        }

        /// <summary>Pushes the current settings to the shader and resizes the projector volume. Called by Show; call it yourself after editing fields on a visible indicator.</summary>
        public void Apply()
        {
            EnsureVolume();

            shape.LocalBounds(out Vector2 center, out Vector2 half);
            float pad = edgeWidth + 0.1f;
            float height = heightAbove + heightBelow;

            // The volume is a unit cube child; the parent keeps unit scale so the shader can use a rotation+translation matrix.
            _volume.localPosition = new Vector3(center.x, (heightAbove - heightBelow) * 0.5f, center.y);
            _volume.localScale = new Vector3((half.x + pad) * 2f, height, (half.y + pad) * 2f);

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(FillColorId, fillColor);
            _block.SetColor(EdgeColorId, edgeColor);
            _block.SetFloat(ShapeId, (float)shape.kind);
            _block.SetFloat(RangeId, shape.range);
            _block.SetFloat(InnerRadiusId, shape.innerRadius);
            _block.SetFloat(HalfAngleId, shape.HalfAngleRadians);
            _block.SetFloat(WidthId, shape.width);
            _block.SetFloat(EdgeWidthId, edgeWidth);
            _block.SetFloat(HeightAboveId, heightAbove);
            _block.SetFloat(HeightBelowId, heightBelow);
            _block.SetFloat(HeightFadeId, heightFade);
            _block.SetFloat(DashLengthId, dashLength);
            _block.SetFloat(DashGapId, dashGap);
            _block.SetMatrix(WorldToLocalId, Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).inverse);
            _renderer.SetPropertyBlock(_block);
        }

        void EnsureVolume()
        {
            if (_volume != null) return;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "AreaVolume";
            var collider = cube.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            _volume = cube.transform;
            _volume.SetParent(transform, false);

            _renderer = cube.GetComponent<MeshRenderer>();
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.sharedMaterial = material != null ? material : Resources.Load<Material>(DefaultMaterialPath);
            if (_renderer.sharedMaterial == null)
            {
                Debug.LogError("AreaIndicator: no material assigned and Resources/" + DefaultMaterialPath + " was not found.", this);
            }

            cube.SetActive(false);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Overworld
{
    /// <summary>A warm, gently flickering point light carried by a character. Added at runtime to the player and to party followers.</summary>
    public class CharacterTorch : MonoBehaviour
    {
        public Color color = new Color(1f, 0.62f, 0.3f);
        public float range = 10f;
        public float intensity = 3f;
        public Vector3 offset = new Vector3(0.25f, 1.7f, 0.35f);
        [Tooltip("The torch burns low while the leader sneaks (Hidden).")]
        public bool dimsWhenHidden;
        [Range(0f, 0.5f)] public float flicker = 0.12f;

        Light _light;
        float _seed;

        public static CharacterTorch Attach(GameObject character, bool dimsWhenHidden = false)
        {
            if (character == null) return null;
            var existing = character.GetComponentInChildren<CharacterTorch>();
            if (existing != null) return existing;

            var torch = character.AddComponent<CharacterTorch>();
            torch.dimsWhenHidden = dimsWhenHidden;
            return torch;
        }

        void Awake()
        {
            _seed = Random.Range(0f, 100f);

            var go = new GameObject("Torch");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = color;
            _light.range = range;
            _light.intensity = intensity;
            _light.shadows = LightShadows.None;
            _light.renderMode = LightRenderMode.ForcePixel;
        }

        void Update()
        {
            float level = dimsWhenHidden && PartyRules.Hidden ? 0.3f : 1f;
            float noise = Mathf.PerlinNoise(_seed, Time.time * 3f) - 0.5f;
            _light.intensity = intensity * level * (1f + noise * 2f * flicker);
        }
    }

    /// <summary>
    /// A dungeon lit by torches: the scene's directional lights are switched off and the ambient light turned down, so the only light
    /// is what the characters carry. Applied when the overworld starts in on-map combat mode.
    /// </summary>
    public static class DungeonLighting
    {
        public static void Apply(Color ambient)
        {
            foreach (var light in Object.FindObjectsByType<Light>())
            {
                if (light.type == LightType.Directional) light.enabled = false;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.reflectionIntensity = 0.1f;
        }
    }
}

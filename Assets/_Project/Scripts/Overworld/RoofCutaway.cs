using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Overworld
{
    /// <summary>
    /// Keeps a cave's roof from hiding the party: roof pieces within <see cref="radius"/> of the player stop rendering (they still
    /// cast shadows, so the cave stays dark) and show again when the player walks away. Lives on the roof group.
    /// </summary>
    public class RoofCutaway : MonoBehaviour
    {
        [Tooltip("Roof pieces closer than this (flat distance) to the player are hidden.")]
        public float radius = 11f;

        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly List<bool> _hidden = new List<bool>();
        Transform _player;

        void Awake()
        {
            GetComponentsInChildren(_renderers);
            foreach (var _ in _renderers) _hidden.Add(false);
        }

        void LateUpdate()
        {
            if (_player == null)
            {
                var controller = FindAnyObjectByType<PlayerController>();
                if (controller == null) return;
                _player = controller.transform;
            }

            Vector3 p = _player.position;
            float sqr = radius * radius;
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;
                Vector3 d = r.bounds.center - p;
                d.y = 0f;
                bool hide = d.sqrMagnitude < sqr;
                if (hide == _hidden[i]) continue;

                _hidden[i] = hide;
                r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// Holds every sprite sliced from the battle UI sheet and hands them out by name, so views never depend on asset paths. The
    /// array is filled by the HUD builder and serialized into the prefab; names are the ones BattleUiSheetSlicer produces
    /// ("card_unit_ally", "digit_orange_3", ...).
    /// </summary>
    public class BattleUiSprites : MonoBehaviour
    {
        [SerializeField] private Sprite[] sprites = new Sprite[0];

        private Dictionary<string, Sprite> _byName;

        public void SetSprites(Sprite[] all)
        {
            sprites = all ?? new Sprite[0];
            _byName = null;
        }

        /// <summary>The sprite called <paramref name="name"/>, or null (with a warning) if the sheet has no such sprite.</summary>
        public Sprite Get(string name)
        {
            if (_byName == null)
            {
                _byName = new Dictionary<string, Sprite>();
                foreach (var sprite in sprites)
                {
                    if (sprite != null)
                    {
                        _byName[sprite.name] = sprite;
                    }
                }
            }

            if (_byName.TryGetValue(name, out var found))
            {
                return found;
            }

            Debug.LogWarning($"[BattleUiSprites] No sprite named '{name}' in the battle UI sheet.");
            return null;
        }
    }
}

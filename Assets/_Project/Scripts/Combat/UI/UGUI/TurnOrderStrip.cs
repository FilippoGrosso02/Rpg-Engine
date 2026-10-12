using System.Collections.Generic;
using Game.Combat.Contracts;
using UnityEngine;

namespace Game.Combat.UI.UGUI
{
    public class TurnOrderStrip : MonoBehaviour
    {
        [SerializeField] private RectTransform iconParent;
        [SerializeField] private TurnOrderIcon iconTemplate;

        private readonly List<GameObject> _spawnedIcons = new List<GameObject>();

        public void Clear()
        {
            foreach (var icon in _spawnedIcons)
            {
                if (icon != null)
                {
                    Destroy(icon);
                }
            }

            _spawnedIcons.Clear();
        }

        public void Rebuild(IReadOnlyList<CombatantRuntime> upcomingOrder)
        {
            Clear();

            if (iconTemplate == null || iconParent == null)
            {
                return;
            }

            foreach (var combatant in upcomingOrder)
            {
                var icon = Instantiate(iconTemplate, iconParent);
                icon.gameObject.SetActive(true);
                icon.Refresh(combatant);
                _spawnedIcons.Add(icon.gameObject);
            }
        }
    }
}

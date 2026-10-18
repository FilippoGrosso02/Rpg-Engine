using System.Linq;
using Game.Data.Registry;
using Game.Data.State;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// Dev tool: press F2 to open a panel listing every hero in the database with Add / Remove buttons for the live party.
    /// Lives outside the OverworldRoot (next to the director) so it keeps working, and hides itself, around battles.
    /// </summary>
    public class PartyRosterDebugPanel : MonoBehaviour
    {
        public OverworldDirector director;
        public KeyCode toggleKey = KeyCode.F2;

        bool _open;
        Vector2 _scroll;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) _open = !_open;
        }

        void OnGUI()
        {
            if (!_open || (director != null && director.InBattle)) return;

            GUILayout.BeginArea(new Rect(10, 10, 340, Screen.height - 20), GUI.skin.box);
            GUILayout.Label($"Party roster  {PartyRoster.Count}/{PartyRoster.MaxSize}   (F2 to close)");
            _scroll = GUILayout.BeginScrollView(_scroll);

            foreach (var actor in DatabaseRegistry.GetAllActors().OrderBy(a => a.displayName))
            {
                bool inParty = PartyRoster.Contains(actor.id);
                GUILayout.BeginHorizontal();
                GUILayout.Label((inParty ? "● " : "○ ") + actor.displayName + "  (" + actor.id + ")");
                if (GUILayout.Button(inParty ? "Remove" : "Add", GUILayout.Width(70)))
                {
                    if (inParty) PartyRoster.Remove(actor.id);
                    else PartyRoster.Add(actor.id);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}

using Game.Combat.Contracts;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>A big caption near the top of the screen announcing who has the surprise when a fight starts. Drawn by the OverworldDirector.</summary>
    public static class SurpriseBanner
    {
        const float Seconds = 3f;

        static string s_text;
        static Color s_color;
        static float s_until;
        static GUIStyle s_style;

        public static void Show(EncounterAdvantage advantage)
        {
            switch (advantage)
            {
                case EncounterAdvantage.Party:
                    s_text = "SURPRISE ATTACK!";
                    s_color = new Color(0.45f, 1f, 0.5f);
                    break;
                case EncounterAdvantage.Enemies:
                    s_text = "AMBUSHED!";
                    s_color = new Color(1f, 0.3f, 0.25f);
                    break;
                default:
                    return;
            }

            s_until = Time.unscaledTime + Seconds;
        }

        public static void Draw()
        {
            float left = s_until - Time.unscaledTime;
            if (left <= 0f || string.IsNullOrEmpty(s_text)) return;

            s_style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            s_style.fontSize = Mathf.RoundToInt(Screen.height * 0.07f);

            float alpha = Mathf.Clamp01(left / 0.6f); // fades over the last moments
            var rect = new Rect(0f, Screen.height * 0.12f, Screen.width, Screen.height * 0.12f);

            GUI.color = new Color(0f, 0f, 0f, 0.85f * alpha);
            GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), s_text, s_style);
            GUI.color = new Color(s_color.r, s_color.g, s_color.b, alpha);
            GUI.Label(rect, s_text, s_style);
            GUI.color = Color.white;
        }
    }
}

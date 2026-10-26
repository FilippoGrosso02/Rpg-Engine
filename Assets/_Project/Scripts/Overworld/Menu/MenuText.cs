using System.Collections.Generic;
using System.Text;
using Game.Combat.UI.UGUI;
using Game.Data.Definitions;
using Game.Data.Registry;
using Game.Data.Rules;

namespace Game.Overworld.Menu
{
    /// <summary>
    /// The words in the overworld menu: word wrapping for the pixel font, the grimoire's rules pages, and the bestiary entries. Plain
    /// strings in, plain strings out, so all of it is unit tested. The pixel font only has capitals, digits and ! ? : . , - + % / ' ( ) = * &lt; &gt;.
    /// </summary>
    public static class MenuText
    {
        public struct Topic
        {
            public string title;
            public string body;
        }

        public static readonly Topic[] Rules =
        {
            new Topic
            {
                title = "ATTACKS",
                body = "ROLL A D10, ADD YOUR ATK AND SUBTRACT THE TARGET'S DEF. A TOTAL OF 6 OR MORE HITS. EQUAL ATK AND DEF IS A 50% CHANCE, AND EACH POINT OF DIFFERENCE IS 10%.\n\n"
                    + "A NATURAL 1 ALWAYS MISSES AND A NATURAL 10 ALWAYS HITS, SO THE CHANCE IS NEVER BELOW 10% OR ABOVE 90%.\n\n"
                    + "ATK IS YOUR STRENGTH FOR MELEE WEAPONS OR YOUR DEXTERITY FOR BOWS AND THROWN WEAPONS, PLUS THE WEAPON'S ATK MODIFIER: LIGHT WEAPONS ADD, HEAVY WEAPONS SUBTRACT."
            },
            new Topic
            {
                title = "ABILITIES",
                body = "EVERY HERO HAS STR, DEX, VIT, MIND AND CHARM SCORES FROM 2 TO 12. A SCORE OF 7 IS AVERAGE.\n\n"
                    + "THE MINOR MODIFIER IS -3 AT 2, -2 AT 3-4, -1 AT 5-6, 0 AT 7, +1 AT 8-9, +2 AT 10-11 AND +3 AT 12. IT SETS HIT POINTS AND INITIATIVE."
            },
            new Topic
            {
                title = "DEFENSE",
                body = "DEF IS 2 PLUS THE ARMOR AND SHIELD YOU WEAR (LEATHER +2, CHAIN MAIL +4, PLATE +6, SHIELD +1), AND NEVER BELOW 2.\n\n"
                    + "EVADE CLASSES (THIEVES AND RANGERS) ALSO ADD THEIR DEX MINOR MODIFIER. GUARDING ADDS +2 DEF UNTIL YOUR NEXT TURN."
            },
            new Topic
            {
                title = "DAMAGE",
                body = "A HIT ROLLS THE WEAPON'S DIE (D4 FOR A DAGGER, D6 FOR A SHORTSWORD, D12 FOR A GREATAXE). NOTHING IS ADDED.\n\n"
                    + "A HIT ALWAYS DOES AT LEAST 1 DAMAGE."
            },
            new Topic
            {
                title = "SAVES",
                body = "EVERY EFFECT HAS A DIFFICULTY VALUE (DV). ROLL A D10, ADD YOUR VITALITY (OR MIND) AND SUBTRACT THE DV. A TOTAL OF 6 OR MORE SAVES. A NATURAL 1 ALWAYS FAILS AND A NATURAL 10 ALWAYS SAVES.\n\n"
                    + "A FAILED SAVE AGAINST WEB LEAVES YOU STUCK. FIREBALL DOES HALF DAMAGE ON A SAVE."
            },
            new Topic
            {
                title = "SPELLS AND AP",
                body = "CASTERS SPEND ACTION POINTS (AP). A SPELL COSTS ITS LEVEL IN AP.\n\n"
                    + "A CASTER STARTS A FIGHT WITH 1 AP, GAINS 1 AT THE START OF EACH TURN AFTER THE FIRST, AND 1 EXTRA AFTER GUARDING."
            },
            new Topic
            {
                title = "TURNS",
                body = "WHEN A FIGHT STARTS EVERYONE ROLLS A D10 AND ADDS THEIR DEX MODIFIER. THE HIGHEST ACTS FIRST, TIES GO TO THE HIGHER DEX, AND THE ORDER STAYS THE SAME EVERY ROUND.\n\n"
                    + "ON YOUR TURN YOU CAN WALK UP TO YOUR MOVE RANGE AND THEN ACT: ATTACK, CAST A SPELL, GUARD OR FLEE."
            },
            new Topic
            {
                title = "MORALE",
                body = "MONSTERS CAN LOSE HEART. WHEN THE FIRST ONE FALLS, WHEN HALF THE GROUP IS DOWN, OR WHEN A LONE MONSTER IS AT HALF HP, EACH ROLLS 2D6.\n\n"
                    + "A TOTAL ABOVE ITS MORALE MAKES IT FLEE. MINDLESS MONSTERS (MORALE 0) NEVER FLEE."
            },
            new Topic
            {
                title = "ACID",
                body = "SLIMES CORRODE. AN ARMORED HERO HIT BY A SLIME LOSES 1 DEF, AND A METAL WEAPON THAT HITS ONE LOSES 1 DAMAGE.\n\n"
                    + "THE DAMAGE STAYS UNTIL THE PARTY RESTS."
            }
        };

        /// <summary>Greedy word wrap to <paramref name="maxChars"/> characters per line; existing line breaks are kept.</summary>
        public static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || maxChars <= 0) return "";

            var result = new StringBuilder();
            var paragraphs = text.Split('\n');
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) result.Append('\n');

                int lineLength = 0;
                foreach (var word in paragraphs[p].Split(' '))
                {
                    if (word.Length == 0) continue;

                    if (lineLength == 0)
                    {
                        result.Append(word);
                        lineLength = word.Length;
                    }
                    else if (lineLength + 1 + word.Length <= maxChars)
                    {
                        result.Append(' ').Append(word);
                        lineLength += 1 + word.Length;
                    }
                    else
                    {
                        result.Append('\n').Append(word);
                        lineLength = word.Length;
                    }
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Sets a label's text and sizes its box to the text, so a multi-line block starts at the top of where it was placed (PixelText
        /// centres its text vertically inside the box).
        /// </summary>
        public static void SetBlock(PixelText label, string text)
        {
            label.text = text ?? "";
            int lines = string.IsNullOrEmpty(text) ? 1 : text.Split('\n').Length;
            var rect = label.rectTransform;
            rect.sizeDelta = new UnityEngine.Vector2(rect.sizeDelta.x, lines * PixelFont.LineHeight - (PixelFont.LineHeight - PixelFont.GlyphHeight));
        }

        /// <summary>Cuts a label to fit a column, ending in ".." when it was shortened.</summary>
        public static string Fit(string text, int maxChars)
        {
            text = (text ?? "").ToUpperInvariant();
            return text.Length <= maxChars || maxChars < 3 ? text : text.Substring(0, maxChars - 2) + "..";
        }

        public static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();

        /// <summary>"10 (+2)" for an ability score.</summary>
        public static string Ability(int score) => score + " (" + Signed(OsrRules.Modifier(score)) + ")";

        public static string Describe(HeroSheet hero)
        {
            var text = new StringBuilder();
            text.Append("STR ").Append(Ability(hero.strength)).Append("   DEX ").Append(Ability(hero.dexterity)).Append("   VIT ").Append(Ability(hero.vitality)).Append('\n');
            text.Append("MIND ").Append(Ability(hero.mind)).Append("   CHARM ").Append(Ability(hero.charm)).Append("\n\n");
            text.Append("DEF ").Append(hero.armorClass).Append("   ATK ").Append(hero.attackBonus).Append("   DAMAGE ").Append(hero.damageText.ToUpperInvariant()).Append("\n\n");
            text.Append("WEAPON: ").Append(string.IsNullOrEmpty(hero.weaponName) ? "NONE" : hero.weaponName.ToUpperInvariant()).Append('\n');
            text.Append("ARMOR: ").Append(hero.armorNames.Count == 0 ? "NONE" : string.Join(", ", hero.armorNames).ToUpperInvariant()).Append("\n\n");

            text.Append("SPELLS:");
            if (hero.skills.Count == 0)
            {
                text.Append(" NONE");
            }
            else
            {
                foreach (var skill in hero.skills)
                {
                    text.Append('\n').Append("  ").Append(skill.name.ToUpperInvariant()).Append(" - AP ").Append(skill.apCost);
                }
            }

            return text.ToString();
        }

        /// <summary>The bestiary page for a monster the party has beaten.</summary>
        public static string Describe(EnemyDefinition enemy, int kills)
        {
            var text = new StringBuilder();
            if (!string.IsNullOrEmpty(enemy.roleLabel)) text.Append(enemy.roleLabel.ToUpperInvariant()).Append("\n\n");

            text.Append("HP ").Append(enemy.hp).Append("   DEF ").Append(enemy.armorClass).Append('\n');
            text.Append("ATK ").Append(enemy.attackBonus).Append("   DAMAGE ").Append(enemy.damage.ToString().ToUpperInvariant()).Append('\n');
            text.Append("MOVE ").Append(enemy.moveRange).Append("   SAVE ").Append(enemy.saveBonus).Append('\n');
            text.Append("MORALE ").Append(enemy.morale > 0 ? enemy.morale.ToString() : "MINDLESS").Append("\n\n");

            var traits = new List<string>();
            if (enemy.corrodesArmor) traits.Add("CORRODES ARMOR");
            if (enemy.corrodesWeapons) traits.Add("CORRODES WEAPONS");
            if (enemy.immuneToCharm) traits.Add("IMMUNE TO CHARM");
            if (enemy.immuneToFear) traits.Add("IMMUNE TO FEAR");
            if (enemy.immuneToSleep) traits.Add("IMMUNE TO SLEEP");
            if (traits.Count > 0) text.Append(string.Join("\n", traits)).Append("\n\n");

            var skillNames = new List<string>();
            for (int i = 1; i < enemy.skillIds.Count; i++)
            {
                var skill = DatabaseRegistry.GetSkill(enemy.skillIds[i]);
                if (skill != null) skillNames.Add(skill.displayName.ToUpperInvariant());
            }

            if (skillNames.Count > 0) text.Append("SPELLS: ").Append(string.Join(", ", skillNames)).Append("\n\n");

            text.Append("DEFEATED: ").Append(kills);
            return text.ToString();
        }
    }
}

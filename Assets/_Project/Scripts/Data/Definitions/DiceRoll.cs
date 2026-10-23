using System;

namespace Game.Data.Definitions
{
    /// <summary>
    /// "NdS + B" — e.g. a sword is d8 + 2 (count 1, sides 8, bonus 2), a club is 2d6. A count of 0 means "no dice"
    /// (a spell that only applies a status, say). Serializable so weapons, monsters and spells can author it in the inspector.
    /// </summary>
    [Serializable]
    public struct DiceRoll
    {
        public int count;
        public int sides;
        public int bonus;

        public DiceRoll(int count, int sides, int bonus = 0)
        {
            this.count = count;
            this.sides = sides;
            this.bonus = bonus;
        }

        public static DiceRoll None => new DiceRoll(0, 0, 0);

        public bool HasDice => count > 0 && sides > 0;

        public int Min => HasDice ? count + bonus : bonus;
        public int Max => HasDice ? count * sides + bonus : bonus;

        /// <summary>Sum of the dice (no bonus). Each die is rolled with <paramref name="rng"/> so tests can seed it.</summary>
        public int RollDice(Random rng)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                total += rng.Next(1, sides + 1);
            }

            return total;
        }

        public int Roll(Random rng) => (HasDice ? RollDice(rng) : 0) + bonus;

        public override string ToString()
        {
            if (!HasDice)
            {
                return bonus.ToString();
            }

            string dice = (count == 1 ? "d" : count + "d") + sides;
            if (bonus == 0)
            {
                return dice;
            }

            return bonus > 0 ? dice + "+" + bonus : dice + bonus;
        }
    }
}

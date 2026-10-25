using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Registry;
using Game.Data.State;
using UnityEngine;

namespace Game.Data.Rules
{
    /// <summary>
    /// A hero's numbers as the menus show them: the same derivation combat uses when it builds an ally (HP from the hit die and VIT,
    /// AC from the armor worn, attack from the ability modifier, damage from the weapon wielded), read from the worn gear in
    /// <see cref="Loadout"/> and the remembered HP in <see cref="PartyState"/>.
    /// </summary>
    public sealed class HeroSheet
    {
        public sealed class SkillLine
        {
            public string name;
            public int apCost;
        }

        public string id;
        public string displayName;
        public string roleLabel;
        public string className;
        public int level;
        public int maxHp;
        public int currentHp;
        public int armorClass;
        public int attackBonus;
        public string damageText;
        public string weaponName;
        public readonly List<string> armorNames = new List<string>();
        public int strength, dexterity, vitality, mind, charm;
        public bool usesAp;
        public readonly List<SkillLine> skills = new List<SkillLine>();

        public static HeroSheet Build(ActorDefinition actor)
        {
            if (actor == null) return null;

            var classDef = DatabaseRegistry.GetClass(actor.classId);
            int hitDie = classDef != null ? classDef.hitDieSides : 8;
            int maxHp = OsrRules.MaxHitPoints(hitDie, actor.vitality);

            int armorBonus = 0;
            var sheet = new HeroSheet();
            foreach (var armorId in Loadout.GetArmorIds(actor))
            {
                var armor = DatabaseRegistry.GetArmor(armorId);
                if (armor == null) continue;

                armorBonus += armor.acBonus;
                sheet.armorNames.Add(armor.displayName);
            }

            var weaponId = Loadout.GetWeaponId(actor);
            var weapon = string.IsNullOrEmpty(weaponId) ? null : DatabaseRegistry.GetWeapon(weaponId);
            bool ranged = weapon != null && weapon.isRanged;
            var dice = weapon != null ? weapon.damage : new DiceRoll(1, 3, 0); // bare hands: d3
            int strengthBonus = weapon == null || weapon.addsStrengthModifier ? OsrRules.Modifier(actor.strength) : 0;
            var corrosion = PartyState.GetCorrosion(actor.id);

            sheet.id = actor.id;
            sheet.displayName = actor.displayName;
            sheet.roleLabel = actor.roleLabel;
            sheet.className = classDef != null ? classDef.displayName : "";
            sheet.level = actor.level;
            sheet.maxHp = maxHp;
            sheet.currentHp = PartyState.GetStartingHp(actor.id, maxHp);
            sheet.armorClass = OsrRules.ArmorClass(armorBonus) - Mathf.Min(corrosion.armor, armorBonus);
            sheet.attackBonus = OsrRules.Modifier(ranged ? actor.dexterity : actor.strength);
            sheet.damageText = new DiceRoll(dice.count, dice.sides, dice.bonus + strengthBonus).ToString();
            sheet.weaponName = weapon != null ? weapon.displayName : "";
            sheet.strength = actor.strength;
            sheet.dexterity = actor.dexterity;
            sheet.vitality = actor.vitality;
            sheet.mind = actor.mind;
            sheet.charm = actor.charm;
            sheet.usesAp = actor.usesAp;

            // The first known skill is the Basic Attack; the rest are spells.
            for (int i = 1; i < actor.startingSkillIds.Count; i++)
            {
                var skill = DatabaseRegistry.GetSkill(actor.startingSkillIds[i]);
                if (skill != null) sheet.skills.Add(new SkillLine { name = skill.displayName, apCost = skill.apCost });
            }

            return sheet;
        }
    }
}

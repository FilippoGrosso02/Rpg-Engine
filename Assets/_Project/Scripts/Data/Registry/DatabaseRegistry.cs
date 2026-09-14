using System.Collections.Generic;
using UnityEngine;
using Game.Data.Definitions;

namespace Game.Data.Registry
{
    /// <summary>
    /// Loads every <see cref="GameDefinition"/> asset from Resources/Data/&lt;Type&gt; and caches
    /// it by its human-readable <c>id</c> for O(1) lookup. This is the single contract every
    /// other system (combat, events, dialogue) calls against to resolve an ID string into data.
    ///
    /// Loading and lookup only — no caching invalidation, hot-reload, async loading, or logic
    /// that consumes the data belongs here.
    /// </summary>
    public static class DatabaseRegistry
    {
        private static bool s_initialized;

        private static Dictionary<string, SkillDefinition> s_skills;
        private static Dictionary<string, ItemDefinition> s_items;
        private static Dictionary<string, ActorDefinition> s_actors;
        private static Dictionary<string, WeaponDefinition> s_weapons;
        private static Dictionary<string, ArmorDefinition> s_armor;
        private static Dictionary<string, EnemyDefinition> s_enemies;
        private static Dictionary<string, StatusEffectDefinition> s_statusEffects;
        private static Dictionary<string, ClassDefinition> s_classes;
        private static GameConfigDefinition s_gameConfig;

        /// <summary>
        /// Loads (or reloads) every definition type from Resources. Safe to call more than
        /// once — each call rebuilds the caches from scratch. Callers never have to invoke
        /// this themselves; every public lookup lazily initializes on first access.
        /// </summary>
        public static void Initialize()
        {
            s_skills = LoadDictionary<SkillDefinition>("Data/Skills");
            s_items = LoadDictionary<ItemDefinition>("Data/Items");
            s_actors = LoadDictionary<ActorDefinition>("Data/Actors");
            s_weapons = LoadDictionary<WeaponDefinition>("Data/Weapons");
            s_armor = LoadDictionary<ArmorDefinition>("Data/Armor");
            s_enemies = LoadDictionary<EnemyDefinition>("Data/Enemies");
            s_statusEffects = LoadDictionary<StatusEffectDefinition>("Data/StatusEffects");
            s_classes = LoadDictionary<ClassDefinition>("Data/Classes");
            s_gameConfig = LoadGameConfig("Data/GameConfig");

            s_initialized = true;
        }

        private static void EnsureInitialized()
        {
            if (!s_initialized)
            {
                Initialize();
            }
        }

        private static Dictionary<string, T> LoadDictionary<T>(string resourcesPath) where T : GameDefinition
        {
            var dict = new Dictionary<string, T>();
            var assets = Resources.LoadAll<T>(resourcesPath);

            foreach (var asset in assets)
            {
                if (string.IsNullOrEmpty(asset.id))
                {
                    Debug.LogError($"[DatabaseRegistry] {typeof(T).Name} asset '{asset.name}' (Resources/{resourcesPath}) has an empty id and was skipped.");
                    continue;
                }

                if (dict.ContainsKey(asset.id))
                {
                    Debug.LogError($"[DatabaseRegistry] Duplicate id '{asset.id}' among {typeof(T).Name} assets — '{asset.name}' conflicts with an already-loaded asset. Keeping the first one loaded and skipping this one.");
                    continue;
                }

                dict.Add(asset.id, asset);
            }

            return dict;
        }

        private static GameConfigDefinition LoadGameConfig(string resourcesPath)
        {
            var configs = Resources.LoadAll<GameConfigDefinition>(resourcesPath);

            if (configs.Length == 0)
            {
                return null;
            }

            if (configs.Length > 1)
            {
                Debug.LogError($"[DatabaseRegistry] Expected exactly one GameConfigDefinition under Resources/{resourcesPath} but found {configs.Length}. Using '{configs[0].name}' and ignoring the rest.");
            }

            return configs[0];
        }

        private static T Get<T>(Dictionary<string, T> dict, string id, string typeName) where T : class
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[DatabaseRegistry] Get{typeName} called with a null or empty id.");
                return null;
            }

            if (dict.TryGetValue(id, out var value))
            {
                return value;
            }

            Debug.LogWarning($"[DatabaseRegistry] {typeName} id '{id}' not found.");
            return null;
        }

        public static SkillDefinition GetSkill(string id)
        {
            EnsureInitialized();
            return Get(s_skills, id, "Skill");
        }

        public static ItemDefinition GetItem(string id)
        {
            EnsureInitialized();
            return Get(s_items, id, "Item");
        }

        public static ActorDefinition GetActor(string id)
        {
            EnsureInitialized();
            return Get(s_actors, id, "Actor");
        }

        public static WeaponDefinition GetWeapon(string id)
        {
            EnsureInitialized();
            return Get(s_weapons, id, "Weapon");
        }

        public static ArmorDefinition GetArmor(string id)
        {
            EnsureInitialized();
            return Get(s_armor, id, "Armor");
        }

        public static EnemyDefinition GetEnemy(string id)
        {
            EnsureInitialized();
            return Get(s_enemies, id, "Enemy");
        }

        public static StatusEffectDefinition GetStatusEffect(string id)
        {
            EnsureInitialized();
            return Get(s_statusEffects, id, "StatusEffect");
        }

        public static ClassDefinition GetClass(string id)
        {
            EnsureInitialized();
            return Get(s_classes, id, "Class");
        }

        /// <summary>
        /// Returns the single boot-config asset, or null (with a warning) if none was found
        /// under Resources/Data/GameConfig.
        /// </summary>
        public static GameConfigDefinition GetGameConfig()
        {
            EnsureInitialized();

            if (s_gameConfig == null)
            {
                Debug.LogWarning("[DatabaseRegistry] GetGameConfig called but no GameConfigDefinition was found under Resources/Data/GameConfig.");
            }

            return s_gameConfig;
        }
    }
}

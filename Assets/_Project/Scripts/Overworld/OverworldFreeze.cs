using System.Collections.Generic;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>
    /// What "the world stops" means while a fight plays out on the overworld (instead of deactivating the whole overworld root):
    /// every enemy's brain and movement stops, the player and the touched enemy group disappear (their combatants take their
    /// place), the exploration camera stops steering. <see cref="Release"/> undoes all of it.
    /// </summary>
    public sealed class OverworldFreeze
    {
        readonly List<OverworldEnemy> _enemies = new List<OverworldEnemy>();
        readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        readonly List<Collider> _disabledColliders = new List<Collider>();
        PlayerController _player;
        OverworldCameraRig _cameraRig;

        readonly List<GroupFollower> _followers = new List<GroupFollower>();

        public static OverworldFreeze Apply(PlayerController player, OverworldEnemy touched, OverworldEnemy[] allEnemies, OverworldCameraRig cameraRig,
            PartyFollowers party = null)
        {
            var freeze = new OverworldFreeze { _player = player, _cameraRig = cameraRig };

            foreach (var enemy in allEnemies)
            {
                if (enemy == null) continue;
                enemy.Freeze(true);
                freeze._enemies.Add(enemy);
            }

            if (touched != null)
            {
                freeze.Hide(touched.gameObject);
                var group = touched.GetComponent<OverworldEnemyGroup>();
                if (group != null) foreach (var member in group.Members) freeze.Hide(member.gameObject);
            }

            if (party != null)
            {
                foreach (var follower in party.Followers)
                {
                    follower.Freeze(true);
                    freeze.Hide(follower.gameObject);
                    freeze._followers.Add(follower);
                }
            }

            if (player != null)
            {
                freeze.Hide(player.gameObject);
                player.SetFrozen(true);
            }

            if (cameraRig != null) cameraRig.Suspend();
            return freeze;
        }

        /// <summary>Turns off renderers and colliders (an aiming ray must not hit the stand-in that is no longer shown).</summary>
        void Hide(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                r.enabled = false;
                _hiddenRenderers.Add(r);
            }

            foreach (var c in root.GetComponentsInChildren<Collider>())
            {
                if (!c.enabled || c is CharacterController) continue; // the player's CharacterController is handled by SetFrozen
                c.enabled = false;
                _disabledColliders.Add(c);
            }
        }

        /// <summary>Anything that was destroyed in the meantime (a beaten enemy) is skipped.</summary>
        public void Release()
        {
            foreach (var r in _hiddenRenderers) if (r != null) r.enabled = true;
            foreach (var c in _disabledColliders) if (c != null) c.enabled = true;
            foreach (var e in _enemies) if (e != null) e.Freeze(false);
            foreach (var f in _followers) if (f != null) f.Freeze(false);
            if (_player != null) _player.SetFrozen(false);
            if (_cameraRig != null) _cameraRig.Resume();

            _hiddenRenderers.Clear();
            _disabledColliders.Clear();
            _enemies.Clear();
            _followers.Clear();
        }
    }
}

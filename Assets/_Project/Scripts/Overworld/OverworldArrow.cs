using System;
using UnityEngine;

namespace Game.Overworld
{
    /// <summary>A placeholder arrow (a thin cylinder) that flies in a straight line to a point and reports when it lands.</summary>
    public class OverworldArrow : MonoBehaviour
    {
        Vector3 _target;
        float _speed;
        Action _onHit;

        /// <summary>Spawns an arrow at <paramref name="from"/> that flies to <paramref name="to"/> at <paramref name="speed"/> metres per second.</summary>
        public static void Fire(Vector3 from, Vector3 to, float speed, Action onHit)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Arrow";
            Destroy(go.GetComponent<Collider>());
            go.transform.position = from;
            go.transform.localScale = new Vector3(0.04f, 0.35f, 0.04f);
            var direction = to - from;
            if (direction.sqrMagnitude > 0.0001f) go.transform.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            go.GetComponent<Renderer>().material.color = new Color(0.85f, 0.75f, 0.5f);

            var arrow = go.AddComponent<OverworldArrow>();
            arrow._target = to;
            arrow._speed = Mathf.Max(1f, speed);
            arrow._onHit = onHit;
        }

        void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, _target, _speed * Time.deltaTime);
            if ((transform.position - _target).sqrMagnitude > 0.0001f) return;

            var callback = _onHit;
            _onHit = null;
            Destroy(gameObject);
            callback?.Invoke();
        }
    }
}

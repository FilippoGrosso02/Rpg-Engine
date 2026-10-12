using UnityEngine;
using UnityEngine.AI;

namespace Game.Combat.TrailsBattle3D.Runtime
{
    /// <summary>
    /// Draws the NavMesh path the acting unit would take to a hovered ground point, plus a
    /// small marker at the destination — green while the point is within the unit's
    /// remaining movement range for this turn, red once it's beyond it. Rebuilt every frame
    /// the mouse hovers a valid floor point during the player's turn.
    /// </summary>
    public class MovementPathPreview : MonoBehaviour
    {
        private const float MarkerRadius = 0.4f;
        private const int MarkerSegments = 20;

        private static readonly Color ReachableColor = new Color(0.4f, 1f, 0.5f, 0.9f);
        private static readonly Color UnreachableColor = new Color(1f, 0.3f, 0.3f, 0.9f);

        private LineRenderer _pathLine;
        private LineRenderer _marker;
        private NavMeshPath _navPath;
        private NavMeshPath _rangeCheckPath;

        private void Awake()
        {
            _navPath = new NavMeshPath();
            _rangeCheckPath = new NavMeshPath();

            var pathGo = new GameObject("PathLine");
            pathGo.transform.SetParent(transform, false);
            _pathLine = pathGo.AddComponent<LineRenderer>();
            ConfigureLine(_pathLine);

            var markerGo = new GameObject("DestinationMarker");
            markerGo.transform.SetParent(transform, false);
            _marker = markerGo.AddComponent<LineRenderer>();
            _marker.loop = true;
            _marker.positionCount = MarkerSegments;
            ConfigureLine(_marker);

            gameObject.SetActive(false);
        }

        private static void ConfigureLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.widthMultiplier = 0.06f;
            line.material = new Material(Shader.Find("Sprites/Default"));
        }

        /// <summary>
        /// <paramref name="pathFrom"/> is where the unit currently stands (what the drawn
        /// path walks from); <paramref name="rangeAnchor"/>/<paramref name="maxRange"/> is
        /// the turn-start position and movement allowance reachability is measured against —
        /// matching the same anchor the actual movement clamp uses, so the color agrees with
        /// what clicking would actually do.
        /// </summary>
        public void Show(Vector3 pathFrom, Vector3 destination, Vector3 rangeAnchor, float maxRange)
        {
            gameObject.SetActive(true);

            bool reachable = true;
            if (NavMesh.CalculatePath(rangeAnchor, destination, NavMesh.AllAreas, _rangeCheckPath)
                && _rangeCheckPath.status == NavMeshPathStatus.PathComplete)
            {
                reachable = ComputePathLength(_rangeCheckPath) <= maxRange + 0.01f;
            }
            else
            {
                reachable = false;
            }

            var color = reachable ? ReachableColor : UnreachableColor;
            _pathLine.startColor = _pathLine.endColor = color;
            _marker.startColor = _marker.endColor = color;

            if (NavMesh.CalculatePath(pathFrom, destination, NavMesh.AllAreas, _navPath) && _navPath.corners.Length > 0)
            {
                _pathLine.positionCount = _navPath.corners.Length;
                for (int i = 0; i < _navPath.corners.Length; i++)
                {
                    _pathLine.SetPosition(i, _navPath.corners[i] + Vector3.up * 0.05f);
                }
            }
            else
            {
                _pathLine.positionCount = 2;
                _pathLine.SetPosition(0, pathFrom + Vector3.up * 0.05f);
                _pathLine.SetPosition(1, destination + Vector3.up * 0.05f);
            }

            for (int i = 0; i < MarkerSegments; i++)
            {
                float angle = (float)i / MarkerSegments * Mathf.PI * 2f;
                var point = destination + new Vector3(Mathf.Cos(angle) * MarkerRadius, 0.05f, Mathf.Sin(angle) * MarkerRadius);
                _marker.SetPosition(i, point);
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private static float ComputePathLength(NavMeshPath path)
        {
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }

            return length;
        }
    }
}

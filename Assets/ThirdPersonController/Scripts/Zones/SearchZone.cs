using System.Collections.Generic;
using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// Denotes an open area for the AI to check out when searching. By default AI only searches covers, using this component an additional areas can be added.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SearchZone : MonoBehaviour
    {
        /// <summary>
        /// Width of the box.
        /// </summary>
        public float Width
        {
            get { return _collider.size.x * transform.localScale.x; }
        }

        /// <summary>
        /// Depth of the box.
        /// </summary>
        public float Depth
        {
            get { return _collider.size.z * transform.localScale.z; }
        }

        /// <summary>
        /// Y coordinate of the block's bottom in world space.
        /// </summary>
        public float Bottom
        {
            get { return _collider.bounds.min.y; }
        }

        private BoxCollider _collider;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider>();
        }

        /// <summary>
        /// Returns a list of all checkable points inside the zone.
        /// </summary>
        public IEnumerable<Vector3> Points(float threshold)
        {
            float width = Width / threshold;
            float depth = Depth / threshold;

            int wcount;
            int dcount;

            if (width <= 0.5f)
                wcount = 1;
            else
                wcount = (int)(width + 0.5f) + 1;

            if (depth <= 0.5f)
                dcount = 1;
            else
                dcount = (int)(depth + 0.5f) + 1;

            Vector3 position;
            position.y = -_collider.size.y * 0.5f;

            var xstep = _collider.size.x / (wcount - 1);
            var zstep = _collider.size.z / (dcount - 1);

            for (int x = 0; x < wcount; x++)
            {
                if (wcount == 0)
                    position.x = _collider.size.x * 0.5f;
                else
                    position.x = x * xstep - _collider.size.x * 0.5f;

                for (int z = 0; z < dcount; z++)
                {
                    if (dcount == 0)
                        position.z = _collider.size.z * 0.5f;
                    else
                        position.z = z * zstep - _collider.size.z * 0.5f;

                    yield return transform.TransformPoint(position);
                }
            }
        }

        private static Dictionary<GameObject, SearchZone> _map = new Dictionary<GameObject, SearchZone>();

        /// <summary>
        /// Optimised way to get a cover component.
        /// </summary>
        public static SearchZone Get(GameObject gameObject)
        {
            if (!_map.ContainsKey(gameObject))
                _map[gameObject] = gameObject.GetComponent<SearchZone>();

            return _map[gameObject];
        }
    }

    /// <summary>
    /// Maintains a list of search zones inside an area (denoted by a position and radius).
    /// </summary>
    public class SearchZoneCache
    {
        public List<SearchZone> Items = new List<SearchZone>();

        public void Reset(Vector3 observer, float maxDistance)
        {
            Items.Clear();
            var count = Physics.OverlapSphereNonAlloc(observer, maxDistance, Util.Colliders, 0x1 << Layers.Zones, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                var collider = Util.Colliders[i];
            
                if (!collider.isTrigger)
                    continue;

                var block = SearchZone.Get(collider.gameObject);

                if (block != null)
                    Items.Add(block);
            }
        }
    }
}

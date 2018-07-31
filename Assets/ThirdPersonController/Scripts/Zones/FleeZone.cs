using System.Collections.Generic;
using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// Denotes a target zone for the fleeing AI to run to. Either disables or destroys AI that reaches it.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FleeZone : MonoBehaviour
    {
        /// <summary>
        /// Width of the box.
        /// </summary>
        public float Width
        {
            get { return _collider.size.x * transform.localScale.x * 0.9f; }
        }

        /// <summary>
        /// Depth of the box.
        /// </summary>
        public float Depth
        {
            get { return _collider.size.z * transform.localScale.z * 0.9f; }
        }

        /// <summary>
        /// Y coordinate of the block's bottom in world space.
        /// </summary>
        public float Bottom
        {
            get { return _collider.bounds.min.y; }
        }

        /// <summary>
        /// All currently active flee blocks.
        /// </summary>
        public static IEnumerable<FleeZone> All
        {
            get { return _blocks; }
        }

        /// <summary>
        /// Number of all fleeing zones inside the level.
        /// </summary>
        public static int Count
        {
            get { return _blocks.Count; }
        }

        /// <summary>
        /// Returns a fleeing index at given index.
        /// </summary>
        public static FleeZone Get(int index)
        {
            return _blocks[index];
        }

        /// <summary>
        /// Time in seconds after an actor that reached the flee zone is removed from the game.
        /// </summary>
        [Tooltip("Time in seconds after an actor that reached the flee zone is removed from the game.")]
        public float RemoveDelay = 3;

        /// <summary>
        /// Are the actors removed from the game by destroying them. If false, they are disabled.
        /// </summary>
        [Tooltip("Are the actors removed from the game by destroying them. If false, they are disabled.")]
        public bool IsRemovingByDestroying = true;

        private BoxCollider _collider;
        private static List<FleeZone> _blocks = new List<FleeZone>();
        private List<GameObject> _actors = new List<GameObject>();
        private Dictionary<GameObject, float> _times = new Dictionary<GameObject, float>();

        /// <summary>
        /// Registers an actor to be removed by the zone.
        /// </summary>
        public void Register(GameObject actor)
        {
            if (!_actors.Contains(actor))
            {
                _actors.Add(actor);
                _times[actor] = 0;
            }
        }

        /// <summary>
        /// Removes the actor from the removal list.
        /// </summary>
        public void Unregister(GameObject actor)
        {
            if (_actors.Contains(actor))
                _actors.Remove(actor);

            if (_times.ContainsKey(actor))
                _times.Remove(actor);
        }

        private void Awake()
        {
            _collider = GetComponent<BoxCollider>();
        }

        private void Update()
        {
            for (int i = _actors.Count - 1; i >= 0; i--)
            {
                var actor = _actors[i];
                _times[actor] += Time.deltaTime;

                if (_times[actor] >= RemoveDelay)
                {
                    Unregister(actor);

                    if (IsRemovingByDestroying)
                        Destroy(actor.gameObject);
                    else
                        actor.gameObject.SetActive(false);
                }
            }
        }

        private void OnEnable()
        {
            if (!_blocks.Contains(this))
                _blocks.Add(this);
        }

        private void OnDisable()
        {
            if (_blocks.Contains(this))
                _blocks.Remove(this);
        }

        private void OnDestroy()
        {
            if (_blocks.Contains(this))
                _blocks.Remove(this);
        }
    }
}

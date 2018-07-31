using System.Collections.Generic;
using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// Stops bullets from moving past the game object. Works in only one direction.
    /// </summary>
    public class BulletShield : MonoBehaviour
    {
        /// <summary>
        /// Hit effect prefab that is instantiated on hit.
        /// </summary>
        [Tooltip("Hit effect prefab that is instantiated on hit.")]
        public GameObject HitEffect;

        /// <summary>
        /// Random sounds played on hit.
        /// </summary>
        [Tooltip("Random sounds played on hit.")]
        public AudioClip[] HitSounds;

        private static Dictionary<GameObject, BulletShield> _map = new Dictionary<GameObject, BulletShield>();

        public static BulletShield Get(GameObject gameObject)
        {
            if (_map.ContainsKey(gameObject))
                return _map[gameObject];
            else
                return null;
        }

        /// <summary>
        /// Returns true if the given object contains a bullet shield component.
        /// </summary>
        public static bool Contains(GameObject gameObject)
        {
            return _map.ContainsKey(gameObject);
        }

        private void OnEnable()
        {
            _map[gameObject] = this;
        }

        private void OnDisable()
        {
            _map.Remove(gameObject);
        }

        public void OnHit(Hit hit)
        {
            if (HitEffect != null)
            {
                var obj = GameObject.Instantiate(HitEffect);
                obj.transform.SetParent(null);
                obj.transform.position = hit.Position;
                obj.transform.LookAt(hit.Position + hit.Normal * 100, Vector3.up);
                obj.SetActive(true);

                GameObject.Destroy(obj, 3);
            }

            if (HitSounds.Length > 0)
            {
                var clip = HitSounds[UnityEngine.Random.Range(0, HitSounds.Length)];
                AudioSource.PlayClipAtPoint(clip, transform.position);
            }
        }
    }
}

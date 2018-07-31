using System.Collections.Generic;
using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// Denotes a zone that prompts the AI to be careful.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DangerZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            other.SendMessage("OnEnterDanger", this, SendMessageOptions.DontRequireReceiver);
        }

        private void OnTriggerExit(Collider other)
        {
            other.SendMessage("OnLeaveDanger", this, SendMessageOptions.DontRequireReceiver);
        }

        private void OnEnable()
        {
            DangerZones.Register(this);
        }

        private void OnDisable()
        {
            DangerZones.Unregister(this);
        }

        private void OnDestroy()
        {
            DangerZones.Unregister(this);
        }
    }

    /// <summary>
    /// Maintains a list of all danger zones inside the level.
    /// </summary>
    public static class DangerZones
    {
        public static IEnumerable<DangerZone> All
        {
            get { return _list; }
        }

        private static List<DangerZone> _list = new List<DangerZone>();
        private static Dictionary<GameObject, DangerZone> _map = new Dictionary<GameObject, DangerZone>();

        public static DangerZone Get(GameObject gameObject)
        {
            if (_map.ContainsKey(gameObject))
                return _map[gameObject];
            else
                return null;
        }

        public static void Register(DangerZone zone)
        {
            if (!_list.Contains(zone))
                _list.Add(zone);

            _map[zone.gameObject] = zone;
        }

        public static void Unregister(DangerZone zone)
        {
            if (_list.Contains(zone))
                _list.Remove(zone);

            if (_map.ContainsKey(zone.gameObject))
                _map.Remove(zone.gameObject);
        }
    }
}

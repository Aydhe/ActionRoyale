using UnityEngine;

namespace CoverShooter
{
    public class CharacterInventory : MonoBehaviour
    {
        /// <summary>
        /// All the weapons belonging in the inventory.
        /// </summary>
        [Tooltip("All the weapons belonging in the inventory.")]
        public WeaponDescription[] Weapons;

        private void Awake()
        {
            for (int i = 0; i < Weapons.Length; i++)
            {
                if (Weapons[i].Item != null) Weapons[i].Item.SetActive(false);
                if (Weapons[i].Holster != null) Weapons[i].Holster.SetActive(true);
            }
        }
    }
}

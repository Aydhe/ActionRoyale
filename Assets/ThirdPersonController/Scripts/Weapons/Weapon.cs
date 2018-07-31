using System;
using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// Weapon aiming setting.
    /// </summary>
    public enum WeaponAiming
    {
        /// <summary>
        /// Wait for controller input to aim.
        /// </summary>
        input,

        /// <summary>
        /// Always point the gun (if not in cover).
        /// </summary>
        always,
        
        /// <summary>
        /// Always point the gun (if not in cover) and turn immediately.
        /// </summary>
        alwaysImmediateTurn
    }

    /// <summary>
    /// Description of a weapon/tool held by a CharacterMotor. 
    /// </summary>
    [Serializable]
    public struct WeaponDescription
    {
        /// <summary>
        /// True if Item is null.
        /// </summary>
        public bool IsNull
        {
            get { return Item == null; }
        }

        /// <summary>
        /// Link to the weapon object containg a Gun component.
        /// </summary>
        [Tooltip("Link to the weapon object containg a Gun component.")]
        public GameObject Item;

        /// <summary>
        /// Link to the holstered weapon object which is made visible when the weapon is not used.
        /// </summary>
        [Tooltip("Link to the holstered weapon object which is made visible when the weapon is not used.")]
        public GameObject Holster;

        /// <summary>
        /// Defines character animations used with this weapon.
        /// </summary>
        [Tooltip("Defines character animations used with this weapon.")]
        public WeaponType Type;

        /// <summary>
        /// Animations to use for a tool. Relevant when weapon type is set to 'tool'.
        /// </summary>
        [Tooltip("Animations to use for a tool. Relevant when weapon type is set to 'tool'.")]
        public Tool Tool;

        /// <summary>
        /// Link to the flashlight attached to the weapon.
        /// </summary>
        public Flashlight Flashlight
        {
            get
            {
                if (_cacheItem == Item)
                    return _cachedFlashlight;
                else
                {
                    cache();
                    return _cachedFlashlight;
                }
            }
        }

        /// <summary>
        /// Shortcut for getting the gun component of the Item.
        /// </summary>
        public BaseGun Gun
        {
            get
            {
                if (_cacheItem == Item)
                    return _cachedGun;
                else
                {
                    cache();
                    return _cachedGun;
                }
            }
        }

        /// <summary>
        /// Shortcut for getting a custom component attached to the item. The value is cached for efficiency.
        /// </summary>
        public T Component<T>() where T : MonoBehaviour
        {
            if (_cacheItem != Item)
                cache();

            if (Item == null)
                return null;

            if (_cachedComponent == null || !(_cachedComponent is T))
                _cachedComponent = Item.GetComponent<T>();

            return _cachedComponent as T;
        }

        /// <summary>
        /// Checks if the weapon is a tool that requires character aiming.
        /// </summary>
        public bool IsAnAimableTool(bool useAlternate)
        {
            return Type == WeaponType.Tool && ToolDescription.Defaults[(int)Tool].HasAiming(useAlternate);
        }

        /// <summary>
        /// Checks if the weapon is a tool that's not single action and used continuously instead.
        /// </summary>
        public bool IsAContinuousTool(bool useAlternate)
        {
            return Type == WeaponType.Tool && ToolDescription.Defaults[(int)Tool].IsContinuous(useAlternate);
        }

        /// <summary>
        /// Shield that is enabled when the weapon is equipped.
        /// </summary>
        [Tooltip("Shield that is enabled when the weapon is equipped.")]
        public GameObject Shield;

        /// <summary>
        /// Will the character be prevented from running, rolling, or jumping while the weapon is equipped.
        /// </summary>
        [Tooltip("Will the character be prevented from running, rolling, or jumping while the weapon is equipped.")]
        public bool IsHeavy;

        /// <summary>
        /// Will the character use covers while using the weapon.
        /// </summary>
        [Tooltip("Will the character be prevented from using covers while the weapon is equipped.")]
        public bool PreventCovers;

        /// <summary>
        /// Will the character be prevented from climbing while the weapon is equipped.
        /// </summary>
        [Tooltip("Will the character be prevented from climbing while the weapon is equipped.")]
        public bool PreventClimbing;

        /// <summary>
        /// If type is set to rifle, should the character use shotgun animations.
        /// </summary>
        [Tooltip("If type is set to rifle, should the character use shotgun animations.")]
        public bool IsShotgunRifle;

        /// <summary>
        /// Is the character always aiming while the weapon is equipped.
        /// </summary>
        [Tooltip("Is the character always aiming while the weapon is equipped.")]
        public WeaponAiming Aiming;

        private BaseGun _cachedGun;
        private MonoBehaviour _cachedComponent;
        private Flashlight _cachedFlashlight;

        private GameObject _cacheItem;

        private void cache()
        {
            _cacheItem = Item;
            _cachedComponent = null;
            _cachedGun = Item == null ? null : Item.GetComponent<BaseGun>();

            _cachedFlashlight = Item == null ? null : Item.GetComponent<Flashlight>();

            if (_cachedFlashlight == null && Item != null)
                _cachedFlashlight = Item.GetComponentInChildren<Flashlight>();
        }

        public override bool Equals(object obj)
        {
            var other = (WeaponDescription)obj;

            return other.Item == Item &&
                   other.Holster == Holster &&
                   other.Shield == Shield;
        }

        public static bool operator ==(WeaponDescription left, WeaponDescription right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeaponDescription left, WeaponDescription right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Defines character animations used with a weapon.
    /// </summary>
    public enum WeaponType
    {
        Pistol,
        Rifle,
        Tool
    }
}
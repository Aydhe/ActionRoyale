using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// Allows the AI to take phonecalls and film using a phone. Mostly used by Civilian Brain and AI Follow
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    [RequireComponent(typeof(Actor))]
    public class AIPhone : AIItemBase
    {
        #region Private fields

        private Actor _actor;
        private CharacterMotor _motor;

        private bool _isFilming;
        private bool _wantsToCall;

        #endregion

        #region Commands

        /// <summary>
        /// Told by the brains to start filming.
        /// </summary>
        public void ToStartFilming()
        {
            if (isActiveAndEnabled)
                ToTakePhone();

            _isFilming = true;
        }

        /// <summary>
        /// Told by the brains to stop filming.
        /// </summary>
        public void ToStopFilming()
        {
            _isFilming = false;
        }

        /// <summary>
        /// Told by the brains to take a weapon to arms.
        /// </summary>
        public void ToTakePhone()
        {
            Equip(_motor, Tool.phone);
        }

        /// <summary>
        /// Told by the brains to disarm any weapon.
        /// </summary>
        public void ToHidePhone()
        {
            Unequip(_motor, Tool.phone);
            _isFilming = false;
        }

        /// <summary>
        /// Told by the brains to initiate a call.
        /// </summary>
        public void ToCall()
        {
            ToStopFilming();
            ToTakePhone();
            _wantsToCall = true;
        }

        /// <summary>
        /// Told by the brains to initiate a phone call.
        /// </summary>
        public void ToPhoneCall()
        {
            ToStopFilming();
            ToTakePhone();
            _wantsToCall = true;
        }

        #endregion

        #region Events

        /// <summary>
        /// Registers that an alternative mode of a tool was used. It is regarded as a phone call.
        /// </summary>
        public void OnToolUsedAlternate()
        {
            if (isActiveAndEnabled && _wantsToCall)
            {
                Message("OnCallMade");
                _wantsToCall = false;
            }
        }

        #endregion

        #region Behaviour

        protected override void Awake()
        {
            base.Awake();

            _actor = GetComponent<Actor>();
            _motor = GetComponent<CharacterMotor>();
        }

        private void Update()
        {
            if (!_actor.IsAlive)
                return;

            if (_motor.EquippedWeapon.Type == WeaponType.Tool && _motor.EquippedWeapon.Tool == Tool.phone)
            {
                if (_wantsToCall)
                    _motor.InputUseToolAlternate();
                else if (_isFilming)
                    _motor.InputUseTool();
            }
        }

        #endregion
    }
}

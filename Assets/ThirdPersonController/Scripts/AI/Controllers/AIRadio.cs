using System.Collections.Generic;
using UnityEngine;

namespace CoverShooter
{
    /// <summary>
    /// Allows the AI to use a radio to take calls. Mostly used by AI Backup Call.
    /// </summary>
    [RequireComponent(typeof(Actor))]
    [RequireComponent(typeof(CharacterMotor))]
    public class AIRadio : AIItemBase
    {
        #region Private fields

        private Actor _actor;
        private CharacterMotor _motor;

        private bool _wantsToCall;

        #endregion

        #region Commands

        /// <summary>
        /// Told by the brains to take out a radio.
        /// </summary>
        public void ToTakeRadio()
        {
            Equip(_motor, Tool.radio);
        }

        /// <summary>
        /// Told by the brains to hide radio if it is equipped.
        /// </summary>
        public void ToHideRadio()
        {
            Unequip(_motor, Tool.radio);
        }

        /// <summary>
        /// Told by the brains to initiate a call.
        /// </summary>
        public void ToCall()
        {
            if (isActiveAndEnabled)
                ToTakeRadio();

            _wantsToCall = true;
        }

        /// <summary>
        /// Told by the brains to initiate a radio call. 
        /// </summary>
        public void ToRadioCall()
        {
            if (isActiveAndEnabled)
                ToTakeRadio();

            _wantsToCall = true;
        }

        #endregion

        #region Events

        /// <summary>
        /// Registers an event that the call animation has finished and executes the script.
        /// </summary>
        public void OnToolUsed()
        {
            if (isActiveAndEnabled && _wantsToCall)
            {
                _wantsToCall = false;
                Message("OnCallMade");
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

            if (_wantsToCall && _motor.EquippedWeapon.Type == WeaponType.Tool & _motor.EquippedWeapon.Tool == Tool.radio)
                _motor.InputUseTool();
        }

        #endregion
    }
}

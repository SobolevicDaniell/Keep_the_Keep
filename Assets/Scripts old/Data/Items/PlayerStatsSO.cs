using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/PlayerStatsSO")]
    public class PlayerStatsSO : ScriptableObject
    {
        const string PREFS_MOUSE_SENS_KEY = "settings.mouse_sensitivity";

        [Header("Health")]
        public int maxHealth = 100;

        [Header("Inventory")]
        public int inventorySlotsCount = 24;
        public int quickSlotsCount = 10;


        [Header("Look")]
        public float keyboardLookSensitivity = 2f;
        public float defaultMouseLookSensitivity = 10f;
        public float minMouseLookSensitivity = 1f;
        public float maxMouseLookSensitivity = 20f;

        [System.NonSerialized] public float mouseLookSensitivity;


        [Header("Movement")]
        public float moveSpeed = 14f;
        public float groundAcceleration = 70f;
        public float groundFriction = 14f;
        public float airAcceleration = 42f;
        public float airDrag = 0.5f;
        public float jumpImpulse = 8f;
        public float gravity = -20f;
        public float groundedCoyoteTime = 0.08f;

        void OnEnable()
        {
            float saved = PlayerPrefs.GetFloat(PREFS_MOUSE_SENS_KEY, defaultMouseLookSensitivity);
            mouseLookSensitivity = Mathf.Clamp(saved, minMouseLookSensitivity, maxMouseLookSensitivity);
        }

        public void SetMouseSensitivity(float value, bool save = true)
        {
            mouseLookSensitivity = Mathf.Clamp(value, minMouseLookSensitivity, maxMouseLookSensitivity);
            if (save)
            {
                PlayerPrefs.SetFloat(PREFS_MOUSE_SENS_KEY, mouseLookSensitivity);
            }
        }
    }
}

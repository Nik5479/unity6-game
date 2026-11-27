using UnityEngine;

namespace ForgottenVillage.Player
{
    /// <summary>
    /// Singleton access point for hero Nilesh.
    /// STUB ONLY: this class owns no movement, animation, or game state.
    /// It delegates everything to the existing PlayerController.
    ///
    /// TODO: when PlayerController.cs is moved into this folder (see
    /// MIGRATION.md), decide whether the Instance lives here or is wired
    /// onto PlayerController itself. Until then, do NOT duplicate movement
    /// or state logic here — PlayerController.cs stays the authority.
    /// </summary>
    public sealed class Player : MonoBehaviour
    {
        public static Player Instance { get; private set; }

        // Set in the inspector if this is not on the same GameObject as
        // PlayerController; otherwise resolved in Awake.
        [SerializeField] private PlayerController controller;

        void Awake()
        {
            // Guarded singleton: unlike GameManager (which assigns
            // Instance = this with no duplicate check), a second Player
            // destroys itself instead of silently replacing the first.
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Player] Duplicate Player destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (controller == null)
                controller = GetComponent<PlayerController>();
            if (controller == null)
                controller = FindFirstObjectByType<PlayerController>();
            if (controller == null)
                Debug.LogWarning("[Player] No PlayerController found; accessors return fallbacks.");
        }

        // NOTE: DontDestroyOnLoad is deliberately NOT set. This is a
        // single-scene game and VillageBuilder wires fresh references
        // (GameManager.player, MonsterKnock.player, cameras) per scene
        // load. A persistent Player would keep stale references to a
        // destroyed scene's objects. Revisit only if multi-scene loading
        // is added, and then persist GameManager with it.

        /// <summary>Player world position, via the existing controller.</summary>
        public Vector3 Position
        {
            get { return controller != null ? controller.transform.position : transform.position; }
        }

        /// <summary>Player transform, via the existing controller.</summary>
        public Transform PlayerTransform
        {
            get { return controller != null ? controller.transform : transform; }
        }

        /// <summary>Current ground speed (m/s), via PlayerController.CurrentSpeed.</summary>
        public float CurrentSpeed
        {
            get { return controller != null ? controller.CurrentSpeed : 0f; }
        }

        /// <summary>Whether Nilesh is crouch-walking, via PlayerController.IsCrouching.</summary>
        public bool IsCrouching
        {
            get { return controller != null && controller.IsCrouching; }
        }

        /// <summary>Underlying controller. Null until Awake resolves it.</summary>
        public PlayerController Controller
        {
            get { return controller; }
        }

        // NOTE: there is deliberately NO health here. PlayerController has
        // no health/damage/dead state (checked 2026-11-27); game-over and
        // survived state live in GameManager (IsOver, GameOver(), Survived()).
        // Add health here only when a damage system is actually designed.
    }
}

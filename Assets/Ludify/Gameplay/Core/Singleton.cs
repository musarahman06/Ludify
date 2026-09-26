using UnityEngine;

namespace Ludify.Gameplay.Core
{
    /// <summary>
    /// Scene-placed MonoBehaviour singleton. The first instance wins; later duplicates destroy themselves.
    /// Only DataManager, GameModeManager and UIManager should derive from this; everything else is
    /// wired by serialized references.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        public static T Instance { get; private set; }

        [SerializeField] bool persistAcrossScenes;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"Duplicate {typeof(T).Name} on '{name}' destroyed.", this);
                Destroy(gameObject);
                return;
            }
            Instance = (T)this;
            if (persistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}

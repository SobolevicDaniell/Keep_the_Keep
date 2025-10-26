using UnityEngine;
namespace Game
{
    
    public abstract class ItemSO : ScriptableObject
    {
        public string Id;
        public GameObject Prefab;

        [Header("UI")]
        public Sprite Icon;
        [Range(1, 1000)]
        public int MaxStack = 1;
        [Range(1, 2)]
        public int priority = 1;
    }

}
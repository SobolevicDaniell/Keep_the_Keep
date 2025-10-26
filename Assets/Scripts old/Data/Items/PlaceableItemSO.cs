using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Game/Placeable Item SO")]
    public class PlaceableItemSO : ItemSO
    {
        public GameObject PlaceablePrefab;

        public float PlaceDistance = 4f;
    }
}

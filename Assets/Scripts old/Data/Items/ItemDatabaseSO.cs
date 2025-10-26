using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(menuName = "Inventory/ItemDatabase")]
    public class ItemDatabaseSO : ScriptableObject
    {
        public List<ItemSO> Items;
        private Dictionary<string, ItemSO> _lookup;

        public ItemSO Get(string id)
        {
            if (Items == null)
            {
                Debug.LogError("ItemDatabaseSO.Items is null!");
                return null;
            }
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, ItemSO>();
                foreach (var i in Items)
                {
                    if (i == null)
                    {
                        Debug.LogError("Item in Items is null!");
                        continue;
                    }
                    _lookup[i.Id] = i;
                }
            }
            _lookup.TryGetValue(id, out var so);
            if (so == null)
                Debug.LogError($"ItemSO not found for id {id}!");

            return so;
        }
    }
}

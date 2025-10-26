using UnityEngine;

namespace Game
{
    public class PlaceItemController : MonoBehaviour
    {
        private InteractionController _ic;
        private bool _constructed;

        public void Construct(InteractionController ic)
        {
            _ic = ic;
            _constructed = true;
        }

        public void TryPlace()
        {
            if (!_constructed) return;
            if (!_ic.Object.HasInputAuthority) return;

            int selected = _ic.netSelectedQuickSlot;
            if (selected < 0) selected = _ic.inventory.SelectedQuickSlot;

            var slots = _ic.inventory.GetQuickSlots();
            if (slots == null || selected < 0 || selected >= slots.Length) return;

            var slot = slots[selected];
            if (slot == null || string.IsNullOrEmpty(slot.Id) || slot.Count <= 0) return;

            var so = _ic.db.Get(slot.Id);
            if (!(so is PlaceableItemSO placeable)) return;

            var cam = _ic.camera;
            if (cam == null) return;

            var ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));

            Vector3 placePos;
            Vector3 placeNormal;

            if (Physics.Raycast(ray, out var hit, _ic.PlaceRange, ~0, QueryTriggerInteraction.Ignore))
            {
                placePos   = hit.point;
                placeNormal = hit.normal;
            }
            else
            {
                placePos   = ray.origin + ray.direction * _ic.PlaceRange;
                placeNormal = Vector3.up;
            }

            var rotation = Quaternion.LookRotation(placeNormal) * Quaternion.Euler(90f, 0f, 0f);

            _ic.playerRpcHandler.RPC_RequestPlaceObject(placeable.Id, placePos, rotation);

            slot.Count -= 1;
            if (slot.Count <= 0)
            {
                slot.Id    = null;
                slot.State = null;
            }

            _ic.inventory.RaiseQuickSlotsChanged();
        }
    }
}

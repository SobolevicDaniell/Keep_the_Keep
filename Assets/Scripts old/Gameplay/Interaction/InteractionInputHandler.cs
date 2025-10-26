using UnityEngine;

namespace Game
{
    public class InteractionInputHandler : MonoBehaviour
    {
        private InteractionController _ic;
        private InputHandler _input;
        private InventoryService _inventory;
        private InteractionPromptView _prompt;

        public void Construct(
            InteractionController ic,
            InputHandler input,
            InventoryService inventory,
            InteractionPromptView prompt)
        {
            _ic        = ic;
            _input     = input;
            _inventory = inventory;
            _prompt    = prompt;

            if (_ic.Object.HasInputAuthority)
                _prompt?.Hide();
        }

        private void Update()
        {
            if (_ic == null) return;
            if (!_ic.Object.HasInputAuthority) return;
        }
    }
}

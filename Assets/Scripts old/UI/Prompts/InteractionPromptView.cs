using UnityEngine;

namespace Game
{
    public class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private GameObject promptRoot;

        public void Show() => promptRoot?.SetActive(true);
        public void Hide() => promptRoot?.SetActive(false);
    }
}

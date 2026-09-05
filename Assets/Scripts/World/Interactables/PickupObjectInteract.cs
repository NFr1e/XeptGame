using UnityEngine;
using XeptGame.Interaction;

namespace XeptGame.World
{
    public class PickupObjectInteract : MonoBehaviour, ISelectable, IInteractable
    {
        public bool IsSelected { get; set; }

        public bool CanSelect(InteractionContext ctx)
        {
            return true;
        }

        public void OnSelected(InteractionContext ctx)
        {

        }

        public void OnDeselected()
        {

        }

        public bool CanInteract(InteractionContext ctx)
        {
            return true;
        }

        public void Interact(InteractionContext ctx)
        {
            Debug.Log("Interacted with " + gameObject.name);
        }
    }
}

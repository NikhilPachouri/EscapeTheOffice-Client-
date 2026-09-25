using UnityEngine;

namespace EscapeOffice.Objects
{
    // Opens once with your own key; the key is consumed (server-side).
    public class KeyDoorObject : DoorObject
    {
        protected override Color ClosedColor => new Color(0.75f, 0.62f, 0.15f);
        public override bool Interactable => true;
        public override bool CanInteractNow => IsSolid;
        protected override string DefaultAction => "use_key";

        public override void Interact()
        {
            if (!GameManager.Instance.HasItem("key")) GameManager.Instance.Toast("Locked. You need a key.");
            SendInteract();
        }
    }
}

using UnityEngine;

namespace EscapeOffice
{
    // Trigger collider for one rectangle of a room.
    public class RoomZone : MonoBehaviour
    {
        [System.NonSerialized] public Room Room;

        void OnTriggerEnter2D(Collider2D other)
        {
            var tracker = other.GetComponentInParent<RoomTracker>();
            if (tracker != null) tracker.Entered(Room);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            var tracker = other.GetComponentInParent<RoomTracker>();
            if (tracker != null) tracker.Exited(Room);
        }
    }
}

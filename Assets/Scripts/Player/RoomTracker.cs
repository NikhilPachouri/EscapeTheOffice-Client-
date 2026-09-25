using System.Collections.Generic;
using UnityEngine;

namespace EscapeOffice
{
    // Keeps track of which room the player is in from the room trigger colliders and reports
    // changes to the server with `room { id }`. The most recently entered room wins at doorways.
    public class RoomTracker : MonoBehaviour
    {
        readonly Dictionary<Room, int> overlaps = new Dictionary<Room, int>();
        readonly List<Room> order = new List<Room>();

        public Room Current { get; private set; }

        public void Entered(Room room)
        {
            overlaps.TryGetValue(room, out int n);
            overlaps[room] = n + 1;
            order.Remove(room);
            order.Add(room);
            SetCurrent(room);
        }

        public void Exited(Room room)
        {
            if (!overlaps.TryGetValue(room, out int n)) return;
            if (n > 1) { overlaps[room] = n - 1; return; }
            overlaps.Remove(room);
            order.Remove(room);
            if (Current == room) SetCurrent(order.Count > 0 ? order[order.Count - 1] : null);
        }

        public void ResetRooms()
        {
            overlaps.Clear();
            order.Clear();
            Current = null;
        }

        void SetCurrent(Room room)
        {
            if (room == Current) return;
            Current = room;
            if (room != null) GameManager.Instance.SendRoom(room.Id);
        }
    }
}

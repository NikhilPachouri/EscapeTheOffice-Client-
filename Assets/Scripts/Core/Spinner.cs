using UnityEngine;

namespace EscapeOffice
{
    // Turns a hero-object part about its own up axis (ArtDirection.json "spin").
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 20f;

        void Update() => transform.Rotate(Vector3.up, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}

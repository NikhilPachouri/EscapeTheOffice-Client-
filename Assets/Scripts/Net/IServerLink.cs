using System;
using Newtonsoft.Json.Linq;

namespace EscapeOffice.Net
{
    // Anything that can stand in for the Go server: the real WebSocket connection or the offline fake.
    public interface IServerLink
    {
        event Action<string, JToken> MessageReceived;
        event Action<string> StatusChanged;

        bool IsOnline { get; }
        void Send(string type, object data);
        void Disconnect();
    }
}

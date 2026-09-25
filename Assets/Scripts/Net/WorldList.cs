using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace EscapeOffice.Net
{
    // The worlds a server offers for new rooms (GET /worlds). The join screen shows a picker only
    // when there are two or more; with one (or none, or a failed fetch) Create sends no world and
    // the server picks.
    public class WorldList
    {
        public List<WorldInfo> Items { get; private set; } = new List<WorldInfo>();

        int generation;

        // Replaces Items when the fetch lands. A newer Refresh wins over one still in flight.
        public IEnumerator Refresh(string wsUrl)
        {
            int mine = ++generation;
            string httpUrl;
            try { httpUrl = ListUrl(wsUrl); }
            catch (Exception e)
            {
                Debug.LogWarning($"[net] bad server url for /worlds: {e.Message}");
                yield break;
            }

            using (var req = UnityWebRequest.Get(httpUrl))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (mine != generation) yield break;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[net] GET {httpUrl}: {req.error}");
                    Items = new List<WorldInfo>();
                    yield break;
                }
                try { Items = JsonConvert.DeserializeObject<WorldsResponse>(req.downloadHandler.text)?.Worlds ?? new List<WorldInfo>(); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[net] bad /worlds reply: {e.Message}");
                    Items = new List<WorldInfo>();
                }
            }
        }

        // wss://host/ws -> https://host/worlds
        static string ListUrl(string wsUrl)
        {
            var b = new UriBuilder(wsUrl);
            b.Scheme = b.Scheme == "wss" ? "https" : b.Scheme == "ws" ? "http" : b.Scheme;
            b.Port = b.Uri.IsDefaultPort ? -1 : b.Port;
            var path = b.Path.TrimEnd('/');
            b.Path = (path.EndsWith("/ws") ? path.Substring(0, path.Length - 3) : path) + "/worlds";
            b.Query = "";
            return b.Uri.ToString();
        }

        class WorldsResponse
        {
            [JsonProperty("worlds")] public List<WorldInfo> Worlds;
        }
    }
}

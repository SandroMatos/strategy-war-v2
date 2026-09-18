using UnityEngine;

namespace LastWars.Client
{
    [CreateAssetMenu(menuName = "LastWars/Client configuration")]
    public sealed class ClientConfiguration : ScriptableObject
    {
        public string apiBaseUrl = "http://127.0.0.1:8000";
        [Min(5)] public float refreshSeconds = 10;
    }
}

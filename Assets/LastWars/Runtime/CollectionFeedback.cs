using System;
using UnityEngine;

namespace LastWars.Client
{
    [Serializable] public sealed class CollectResourcesDto
    {
        public string building_id;
        public ResourcesDto collected_resources, wallet_balances, local_stored_resources;
    }

    public static class CollectionFeedback
    {
        public static string PrefabPath(ResourceKind kind) => "CollectionEffects/" + kind + "Burst";

        // Called only for quantities acknowledged by the server, never on failed/empty collections.
        public static void Play(ResourcesDto collected, Vector3 position, Transform owner)
        {
            if (collected == null) return;
            foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
            {
                if (ProductionRules.Amount(collected, kind) <= 0) continue;
                var prefab = Resources.Load<ParticleSystem>(PrefabPath(kind));
                if (prefab == null) { Debug.LogWarning("Prefab de coleta ausente: " + PrefabPath(kind)); continue; }
                // Cone emits along local Z: point the cone upward in world space.
                var burst = UnityEngine.Object.Instantiate(prefab, position, Quaternion.Euler(-90, 0, 0), owner);
                burst.Play(true);
            }
        }
    }
}

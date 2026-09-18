using UnityEngine;
using UnityEngine.UI;

namespace LastWars.Client
{
    // Render the real ParticleSystem simulation in an overlay Canvas, above building UI.
    public sealed class ResourceParticleOverlay : MaskableGraphic
    {
        ParticleSystem source;
        Camera worldCamera;
        Texture icon;
        ParticleSystem.Particle[] particles;
        GameObject overlay;
        public override Texture mainTexture => icon != null ? icon : Texture2D.whiteTexture;

        public static void Attach(ParticleSystem source, Camera camera, Transform owner)
        {
            if (camera == null) return;
            var renderer = source.GetComponent<ParticleSystemRenderer>();
            var material = renderer.sharedMaterial;
            var texture = material != null ? material.GetTexture("_BaseMap") : null;
            if (texture == null) return;
            var root = new GameObject("Collection overlay", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var go = new GameObject("Resource particles", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(root.transform, false);
            var graphic = go.AddComponent<ResourceParticleOverlay>();
            graphic.rectTransform.anchorMin = Vector2.zero;
            graphic.rectTransform.anchorMax = Vector2.one;
            graphic.rectTransform.offsetMin = graphic.rectTransform.offsetMax = Vector2.zero;
            graphic.source = source; graphic.worldCamera = camera; graphic.icon = texture;
            graphic.overlay = root; graphic.raycastTarget = false;
            graphic.particles = new ParticleSystem.Particle[source.main.maxParticles];
            renderer.enabled = false;
        }

        void LateUpdate()
        {
            if (source == null || worldCamera == null) { Destroy(overlay); return; }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (source == null || worldCamera == null || particles == null) return;
            int count = source.GetParticles(particles);
            for (int i = 0; i < count; i++)
            {
                var p = particles[i];
                var position = p.position; // Collection prefabs simulate in world space.
                var screen = worldCamera.WorldToScreenPoint(position);
                if (screen.z <= 0) continue;
                float size = p.GetCurrentSize(source);
                var edge = worldCamera.WorldToScreenPoint(position + worldCamera.transform.right * size * .5f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, null, out var center);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, edge, null, out var side);
                float radius = Vector2.Distance(center, side);
                float angle = -p.rotation * Mathf.Deg2Rad;
                var right = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                var up = new Vector2(-right.y, right.x);
                Color32 color = p.GetCurrentColor(source);
                int start = vh.currentVertCount;
                vh.AddVert(center - right - up, color, new Vector2(0, 0));
                vh.AddVert(center - right + up, color, new Vector2(0, 1));
                vh.AddVert(center + right + up, color, new Vector2(1, 1));
                vh.AddVert(center + right - up, color, new Vector2(1, 0));
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}

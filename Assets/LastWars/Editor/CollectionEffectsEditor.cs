using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastWars.Client.Editor
{
    public static class CollectionEffectsEditor
    {
        const string Folder = "Assets/LastWars/Resources/CollectionEffects";
        [InitializeOnLoadMethod]
        static void Initialize() => EditorApplication.delayCall += EnsureAssets;

        static void EnsureAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
                if (!File.Exists(Folder + "/" + kind + "Burst.prefab")) { Build(); break; }
        }

        [MenuItem("LastWars/Gerar prefabs de burst de coleta")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind))) Create(kind);
            AssetDatabase.SaveAssets();
            Debug.Log("Coleta: quatro prefabs de ParticleSystem gerados com os ícones do HUD.");
        }

        static void Create(ResourceKind kind)
        {
            var icon = ResourceIcon.Create(null, kind, Vector2.one);
            var mesh = icon.CreateMesh();
            var texture = Rasterize(mesh);
            var texturePath = Folder + "/" + kind + "Icon.png";
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(mesh);
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(icon.gameObject);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var materialPath = Folder + "/" + kind + "Burst.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (int)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);

            var go = new GameObject(kind + "Burst");
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false; main.duration = .15f; main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(.38f, .6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.3f, .3f);
            main.gravityModifier = .35f; main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true; main.stopAction = ParticleSystemStopAction.Destroy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission;
            emission.rateOverTime = 0; emission.rateOverDistance = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)18) });
            var shape = particles.shape;
            shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28; shape.radius = .18f;
            var colors = particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .6f), new GradientAlphaKey(0, 1) });
            colors.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            PrefabUtility.SaveAsPrefabAsset(go, Folder + "/" + kind + "Burst.prefab");
            UnityEngine.Object.DestroyImmediate(go);
        }

        // Rasterize the very same vector geometry used by ResourceIcon; no duplicate artwork.
        static Texture2D Rasterize(Mesh mesh)
        {
            const int size = 128;
            var pixels = new Color[size * size];
            var vertices = mesh.vertices; var triangles = mesh.triangles; var colors = mesh.colors;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector2 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                float denominator = Cross(b - a, c - a);
                if (Mathf.Abs(denominator) < .000001f) continue;
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + .5f) / size - .5f, (y + .5f) / size - .5f);
                    float u = Cross(p - a, c - a) / denominator, v = Cross(b - a, p - a) / denominator;
                    if (u >= 0 && v >= 0 && u + v <= 1) pixels[y * size + x] = colors[triangles[t]];
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}

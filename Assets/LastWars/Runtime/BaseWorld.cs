using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LastWars.Client
{
    public sealed class BaseWorld : MonoBehaviour
    {
        readonly List<Material> materials = new List<Material>();
        readonly Dictionary<string, GameObject> buildings = new Dictionary<string, GameObject>();
        Transform content, selection;
        Camera view;
        Vector3 target, downPosition, previousPosition;
        bool dragging;
        float zoom = 12;
        public Camera ViewCamera => view;
        public GameObject BuildingModel(string id) => buildings.TryGetValue(id, out var model) ? model : null;
        public Action<string> Selected;
        public bool InputBlocked { get; set; }

        public void Initialize()
        {
            content = new GameObject("Base geometry").transform;
            content.SetParent(transform);
            view = new GameObject("Base camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            view.transform.SetParent(transform);
            view.tag = "MainCamera";
            view.orthographic = true;
            view.backgroundColor = new Color(.73f, .61f, .40f);
            view.clearFlags = CameraClearFlags.SolidColor;
            view.nearClipPlane = .1f; view.farClipPlane = 200;
            view.transform.rotation = Quaternion.Euler(48, 45, 0);
            var sun = new GameObject("Desert sun", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(transform); sun.type = LightType.Directional;
            sun.intensity = 1.2f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55, -25, 0);
            RenderSettings.ambientLight = new Color(.66f, .66f, .60f);
            Pose();
        }
        Material Material(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            materials.Add(material); return material;
        }
        GameObject Shape(PrimitiveType shape, Transform parent, Vector3 pos, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        public void Render(BaseDto data, string selected, bool frame)
        {
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (var material in materials) Destroy(material);
            materials.Clear(); buildings.Clear(); selection = null;
            var sand = Material(new Color(.76f, .64f, .42f));
            var slab = Material(new Color(.40f, .43f, .43f));
            var wall = Material(new Color(.65f, .60f, .48f));
            var blue = Material(new Color(.17f, .40f, .57f));
            var pale = Material(new Color(.70f, .76f, .75f));
            var gold = Material(new Color(.88f, .66f, .19f));
            var green = Material(new Color(.38f, .52f, .26f));
            var bounds = data.grid_bounds;
            float w = bounds.max_x - bounds.min_x, h = bounds.max_y - bounds.min_y;
            var center = new Vector3((bounds.min_x + bounds.max_x) * .5f, 0, (bounds.min_y + bounds.max_y) * .5f);
            Shape(PrimitiveType.Cube, content, center + Vector3.down * .4f, new Vector3(120, .5f, 120), sand);
            Shape(PrimitiveType.Cube, content, center + Vector3.down * .08f, new Vector3(w, .16f, h), slab);
            Shape(PrimitiveType.Cube, content, new Vector3(center.x, .2f, bounds.max_y), new Vector3(w, .4f, .18f), wall);
            Shape(PrimitiveType.Cube, content, new Vector3(bounds.min_x, .2f, center.z), new Vector3(.18f, .4f, h), wall);
            // Small road markings make the otherwise temporary geometry readable at an isometric angle.
            for (int x = bounds.min_x; x < bounds.max_x; x++)
                Shape(PrimitiveType.Cube, content, new Vector3(x + .5f, .015f, center.z), new Vector3(.5f, .025f, .05f), pale);
            foreach (var b in data.buildings)
            {
                var root = new GameObject(Contracts.Name(b.type)); root.transform.SetParent(content, false);
                root.transform.position = new Vector3(b.grid_x + b.width * .5f, 0, b.grid_y + b.height * .5f);
                root.AddComponent<BuildingMarker>().Id = b.id;
                buildings.Add(b.id, root);
                float bw = Mathf.Max(.4f, b.width - .22f), bh = Mathf.Max(.4f, b.height - .22f);
                var accent = b.type.Contains("farm") ? green : b.type.Contains("gold") ? gold : blue;
                if (b.status == "destroyed" || b.status == "pending") accent = wall;
                Shape(PrimitiveType.Cube, root.transform, new Vector3(0, .09f, 0), new Vector3(bw, .18f, bh), wall);
                if (b.type == "food_farm")
                {
                    for (int row = 0; row < 4; row++) Shape(PrimitiveType.Cube, root.transform,
                        new Vector3(0, .24f, (row - 1.5f) * bh / 4), new Vector3(bw * .86f, .3f, bh / 7), gold);
                }
                else
                {
                    float height = b.type == "command_center" ? 1.25f : .65f;
                    if (b.type == "wall") height = .45f;
                    Shape(PrimitiveType.Cube, root.transform, new Vector3(0, height / 2 + .18f, 0), new Vector3(bw * .78f, height, bh * .78f), pale);
                    Shape(PrimitiveType.Cube, root.transform, new Vector3(0, height + .22f, 0), new Vector3(bw * .9f, .18f, bh * .9f), accent);
                    Shape(PrimitiveType.Cube, root.transform, new Vector3(0, .43f, -bh * .4f), new Vector3(bw * .26f, .48f, .05f), blue);
                    if (b.type.Contains("mine") || b.type.Contains("refinery"))
                        Shape(PrimitiveType.Cylinder, root.transform, new Vector3(bw * .2f, .75f, bh * .2f), new Vector3(.3f, .75f, .3f), accent);
                    if (b.type == "hospital")
                    {
                        Shape(PrimitiveType.Cube, root.transform, new Vector3(0, height + .33f, 0), new Vector3(.5f, .03f, .15f), gold);
                        Shape(PrimitiveType.Cube, root.transform, new Vector3(0, height + .33f, 0), new Vector3(.15f, .03f, .5f), gold);
                    }
                }
                if (b.status == "building" || b.status == "ready_to_upgrade")
                    Shape(PrimitiveType.Cube, root.transform, new Vector3(-bw * .4f, 1.3f, 0), new Vector3(.08f, 2.6f, .08f), gold);
            }
            if (frame) { target = center; zoom = Mathf.Max(w, h) * .85f; }
            Select(selected); Pose();
        }
        public void Obstacles(ObstacleDto[] obstacles)
        {
            var rock = Material(new Color(.38f, .34f, .27f));
            foreach (var o in obstacles ?? Array.Empty<ObstacleDto>())
                if (!o.is_cleared) Shape(PrimitiveType.Sphere, content,
                    new Vector3(o.grid_x + o.width * .5f, .15f, o.grid_y + o.height * .5f),
                    new Vector3(o.width * .8f, .6f, o.height * .8f), rock);
        }
        public void Select(string id)
        {
            if (selection != null) Destroy(selection.gameObject);
            if (id == null || !buildings.TryGetValue(id, out var go)) return;
            var box = go.GetComponentsInChildren<Renderer>()[0].bounds;
            selection = Shape(PrimitiveType.Cube, go.transform, new Vector3(0, .01f, 0),
                new Vector3(box.size.x + .18f, .03f, box.size.z + .18f), Material(new Color(.12f, .85f, .95f))).transform;
        }
        public void Recenter(BaseDto data)
        {
            target = new Vector3((data.grid_bounds.min_x + data.grid_bounds.max_x) * .5f, 0,
                (data.grid_bounds.min_y + data.grid_bounds.max_y) * .5f); Pose();
        }
        bool OverUI() => EventSystem.current != null && (Input.touchCount > 0
            ? EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
            : EventSystem.current.IsPointerOverGameObject());
        void Update()
        {
            if (view == null || InputBlocked) { dragging = false; return; }
            if (Input.GetMouseButtonDown(0)) { dragging = !OverUI(); downPosition = previousPosition = Input.mousePosition; }
            if (dragging && Input.GetMouseButton(0) && Input.touchCount < 2)
            {
                var delta = Input.mousePosition - previousPosition;
                var right = view.transform.right; var forward = Vector3.ProjectOnPlane(view.transform.up, Vector3.up).normalized;
                target -= (right * delta.x + forward * delta.y) * (zoom * 2 / Mathf.Max(1, Screen.height));
                previousPosition = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (dragging && !OverUI() && (Input.mousePosition - downPosition).sqrMagnitude < 64 &&
                    Physics.Raycast(view.ScreenPointToRay(Input.mousePosition), out var hit))
                { var marker = hit.collider.GetComponentInParent<BuildingMarker>(); if (marker != null) Selected?.Invoke(marker.Id); }
                dragging = false;
            }
            if (!OverUI()) zoom = Mathf.Clamp(zoom - Input.mouseScrollDelta.y, 4, 30);
            if (Input.touchCount == 2 && !OverUI())
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                float previous = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
                zoom = Mathf.Clamp(zoom - ((a.position - b.position).magnitude - previous) * .02f, 4, 30);
                dragging = false;
            }
            Pose();
        }
        void Pose() { view.orthographicSize = zoom; view.transform.position = target - view.transform.forward * 45; }
        void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}

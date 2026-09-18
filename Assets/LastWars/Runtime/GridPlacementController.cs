using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LastWars.Client
{
    public sealed class GridPlacementController : MonoBehaviour
    {
        BaseDto state;
        ObstacleDto[] obstacles;
        BuildingDto building;
        Camera cameraView;
        GameObject visuals, preview;
        Material validMaterial, invalidMaterial, gridMaterial;
        Renderer[] previewRenderers;
        public bool IsActive => visuals != null;
        public bool CanConfirm { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public Action<int, int, string> Changed;
        public Action<string, int, int> Confirmed;
        public Action Cancelled;

        public void Begin(BaseDto data, ObstacleDto[] blockers, BuildingDto item, Camera camera, GameObject model)
        {
            End(); state = data; obstacles = blockers; building = item; cameraView = camera;
            visuals = new GameObject("Placement preview and grid"); visuals.transform.SetParent(transform, false);
            validMaterial = MakeMaterial(new Color(.15f, .9f, .45f));
            invalidMaterial = MakeMaterial(new Color(1, .19f, .15f));
            gridMaterial = MakeMaterial(new Color(.68f, .83f, .88f));
            var bounds = state.grid_bounds;
            for (int x = bounds.min_x; x <= bounds.max_x; x++)
                Bar(new Vector3(x, .035f, (bounds.min_y + bounds.max_y) * .5f), new Vector3(.025f, .025f, bounds.max_y - bounds.min_y));
            for (int y = bounds.min_y; y <= bounds.max_y; y++)
                Bar(new Vector3((bounds.min_x + bounds.max_x) * .5f, .035f, y), new Vector3(bounds.max_x - bounds.min_x, .025f, .025f));
            preview = Instantiate(model, visuals.transform); preview.name = "Building ghost";
            foreach (var collider in preview.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var footprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            footprint.name = "Full footprint"; footprint.transform.SetParent(preview.transform, false);
            footprint.transform.localPosition = new Vector3(0, .06f, 0);
            footprint.transform.localScale = new Vector3(building.width - .04f, .04f, building.height - .04f);
            footprint.GetComponent<Collider>().enabled = false;
            previewRenderers = preview.GetComponentsInChildren<Renderer>();
            SetCandidate(item.grid_x, item.grid_y);
        }
        static Material MakeMaterial(Color color) => new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")) { color = color };
        void Bar(Vector3 position, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(visuals.transform, false);
            go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Collider>().enabled = false; go.GetComponent<Renderer>().sharedMaterial = gridMaterial;
        }
        public void SetCandidate(int x, int y)
        {
            if (!IsActive) return;
            X = x; Y = y;
            string error = GridPlacementRules.Validate(state, obstacles, building, x, y);
            CanConfirm = error == null && (x != building.grid_x || y != building.grid_y);
            if (error == null && !CanConfirm) error = "Escolha uma célula diferente da posição atual.";
            preview.transform.position = new Vector3(x + building.width * .5f, .08f, y + building.height * .5f);
            foreach (var renderer in previewRenderers) renderer.sharedMaterial = error == null ? validMaterial : invalidMaterial;
            Changed?.Invoke(x, y, error);
        }
        void Update()
        {
            if (!IsActive) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { Cancel(); return; }
            // Only a press/drag updates the candidate: moving the pointer to Confirm never moves the ghost.
            if (!Input.GetMouseButton(0) || Input.touchCount > 1) return;
            if (EventSystem.current != null && (Input.touchCount > 0
                ? EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)
                : EventSystem.current.IsPointerOverGameObject())) return;
            var ray = cameraView.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance))
            {
                var point = ray.GetPoint(distance);
                int x = Mathf.RoundToInt(point.x - building.width * .5f);
                int y = Mathf.RoundToInt(point.z - building.height * .5f);
                if (x != X || y != Y) SetCandidate(x, y);
            }
        }
        public void Confirm()
        {
            if (!IsActive || !CanConfirm || GridPlacementRules.Validate(state, obstacles, building, X, Y) != null) return;
            var id = building.id; int x = X, y = Y; End(); Confirmed?.Invoke(id, x, y);
        }
        public void Cancel() { if (!IsActive) return; End(); Cancelled?.Invoke(); }
        public void End()
        {
            if (visuals != null) { visuals.SetActive(false); Destroy(visuals); }
            visuals = null; CanConfirm = false;
            if (validMaterial != null) Destroy(validMaterial);
            if (invalidMaterial != null) Destroy(invalidMaterial);
            if (gridMaterial != null) Destroy(gridMaterial);
        }
        void OnDisable() { End(); }
    }
}

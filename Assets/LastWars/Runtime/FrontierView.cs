using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastWars.Client
{
    public sealed class FrontierView : MonoBehaviour
    {
        static readonly Color Navy = new Color(.09f, .14f, .22f, .97f);
        static readonly Color Slate = new Color(.17f, .24f, .34f, 1);
        static readonly Color Cyan = new Color(.04f, .55f, .72f, 1);
        static readonly Color Green = new Color(.21f, .55f, .30f, 1);
        RectTransform safe, dialog;
        GameObject shade, hud, placementPanel;
        TMP_Text placementStatus;
        Button placementConfirm;
        TMP_Text status, commander;
        readonly TMP_Text[] resourceAmounts = new TMP_Text[5];
        readonly RectTransform[] resourceFills = new RectTransform[5];
        readonly AnimatedResourceValue[] counters = new AnimatedResourceValue[5];
        int availableBuilders, totalBuilders;
        ResourcesDto hudResources;
        long resourceCapacity;
        CanvasGroup dialogGroup, hudGroup;
        Rect lastSafe;
        Vector2 lastScreen;
        public bool HasDialog => shade != null;
        public Action Refresh, Recenter, ListBuildings, Tutorial, Retry;

        public void Initialize()
        {
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            safe = Rect("Safe area", transform); Stretch(safe);
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            hud = new GameObject("HUD", typeof(RectTransform)); hud.transform.SetParent(safe, false); Stretch((RectTransform)hud.transform);
            hudGroup = hud.AddComponent<CanvasGroup>();
            var top = Panel("Command strip", hud.transform, Navy); Top(top, 108);
            commander = Text(top, "IRON FRONTIER", 22, 48);
            commander.rectTransform.anchorMin = new Vector2(.16f, 1);
            commander.rectTransform.anchorMax = new Vector2(.84f, 1);
            commander.rectTransform.offsetMin = new Vector2(12, -48);
            commander.rectTransform.offsetMax = new Vector2(-12, -8);
            commander.alignment = TextAlignmentOptions.Center;
            commander.enableWordWrapping = false;
            commander.enableAutoSizing = true; commander.fontSizeMin = 12; commander.fontSizeMax = 22;
            var primaryRow = Rect("Builders", top);
            primaryRow.anchorMin = primaryRow.anchorMax = Vector2.one;
            primaryRow.offsetMin = new Vector2(-144, -48); primaryRow.offsetMax = new Vector2(-12, -8);
            var secondaryRow = Rect("Resources", top);
            secondaryRow.anchorMin = new Vector2(0, 1); secondaryRow.anchorMax = Vector2.one;
            secondaryRow.offsetMin = new Vector2(12, -100); secondaryRow.offsetMax = new Vector2(-12, -56);
            foreach (var resourceRow in new[] { primaryRow, secondaryRow })
            {
                var resourceLayout = resourceRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                resourceLayout.spacing = 8;
                resourceLayout.childForceExpandWidth = false; resourceLayout.childForceExpandHeight = true;
            }
            // Display priority is independent from resource IDs and animated counter indices.
            foreach (int i in new[] { 2, 0, 4, 1, 3 })
            {
                bool builder = i == 4;
                bool secondary = !builder;
                var kind = builder ? ResourceKind.Food : (ResourceKind)i;
                var card = Panel(builder ? "Construtores" : ProductionRules.Name(kind), secondary ? secondaryRow : primaryRow, Slate);
                var cardLayout = card.gameObject.AddComponent<LayoutElement>();
                cardLayout.minWidth = 0; cardLayout.preferredWidth = 0;
                cardLayout.flexibleWidth = 1;
                var fill = Panel("Capacity fill", card, Cyan);
                Stretch(fill); fill.anchorMax = new Vector2(0, 1);
                fill.GetComponent<Image>().raycastTarget = false;
                resourceFills[i] = fill;
                float iconSize = builder ? 30 : 34;
                var icon = ResourceIcon.Create(card, kind, new Vector2(iconSize, iconSize));
                icon.Builder = builder; icon.SetVerticesDirty();
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, .5f);
                icon.rectTransform.anchoredPosition = new Vector2(builder ? 21 : 24, 0);
                var amount = Text(card, "0", 20, 26);
                Stretch(amount.rectTransform);
                amount.rectTransform.offsetMin = new Vector2(builder ? 34 : 40, 3);
                amount.rectTransform.offsetMax = new Vector2(builder ? -34 : -40, -3);
                amount.enableAutoSizing = true; amount.fontSizeMin = 10; amount.fontSizeMax = 20;
                amount.alignment = TextAlignmentOptions.Center;
                amount.enableWordWrapping = false; resourceAmounts[i] = amount;
                counters[i] = card.gameObject.AddComponent<AnimatedResourceValue>();
                counters[i].Initialize(amount, fill, Cyan, Green);
            }
            var bottom = Panel("Navigation", hud.transform, Navy); Bottom(bottom, 108);
            var row = Rect("Actions", bottom); row.anchorMin = new Vector2(0, 1); row.anchorMax = Vector2.one;
            row.pivot = new Vector2(.5f, 1); row.offsetMin = new Vector2(12, -53); row.offsetMax = new Vector2(-12, -8);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 8; layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            Button(row, "BASE", () => Recenter?.Invoke()); Button(row, "EDIFÍCIOS", () => ListBuildings?.Invoke());
            Button(row, "TUTORIAL", () => Tutorial?.Invoke()); Button(row, "ATUALIZAR", () => Refresh?.Invoke());
            status = Text(bottom, "Arraste para mover • Role para aproximar • Selecione um edifício", 15, 42);
            Position(status.rectTransform, new Vector2(16, 7), new Vector2(-16, 48), false);
            hud.SetActive(false); ApplySafeArea();
        }
        void Update() { if (lastSafe != Screen.safeArea || lastScreen != new Vector2(Screen.width, Screen.height)) ApplySafeArea(); }
        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea; lastScreen = new Vector2(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;
            safe.anchorMin = new Vector2(lastSafe.xMin / Screen.width, lastSafe.yMin / Screen.height);
            safe.anchorMax = new Vector2(lastSafe.xMax / Screen.width, lastSafe.yMax / Screen.height);
            ResizeDialog();
        }
        void ResizeDialog()
        {
            if (dialog == null) return;
            Canvas.ForceUpdateCanvases();
            dialog.sizeDelta = new Vector2(Mathf.Min(510, safe.rect.width - 24), Mathf.Min(540, safe.rect.height - 32));
        }
        public void Busy(bool value)
        {
            hudGroup.interactable = !value && placementPanel == null;
            if (dialogGroup != null) dialogGroup.interactable = !value;
        }
        public void ShowPlacement(Action confirm, Action cancel)
        {
            Close(); HidePlacement(); hudGroup.interactable = false;
            var panel = Panel("Placement controls", safe, Navy); Bottom(panel, 160); placementPanel = panel.gameObject;
            placementStatus = Text(panel, "", 17, 80);
            Position(placementStatus.rectTransform, new Vector2(16, 76), new Vector2(-16, 150), false);
            var row = Rect("Placement actions", panel); row.anchorMin = Vector2.zero; row.anchorMax = new Vector2(1, 0);
            row.offsetMin = new Vector2(16, 16); row.offsetMax = new Vector2(-16, 65);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 12;
            layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            placementConfirm = Button(row, "CONFIRMAR POSIÇÃO", () => confirm(), Green);
            Button(row, "CANCELAR", () => cancel());
        }
        public void PlacementStatus(int x, int y, string error)
        {
            if (placementPanel == null) return;
            placementStatus.text = $"Célula ({x}, {y}) • Clique/toque ou arraste na grade.\n" + (error ?? "Posição livre. Confirme para salvar no servidor.");
            placementStatus.color = error == null ? new Color(.5f, 1, .6f) : new Color(1, .7f, .5f);
            placementConfirm.interactable = error == null;
        }
        public void HidePlacement()
        {
            if (placementPanel != null) { placementPanel.SetActive(false); Destroy(placementPanel); }
            placementPanel = null; if (hudGroup != null) hudGroup.interactable = true;
        }
        public void Status(string message, bool error = false)
        { status.text = message; status.color = error ? new Color(1, .69f, .48f) : Color.white; }
        public void Hud(BaseDto data, string name)
        {
            hud.SetActive(true);
            commander.text = name;
            availableBuilders = data.available_builders; totalBuilders = data.total_builders;
            hudResources = data.resources;
            UpdateResourceBars();
        }
        // Capacity comes from the existing production endpoint, not the base contract.
        public void StorageCapacity(long capacity)
        {
            resourceCapacity = System.Math.Max(0, capacity);
            UpdateResourceBars();
        }
        void UpdateResourceBars()
        {
            for (int i = 0; i < resourceAmounts.Length; i++)
            {
                if (counters[i] == null) continue;
                long amount = i == 4 ? availableBuilders : ProductionRules.Amount(hudResources, (ResourceKind)i);
                counters[i].SetValue(amount, i == 4 ? totalBuilders : resourceCapacity);
            }
        }
        public void Close() { if (shade != null) Destroy(shade); shade = null; dialog = null; dialogGroup = null; }
        public RectTransform Dialog(string title, bool close = true)
        {
            Close();
            var overlay = Panel("Dialog backdrop", safe, new Color(0, 0, 0, .62f)); Stretch(overlay); shade = overlay.gameObject;
            dialog = Panel("Dialog", overlay, Navy); dialog.anchorMin = dialog.anchorMax = new Vector2(.5f, .5f); dialog.pivot = new Vector2(.5f, .5f);
            dialogGroup = dialog.gameObject.AddComponent<CanvasGroup>(); ResizeDialog();
            var heading = Text(dialog, title, 24, 45); Position(heading.rectTransform, new Vector2(20, -8), new Vector2(-70, -55), true);
            if (close)
            {
                var button = Button(dialog, "X", Close); var r = (RectTransform)button.transform;
                r.anchorMin = r.anchorMax = Vector2.one; r.pivot = Vector2.one; r.anchoredPosition = new Vector2(-12, -12); r.sizeDelta = new Vector2(42, 38);
            }
            var scrollRoot = Rect("Scroll", dialog); Stretch(scrollRoot); scrollRoot.offsetMin = new Vector2(18, 18); scrollRoot.offsetMax = new Vector2(-18, -62);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Panel("Viewport", scrollRoot, Color.clear); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 12; layout.childControlHeight = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            return content;
        }
        public void Login(string endpoint, string message, bool resume, Action<string> create, Action retry)
        {
            var content = Dialog("IRON FRONTIER", false);
            Text(content, "RECONSTRUA. EVOLUA. COMANDE.", 16, 35);
            Text(content, message, 18, 78);
            if (resume) Button(content, "RECONECTAR À MINHA BASE", () => retry(), Green);
            else
            {
                Text(content, "Nome do comandante • 3 a 32 caracteres", 15, 26);
                var input = Input(content);
                Button(content, "INICIAR BASE", () => create(input.text), Green);
            }
            Text(content, "Seu progresso é salvo no servidor. Guarde a sessão deste dispositivo.", 14, 55);
            Text(content, "Servidor: " + endpoint, 12, 45);
        }
        static TMP_InputField Input(Transform parent)
        {
            var root = Panel("Commander name", parent, Slate); Height(root, 52);
            var input = root.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect("Text area", root); Stretch(viewport); viewport.offsetMin = new Vector2(12, 4); viewport.offsetMax = new Vector2(-12, -4);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text(viewport, "", 20, 40); Stretch(text.rectTransform);
            var placeholder = Text(viewport, "Seu nome", 20, 40); Stretch(placeholder.rectTransform); placeholder.color = new Color(.65f, .72f, .79f);
            input.textViewport = viewport; input.textComponent = text; input.placeholder = placeholder;
            input.characterLimit = 32; input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }
        public static TMP_Text Text(Transform parent, string value, float size = 18, float height = 42)
        {
            var rect = Rect("Text", parent); Height(rect, height);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size;
            text.color = Color.white; text.richText = false; text.raycastTarget = false;
            text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
        public static Button Button(Transform parent, string caption, UnityAction action, Color? color = null)
        {
            var rect = Panel(caption, parent, color ?? Cyan); Height(rect, 46);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.disabledColor = new Color(.35f, .39f, .43f, .65f); button.colors = colors;
            button.onClick.AddListener(action);
            var text = Text(rect, caption, 17, 42); Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(7, 3); text.rectTransform.offsetMax = new Vector2(-7, -3);
            text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = true; text.fontSizeMin = 11; text.fontSizeMax = 17;
            return button;
        }
        static RectTransform Rect(string name, Transform parent)
        { var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false); return r; }
        static RectTransform Panel(string name, Transform parent, Color color)
        { var r = Rect(name, parent); r.gameObject.AddComponent<Image>().color = color; return r; }
        static void Height(RectTransform rect, float height) { rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height; }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void Top(RectTransform r, float h) { r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.offsetMin = new Vector2(0, -h); r.offsetMax = Vector2.zero; }
        static void Bottom(RectTransform r, float h) { r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(1, 0); r.pivot = new Vector2(.5f, 0); r.offsetMin = Vector2.zero; r.offsetMax = new Vector2(0, h); }
        static void Position(RectTransform r, Vector2 min, Vector2 max, bool top)
        { r.anchorMin = new Vector2(0, top ? 1 : 0); r.anchorMax = new Vector2(1, top ? 1 : 0); r.offsetMin = new Vector2(min.x, Mathf.Min(min.y, max.y)); r.offsetMax = new Vector2(max.x, Mathf.Max(min.y, max.y)); }
    }
}

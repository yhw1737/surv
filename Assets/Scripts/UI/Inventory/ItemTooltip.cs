using Isle.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Isle.UI.Inventory
{
    /// <summary>
    /// SYS-INV-01 §Required UX: hover tooltip. Shows only the fields the data model actually has
    /// today — weight, size and stack count. Freshness/quality/enchants/crafter are omitted on
    /// purpose: spoilage-instance tracking, item quality, enchants and crafting attribution are all
    /// unbuilt systems (no such fields exist on <see cref="ItemDef"/> or <see cref="Placement"/>
    /// yet). This is a documented limitation, not invented data — extend it once those systems exist.
    /// <para>
    /// The item name is shown as its raw lang key (e.g. <c>"@item.raw_meat"</c>) rather than
    /// translated text, since no localization/lang-table loader exists anywhere in the project yet.
    /// Same placeholder-stage honesty as <see cref="PlaceholderIcons"/> showing a coloured shape
    /// instead of real art.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ItemTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // ponytail: one shared tooltip panel reused by every icon — only one can be hovered at a
        // time anyway, so a per-icon panel would just be N idle GameObjects.
        static GameObject _panel;
        static Text _label;

        Placement _placement;

        public void Bind(Placement placement) => _placement = placement;

        public void OnPointerEnter(PointerEventData eventData)
        {
            EnsurePanel();
            var size = _placement.EffectiveSize();
            _label.text = $"{_placement.Item.Name}\n{size.W}x{size.H}  {_placement.Item.Weight:0.0}kg  x{_placement.Count}";
            // Newer UI (e.g. a bag grid opened after this panel was first built — the panel is a
            // lazily-created singleton, see EnsurePanel) sits later in the Canvas hierarchy and
            // renders on top by default; force this panel to the front every time it's shown.
            _panel.transform.SetAsLastSibling();
            _panel.transform.position = eventData.position;
            _panel.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData) => _panel.SetActive(false);

        // ponytail: every icon's ItemTooltip runs this, but only the one shared panel exists and
        // setting its position to the same value from more than one instance in the same frame is
        // harmless — cheaper than wiring a single "who's currently hovered" owner for a tooltip.
        void Update()
        {
            if (_panel == null || !_panel.activeSelf) return;
            _panel.transform.position = Mouse.current.position.ReadValue();
        }

        static void EnsurePanel()
        {
            if (_panel != null) return;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            _panel = new GameObject("ItemTooltip", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var panelImage = _panel.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.8f);
            // The panel is placed directly under the cursor (OnPointerEnter) — left raycast-blocking,
            // it eats the pointer that's supposed to stay over the item icon underneath, firing
            // OnPointerExit the instant it appears, hiding it, re-exposing the icon, and re-entering:
            // an every-frame show/hide flicker.
            panelImage.raycastTarget = false;
            var panelRect = (RectTransform)_panel.transform;
            // Top-left anchor/pivot so the panel's own origin — not its centre — is what tracks the
            // cursor (see OnPointerEnter/Update): the box extends down-right from the mouse, same
            // top-left convention every other dynamically-built rect in this codebase already uses.
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(160, 48);

            // ponytail: Text needs its own child GameObject — see GridView.BuildAutoSortButton.
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(_panel.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            _label = labelGo.GetComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // ponytail: built-in font, real UI style comes at T-160
            _label.fontSize = 14;
            _label.color = Color.white;
            _label.raycastTarget = false;

            _panel.SetActive(false);
        }
    }
}

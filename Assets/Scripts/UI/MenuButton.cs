using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farhad
{
    // A stone on the main menu. Pointing at it selects it, so the mouse, the keyboard and a pad all
    // share one highlight, and the selected stone grows a little and lifts its label. The label is
    // in the game's language; a stone with words in only one language (the language stones)
    // always shows those.
    [RequireComponent(typeof(Button))]
    public class MenuButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        [Min(1f)] public float selectedScale = 1.08f;
        [Tooltip("Seconds to grow or shrink.")]
        [Min(0.01f)] public float scaleSeconds = 0.12f;
        public Graphic label;
        public Color labelColor = new Color(0.1f, 0.09f, 0.09f);
        public Color selectedLabelColor = Color.black;
        public string english;
        public string persian;
        public Font englishFont;
        public Font persianFont;

        bool selected;

        void OnEnable()
        {
            if (label != null) label.color = labelColor;
            ShowLabel();
            Language.Changed += ShowLabel;
        }

        // A stone hidden while selected never hears it was let go, so it lets go itself.
        void OnDisable()
        {
            Language.Changed -= ShowLabel;
            selected = false;
            if (label != null) label.color = labelColor;
            transform.localScale = Vector3.one;
        }

        public void ShowLabel()
        {
            if (label is not Text text) return;
            bool persianWords = string.IsNullOrEmpty(english) ||
                                Language.Current == Lang.Persian && !string.IsNullOrEmpty(persian);
            text.font = persianWords ? persianFont : englishFont;
            text.text = persianWords ? PersianText.Visual(PersianText.Shape(persian ?? "")) : english;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var button = GetComponent<Button>();
            if (button.interactable && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData eventData)
        {
            selected = true;
            if (label != null) label.color = selectedLabelColor;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selected = false;
            if (label != null) label.color = labelColor;
        }

        void Update()
        {
            float target = selected ? selectedScale : 1f;
            float step = (selectedScale - 1f) * Time.unscaledDeltaTime / scaleSeconds;
            float scale = Mathf.MoveTowards(transform.localScale.x, target, step);
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}

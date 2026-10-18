using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Farhad
{
    // A stone on the main menu. Pointing at it selects it, so the mouse, the keyboard and a pad all
    // share one highlight, and the selected stone grows a little and lifts its label.
    [RequireComponent(typeof(Button))]
    public class MenuButton : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        [Min(1f)] public float selectedScale = 1.08f;
        [Tooltip("Seconds to grow or shrink.")]
        [Min(0.01f)] public float scaleSeconds = 0.12f;
        public Graphic label;
        public Color labelColor = new Color(0.1f, 0.09f, 0.09f);
        public Color selectedLabelColor = Color.black;

        bool selected;

        void OnEnable()
        {
            if (label != null) label.color = labelColor;
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

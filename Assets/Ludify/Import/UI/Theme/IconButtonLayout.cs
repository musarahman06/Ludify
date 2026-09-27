using UnityEngine;

namespace Ludify.Import
{
    /// <summary>Keeps an icon button's label just right of its square icon block, whatever the button's height.</summary>
    [ExecuteAlways]
    public sealed class IconButtonLayout : MonoBehaviour
    {
        void OnRectTransformDimensionsChange() => Layout();
        void Start() => Layout();

        void Layout()
        {
            var rt = (RectTransform)transform;
            Transform label = transform.Find("Label");
            if (label == null) return;
            var lrt = (RectTransform)label;
            lrt.offsetMin = new Vector2(rt.rect.height + 18, lrt.offsetMin.y);
        }
    }
}

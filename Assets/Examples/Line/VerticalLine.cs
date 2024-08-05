using TMPro;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class VerticalLine : MonoBehaviour
{
    [SerializeField] private bool m_IsVertical;

    [SerializeField] private TMP_Text m_LinePosTxt;
    [SerializeField] private bool m_ShowTxt;

    private RectTransform m_RectCache;

    public void Refresh(float spotPos)
    {
        if (null == m_RectCache)
        {
            m_RectCache = GetComponent<RectTransform>();
        }

        if (m_IsVertical)
        {
            var anchor = new Vector2(0.5f, spotPos % 1);
            if (anchor.y < 0)
            {
                anchor.y += 1f;
            }

            m_RectCache.anchorMin = anchor;
            m_RectCache.anchorMax = anchor;
            m_RectCache.pivot = Vector2.one * 0.5f;
            var pos = m_RectCache.anchoredPosition;
            pos.y = 0f;
            m_RectCache.anchoredPosition = pos;
        }
        else
        {
            var anchor = new Vector2(spotPos % 1, 0.5f);
            if (anchor.x < 0)
            {
                anchor.x += 1f;
            }

            m_RectCache.anchorMin = anchor;
            m_RectCache.anchorMax = anchor;
            m_RectCache.pivot = Vector2.one * 0.5f;
            var pos = m_RectCache.anchoredPosition;
            pos.x = 0f;
            m_RectCache.anchoredPosition = pos;
        }

        if (m_ShowTxt)
        {
            m_LinePosTxt.gameObject.SetActive(true);
            m_LinePosTxt.text = spotPos.ToString("F1");
        }
        else
        {
            m_LinePosTxt.gameObject.SetActive(false);
        }
    }
}

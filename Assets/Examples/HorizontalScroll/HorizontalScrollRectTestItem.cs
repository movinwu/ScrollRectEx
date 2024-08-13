using ScrollViewEx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HorizontalScrollRectTestItem : HorizontalScrollRectViewItem
{

    [SerializeField] private Line[] m_AllHorizontalLine;

    public void Refresh()
    {
        var pos = this.CurIndex;
        for (int i = 0; i < m_AllHorizontalLine.Length; i++)
        {
            m_AllHorizontalLine[i].Refresh(pos + 0.1f * (i + 1));
        }
    }
}

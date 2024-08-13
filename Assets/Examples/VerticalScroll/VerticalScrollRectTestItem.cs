using ScrollViewEx;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VerticalScrollRectTestItem : VerticalScrollRectViewItem
{

    [SerializeField] private Line[] m_AllVerticalLine;

    public void Refresh()
    {
        var pos = this.CurIndex;
        for (int i = 0; i < m_AllVerticalLine.Length; i++)
        {
            m_AllVerticalLine[i].Refresh(pos + 0.1f * (i + 1));
        }
    }
}

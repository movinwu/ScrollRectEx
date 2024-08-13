using ScrollViewEx;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class HorizontalScrollRectPanel : MonoBehaviour
{
    public HorizontalScrollRect scroll;

    [SerializeField] private Line[] m_AllHorizontalLine;

    //private bool m_ChangeHeight;

    private void Start()
    {
        //m_ChangeHeight = false;

        for (int i = 0; i < m_AllHorizontalLine.Length; i++)
        {
            m_AllHorizontalLine[i].Refresh(0.1f * (i + 1));
        }

        scroll.StartScrollView(
            itemCount: 16,
            refreshItemAction: item => (item as HorizontalScrollRectTestItem).Refresh(),
            recycleItemAction: null,
            getchildItemPrefabIndex: index => index % 3,
            //getItemHeight: index =>
            //{
            //    var prefabInd = index % 3;
            //    var baseHeight = 300f;
            //    if (prefabInd == 0)
            //    {
            //        baseHeight = 460f;
            //    }
            //    else if (prefabInd == 1)
            //    {
            //        baseHeight = 600f;
            //    }
            //    return baseHeight * (m_ChangeHeight ? 2f : 1f);
            //},
            initItemPos: 0.2f,
            isLoop: true);

       StartCoroutine(ScrollTo());
    }

    private IEnumerator ScrollTo()
    {
        //yield return new WaitForSeconds(5f);

        //m_ChangeHeight = true;
        //scroll.ForceUpdateShownItems();

        //yield return new WaitForSeconds(5f);

        //scroll.JumpTo(3.5f);

        yield return new WaitForSeconds(4f);

        scroll.ScrollToBySpeed(1.6f, 300f, false, () =>
        {
            Debug.Log("¹ö¶¯Íê±Ï");
        });
    }
}

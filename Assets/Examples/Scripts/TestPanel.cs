using ScrollViewEx;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public struct TestChildData {
    public string Title;
    public string Note1;
    public string Note2;

    public TestChildData(string t, string n1, string n2) {
        Title = t;
        Note1 = n1;
        Note2 = n2;
    }
}

public class TestPanel : MonoBehaviour {
    public VerticalScrollRect theList;
    private List<TestChildData> data = new List<TestChildData>();
    public Button m_Btn;
    public InputField m_InputField;

    private void Start() {

        RetrieveData();

        theList.StartScrollView(data.Count, item => PopulateItem(item), null, null, initItemPos: 18f);

        //m_Btn.onClick.RemoveAllListeners();
        //m_Btn.onClick.AddListener(() =>
        //{
        //    if (int.TryParse(m_InputField.text, out var index))
        //    {
        //        theList.JumpTo(index);
        //    }
        //});

        StartCoroutine(ScrollTo());
    }

    private IEnumerator ScrollTo()
    {
        yield return new WaitForSeconds(1f);

        theList.ScrollToByTime(0f, 1f, true, () =>
        {
            theList.ScrollToBySpeed(7.2f, 100f, false, () =>
            {
                Debug.Log("¹ö¶¯Íê±Ï");
            });
        });
    }

    private void RetrieveData() {
        data.Clear();
        int row = 0;

        // You'd obviously load real data here
        string[] randomTitles = new[] {
            "Hello World",
            "This is fine",
            "You look nice today",
            "Recycling is good",
            "Why not",
            "Go outside",
            "And do something",
            "Less boring instead"
        };
        for (int i = 0; i < 18; ++i) {
            data.Add(new TestChildData(randomTitles[i % randomTitles.Length], $"Row {row++}", Random.Range(0, 256).ToString()));
        }
    }

    private void PopulateItem(RecyclingListViewItem item) 
    {
        var child = item as TestChildItem;
        child.ChildData = data[item.CurIndex];
    }
    
}

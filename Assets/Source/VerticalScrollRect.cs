using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ScrollViewEx
{
    /// <summary>
    /// ��ֱ������
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class VerticalScrollRect : MonoBehaviour
    {
        [Header("����itemԤ����")]
        [SerializeField] private RecyclingListViewItem[] m_ChildPrefab;
        [Header("����item���")]
        [SerializeField] private float[] m_AllSpacing;
        [Header("���վ���")]
        [SerializeField] private float m_PreAllocLength = 200;

        [Header("��������")]
        [SerializeField] private EScrollDirection m_ScrollDirection;

        /// <summary>
        /// Ԫ������
        /// </summary>
        private int m_ItemCount;

        /// <summary>
        /// ��ǰ��һ��Ԫ���±�
        /// </summary>
        private int m_CurItemIndex;

        /// <summary>
        /// ��ǰԪ��λ��
        /// </summary>
        private float m_CurItemPos;

        /// <summary>
        /// ˢ��Ԫ��.
        /// </summary>
        private Action<RecyclingListViewItem> m_RefreshItemAction;

        /// <summary>
        /// ����Ԫ��
        /// </summary>
        private Action<RecyclingListViewItem> m_RecycleItemAction;

        /// <summary>
        /// ��ȡ������Ԥ�����±�
        /// </summary>
        private Func<int, int> m_GetChildItemPrefabIndex;

        /// <summary>
        /// ��ȡ�߾��±�
        /// </summary>
        private Func<int, int> m_GetChildItemPaddingIndex;

        private ScrollRect m_ScrollRect;

        /// <summary>
        /// ��ǰԪ���ڿ���content�仯
        /// </summary>
        private bool m_ThisControlContent;

        private LinkedList<RecyclingListViewItem> m_UsingItem = new LinkedList<RecyclingListViewItem>();
        private List<Queue<RecyclingListViewItem>> m_PoolingItem = new List<Queue<RecyclingListViewItem>>();

        /// <summary>
        /// Ԫ�ظ߶Ȼ���
        /// </summary>
        private List<float> m_ItemHeightCache = new List<float>();

        /// <summary>
        /// content�߶�
        /// </summary>
        private float m_ContentHeight;

        /// <summary>
        /// viewport��rect
        /// </summary>
        private Rect m_ViewportRect;

        /// <summary>
        /// �Զ���������Э��
        /// </summary>
        private Coroutine m_AutoScrollAnimation;

        /// <summary>
        /// ��ʼ������
        /// </summary>
        /// <param name="itemCount"></param>
        /// <param name="refreshItemAction"></param>
        /// <param name="recycleItemAction"></param>
        /// <param name="getchildItemPrefabIndex"></param>
        /// <param name="getChildItemPaddingIndex"></param>
        /// <param name="initItemPos">��ʼ����ʾ�±�</param>
        public void StartScrollView(
            int itemCount,
            Action<RecyclingListViewItem> refreshItemAction,
            Action<RecyclingListViewItem> recycleItemAction,
            Func<int, int> getchildItemPrefabIndex = null,
            Func<int, int> getChildItemPaddingIndex = null,
            float initItemPos = 0)
        {
            if (itemCount == 0)
            {
                Debug.LogError("Ԫ����������Ϊ0");
                return;
            }

            m_ScrollRect = GetComponent<ScrollRect>();
            //���û�������
            m_ScrollRect.vertical = true;
            m_ScrollRect.horizontal = false;
            if (null != m_ScrollRect.horizontalScrollbar)
            {
                m_ScrollRect.horizontalScrollbar.gameObject.SetActive(false);
            }
            m_ScrollRect.horizontalScrollbar = null;

            if (null == m_ChildPrefab || m_ChildPrefab.Length == 0)
            {
                Debug.LogError("û��ָ��Ԥ����");
                return;
            }
            if (null == m_AllSpacing || m_AllSpacing.Length == 0)
            {
                Debug.LogError("û��ָ���߾�");
                return;
            }

            //��һ�ο���������
            if (m_PoolingItem.Count == 0)
            {
                for (int i = 0; i < m_ChildPrefab.Length; i++)
                {
                    m_ChildPrefab[i].PrefabIndex = i;
                    m_PoolingItem.Add(new Queue<RecyclingListViewItem>());

                    if (m_ScrollDirection == EScrollDirection.Down2Up)
                    {
                        m_ChildPrefab[i].RectTransform.anchorMin = new Vector2(0.5f, 0f);
                        m_ChildPrefab[i].RectTransform.anchorMax = new Vector2(0.5f, 0f);
                        m_ChildPrefab[i].RectTransform.pivot = new Vector2(0.5f, 0f);
                    }
                    else
                    {
                        m_ChildPrefab[i].RectTransform.anchorMin = new Vector2(0.5f, 1f);
                        m_ChildPrefab[i].RectTransform.anchorMax = new Vector2(0.5f, 1f);
                        m_ChildPrefab[i].RectTransform.pivot = new Vector2(0.5f, 1f);
                    }
                }
            }
            else
            {
                //��������item
                Clear();
            }

            //��������Ԥ����
            for (int i = 0; i < m_ChildPrefab.Length; i++)
            {
                m_ChildPrefab[i].gameObject.SetActive(false);
            }

            m_RefreshItemAction = refreshItemAction;
            m_RecycleItemAction = recycleItemAction;
            m_GetChildItemPrefabIndex = getchildItemPrefabIndex;
            m_GetChildItemPaddingIndex = getChildItemPaddingIndex;
            if (null == m_GetChildItemPrefabIndex)
            {
                m_GetChildItemPrefabIndex = DefaultGetIndex;
            }
            if (null == m_GetChildItemPaddingIndex)
            {
                m_GetChildItemPaddingIndex = DefaultGetIndex;
            }

            m_ItemCount = Mathf.Max(0, itemCount);
            m_CurItemIndex = Mathf.Clamp((int)initItemPos, 0, itemCount - 1);
            m_CurItemPos = Mathf.Clamp(initItemPos, 0, itemCount);

            m_ScrollRect.onValueChanged.RemoveAllListeners();
            m_ScrollRect.onValueChanged.AddListener(OnScrollRectValueChange);


            //����content
            m_ThisControlContent = true;
            UpdateHeightCache();
            float initPercent = m_CurItemPos / m_ItemCount;
            UpdateViewportRect();
            UpdateContent(initPercent);
            UpdateChildItem(initPercent);
            UpdateItemPosition(initPercent);
            m_ThisControlContent = false;
        }

        /// <summary>
        /// Ĭ�ϻ�ȡ�±꺯��
        /// </summary>
        /// <param name="itemIndex"></param>
        /// <returns></returns>
        private int DefaultGetIndex(int itemIndex) => 0;

        /// <summary>
        /// ������ʱ
        /// </summary>
        /// <param name="position"></param>
        private void OnScrollRectValueChange(Vector2 position)
        {
            //���µ�ǰλ��
            m_CurItemPos = position.y * m_ItemCount;

            //��ǰԪ�ؿ���scrollviewʱ,��������Ч
            if (m_ThisControlContent)
            {
                return;
            }

            m_ThisControlContent = true;
            UpdateChildItem(position.y);
            UpdateItemPosition(position.y);
            m_ThisControlContent = false;
        }

        /// <summary>
        /// ����viewport��Rect
        /// </summary>
        private void UpdateViewportRect()
        {
            //���scrollrect��scrollbar������,��֧��Visibilityѡ��ΪAutoHideAndExpandViewport
            //���ѡ����޸�viewport��recttransform,���µڶ�֡���ܻ�ȡ��viewport����ȷ�ߴ�
            //���������취�ֶ�����viewportRect,�Խ����scrollbar��Visibilityѡ�������
            if (null != m_ScrollRect.verticalScrollbar && m_ScrollRect.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport)
            {
                Debug.LogError("Vertical Scrollbar��Visibilityѡ�֧��AutoHideAndExpandViewport");
            }

            m_ViewportRect = m_ScrollRect.viewport.rect;
        }

        /// <summary>
        /// ����content��С��λ��
        /// </summary>
        /// <param name="curPos">��ǰλ��,0-1</param>
        private void UpdateContent(float curPos)
        {
            //����content�ߴ�
            var content = m_ScrollRect.content;
            if (m_ScrollDirection == EScrollDirection.Down2Up)
            {
                content.anchorMin = new Vector2(0.5f, 0f);
                content.anchorMax = new Vector2(0.5f, 0f);
                content.pivot = new Vector2(0.5f, 0f);
                content.sizeDelta = new Vector2(m_ViewportRect.width, m_ContentHeight);
                content.anchoredPosition = new Vector2(0, CalcContentPos(curPos));
            }
            else
            {
                content.anchorMin = new Vector2(0.5f, 1f);
                content.anchorMax = new Vector2(0.5f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = new Vector2(m_ViewportRect.width, m_ContentHeight);
                content.anchoredPosition = new Vector2(0, CalcContentPos(curPos));
            }
        }

        /// <summary>
        /// ��������itemλ��
        /// </summary>
        /// <param name="curPos">��ǰλ��,0-1</param>
        private void UpdateItemPosition(float curPos)
        {
            if (m_UsingItem.Count > 0)
            {
                var itemNode = m_UsingItem.First;
                while (null != itemNode)
                {
                    var item = itemNode.Value;
                    var anchoredPos = item.RectTransform.anchoredPosition;
                    anchoredPos.y = m_ItemHeightCache[item.CurIndex];
                    if (m_ScrollDirection == EScrollDirection.Up2Down)
                    {
                        anchoredPos.y = -anchoredPos.y;
                    }
                    item.RectTransform.anchoredPosition = anchoredPos;

                    itemNode = itemNode.Next;
                }
            }
        }

        /// <summary>
        /// ������ʾ��item����
        /// </summary>
        /// <param name="curPos">��ǰλ��,0-1</param>
        private void UpdateChildItem(float curPos)
        {
            //��ǰcontent��λ��
            var content = m_ScrollRect.content;
            var curHeight = content.anchoredPosition.y;

            //��ӵ�һ��Ԫ��
            if (m_UsingItem.Count == 0)
            {
                var item = NewSingleItem(m_CurItemIndex);
                m_UsingItem.AddFirst(item);
            }

            if (m_ScrollDirection == EScrollDirection.Down2Up)
            {
                curHeight = -curHeight;
            }

            //��ǰ����
            var heightDownLimit = curHeight - m_PreAllocLength;
            bool operation = false;
            while (m_UsingItem.Count > 0)
            {
                var first = m_UsingItem.First.Value;
                var index = first.CurIndex;
                var height = m_ItemHeightCache[index];
                //����
                if (height > heightDownLimit && index > 0)
                {
                    var item = NewSingleItem(index - 1);
                    m_UsingItem.AddFirst(item);
                    operation = true;
                    continue;
                }

                var itemIndex = m_GetChildItemPrefabIndex(index);
                height += m_ChildPrefab[itemIndex].RectTransform.rect.height;
                //����
                if (height < heightDownLimit && !operation)
                {
                    RecycleSingleItem(first);
                    m_UsingItem.RemoveFirst();
                    continue;
                }

                break;
            }

            curHeight += m_ViewportRect.height;

            //�������
            var heightUpLimit = curHeight + m_PreAllocLength;
            operation = false;
            while (m_UsingItem.Count > 0)
            {
                var last = m_UsingItem.Last.Value;
                var index = last.CurIndex;

                var height = m_ItemHeightCache[index];
                //����
                if (height > heightUpLimit)
                {
                    RecycleSingleItem(last);
                    m_UsingItem.RemoveLast();
                    operation = true;
                    continue;
                }

                var itemIndex = m_GetChildItemPrefabIndex(index);
                height += m_ChildPrefab[itemIndex].RectTransform.rect.height;
                //����
                if (height < heightUpLimit && index < m_ItemCount - 1 && !operation)
                {
                    var item = NewSingleItem(index + 1);
                    m_UsingItem.AddLast(item);
                    continue;
                }

                break;
            }
        }

        private void UpdateHeightCache()
        {
            //���¸߶Ȼ���
            m_ItemHeightCache.Clear();
            m_ContentHeight = 0;
            for (int i = 0; i < m_ItemCount; i++)
            {
                m_ItemHeightCache.Add(m_ContentHeight);

                var itemIndex = m_GetChildItemPrefabIndex(i);
                m_ContentHeight += m_ChildPrefab[itemIndex].RectTransform.rect.height;
                if (i != m_ItemCount - 1)
                {
                    var paddingIndex = m_GetChildItemPaddingIndex(i);
                    m_ContentHeight += m_AllSpacing[paddingIndex];
                }
            }
        }

        /// <summary>
        /// ����contentλ��
        /// </summary>
        /// <param name="percentPos">�ٷֱȳߴ�</param>
        /// <returns></returns>
        private float CalcContentPos(float percentPos)
        {
            var height = m_ContentHeight * percentPos;
            if (m_ContentHeight > m_ViewportRect.height && height > m_ContentHeight - m_ViewportRect.height)
            {
                height = m_ContentHeight - m_ViewportRect.height;
            }
            if (m_ScrollDirection == EScrollDirection.Down2Up)
            {
                return -height;
            }
            return height;
        }

        /// <summary>
        /// �������
        /// </summary>
        public void Clear()
        {
            while (m_UsingItem.Count > 0)
            {
                RecycleSingleItem(m_UsingItem.First.Value);
                m_UsingItem.RemoveFirst();
            }
            m_ItemCount = 0;
            m_RefreshItemAction = null;
            m_RecycleItemAction = null;
            m_GetChildItemPaddingIndex = null;
            m_GetChildItemPrefabIndex = null;
        }

        /// <summary>
        /// �´�������Ԫ��
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private RecyclingListViewItem NewSingleItem(int index)
        {
            var prefabIndex = m_GetChildItemPrefabIndex(index);
            RecyclingListViewItem item = null;
            if (m_PoolingItem[prefabIndex].Count > 0)
            {
                item = m_PoolingItem[prefabIndex].Dequeue();
            }
            else
            {
                item = GameObject.Instantiate(m_ChildPrefab[prefabIndex].gameObject, m_ScrollRect.content).GetComponent<RecyclingListViewItem>();
            }
            item.PrefabIndex = prefabIndex;
            item.CurIndex = index;
            item.ParentRecyle = this;
            //item.RectTransform.
            m_RefreshItemAction?.Invoke(item);
            item.gameObject.SetActive(true);
            return item;
        }

        /// <summary>
        /// ���յ���Ԫ��
        /// </summary>
        private void RecycleSingleItem(RecyclingListViewItem item)
        {
            item.gameObject.SetActive(false);
            m_RecycleItemAction?.Invoke(item);
            m_PoolingItem[item.PrefabIndex].Enqueue(item);
        }

        /// <summary>
        /// ˲����Ծ��ָ��λ��
        /// </summary>
        /// <param name="position">�� 0-Ԫ�ظ��� ȡֵ</param>
        public void JumpTo(float position)
        {
            position = position % m_ItemCount;
            if (position < 0)
            {
                position += m_ItemCount;
            }

            //�����жϵ�ǰ�Զ������Ķ���
            StopAnimation();

            StartScrollView(m_ItemCount, m_RefreshItemAction, m_RecycleItemAction, m_GetChildItemPrefabIndex, m_GetChildItemPaddingIndex, position);
        }

        /// <summary>
        /// ָ���ٶȹ�����ָ��λ��
        /// </summary>
        /// <param name="targetPos">�������±�λ��</param>
        /// <param name="speed"></param>
        /// <param name="blockRaycasts">�Ƿ����ε��</param>
        /// <param name="onScrollEnd">��������ϻص�</param>
        public void ScrollToBySpeed(float targetPos, float speed, bool blockRaycasts = true, Action onScrollEnd = null)
        {
            if (speed <= 0)
            {
                onScrollEnd?.Invoke();
                return;
            }

            StopAnimation();

            //��׼��λ��
            var curContentPos = Mathf.Abs(CalcContentPos(m_CurItemPos / m_ItemCount));
            var targetContentPos = Mathf.Abs(CalcContentPos(targetPos / m_ItemCount));

            //�������
            if (Mathf.Abs(curContentPos - targetContentPos) > 0.1f)
            {
                var distance = targetContentPos - curContentPos;
                var direction = m_ScrollDirection;
                //��������ת
                if (distance < 0)
                {
                    if (direction == EScrollDirection.Down2Up)
                    {
                        direction = EScrollDirection.Up2Down;
                    }
                    else
                    {
                        direction = EScrollDirection.Down2Up;
                    }
                }
                distance = Mathf.Abs(distance);

                var time = distance / speed;
                StartAnimation(speed, time, direction, onScrollEnd);
                if (blockRaycasts)
                {
                    var canvasGroup = this.GetComponent<CanvasGroup>();
                    if (null == canvasGroup)
                    {
                        canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
                    }
                    canvasGroup.blocksRaycasts = false;
                }
            }
            else
            {
                onScrollEnd?.Invoke();
            }
        }

        /// <summary>
        /// ָ��ʱ�������ָ��λ��
        /// </summary>
        /// <param name="targetPos"></param>
        /// <param name="time"></param>
        /// <param name="blockRaycasts">�Ƿ����ε��</param>
        /// <param name="onScrollEnd">��������ϻص�</param>
        public void ScrollToByTime(float targetPos, float time, bool blockRaycasts = true, Action onScrollEnd = null)
        {
            if (time <= 0)
            {
                onScrollEnd?.Invoke();
                return;
            }

            StopAnimation();

            //��׼��λ��
            var curContentPos = Mathf.Abs(CalcContentPos(m_CurItemPos / m_ItemCount));
            var targetContentPos = Mathf.Abs(CalcContentPos(targetPos / m_ItemCount));

            //�������
            if (Mathf.Abs(curContentPos - targetContentPos) > 0.1f)
            {
                var distance = targetContentPos - curContentPos;
                var direction = m_ScrollDirection;
                //��������ת
                if (distance < 0)
                {
                    if (direction == EScrollDirection.Down2Up)
                    {
                        direction = EScrollDirection.Up2Down;
                    }
                    else
                    {
                        direction = EScrollDirection.Down2Up;
                    }
                }
                distance = Mathf.Abs(distance);

                var speed = distance / time;
                StartAnimation(speed, time, direction, onScrollEnd);
                if (blockRaycasts)
                {
                    var canvasGroup = this.GetComponent<CanvasGroup>();
                    if (null == canvasGroup)
                    {
                        canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
                    }
                    canvasGroup.blocksRaycasts = false;
                }
            }
            else
            {
                onScrollEnd?.Invoke();
            }
        }

        /// <summary>
        /// �����Զ���������
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="time"></param>
        /// <param name="scrollDirection">��������</param>
        /// <param name="onScrollEnd"></param>
        private void StartAnimation(float speed, float time, EScrollDirection scrollDirection, Action onScrollEnd)
        {
            m_AutoScrollAnimation = StartCoroutine(AutoMoveCoroutine(speed, time, scrollDirection, onScrollEnd));
        }

        /// <summary>
        /// �Զ�����Э��
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="time"></param>
        /// <param name="scrollDirection"></param>
        /// <param name="onScrollEnd"></param>
        /// <returns></returns>
        private IEnumerator AutoMoveCoroutine(float speed, float time, EScrollDirection scrollDirection, Action onScrollEnd)
        {
            float timer = 0;
            while(timer < time)
            {
                yield return new WaitForEndOfFrame();
                var deltaTime = Time.deltaTime;
                var moveDeltaTime = deltaTime;
                if (time - timer < deltaTime)
                {
                    moveDeltaTime = time - timer;
                }
                timer += deltaTime;
                if (scrollDirection == EScrollDirection.Down2Up)
                {
                    var pos = m_ScrollRect.content.anchoredPosition;
                    pos.y -= speed * moveDeltaTime;
                    m_ScrollRect.content.anchoredPosition = pos;
                }
                else
                {
                    var pos = m_ScrollRect.content.anchoredPosition;
                    pos.y += speed * moveDeltaTime;
                    m_ScrollRect.content.anchoredPosition = pos;
                }
            }

            m_AutoScrollAnimation = null;
            var canvasGroup = this.GetComponent<CanvasGroup>();
            if (null != canvasGroup)
            {
                canvasGroup.blocksRaycasts = true;
            }

            onScrollEnd?.Invoke();
        }

        /// <summary>
        /// �ر��Զ���������
        /// </summary>
        private void StopAnimation()
        {
            if (null != m_AutoScrollAnimation)
            {
                StopCoroutine(m_AutoScrollAnimation);
                m_AutoScrollAnimation = null;
            }
            var canvasGroup = this.GetComponent<CanvasGroup>();
            if (null != canvasGroup)
            {
                canvasGroup.blocksRaycasts = true;
            }
        }

        private void OnEnable()
        {
            StopAllCoroutines();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private enum EScrollDirection : byte
        {
            Down2Up,

            Up2Down,
        }
    }
}

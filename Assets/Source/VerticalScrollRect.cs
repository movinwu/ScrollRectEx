using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ScrollViewEx
{
    /// <summary>
    /// 竖直滚动条
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class VerticalScrollRect : MonoBehaviour
    {
        [Header("所有item预制体")]
        [SerializeField] private ScrollRectViewItem[] m_ChildPrefab;
        [Header("所有item间隔")]
        [SerializeField] private float[] m_AllSpacing = new float[] { 0f, };
        [Header("回收距离")]
        [SerializeField] private float m_PreAllocLength = 200;

        [Header("滚动方向")]
        [SerializeField] private EScrollDirection m_ScrollDirection;

        /// <summary>
        /// 元素数量
        /// </summary>
        private int m_ItemCount;

        /// <summary>
        /// 当前第一个元素下标
        /// </summary>
        private int m_CurItemIndex;

        /// <summary>
        /// 当前元素位置
        /// </summary>
        private float m_CurItemPos;

        /// <summary>
        /// 刷新元素.
        /// </summary>
        private Action<ScrollRectViewItem> m_RefreshItemAction;

        /// <summary>
        /// 回收元素
        /// </summary>
        private Action<ScrollRectViewItem> m_RecycleItemAction;

        /// <summary>
        /// 获取子物体预制体下标
        /// </summary>
        private Func<int, int> m_GetChildItemPrefabIndex;

        /// <summary>
        /// 获取边距下标
        /// </summary>
        private Func<int, int> m_GetChildItemPaddingIndex;

        /// <summary>
        /// 获取item高度
        /// </summary>
        private Func<int, float> m_GetItemHeight;

        private ScrollRect m_ScrollRect;

        /// <summary>
        /// 当前元素在控制content变化
        /// </summary>
        private bool m_ThisControlContent;

        private LinkedList<ScrollRectViewItem> m_UsingItem = new LinkedList<ScrollRectViewItem>();
        private List<Queue<ScrollRectViewItem>> m_PoolingItem = new List<Queue<ScrollRectViewItem>>();

        /// <summary>
        /// 元素高度缓存
        /// </summary>
        private List<float> m_ItemHeightCache = new List<float>();

        /// <summary>
        /// content高度
        /// </summary>
        private float m_ContentHeight;

        /// <summary>
        /// viewport的rect
        /// </summary>
        private Rect m_ViewportRect;

        /// <summary>
        /// 自动滚动动画协程
        /// </summary>
        private Coroutine m_AutoScrollAnimation;

        /// <summary>
        /// 开始滚动条
        /// </summary>
        /// <param name="itemCount"></param>
        /// <param name="refreshItemAction"></param>
        /// <param name="recycleItemAction"></param>
        /// <param name="getchildItemPrefabIndex"></param>
        /// <param name="getChildItemPaddingIndex"></param>
        /// <param name="getItemHeight">获取item高度</param>
        /// <param name="initItemPos">初始化显示下标</param>
        public void StartScrollView(
            int itemCount,
            Action<ScrollRectViewItem> refreshItemAction,
            Action<ScrollRectViewItem> recycleItemAction,
            Func<int, int> getchildItemPrefabIndex = null,
            Func<int, int> getChildItemPaddingIndex = null,
            Func<int, float> getItemHeight = null,
            float initItemPos = 0)
        {
            if (itemCount == 0)
            {
                Debug.LogError("元素数量不能为0");
                return;
            }

            m_ScrollRect = GetComponent<ScrollRect>();
            //设置滑动方向
            m_ScrollRect.vertical = true;
            m_ScrollRect.horizontal = false;
            if (null != m_ScrollRect.horizontalScrollbar)
            {
                m_ScrollRect.horizontalScrollbar.gameObject.SetActive(false);
            }
            m_ScrollRect.horizontalScrollbar = null;

            if (null == m_ChildPrefab || m_ChildPrefab.Length == 0)
            {
                Debug.LogError("没有指定预制体");
                return;
            }
            if (null == m_AllSpacing || m_AllSpacing.Length == 0)
            {
                Debug.LogError("没有指定边距");
                return;
            }

            //第一次开启滚动条
            if (m_PoolingItem.Count == 0)
            {
                for (int i = 0; i < m_ChildPrefab.Length; i++)
                {
                    m_ChildPrefab[i].PrefabIndex = i;
                    m_PoolingItem.Add(new Queue<ScrollRectViewItem>());

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
                //回收所有item
                Clear();
            }

            //隐藏所有预制体
            for (int i = 0; i < m_ChildPrefab.Length; i++)
            {
                m_ChildPrefab[i].gameObject.SetActive(false);
            }

            m_RefreshItemAction = refreshItemAction;
            m_RecycleItemAction = recycleItemAction;
            m_GetChildItemPrefabIndex = getchildItemPrefabIndex;
            m_GetChildItemPaddingIndex = getChildItemPaddingIndex;
            m_GetItemHeight = getItemHeight;
            if (null == m_GetChildItemPrefabIndex)
            {
                m_GetChildItemPrefabIndex = DefaultGetIndex;
            }
            if (null == m_GetChildItemPaddingIndex)
            {
                m_GetChildItemPaddingIndex = DefaultGetIndex;
            }
            if (null == m_GetItemHeight)
            {
                m_GetItemHeight = DefaultGetItemHeight;
            }

            m_ItemCount = Mathf.Max(0, itemCount);
            m_CurItemIndex = Mathf.Clamp((int)initItemPos, 0, itemCount - 1);
            m_CurItemPos = Mathf.Clamp(initItemPos, 0, itemCount);

            m_ScrollRect.onValueChanged.RemoveAllListeners();
            m_ScrollRect.onValueChanged.AddListener(OnScrollRectValueChange);


            //更新content
            m_ThisControlContent = true;
            UpdateHeightCache();
            float initPercent = Mathf.Abs(CalcItemRectPos(m_CurItemPos)) / m_ContentHeight;
            UpdateViewportRect();
            UpdateContent(initPercent);
            UpdateChildItem(initPercent);
            UpdateItemPosition(initPercent);
            m_ThisControlContent = false;
        }

        /// <summary>
        /// 默认获取下标函数
        /// </summary>
        /// <param name="itemIndex"></param>
        /// <returns></returns>
        private int DefaultGetIndex(int itemIndex) => 0;

        /// <summary>
        /// 默认获取item高度
        /// </summary>
        /// <param name="itemIndex"></param>
        /// <returns></returns>
        private float DefaultGetItemHeight(int itemIndex)
        {
            var prefabIndex = m_GetChildItemPrefabIndex(itemIndex);
            var prefab = m_ChildPrefab[prefabIndex];
            return prefab.RectTransform.rect.height;
        }

        /// <summary>
        /// 当滑动时
        /// </summary>
        /// <param name="position"></param>
        private void OnScrollRectValueChange(Vector2 position)
        {
            //更新当前位置
            m_CurItemPos = position.y * m_ItemCount;

            //当前元素控制scrollview时,监听不生效
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
        /// 更新viewport的Rect
        /// </summary>
        private void UpdateViewportRect()
        {
            //检查scrollrect中scrollbar的设置,不支持Visibility选项为AutoHideAndExpandViewport
            //这个选项会修改viewport的recttransform,导致第二帧才能获取到viewport的正确尺寸
            //这里可以想办法手动计算viewportRect,以解除对scrollbar的Visibility选项的限制
            if (null != m_ScrollRect.verticalScrollbar && m_ScrollRect.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport)
            {
                Debug.LogError("Vertical Scrollbar的Visibility选项不支持AutoHideAndExpandViewport");
            }

            m_ViewportRect = m_ScrollRect.viewport.rect;
        }

        /// <summary>
        /// 更新content大小和位置
        /// </summary>
        /// <param name="curPos">当前位置,0-1</param>
        private void UpdateContent(float curPos)
        {
            //更新content尺寸
            var content = m_ScrollRect.content;
            if (m_ScrollDirection == EScrollDirection.Down2Up)
            {
                content.anchorMin = new Vector2(0.5f, 0f);
                content.anchorMax = new Vector2(0.5f, 0f);
                content.pivot = new Vector2(0.5f, 0f);
                content.sizeDelta = new Vector2(m_ViewportRect.width, m_ContentHeight);
                content.anchoredPosition = new Vector2(0, CalcContentRectPos(curPos));
            }
            else
            {
                content.anchorMin = new Vector2(0.5f, 1f);
                content.anchorMax = new Vector2(0.5f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = new Vector2(m_ViewportRect.width, m_ContentHeight);
                content.anchoredPosition = new Vector2(0, CalcContentRectPos(curPos));
            }
        }

        /// <summary>
        /// 更新所有item位置
        /// </summary>
        /// <param name="curPos">当前位置,0-1</param>
        private void UpdateItemPosition(float curPos)
        {
            if (m_UsingItem.Count > 0)
            {
                var itemNode = m_UsingItem.First;
                while (null != itemNode)
                {
                    var item = itemNode.Value;
                    var anchoredPos = item.RectTransform.anchoredPosition;
                    anchoredPos.y = CalcItemRectPos(item.CurIndex);
                    item.RectTransform.anchoredPosition = anchoredPos;

                    itemNode = itemNode.Next;
                }
            }
        }

        /// <summary>
        /// 更新显示的item集合
        /// </summary>
        /// <param name="curPos">当前位置,0-1</param>
        private void UpdateChildItem(float curPos)
        {
            //当前content的位置
            var content = m_ScrollRect.content;
            var curHeight = content.anchoredPosition.y;

            //添加第一个元素
            if (m_UsingItem.Count == 0)
            {
                var item = NewSingleItem(m_CurItemIndex);
                m_UsingItem.AddFirst(item);
            }

            if (m_ScrollDirection == EScrollDirection.Down2Up)
            {
                curHeight = -curHeight;
            }

            //向前新增
            var heightDownLimit = curHeight - m_PreAllocLength;
            bool operation = false;
            while (m_UsingItem.Count > 0)
            {
                var first = m_UsingItem.First.Value;
                var index = first.CurIndex;
                var height = m_ItemHeightCache[index];
                //新增
                if (height > heightDownLimit && index > 0)
                {
                    var item = NewSingleItem(index - 1);
                    m_UsingItem.AddFirst(item);
                    operation = true;
                    continue;
                }

                var itemIndex = m_GetChildItemPrefabIndex(index);
                height += m_ChildPrefab[itemIndex].RectTransform.rect.height;
                //销毁
                if (height < heightDownLimit && !operation)
                {
                    RecycleSingleItem(first);
                    m_UsingItem.RemoveFirst();
                    continue;
                }

                break;
            }

            curHeight += m_ViewportRect.height;

            //向后新增
            var heightUpLimit = curHeight + m_PreAllocLength;
            operation = false;
            while (m_UsingItem.Count > 0)
            {
                var last = m_UsingItem.Last.Value;
                var index = last.CurIndex;

                var height = m_ItemHeightCache[index];
                //销毁
                if (height > heightUpLimit)
                {
                    RecycleSingleItem(last);
                    m_UsingItem.RemoveLast();
                    operation = true;
                    continue;
                }

                var itemIndex = m_GetChildItemPrefabIndex(index);
                height += m_ChildPrefab[itemIndex].RectTransform.rect.height;
                //新增
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
            //更新高度缓存
            m_ItemHeightCache.Clear();
            m_ContentHeight = 0;
            for (int i = 0; i < m_ItemCount; i++)
            {
                m_ItemHeightCache.Add(m_ContentHeight);

                m_ContentHeight += m_GetItemHeight(i);
                if (i != m_ItemCount - 1)
                {
                    var paddingIndex = m_GetChildItemPaddingIndex(i);
                    m_ContentHeight += m_AllSpacing[paddingIndex];
                }
            }
        }

        /// <summary>
        /// 计算content位置
        /// </summary>
        /// <param name="percentPos">百分比尺寸</param>
        /// <returns></returns>
        private float CalcContentRectPos(float percentPos)
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
        /// 计算item为止
        /// </summary>
        /// <param name="itemPos"></param>
        /// <param name="ignoreViewport"></param>
        /// <returns></returns>
        private float CalcItemRectPos(float itemPos, bool ignoreViewport = true)
        {
            itemPos = Mathf.Clamp(itemPos, 0, m_ItemCount);
            int itemIndex = Mathf.Clamp(Mathf.FloorToInt(itemPos), 0, m_ItemCount - 1);
            var height = m_ItemHeightCache[itemIndex];
            var itemHeight = m_GetItemHeight(itemIndex);
            height += itemHeight * (itemPos - itemIndex);
            if (!ignoreViewport)
            {
                if (m_ContentHeight > m_ViewportRect.height && height > m_ContentHeight - m_ViewportRect.height)
                {
                    height = m_ContentHeight - m_ViewportRect.height;
                }
            }
            if (m_ScrollDirection == EScrollDirection.Up2Down)
            {
                return -height;
            }
            return height;
        }

        /// <summary>
        /// 快速清空
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
        /// 新创建单个元素
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private ScrollRectViewItem NewSingleItem(int index)
        {
            var prefabIndex = m_GetChildItemPrefabIndex(index);
            ScrollRectViewItem item = null;
            if (m_PoolingItem[prefabIndex].Count > 0)
            {
                item = m_PoolingItem[prefabIndex].Dequeue();
            }
            else
            {
                item = GameObject.Instantiate(m_ChildPrefab[prefabIndex].gameObject, m_ScrollRect.content).GetComponent<ScrollRectViewItem>();
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
        /// 回收单个元素
        /// </summary>
        private void RecycleSingleItem(ScrollRectViewItem item)
        {
            item.gameObject.SetActive(false);
            m_RecycleItemAction?.Invoke(item);
            m_PoolingItem[item.PrefabIndex].Enqueue(item);
        }

        /// <summary>
        /// 瞬间跳跃到指定位置
        /// </summary>
        /// <param name="position">从 0-元素个数 取值</param>
        public void JumpTo(float position)
        {
            position = position % m_ItemCount;
            if (position < 0)
            {
                position += m_ItemCount;
            }

            //立即中断当前自动滚动的动画
            StopAnimation();

            StartScrollView(m_ItemCount, m_RefreshItemAction, m_RecycleItemAction, m_GetChildItemPrefabIndex, m_GetChildItemPaddingIndex, m_GetItemHeight, position);
        }

        /// <summary>
        /// 指定速度滚动到指定位置
        /// </summary>
        /// <param name="targetPos">滚动的下标位置</param>
        /// <param name="speed"></param>
        /// <param name="blockRaycasts">是否屏蔽点击</param>
        /// <param name="onScrollEnd">当滚动完毕回调</param>
        public void ScrollToBySpeed(float targetPos, float speed, bool blockRaycasts = true, Action onScrollEnd = null)
        {
            if (speed <= 0)
            {
                onScrollEnd?.Invoke();
                return;
            }

            StopAnimation();

            //标准化位置
            var curContentPos = Mathf.Abs(CalcItemRectPos(m_CurItemPos, false));
            var targetContentPos = Mathf.Abs(CalcItemRectPos(targetPos, false));

            //计算滚动
            if (Mathf.Abs(curContentPos - targetContentPos) > 0.1f)
            {
                var distance = targetContentPos - curContentPos;
                var direction = m_ScrollDirection;
                //滚动方向翻转
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
        /// 指定时间滚动到指定位置
        /// </summary>
        /// <param name="targetPos"></param>
        /// <param name="time"></param>
        /// <param name="blockRaycasts">是否屏蔽点击</param>
        /// <param name="onScrollEnd">当滚动完毕回调</param>
        public void ScrollToByTime(float targetPos, float time, bool blockRaycasts = true, Action onScrollEnd = null)
        {
            if (time <= 0)
            {
                onScrollEnd?.Invoke();
                return;
            }

            StopAnimation();

            //标准化位置
            var curContentPos = Mathf.Abs(CalcItemRectPos(m_CurItemPos, false));
            var targetContentPos = Mathf.Abs(CalcItemRectPos(targetPos, false));

            //计算滚动
            if (Mathf.Abs(curContentPos - targetContentPos) > 0.1f)
            {
                var distance = targetContentPos - curContentPos;
                var direction = m_ScrollDirection;
                //滚动方向翻转
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
        /// 开启自动滚动动画
        /// </summary>
        /// <param name="speed"></param>
        /// <param name="time"></param>
        /// <param name="scrollDirection">滚动方向</param>
        /// <param name="onScrollEnd"></param>
        private void StartAnimation(float speed, float time, EScrollDirection scrollDirection, Action onScrollEnd)
        {
            m_AutoScrollAnimation = StartCoroutine(AutoMoveCoroutine(speed, time, scrollDirection, onScrollEnd));
        }

        /// <summary>
        /// 自动滚动协程
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
        /// 关闭自动滚动动画
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

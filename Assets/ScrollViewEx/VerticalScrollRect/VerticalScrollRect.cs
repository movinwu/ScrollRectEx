using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ScrollViewEx
{
    /// <summary>
    /// 竖直滚动条
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class VerticalScrollRect : MonoBehaviour, IEndDragHandler
    {
        [Header("所有item预制体")]
        [SerializeField] private VerticalScrollRectViewItem[] m_ChildPrefab;
        [Header("所有item之间间隔")]
        [SerializeField] private float[] m_AllSpacing = new float[] { 0f, };
        [Header("上下边距")]
        [SerializeField] private float m_UpPadding;
        [SerializeField] private float m_DownPadding;
        [Header("回收距离")]
        [SerializeField] private float m_PreAllocLength = 200;

        [Header("滚动方向")]
        [SerializeField] private EScrollDirection m_ScrollDirection;

        [Header("是否开启自动定位")]
        [SerializeField] private bool m_EnableAutoSnap;

        [Header("自动定位item对齐位置,开启自动定位后生效")]
        [SerializeField, Range(0, 1)] private float m_ItemSnapPivot;

        [Header("定位viewport对齐位置")]
        [SerializeField, Range(0, 1)] private float m_ViewportSnapPivot;

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
        private Action<VerticalScrollRectViewItem> m_RefreshItemAction;

        /// <summary>
        /// 回收元素
        /// </summary>
        private Action<VerticalScrollRectViewItem> m_RecycleItemAction;

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

        /// <summary>
        /// 当滚动条滚动时在每个元素上执行的回调
        /// </summary>
        private Action<VerticalScrollRectViewItem, float> m_OnScrollRectValueChangeItemAction;

        /// <summary>
        /// 当滚动条滚动时执行的回调
        /// </summary>
        private Action<float> m_OnScrollRectValueChangeAction;

        private ScrollRect m_ScrollRect;

        /// <summary>
        /// 更新时需要刷新item
        /// </summary>
        private bool m_NeedRefreshItemOnUpdate;

        private LinkedList<VerticalScrollRectViewItem> m_UsingItem = new LinkedList<VerticalScrollRectViewItem>();
        private List<Queue<VerticalScrollRectViewItem>> m_PoolingItem = new List<Queue<VerticalScrollRectViewItem>>();

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
        /// snap状态
        /// </summary>
        private ESnapStatus m_SnapStatus;

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
        /// <param name="onScrollRectValueChangeItemAction">当滚动条滚动时每个item上执行函数</param>
        /// <param name="onScrollRectValueChangeAction">当滚动条滚动时执行函数</param>
        public void StartScrollView(
            int itemCount,
            Action<VerticalScrollRectViewItem> refreshItemAction,
            Action<VerticalScrollRectViewItem> recycleItemAction,
            Func<int, int> getchildItemPrefabIndex = null,
            Func<int, int> getChildItemPaddingIndex = null,
            Func<int, float> getItemHeight = null,
            Action<VerticalScrollRectViewItem, float> onScrollRectValueChangeItemAction = null,
            Action<float> onScrollRectValueChangeAction = null,
            float initItemPos = 0)
        {
            m_ScrollRect = GetComponent<ScrollRect>();
            m_ScrollRect.onValueChanged.RemoveAllListeners();

            if (itemCount == 0)
            {
                Debug.LogError("元素数量不能为0");
                return;
            }

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

            StopAnimation();

            //第一次开启滚动条
            if (m_PoolingItem.Count == 0)
            {
                for (int i = 0; i < m_ChildPrefab.Length; i++)
                {
                    m_ChildPrefab[i].PrefabIndex = i;
                    m_PoolingItem.Add(new Queue<VerticalScrollRectViewItem>());

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
                StopScrollView();
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
            m_OnScrollRectValueChangeItemAction = onScrollRectValueChangeItemAction;
            m_OnScrollRectValueChangeAction = onScrollRectValueChangeAction;
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

            m_SnapStatus = ESnapStatus.NotNeedSnap;

            //更新content
            m_NeedRefreshItemOnUpdate = false;
            UpdateHeightCache();
            UpdateViewportRect();
            UpdateContent();
            UpdateChildItem();
            UpdateItemPosition();
            m_NeedRefreshItemOnUpdate = true;

            m_ScrollRect.onValueChanged.AddListener(OnScrollRectValueChange);
        }

        /// <summary>
        /// 重置元素数量
        /// </summary>
        /// <param name="newItemCount"></param>
        public void ResetItemCount(int newItemCount)
        {
            StartScrollView(newItemCount, m_RefreshItemAction, m_RecycleItemAction, m_GetChildItemPrefabIndex, m_GetChildItemPaddingIndex, m_GetItemHeight, m_OnScrollRectValueChangeItemAction, m_OnScrollRectValueChangeAction, m_CurItemPos);
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
            m_CurItemPos = ContentPercentPos2DataPos();

            //执行回调
            m_OnScrollRectValueChangeAction?.Invoke(m_CurItemPos);
            if (m_UsingItem.Count > 0 && null != m_OnScrollRectValueChangeItemAction)
            {
                var cur = m_UsingItem.First;
                while (null != cur)
                {
                    m_OnScrollRectValueChangeItemAction(cur.Value, m_CurItemPos);
                    cur = cur.Next;
                }
            }

            if (m_SnapStatus == ESnapStatus.NotNeedSnap)
            {
                m_SnapStatus = ESnapStatus.PrepareToSnap;
            }

            if (m_NeedRefreshItemOnUpdate)
            {
                ForceUpdateShownItems();
            }
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
        private void UpdateContent()
        {
            //更新content尺寸
            var content = m_ScrollRect.content;
            if (m_ScrollDirection == EScrollDirection.Down2Up)
            {
                content.anchorMin = new Vector2(0.5f, 0f);
                content.anchorMax = new Vector2(0.5f, 0f);
                content.pivot = new Vector2(0.5f, 0f);
                content.sizeDelta = new Vector2(m_ViewportRect.width, m_ContentHeight);
                var anchorPos = -DataPos2ContentPos(m_CurItemPos, ignoreViewportPivot: false, ignoreViewportLimit: false);
                content.anchoredPosition = new Vector2(0, anchorPos);
            }
            else
            {
                content.anchorMin = new Vector2(0.5f, 1f);
                content.anchorMax = new Vector2(0.5f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.sizeDelta = new Vector2(m_ViewportRect.width, m_ContentHeight);
                var anchorPos = DataPos2ContentPos(m_CurItemPos, ignoreViewportPivot: false, ignoreViewportLimit: false);
                content.anchoredPosition = new Vector2(0, anchorPos);
            }
        }

        /// <summary>
        /// 更新所有item位置
        /// </summary>
        private void UpdateItemPosition()
        {
            if (m_UsingItem.Count > 0)
            {
                var itemNode = m_UsingItem.First;
                while (null != itemNode)
                {
                    var item = itemNode.Value;
                    var anchoredPos = item.RectTransform.anchoredPosition;
                    anchoredPos.y = DataPos2ContentPos(item.CurIndex);
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
        /// 更新显示的item集合
        /// </summary>
        private void UpdateChildItem()
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

            curHeight -= m_ViewportRect.height * m_ViewportSnapPivot;

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
            if (m_ScrollDirection == EScrollDirection.Down2Up)
            {
                m_ContentHeight += m_DownPadding;
            }
            else
            {
                m_ContentHeight += m_UpPadding;
            }
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
            if (m_ScrollDirection == EScrollDirection.Down2Up)
            {
                m_ContentHeight += m_UpPadding;
            }
            else
            {
                m_ContentHeight += m_DownPadding;
            }
        }

        /// <summary>
        /// 校验高度缓存是否发生变化
        /// </summary>
        /// <returns></returns>
        private void CheckHeightCache()
        {
            var itemHeithtCache = m_ItemHeightCache.ToList();
            var contentHeight = m_ContentHeight;

            UpdateHeightCache();

            if (m_ContentHeight != contentHeight)
            {
                UpdateContent();
                return;
            }

            for (int i = 0; i < m_ItemHeightCache.Count; i++)
            {
                if (m_ItemHeightCache[i] != itemHeithtCache[i])
                {
                    UpdateContent();
                    return;
                }
            }
        }

        /// <summary>
        /// 数据坐标转化为content坐标
        /// </summary>
        /// <param name="itemPos"></param>
        /// <param name="ignoreViewportLimit">忽略viewport位置限制</param>
        /// <param name="ignoreViewportPivot">忽略viewport锚点</param>
        /// <returns></returns>
        private float DataPos2ContentPos(float itemPos, bool ignoreViewportLimit = true, bool ignoreViewportPivot = true)
        {
            itemPos = Mathf.Clamp(itemPos, 0, m_ItemCount);
            int itemIndex = Mathf.Clamp(Mathf.FloorToInt(itemPos), 0, m_ItemCount - 1);
            var height = m_ItemHeightCache[itemIndex];
            var itemHeight = m_GetItemHeight(itemIndex);
            height += itemHeight * (itemPos - itemIndex);
            if (!ignoreViewportPivot)
            {
                height -= m_ViewportSnapPivot * m_ViewportRect.height;
            }
            if (!ignoreViewportLimit)
            {
                if (m_ContentHeight > m_ViewportRect.height && height > m_ContentHeight - m_ViewportRect.height)
                {
                    height = m_ContentHeight - m_ViewportRect.height;
                }
                height = Mathf.Max(0, height);
            }
            return height;
        }

        /// <summary>
        /// content位置转化为数据坐标位置
        /// </summary>
        /// <param name="offset">偏移量</param>
        /// <returns></returns>
        private float ContentPercentPos2DataPos(float offset = 0)
        {
            var index = -1;
            var height = m_ScrollRect.content.anchoredPosition.y;
            if (m_ScrollDirection == EScrollDirection.Down2Up)
            {
                height = -height;
            }
            height += offset;
            for (int i = m_ItemHeightCache.Count - 1; i >= 0; i--)
            {
                if (m_ItemHeightCache[i] <= height)
                {
                    index = i;
                    break;
                }
            }
            index = Mathf.Max(0, index);
            var top = m_ContentHeight;
            if (index < m_ItemHeightCache.Count - 1)
            {
                top = m_ItemHeightCache[index + 1];
            }
            var bottom = m_ItemHeightCache[index];
            return index + (height - bottom) / (top - bottom);
        }

        /// <summary>
        /// 停止滚动条
        /// </summary>
        public void StopScrollView()
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
        /// 强制更新所有显示的元素
        /// </summary>
        public void ForceUpdateShownItems()
        {
            m_NeedRefreshItemOnUpdate = false;
            CheckHeightCache();
            UpdateChildItem();
            UpdateItemPosition();
            m_NeedRefreshItemOnUpdate = true;
        }

        /// <summary>
        /// 改变元素数量
        /// </summary>
        /// <param name="newItemCount"></param>
        public void ChangeItemCount(int newItemCount)
        {
            StartScrollView(newItemCount, m_RefreshItemAction, m_RecycleItemAction, m_GetChildItemPrefabIndex, m_GetChildItemPaddingIndex, m_GetItemHeight, m_OnScrollRectValueChangeItemAction, m_OnScrollRectValueChangeAction, m_CurItemPos);
        }

        /// <summary>
        /// 新创建单个元素
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        private VerticalScrollRectViewItem NewSingleItem(int index)
        {
            var prefabIndex = m_GetChildItemPrefabIndex(index);
            VerticalScrollRectViewItem item = null;
            if (m_PoolingItem[prefabIndex].Count > 0)
            {
                item = m_PoolingItem[prefabIndex].Dequeue();
            }
            else
            {
                item = GameObject.Instantiate(m_ChildPrefab[prefabIndex].gameObject, m_ScrollRect.content).GetComponent<VerticalScrollRectViewItem>();
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
        private void RecycleSingleItem(VerticalScrollRectViewItem item)
        {
            item.gameObject.SetActive(false);
            m_RecycleItemAction?.Invoke(item);
            m_PoolingItem[item.PrefabIndex].Enqueue(item);
        }

        /// <summary>
        /// 瞬间跳跃到指定位置
        /// </summary>
        /// <param name="position">从 0-元素个数 取值</param>
        /// <param name="autoSnap">跳跃完成后是否自动定位</param>
        public void JumpTo(float position, bool autoSnap = false)
        {
            position = position % m_ItemCount;
            if (position < 0)
            {
                position += m_ItemCount;
            }

            if (m_ContentHeight <= m_ViewportRect.height)
            {
                return;
            }

            //立即中断当前自动滚动的动画
            StopAnimation();
            m_ScrollRect.StopMovement();

            if (autoSnap)
            {
                m_SnapStatus = ESnapStatus.PrepareToSnap;
            }

            StartScrollView(m_ItemCount, m_RefreshItemAction, m_RecycleItemAction, m_GetChildItemPrefabIndex, m_GetChildItemPaddingIndex, m_GetItemHeight, m_OnScrollRectValueChangeItemAction, m_OnScrollRectValueChangeAction, position);
            if (autoSnap)
            {
                StartAutoSnap();
            }
        }

        /// <summary>
        /// 指定速度滚动到指定位置
        /// </summary>
        /// <param name="targetPos">滚动的下标位置</param>
        /// <param name="speed"></param>
        /// <param name="blockRaycasts">是否屏蔽点击</param>
        /// <param name="onScrollEnd">当滚动完毕回调</param>
        /// <param name="autoSnap">滚动完毕后是否自动定位</param>
        public void ScrollToBySpeed(float targetPos, float speed, bool blockRaycasts = true, Action onScrollEnd = null, bool autoSnap = false)
        {
            if (speed <= 0)
            {
                onScrollEnd?.Invoke();
                return;
            }

            if (m_ContentHeight <= m_ViewportRect.height)
            {
                onScrollEnd?.Invoke();
                return;
            }

            StopAnimation();
            m_ScrollRect.StopMovement();

            if (autoSnap)
            {
                m_SnapStatus = ESnapStatus.PrepareToSnap;
            }

            //标准化位置
            var curContentPos = Mathf.Abs(DataPos2ContentPos(m_CurItemPos, false));
            var targetContentPos = Mathf.Abs(DataPos2ContentPos(targetPos, false, false));

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
                    BlockRaycasts();
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
        /// <param name="blockRaycasts">是否屏蔽射线检测</param>
        /// <param name="onScrollEnd">当滚动完毕回调</param>
        /// <param name="autoSnap">滚动完毕后是否自动定位</param>
        public void ScrollToByTime(float targetPos, float time, bool blockRaycasts = false, Action onScrollEnd = null, bool autoSnap = false)
        {
            if (time <= 0)
            {
                onScrollEnd?.Invoke();
                return;
            }

            StopAnimation();
            m_ScrollRect.StopMovement();

            if (autoSnap)
            {
                m_SnapStatus = ESnapStatus.PrepareToSnap;
            }

            //标准化位置
            var curContentPos = Mathf.Abs(DataPos2ContentPos(m_CurItemPos, false));
            var targetContentPos = Mathf.Abs(DataPos2ContentPos(targetPos, false, false));

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
                if (!blockRaycasts)
                {
                    BlockRaycasts();
                }
            }
            else
            {
                onScrollEnd?.Invoke();
            }
        }

        /// <summary>
        /// 关闭点击检测
        /// </summary>
        private void BlockRaycasts()
        {
            var canvasGroup = this.GetComponent<CanvasGroup>();
            if (null == canvasGroup)
            {
                canvasGroup = this.gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// 开启点击检测
        /// </summary>
        private void UnblockRaycasts()
        {
            var canvasGroup = this.GetComponent<CanvasGroup>();
            if (null != canvasGroup)
            {
                canvasGroup.blocksRaycasts = true;
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
            m_NeedRefreshItemOnUpdate = true;
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
            UnblockRaycasts();

            onScrollEnd?.Invoke();

            StartAutoSnap();
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
            UnblockRaycasts();
        }

        /// <summary>
        /// 开始自动定位
        /// </summary>
        private void StartAutoSnap()
        {
            if (!m_EnableAutoSnap || m_SnapStatus != ESnapStatus.PrepareToSnap)
            {
                return;
            }

            //计算是否回弹,如果回弹,不走自动定位
            if (m_ScrollRect.movementType == ScrollRect.MovementType.Elastic)
            {
                if (ContentPercentPos2DataPos() < 0 || ContentPercentPos2DataPos(m_ViewportRect.height) > m_ItemCount)
                {
                    m_SnapStatus = ESnapStatus.NotNeedSnap;
                    return;
                }
            }

            StopAnimation();
            m_ScrollRect.StopMovement(); 

            m_SnapStatus = ESnapStatus.Snapping;
            var itemPos = Mathf.Round(ContentPercentPos2DataPos(m_ViewportRect.height * m_ViewportSnapPivot) - m_ItemSnapPivot) + m_ItemSnapPivot;
            ScrollToByTime(
                targetPos: itemPos,
                time: 0.3f,
                blockRaycasts: false,
                onScrollEnd: () => m_SnapStatus = ESnapStatus.NotNeedSnap,
                autoSnap: false
                );
        }

        private void OnEnable()
        {
            StopAllCoroutines();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (m_SnapStatus == ESnapStatus.PrepareToSnap)
            {
                StartAutoSnap();
            }
        }

        private enum EScrollDirection : byte
        {
            Down2Up,

            Up2Down,
        }
    }
}

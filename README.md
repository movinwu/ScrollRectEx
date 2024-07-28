之前一直使用的是SuperScrollView组件,直到遇到了一个需求:单个item的长度超过了一个屏幕高度(大约6000像素-8000像素)的一个竖直滚动条,同时需要支持代码滚动到指定位置和快速定位到指定位置,但是SuperScrollView提供的相应函数都需要开启snap,在实验了诸如[EnhancedScrollView](https://github.com/tinyantstudio/EnhancedScrollView)\\[FancyScrollView](https://github.com/setchi/FancyScrollView)等组件后,发现都不支持长item功能,因此决定自行实现一个滚动条以满足需求.
# 功能概述
1. 支持一个列表中同时存在多种item类型,通过委托将数据下标和使用的item下标对应
2. 支持一个列表中同时存在多种item间隔,通过委托将数据下标和使用的item下标对应
3. 支持snap
4. 支持jump到指定数据显示位置,可以指定小数(如jump到3.5位置代表jump到数据下标为3的item的中间位置)
5. 支持指定时间或指定速度的方式自动滚动到指定数据显示位置(同样可以指定小数)
# 使用说明
## 非无线竖直滚动条
1. 

using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AE_EagleEyeDemo
{
    public partial class Form1 : Form
    {
        private string m_Path = Application.StartupPath + @"\Data";
        public Form1()
        {
            InitializeComponent();
        }
        /// <summary>
        /// 主地图OnMapReplaced事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void axMapControl1_OnMapReplaced(object sender, ESRI.ArcGIS.Controls.IMapControlEvents2_OnMapReplacedEvent e)
        {
            //主地图有地图或图层的时候鹰眼加载图层
            if (axMapControl1.LayerCount > 0)
            {
                axMapControl2.ClearLayers(); //先清除鹰眼的地图
                //图层自下而上加载，防止要素间互相压盖
                for (int i = axMapControl1.Map.LayerCount - 1; i >= 0; i--)
                {
                    axMapControl2.AddLayer(axMapControl1.get_Layer(i));
                }
                //设置鹰眼地图鱼主地图相同空间参考系
                //必要：防止由于图层放置顺序改变而改变了鹰眼的空间参考系
                axMapControl2.SpatialReference = axMapControl1.SpatialReference;
                //设置鹰眼的显示范围=完整显示（FullExtent)
                axMapControl2.Extent = axMapControl2.FullExtent;
                //每次加载或者删除图层之后都要刷新一次MapControl
                axMapControl2.Refresh();
            }
        }
        /// <summary>
        /// 主地图OnExtentUpdated事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void axMapControl1_OnExtentUpdated(object sender, ESRI.ArcGIS.Controls.IMapControlEvents2_OnExtentUpdatedEvent e)
        {
            //刷新axMapControl2
            axMapControl2.Refresh();
            //以主地图的Extent作为鹰眼红线框的大小范围
            IEnvelope pEnvelope = axMapControl1.Extent;
            //鹰眼强制转换为IGraphicsContainer
            //IGraphicsContainer是绘图容器接口, 主要功能是在MapControl控件类上添加绘图要素。
            IGraphicsContainer pGraphicsContainer = axMapControl2.Map as IGraphicsContainer;
            //鹰眼强制转换为pActiveView
            IActiveView pActiveView = pGraphicsContainer as IActiveView;
            //删除鹰眼原有要素
            pGraphicsContainer.DeleteAllElements();
            //实例化矩形框要素
            IRectangleElement pRectangleElement = new RectangleElementClass();
            //强转矩形要素框为要素
            IElement pElement = pRectangleElement as IElement;
            //赋值几何实体的最小外接矩形, 即包络线
            pElement.Geometry = pEnvelope;

            //使用面要素刷新(存在覆盖注释问题)
            //DrawPolyline2(pGraphicsContainer, pActiveView, pElement);

            //使用线要素刷新(已解决重叠问题)(推荐使用)
            //使用IScreenDisplay的DrawPolyline方法,在鹰眼视图画出红线框
            DrawPolyline(axMapControl2.ActiveView, pEnvelope);

        }
        /// <summary>
        /// 使用面要素刷新(存在覆盖注释问题)
        /// </summary>
        /// <param name="pGraphicsContainer"></param>
        /// <param name="pActiveView"></param>
        /// <param name="pElement"></param>
        private static void DrawPolyline2(IGraphicsContainer pGraphicsContainer, IActiveView pActiveView, IElement pElement)
        {
            //以下代码设置要素外框边线的颜色、透明度属性
            IRgbColor pColor = new RgbColorClass();
            pColor.Red = 255;
            pColor.Green = 0;
            pColor.Blue = 0;
            pColor.Transparency = 255;

            //以下代码设置要素外框边线的颜色、宽度属性
            ILineSymbol pOutline = new SimpleLineSymbolClass();
            pOutline.Width = 2;
            pOutline.Color = pColor;
            pColor = new RgbColorClass();
            pColor.NullColor = true;

            //以下代码设置要素内部的填充颜色、边线符号属性
            IFillSymbol pFillSymbol = new SimpleFillSymbolClass();
            pFillSymbol.Color = pColor;
            pFillSymbol.Outline = pOutline;

            //实现线框的生成
            IFillShapeElement pFillShapeElement = pElement as IFillShapeElement;
            pFillShapeElement.Symbol = pFillSymbol;
            pGraphicsContainer.AddElement((IElement)pFillShapeElement, 0);

            //刷新鹰眼视图的填充要素（绘图框）
            pActiveView.PartialRefresh(esriViewDrawPhase.esriViewGraphics, pFillShapeElement, null);
        }

        /// <summary>
        /// 使用线要素刷新(已解决重叠问题)(推荐使用)
        /// 使用IScreenDisplay的DrawPolyline方法,在鹰眼视图画出红线框
        /// </summary>
        /// <param name="activeView">鹰眼视图的活动窗体</param>
        /// <param name="geometry">制框范围</param>
        private void DrawPolyline(IActiveView activeView, IGeometry geometry)
        {
            if (activeView == null)
                return; //如果活动窗体为空, 则返回
            //强行刷新鹰眼视图, 目的: 清除前一次的绘图框, 避免重复绘图框
            axMapControl2.ActiveView.ScreenDisplay.UpdateWindow(); //解决重复绘图框的关键代码
            IScreenDisplay screenDisplay = activeView.ScreenDisplay;
            //Screen的绘图状态处于准备状态
            //参数: (指定设备(Dc=Device), 缓冲区(-1=NoScreenCache,-2=AllScreenCache, -3=ScreenRecoding))
            //解析: 设备(Device)参数指图形的绘制区域
            //缓冲区(Cache)参数指图形是否经由缓存后再绘制在屏幕(Window/Screen)上。
            //一般默认为NoScreenCache, 即不经过缓存直接绘制
            screenDisplay.StartDrawing(screenDisplay.hDC, (System.Int16)esriScreenCache.esriNoScreenCache);
            //实例化颜色对象
            IRgbColor rgbColor = new RgbColorClass();
            rgbColor.Red = 255;
            IColor color = rgbColor;
            //实例化符号(Symbol)对象
            ISimpleLineSymbol simpleLineSymbol = new SimpleLineSymbolClass();
            simpleLineSymbol.Color = color;
            simpleLineSymbol.Width = 2;
            ISymbol symbol = (ISymbol)simpleLineSymbol;
            screenDisplay.SetSymbol(symbol);
            screenDisplay.DrawPolyline(geometry);
            screenDisplay.FinishDrawing();
        }
        /// <summary>
        /// 鹰眼地图的OnMouseDown事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void axMapControl2_OnMouseDown(object sender, ESRI.ArcGIS.Controls.IMapControlEvents2_OnMouseDownEvent e)
        {
            if (axMapControl2.LayerCount > 0)
            {
                //如果e.button==1, 则表示按下的是鼠标左键
                if (e.button == 1)
                {
                    axMapControl2.Refresh();
                    //捕捉鼠标单击时的地图坐标
                    IPoint pPoint = new PointClass();
                    pPoint.PutCoords(e.mapX, e.mapY);
                    //将地图的中心点移动到鼠标点击的点pPoint
                    axMapControl1.CenterAt(pPoint);
                    axMapControl1.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                }
                else if (e.button == 2)
                {//如果e.button==2, 则表示按下的是鼠标右键
                    //鹰眼地图的TrackRectangle()方法, 随着鼠标拖动得到一个矩形框
                    IEnvelope pEnvelope = axMapControl2.TrackRectangle();
                    axMapControl1.Extent = pEnvelope;//鼠标拖动生成的矩形框范围
                    axMapControl1.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                }
            }
        }
        /// <summary>
        /// 加载
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddShpFiletoolStripLabel3_Click(object sender, EventArgs e)
        {
            loadMapDoc2();
        }


        private void 加载mxd地图文档toolStripLabel1_Click(object sender, EventArgs e)
        {
            //方法二：
            loadMapDoc2();
        }
        /// <summary>
        /// 方法二：运用MapDocument对象中的Open方法的函数加载mxd文档
        /// </summary>
        private void loadMapDoc2()
        {
            IMapDocument mapDocument = new MapDocumentClass();
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Title = "打开地图文档";
                ofd.Filter = "map documents(*.mxd)|*.mxd";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string pFileName = ofd.FileName;
                    //pFileName——地图文档的路径, ""——赋予默认密码
                    mapDocument.Open(pFileName, "");
                    for (int i = 0; i < mapDocument.MapCount; i++)
                    {
                        //通过get_Map(i)方法逐个加载
                        axMapControl1.Map = mapDocument.get_Map(i);
                    }
                    axMapControl1.Refresh();
                }
                else
                {
                    mapDocument = null;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }

        }

        /// <summary>
        /// 鹰眼地图的OnMouseMove事件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void axMapControl2_OnMouseMove(object sender, ESRI.ArcGIS.Controls.IMapControlEvents2_OnMouseMoveEvent e)
        {
            //如果e.button==1, 则表示按下的是鼠标左键
            if (e.button == 1)
            {
                axMapControl2.Refresh();
                //捕捉鼠标单击时的地图坐标
                IPoint pPoint = new PointClass();
                pPoint.PutCoords(e.mapX, e.mapY);
                //将地图的中心点移动到鼠标点击的点pPoint
                axMapControl1.CenterAt(pPoint);
                axMapControl1.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
            }
        }
    }
}

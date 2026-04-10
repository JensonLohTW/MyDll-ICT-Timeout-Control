using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyDll;
using static System.Net.Mime.MediaTypeNames;
using static MyDll.ParameterManager;
using System.Drawing;

namespace MainTestMydll
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //// 初始化时自动加载配置
            List<ParameterDefinition> defaultParam = new List<ParameterDefinition>
            {
                //new ParameterManager.ParameterDefinition("通用", "料号", "料号"),
                //new ParameterManager.ParameterDefinition("通用", "穴位数量", -1),

                new ParameterManager.ParameterDefinition("相机1", "触发方式", "硬触发"),
                new ParameterManager.ParameterDefinition("相机1", "光源控制", ""),
                new ParameterManager.ParameterDefinition("相机1", "拍照点位", new List<int>{ 1 }),
                new ParameterManager.ParameterDefinition("相机1", "曝光次数", new List<int>{ 4 }),
                new ParameterManager.ParameterDefinition("相机1", "去向流程", new List<int>{ 301 }),

                new ParameterManager.ParameterDefinition("相机2", "触发方式", "硬触发"),
                new ParameterManager.ParameterDefinition("相机2", "光源控制", ""),
                new ParameterManager.ParameterDefinition("相机2", "拍照点位", new List<int>{ 2 }),
                new ParameterManager.ParameterDefinition("相机2", "曝光次数", new List<int>{ 4 }),
                new ParameterManager.ParameterDefinition("相机2", "去向流程", new List<int>{ 302 }),

                new ParameterManager.ParameterDefinition("相机3", "触发方式", ""),
                new ParameterManager.ParameterDefinition("相机3", "光源控制", ""),
                new ParameterManager.ParameterDefinition("相机3", "拍照点位", new List<int>{ }),
                new ParameterManager.ParameterDefinition("相机3", "曝光次数", new List<int>{ }),
                new ParameterManager.ParameterDefinition("相机3", "去向流程", new List<int>{ }),

                new ParameterManager.ParameterDefinition("相机4", "触发方式", ""),
                new ParameterManager.ParameterDefinition("相机4", "光源控制", ""),
                new ParameterManager.ParameterDefinition("相机4", "拍照点位", new List<int>{ }),
                new ParameterManager.ParameterDefinition("相机4", "曝光次数", new List<int>{ }),
                new ParameterManager.ParameterDefinition("相机4", "去向流程", new List<int>{ }),

                new ParameterManager.ParameterDefinition("图像组前处理", "ROI1", new List<Rectangle>{ new Rectangle(100, 50, 20, 40),  new Rectangle(10, 30, 20, 40) }),
                new ParameterManager.ParameterDefinition("图像组前处理", "ROI2", new List<Rectangle>{ }),
                new ParameterManager.ParameterDefinition("图像组前处理", "ROI3", new List<Rectangle>{ }),
                new ParameterManager.ParameterDefinition("图像组前处理", "ROI4", new List<Rectangle>{ }),

                new ParameterManager.ParameterDefinition("图像组前处理", "拍照点位", new List<int>{ 1, 1, 1, 1, 2, 2, 2, 2 }),
                new ParameterManager.ParameterDefinition("图像组前处理", "曝光编号", new List<int>{ 1, 2, 3, 4, 1, 2, 3, 4 }),
                new ParameterManager.ParameterDefinition("图像组前处理", "检测流程", new List<int>{ 401, 402, 403, 404, 405, 406, 407 , 408}),
                new ParameterManager.ParameterDefinition("图像组前处理", "视图过滤", new List<string>{ "", "", "", "", "", "", "", "", }),

                new ParameterManager.ParameterDefinition("后处理", "特征过滤", ""),
                new ParameterManager.ParameterDefinition("后处理", "位置过滤", ""),


            };

            var paramManager = ParameterManager.Instance;
            paramManager.Initialize(@"D:\阿丘FPC检测程序\AQVConfig.ini", defaultParam);

            var v = paramManager.GetValue<string>("相机1", "触发方式");
            var rect_list = paramManager.GetValue<List<Rectangle>>("图像组前处理", "ROI2");
            //Console.WriteLine($"相机1.触发方式: {v}");

            //Console.WriteLine($"Rectangle0 : {rect_list[0].Left} {rect_list[0].Top} {rect_list[0].Right} {rect_list[0].Bottom}");
            //Console.WriteLine($"Rectangle1 : {rect_list[1].Left} {rect_list[1].Top} {rect_list[1].Right} {rect_list[1].Bottom}");

            //var rect_list = new List<Rectangle> { new Rectangle(100, 50, 20, 40), new Rectangle(100, 50, 20, 40) };
            //string rect_str = string.Join(",", rect_list.Select(r => $"{r.Left}_{r.Top}_{r.Right}_{r.Bottom}"));
            //Console.WriteLine(rect_str);
        }
    }
}

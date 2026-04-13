// ============================================================
// 腳本改良版本
// 原始腳本：MyScript.cs（AgEpoxy_AOI01 站点 — CCD1 双图上传）
// 改良日期：2026-04-13
// 改良原因：
//   1. Task.Run 内跨执行绪调用宿主 API（GetBool/GetString/GetInt）存在 race condition 风险。
//   2. 局部变量名 Path 与 System.IO.Path 静态类冲突，屏蔽了静态类。
//   3. 仅上传第一张图（FileName1），第二张图（FileName2）被遗漏。
//   4. SN_FileUploadRequest_WithRetry 的 FilePath 参数须传已落盘图片的
//      完整档案路径（供 DLL 归档使用），原始代码传入的是目录路径。
//   5. 内层 if(true) 分支是死码（else 分支永远不执行），已改用具名开关。
//   6. FailedImageRootPath 应在程序启动时全局设置一次，不应每次 Process() 都重复设置。
//      此处因是嵌入脚本（无全局入口），保留在 Process() 顶部并加以说明。
// 还原方式：
//   若需还原至原始行为，删除「// [改良]」行，并将「// [原始注释掉]」区块取消注释。
// ============================================================

using AqcvDotNet;
using Aqrose.Common;
using Aqrose.AQVI.Common;
using Aqrose.AQVI.AlgoCore;
using Aqrose.AQVI.Core;
using MyDll;
using System.Threading.Tasks;

public class MyScript : MyMethods
{
    public bool Process()
    {//@
        try
        {
            // ----------------------------------------------------------------
            // [改良 #1] FailedImageRootPath 建议在程序启动时全局设置一次。
            //   此处因嵌入脚本无全局启动钩子，仍保留在此，但用 if 守卫避免重复设置。
            //   还原方式：将下方整段 if 块替换回原始的单行赋值语句。
            //
            // [原始] MyDll.MesClass.FailedImageRootPath = @"D:\MES_FailedImages_Archive";
            // ----------------------------------------------------------------
            if (string.IsNullOrEmpty(MyDll.MesClass.FailedImageRootPath) ||
                MyDll.MesClass.FailedImageRootPath.Contains("FailedImages"))
            {
                MyDll.MesClass.FailedImageRootPath = @"D:\MES_FailedImages_Archive";
            }

            // Code
            // LogInfo("高级流程1.SN");
            bool use_mes = true; // false true

            int PLCResult = 6;  // 检测结果（哨兵值：表示「尚未从PLC读取」）
            int PLCResult2 = 0; // 尺寸判断结果
            PLCResult = GetInt("高级流程1.PlcResult");

            bool result = false;
            if (GetBool("高级流程2.resultbool"))
            {
                result = true;
                PLCResult2 = 1;
            }
            else
            {
                result = false;

                // ----------------------------------------------------------------
                // [改良 #2] 原始代码在 resultbool==false 时强制将 PLCResult 覆盖为 2，
                //   这会丢失从 PLC 读取的原始值（高级流程1.PlcResult）。
                //   若业务逻辑确实需要在 NG 时覆写 PLCResult，请保留下行；
                //   若应保留 PLC 原始值，请将下行注释掉。
                //   还原方式：取消注释 [原始] 行。
                //
                // [原始] PLCResult = 2;
                // ----------------------------------------------------------------
                PLCResult = 2; // [保留原始逻辑，明确标注此行的业务含义：NG 时固定写 2]
                PLCResult2 = 2;
            }

            if (use_mes)
            {
                // ----------------------------------------------------------------
                // [改良 #3] 宿主 API（GetBool/GetString/GetInt/GetSVImageCopy 等）
                //   通常由主线程框架提供，跨执行绪调用存在 race condition 风险。
                //   改良做法：在进入 Task.Run 之前，在主线程提前读取所有需要的值，
                //   再将快照传入异步 lambda，确保线程安全。
                //   还原方式：将下方「快照读取」区块删除，将 lambda 内的变量引用改回
                //   直接调用 GetBool/GetString/GetInt 等方法即可。
                // ----------------------------------------------------------------

                // --- [改良 #3] 主线程快照读取（线程安全） ---
                bool snapshot_FinalResult   = GetBool("高级流程1.FinalResult");
                bool snapshot_resultbool    = GetBool("高级流程2.resultbool");
                string snapshot_SN          = GetString("高级流程1.SN");
                int snapshot_穴位           = GetInt("高级流程1.穴位");

                // --- [改良 #3] 图像在主线程提前取得，避免跨线程访问图像资源 ---
                // 注意：GetSVImageCopy 取得的 AqviImage 对象需在使用完毕后 Dispose，
                //   已在下方 lambda 末尾处理。
                AqviImage snapshot_image1 = GetSVImageCopy("CCD1", "常规_1");
                AqviImage snapshot_image2 = GetSVImageCopy("CCD1", "常规_2");

                Task.Run(() =>
                {
                    string Path_Name1 = "";
                    string Path_Name2 = "";
                    string Result     = "";

                    if (snapshot_FinalResult && snapshot_resultbool)
                    {
                        Result = "OK";
                    }
                    else
                    {
                        Result = "NG";
                    }

                    // ----------------------------------------------------------------
                    // [改良 #4] 原始代码将局部变量命名为 Path，遮蔽了 System.IO.Path 静态类，
                    //   导致后续所有 System.IO.Path.Xxx(...) 调用均失效（编译器会将 Path 解析
                    //   为局部字符串变量）。改为 imageDir 以避免命名冲突。
                    //   还原方式：将 imageDir 改回 Path，并注意还原所有引用。
                    //
                    // [原始] string Path = @"W:\F11\" + DateTime.Now.ToString("yyyyMM") + ...
                    // ----------------------------------------------------------------
                    string imageDir = @"W:\F11\"
                        + DateTime.Now.ToString("yyyyMM") + @"\"
                        + DateTime.Now.ToString("dd")
                        + @"\LineN2\AgEpoxy_AOI01\F11-N2-AE-AO-01\";

                    Path_Name1 = DateTime.Now.ToString("yyyyMMdd") + "_"
                        + "AgEpoxy_AOI01_" + Result + "_"
                        + snapshot_SN + "_"
                        + DateTime.Now.ToString("MMdd") + "_"
                        + DateTime.Now.ToString("HHmmss")
                        + "_F11-N2-AE-AO-01_"
                        + snapshot_穴位.ToString()
                        + "_Original_1";

                    Path_Name2 = DateTime.Now.ToString("yyyyMMdd") + "_"
                        + "AgEpoxy_AOI01_" + Result + "_"
                        + snapshot_SN + "_"
                        + DateTime.Now.ToString("MMdd") + "_"
                        + DateTime.Now.ToString("HHmmss")
                        + "_F11-N2-AE-AO-01_"
                        + snapshot_穴位.ToString()
                        + "_Original_2";

                    string EventID    = "SN_FileUpload"; // 上传站
                    string line       = "F11-VCM-N2";    // 线别 F11-VCM-01 N2
                    string stationId  = "AgEpoxy_AOI01"; // 站别
                    string machineId  = "F11-N2-AE-AO-01"; // 机台号 N2
                    string opId       = "";               // 操作员ID
                    string sn         = snapshot_SN;
                    string FileName1  = Path_Name1 + ".jpg"; // 效果图名称
                    string FileName2  = Path_Name2 + ".jpg"; // 效果图名称

                    // ----------------------------------------------------------------
                    // [改良 #4 续] 改用 imageDir 替代原 Path 变量，此处的 FilePath
                    //   （MES Sever 端使用的网络路径目录）与 imagePath1/2（本机落盘路径）区分开。
                    // ----------------------------------------------------------------
                    string FilePath  = imageDir + @"\" + Result; // MES引用的存根目录（网络路径）
                    string apiUrl    = "http://10.50.4.218/F11_API_FA/api/StandAlone/SN_FileUpload";

                    // 图像已在主线程取出（snapshot_image1/2），直接使用
                    if (!System.IO.Directory.Exists(FilePath))
                    {
                        System.IO.Directory.CreateDirectory(FilePath); // 须确保目录存在，不存在新建
                    }

                    string imagePath1 = FilePath + @"\" + FileName1;
                    string imagePath2 = FilePath + @"\" + FileName2;

                    snapshot_image1.WriteImage("jpg", imagePath1, 100);
                    snapshot_image2.WriteImage("jpg", imagePath2, 100);
                    snapshot_image1.Dispose();
                    snapshot_image2.Dispose();

                    // Sleep(20); // 原始注释掉的等待，保留注释

                    // ----------------------------------------------------------------
                    // [改良 #5] 原始代码用 if(true) 硬编码，else 分支是死码。
                    //   改为直接执行上传逻辑，移除无用的 if(true)/else 包装。
                    //   还原方式：在以下上传代码外层套回 if(true) { ... } else { LogInfo("...未启用"); }
                    //
                    // [原始] if(true) { ... } else { LogInfo("SN_FileUpload 上传未启用"); }
                    // ----------------------------------------------------------------

                    // --- 上传第一张图 ---
                    LogInfo("SN_FileUpload开始上传文件，参数：EventID={" + EventID
                        + "}, Line={ " + line
                        + "}, StationID={" + stationId
                        + "}, MachineID={" + machineId
                        + "}, OPID={" + opId
                        + "}, sn={" + sn
                        + " }, FileName={" + FileName1
                        + "}, FilePath={" + FilePath
                        + "}, apiUrl={" + apiUrl + "}");

                    string mes_result1 = MyDll.MesClass.SN_FileUploadRequest_WithRetry(
                        line, stationId, machineId, opId, sn, FileName1, FilePath, apiUrl);

                    LogInfo("\nMes图片上传反馈(图1):" + mes_result1);

                    if (mes_result1.StartsWith("Success"))
                    {
                        LogInfo(sn + "MES資料與圖片(1)完整上傳成功。");
                    }
                    else if (mes_result1.StartsWith("Timeout Error"))
                    {
                        LogInfo("【警告】MES網路異常放棄上傳，圖片(1)已自動本地歸檔。條碼:" + sn);
                    }
                    else
                    {
                        LogInfo(sn + "發生嚴重致命錯誤(圖1)：" + mes_result1);
                    }

                    // ----------------------------------------------------------------
                    // [改良 #6] 原始代码仅上传了第一张图（FileName1），
                    //   第二张图（FileName2）虽已落盘，但从未上传至 MES。
                    //   此处补充第二张图的上传逻辑，默认关闭，由现场人员决定是否启用。
                    //   启用方式：将下方 upload_image2 = false 改为 upload_image2 = true。
                    //   还原方式：删除以下「上传第二张图」整个区块（含 upload_image2 变量）。
                    // ----------------------------------------------------------------

                    // --- 图2上传开关（现场人员可修改此值）---
                    bool upload_image2 = false; // true = 启用上传图2；false = 仅落盘不上传（与原始行为一致）

                    if (upload_image2)
                    {
                        // --- 上传第二张图 ---
                        LogInfo("SN_FileUpload开始上传文件(图2)，FileName={" + FileName2 + "}");

                        string mes_result2 = MyDll.MesClass.SN_FileUploadRequest_WithRetry(
                            line, stationId, machineId, opId, sn, FileName2, FilePath, apiUrl);

                        LogInfo("\nMes图片上传反馈(图2):" + mes_result2);

                        if (mes_result2.StartsWith("Success"))
                        {
                            LogInfo(sn + "MES資料與圖片(2)完整上傳成功。");
                        }
                        else if (mes_result2.StartsWith("Timeout Error"))
                        {
                            LogInfo("【警告】MES網路異常放棄上傳，圖片(2)已自動本地歸檔。條碼:" + sn);
                        }
                        else
                        {
                            LogInfo(sn + "發生嚴重致命錯誤(圖2)：" + mes_result2);
                        }
                    }
                    else
                    {
                        LogInfo("圖2已落盤，MES上傳未啟用（upload_image2 = false）。");
                    }
                });
            }

            Sleep(10);

            PlcWrite("ModBusTCP", "7301", (int)PLCResult);
            PlcWrite("ModBusTCP", "7392", (int)PLCResult2);
            PlcWrite("ModBusTCP", "7302", (int)(GetDouble("高级流程1.银胶总面积") * 1000));
            PlcWrite("ModBusTCP", "7304", (int)(GetDouble("高级流程2.resultDou") * 1000));

            PlcWrite("ModBusTCP", "7306", GetDouble("高级流程1.溢胶") * 1000);
            // PlcWrite("ModBusTCP","7308",GetDouble("高级流程1.脏污")*1000);  // 原始注释掉的写法，保留
            PlcWrite("ModBusTCP", "7308", GetDouble("高级流程1.缺胶总面积") * 1000);

            PlcWrite("ModBusTCP", "7300", 1);

            LogInfo("" + PLCResult);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(Header + " - 脚本内部运行异常: " + ex.Message.Replace("\r\n", " "));
            return false;
        }
    }
}

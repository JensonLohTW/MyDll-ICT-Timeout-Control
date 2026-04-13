using AqcvDotNet;
using Aqrose.Common;
using Aqrose.AQVI.Common;
using Aqrose.AQVI.AlgoCore;
using Aqrose.AQVI.Core;
using MyDll;
using System.Threading.Tasks;
public class MyScript: MyMethods
{
    public bool Process()
    {//@
        try
        {	MyDll.MesClass.FailedImageRootPath = @"D:\MES_FailedImages_Archive";
            // Code
            // LogInfo("高级流程1.SN");
            bool use_mes = true;// false true
            
            
            int PLCResult = 6; // 检测结果
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
                PLCResult = 2;
                PLCResult2 = 2;
            }
            
            if (use_mes)
            {
                Task.Run(() =>{
                    string Path_Name1 ="";
                    string Path_Name2 ="";
                    string Result = "";
                    
                    
                    if(GetBool("高级流程1.FinalResult") && GetBool("高级流程2.resultbool"))
                    {
                        Result="OK";
                        
                    }else
                    {
                        Result="NG";
                    }
                    //F11-VCM-01
                    string Path = @"W:\F11\" +DateTime.Now.ToString("yyyyMM") +@"\" +DateTime.Now.ToString("dd") +@"\LineN2\AgEpoxy_AOI01\F11-N2-AE-AO-01\" ;
                    Path_Name1  = DateTime.Now.ToString("yyyyMMdd") + "_" + "AgEpoxy_AOI01_"+ Result +"_" +GetString("高级流程1.SN") +"_"+  DateTime.Now.ToString("MMdd")+ "_" +  DateTime.Now.ToString("HHmmss") + "_F11-N2-AE-AO-01_" + GetInt("高级流程1.穴位").ToString() +"_Original_1" ;
                    Path_Name2  = DateTime.Now.ToString("yyyyMMdd") + "_" + "AgEpoxy_AOI01_"+ Result +"_" +GetString("高级流程1.SN") +"_"+  DateTime.Now.ToString("MMdd")+ "_" +  DateTime.Now.ToString("HHmmss") + "_F11-N2-AE-AO-01_" + GetInt("高级流程1.穴位").ToString() +"_Original_2" ;
                    
                    
                    
                    string EventID = "SN_FileUpload";//上传站
                    string line = "F11-VCM-N2";//线别 F11-VCM-01 N2
                    string stationId = "AgEpoxy_AOI01";//站别
                    string machineId = "F11-N2-AE-AO-01";//机台号 N2
                    string opId = "" ;//操作员ID
                    string sn = GetString("高级流程1.SN");
                    string FileName1 = Path_Name1 + ".jpg" ;//效果图名称
                    string FileName2 = Path_Name2 + ".jpg" ;//效果图名称
                    string FilePath = Path + @"\" +Result;//效果图地址
                    string apiUrl = "http://10.50.4.218/F11_API_FA/api/StandAlone/SN_FileUpload";
                    
                    
                    AqviImage image1 = GetSVImageCopy("CCD1", "常规_1");
                    AqviImage image2 = GetSVImageCopy("CCD1", "常规_2");
                    if(!Directory.Exists(FilePath))
                    {
                        Directory.CreateDirectory(FilePath);//须确保目录存在，不存在新建
                    }
                    //string imagePath = Path.Combine(FilePath, FileName);
                    string imagePath1 = FilePath + @"\" + FileName1;
                    string imagePath2 = FilePath + @"\" + FileName2;
                    image1.WriteImage("jpg", imagePath1, 100);
                    image2.WriteImage("jpg", imagePath2, 100);
                    image1.Dispose();
                    image2.Dispose();
                    
                    //Sleep(20);
                    
                    
                    if(true)
                    {
                        LogInfo("SN_FileUpload开始上传文件，参数：EventID={" + EventID + "}, Line={ " + line + "}, StationID={" + stationId + 
                        "}, MachineID={" + machineId + "}, OPID={" + opId + "}, sn={" + sn + " }, FileName={" + FileName1 + 
                        "}, FilePath={"+ FilePath + "}, apiUrl={"+ apiUrl + "}");
                        
                        string mes_class = MyDll.MesClass.SN_FileUploadRequest_WithRetry(line,stationId,machineId,opId, sn,FileName1,FilePath,apiUrl);
                        //LogInfo("产品SN:" + GetString("数据出队1.SN") +"穴位:" + GetInt("数据出队1.穴位").ToString() + "\nMes图片上传反馈:" + mes_class);
                        LogInfo("\nMes图片上传反馈:" + mes_class);
                        if (mes_class.StartsWith("Success"))
                        {
                            
                            LogInfo(sn+"MES資料與圖片完整上傳成功。");
                        }
                        else if (mes_class.StartsWith("Timeout Error"))
                        {
                            LogInfo("【警告】MES網路異常放棄上傳，圖片已自動本地歸檔。條碼:"+sn);
                        }
                        else
                        {
                            
                            LogInfo(sn+"發生嚴重致命錯誤：" + mes_class);
                        }
                    }
                    else 
                    { 
                        LogInfo("SN_FileUpload 上传未启用");
                    }
                });
            }
            Sleep(10);
            
            PlcWrite("ModBusTCP","7301",(int)PLCResult);
            PlcWrite("ModBusTCP","7392",(int)PLCResult2);
            PlcWrite("ModBusTCP","7302",(int)(GetDouble("高级流程1.银胶总面积")*1000));
            PlcWrite("ModBusTCP","7304",(int)(GetDouble("高级流程2.resultDou")*1000));
            
            PlcWrite("ModBusTCP","7306",GetDouble("高级流程1.溢胶")*1000);
            //PlcWrite("ModBusTCP","7308",GetDouble("高级流程1.脏污")*1000);
            PlcWrite("ModBusTCP","7308",GetDouble("高级流程1.缺胶总面积")*1000);
            
            PlcWrite("ModBusTCP","7300",1);
            
            LogInfo("" + PLCResult);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(Header +" - 脚本内部运行异常: "+ex.Message.Replace("\r\n"," "));
            return false;
        }
    }
}
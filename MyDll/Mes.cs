using System.Windows.Forms;
using System.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using static System.Collections.Specialized.BitVector32;
using System.Drawing;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MyDll {
    // 数据模型类定义
    public class DcInfoItem {
        public string Item { get; set; }
        public string Value { get; set; }
        public string Result { get; set; }
    }

    public class CompListItem {
        public string CompID { get; set; }
        public string Qty { get; set; }
    }

    public class FileInfo {
        public string FileName { get; set; }
        public string FilePath { get; set; }
    }

    public class SnInfoItem {
        public string SN { get; set; }
        public string Result { get; set; }
        public string ErrCode { get; set; }
        public List<DcInfoItem> DC_Info { get; set; }
        public List<CompListItem> CompList { get; set; }
    }

    public class SNList {
        public string SN { get; set; }
        public List<FileInfo> FileInfo { get; set; }
    }

    public class SnCheckOutModel {
        public string EventID { get; set; }
        public string Line { get; set; }
        public string StationID { get; set; }
        public string MachineID { get; set; }
        public string Mold { get; set; }
        public string OPID { get; set; }
        public string TOKen { get; set; }
        public string FixSN { get; set; }
        public List<SnInfoItem> SNInfo { get; set; }
        public string SendTime { get; set; }
    }

    public class SnFileUploadModel {
        public string EventID { get; set; }
        public string Line { get; set; }
        public string StationID { get; set; }
        public string MachineID { get; set; }
        public string Mold { get; set; }
        public string OPID { get; set; }
        public string TOKen { get; set; }

        public string FixSN { get; set; }

        // 改为 List<SNList> 类型
        public List<SNList> SNList { get; set; }
        public string SendTime { get; set; }
    }

    public class MesClass {
        private static string _failedImageRootPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FailedImages");

        /// <summary>
        /// 失败图片归档根目录。调用方可在上传前设置该路径，例如 MesClass.FailedImageRootPath = @"D:\FailedImages"。
        /// </summary>
        public static string FailedImageRootPath {
            get { return _failedImageRootPath; }
            set { _failedImageRootPath = value; }
        }

        /// <summary>
        /// 发送SN文件上传请求到MES系统
        /// </summary>
        /// <param name="apiUrl">MES系统API地址</param>
        /// <param name="sn">序列号</param>
        /// <param name="result">结果(PASS/FAIL)</param>
        /// <param name="errCode">错误代码</param>
        /// <param name="dcInfoList">DC信息列表</param>
        /// <param name="compList">组件列表</param>
        /// <returns>返回请求结果</returns>
        public static string SN_CheckOutRequest(
            string Line, string StationID, string MachineID, string Mold, string OPID,
            string TOKen, string FixSN,
            string apiUrl,
            string sn,
            string result,
            string errCode,
            List<DcInfoItem> dcInfoList,
            List<CompListItem> compList) {
            try {
                // 构建上传数据模型
                var uploadData = new SnCheckOutModel {
                    EventID = "SN_CheckOut",
                    Line = Line,
                    StationID = StationID,
                    MachineID = MachineID,
                    Mold = Mold,
                    OPID = OPID,
                    TOKen = TOKen,
                    FixSN = FixSN,
                    SNInfo = new List<SnInfoItem> {
                        new SnInfoItem {
                            SN = sn,
                            Result = result,
                            ErrCode = errCode,
                            DC_Info = dcInfoList,
                            CompList = compList
                        }
                    },
                    SendTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
                };

                // 序列化为JSON
                string json = JsonConvert.SerializeObject(uploadData, Formatting.Indented);

                // 同步发送HTTP请求
                using (var httpClient = new HttpClient()) {
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = httpClient.PostAsync(apiUrl, content).Result;

                    if (response.IsSuccessStatusCode) {
                        return
                            $"Success: {response.StatusCode}, Response: {response.Content.ReadAsStringAsync().Result}";
                    }
                    else {
                        return $"Error: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                    }
                }
            }
            catch (Exception ex) {
                return $"Exception: {ex.Message}";
            }
        }

        public static string SendSN_FileUploadRequest(
            string Line, string StationID, string MachineID, string OPID,
            string sn, string FileName, string FilePath, string apiUrl) {
            try {
                // 构建上传数据模型
                var uploadData = new SnFileUploadModel {
                    EventID = "SN_FileUpload",
                    Line = Line,
                    StationID = StationID,
                    MachineID = MachineID,
                    OPID = OPID,
                    // 给 SNList 赋值
                    SNList = new List<SNList> {
                        new SNList {
                            SN = sn,
                            FileInfo = new List<FileInfo> {
                                new FileInfo {
                                    FileName = FileName,
                                    FilePath = FilePath,
                                }
                            }
                        }
                    },
                    SendTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
                };

                // 序列化为JSON
                string json = JsonConvert.SerializeObject(uploadData, Formatting.Indented);

                // 同步发送HTTP请求
                using (var httpClient = new HttpClient()) {
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = httpClient.PostAsync(apiUrl, content).Result;

                    if (response.IsSuccessStatusCode) {
                        return
                            $"Success: {response.StatusCode}, Response: {response.Content.ReadAsStringAsync().Result}";
                    }
                    else {
                        return $"Error: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                    }
                }
            }
            catch (Exception ex) {
                return $"Exception: {ex.Message}";
            }
        }

        // 使用示例******************************无数组*********************************************/
        public static string ExampleUsage(string Line, string StationID, string MachineID, string Mold, string OPID,
            string TOKen, string FixSN, string apiUrl, string sn, string result, string errCode) {
            // 构建DC信息列表
            var dcInfo = new List<DcInfoItem> {
                new DcInfoItem { Item = "FAI_5001", Value = "0", Result = "PASS" },
                new DcInfoItem { Item = "FG_FAI_11_2", Value = "18.205", Result = "PASS" },
                new DcInfoItem { Item = "FG_FAI_11_3", Value = "18.213", Result = "PASS" }
            };

            // 构建组件列表
            var compList = new List<CompListItem> {
                new CompListItem { CompID = "#10", Qty = "10" }
            };

            // 调用发送函数
            string result_1 = SN_CheckOutRequest(
                Line, StationID, MachineID, Mold, OPID,
                TOKen, FixSN, apiUrl, sn, result, errCode,
                dcInfo, compList);

            return result_1;
        }

        public static string SN_FileUpload(string Line, string StationID, string MachineID, string OPID, string sn,
            string FileName, string FilePath, string apiUrl) {
            // 调用发送函数
            string result = SendSN_FileUploadRequest(Line, StationID, MachineID, OPID, sn, FileName, FilePath, apiUrl);

            return result;
        }

        /***************************************有数组版*************************************************/
        // 修改后的使用示例，通过数组动态构建dcInfo和compList
        public static string ExampleUsage_List(
            string Line, string StationID, string MachineID, string Mold, string OPID,
            string TOKen, string FixSN, string apiUrl, string sn, string result, string errCode,
            // 新增数组参数：DC信息的Item、Value、Result数组（三者长度需一致）
            string[] dcItems, string[] dcValues, string[] dcResults,
            // 新增数组参数：组件的CompID和Qty数组（两者长度需一致）
            string[] compIds, string[] compQtys) {
            // 构建DC信息列表（通过数组动态生成）
            var dcInfo = new List<DcInfoItem>();
            if (dcItems != null && dcValues != null && dcResults != null) {
                // 取三个数组中长度最小的值作为循环次数，避免数组越界
                int dcCount = Math.Min(dcItems.Length, Math.Min(dcValues.Length, dcResults.Length));
                for (int i = 0; i < dcCount; i++) {
                    dcInfo.Add(new DcInfoItem {
                        Item = dcItems[i], // 对应数组中的Item
                        Value = dcValues[i], // 对应数组中的Value
                        Result = dcResults[i] // 对应数组中的Result
                    });
                }
            }

            // 构建组件列表（通过数组动态生成）
            var compList = new List<CompListItem>();
            /*
            if (compIds != null && compQtys != null)
            {
                // 取两个数组中长度最小的值作为循环次数，避免数组越界
                int compCount = Math.Min(compIds.Length, compQtys.Length);
                for (int i = 0; i < compCount; i++)
                {
                    compList.Add(new CompListItem
                    {
                        CompID = compIds[i],     // 对应数组中的CompID
                        Qty = compQtys[i]        // 对应数组中的Qty
                    });
                }
            }
            */
            // 调用发送函数
            string result_out = SN_CheckOutRequest(
                Line, StationID, MachineID, Mold, OPID,
                TOKen, FixSN, apiUrl, sn, result, errCode,
                dcInfo, compList);

            return result_out;
        }

        // 调用示例
        public static void TestExample() {
            // 准备DC信息的三个数组（一一对应）
            string[] dcItems = { "FAI_5001", "FG_FAI_11_2", "FG_FAI_11_3" };
            string[] dcValues = { "0", "18.205", "18.213" };
            string[] dcResults = { "PASS", "PASS", "PASS" };

            // 准备组件信息的两个数组（一一对应）
            string[] compIds = { "#10", "#20", "#30" };
            string[] compQtys = { "10", "20", "30" };

            // 调用方法
            string result = ExampleUsage_List(
                Line: "F-PA-02",
                StationID: "OQC_AVI",
                MachineID: "F-02-M9-AV-01",
                Mold: "",
                OPID: "12280738",
                TOKen: "",
                FixSN: "",
                apiUrl: "https://your-mes-server.com/api/upload",
                sn: "CCAE173002551+CCQA05309+0",
                result: "FAIL",
                errCode: "FG_FAI_11_3",
                dcItems: dcItems,
                dcValues: dcValues,
                dcResults: dcResults,
                compIds: compIds,
                compQtys: compQtys
            );

            Console.WriteLine(result);
        }


        /******************************************** 0209更新超时上传设置 *********************************************************/
        // 设置超时时间（这里示例设置为30秒，你可以根据实际需求调整）
        private static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(5);

        public static string SN_FileUpload_0209(string Line, string StationID, string MachineID, string OPID, string sn,
            string FileName, string FilePath, string apiUrl) {
            // 调用发送函数
            string result =
                SendSN_FileUploadRequest_0209(Line, StationID, MachineID, OPID, sn, FileName, FilePath, apiUrl);

            return result;
        }

        public static string SendSN_FileUploadRequest_0209(
            string Line, string StationID, string MachineID, string OPID,
            string sn, string FileName, string FilePath, string apiUrl) {
            try {
                // 构建上传数据模型
                var uploadData = new SnFileUploadModel {
                    EventID = "SN_FileUpload",
                    Line = Line,
                    StationID = StationID,
                    MachineID = MachineID,
                    OPID = OPID,
                    // 给 SNList 赋值
                    SNList = new List<SNList> {
                        new SNList {
                            SN = sn,
                            FileInfo = new List<FileInfo> {
                                new FileInfo {
                                    FileName = FileName,
                                    FilePath = FilePath,
                                }
                            }
                        }
                    },
                    SendTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
                };

                // 序列化为JSON
                string json = JsonConvert.SerializeObject(uploadData, Formatting.Indented);

                // 同步发送HTTP请求
                using (var httpClient = new HttpClient()) {
                    // 1. 设置HttpClient的整体超时时间（核心修改点）
                    httpClient.Timeout = _requestTimeout;

                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = httpClient.PostAsync(apiUrl, content).Result;

                    if (response.IsSuccessStatusCode) {
                        return
                            $"Success: {response.StatusCode}, Response: {response.Content.ReadAsStringAsync().Result}";
                    }
                    else {
                        return $"Error: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                    }
                }
            }
            catch (TaskCanceledException ex) {
                // 单独捕获超时异常（TaskCanceledException包含超时场景）
                return
                    $"Timeout Error: Request timed out after {_requestTimeout.TotalSeconds} seconds. Details: {ex.Message}";
            }
            catch (Exception ex) {
                return
                    $"Timeout Error: Request timed out after {_requestTimeout.TotalSeconds} seconds. Details: {ex.Message}";
            }
        }


        public static string ExampleUsage_List_0209(
            string Line, string StationID, string MachineID, string Mold, string OPID,
            string TOKen, string FixSN, string apiUrl, string sn, string result, string errCode,
            // 新增数组参数：DC信息的Item、Value、Result数组（三者长度需一致）
            string[] dcItems, string[] dcValues, string[] dcResults,
            // 新增数组参数：组件的CompID和Qty数组（两者长度需一致）
            string[] compIds, string[] compQtys) {
            // 构建DC信息列表（通过数组动态生成）
            var dcInfo = new List<DcInfoItem>();
            if (dcItems != null && dcValues != null && dcResults != null) {
                // 取三个数组中长度最小的值作为循环次数，避免数组越界
                int dcCount = Math.Min(dcItems.Length, Math.Min(dcValues.Length, dcResults.Length));
                for (int i = 0; i < dcCount; i++) {
                    dcInfo.Add(new DcInfoItem {
                        Item = dcItems[i], // 对应数组中的Item
                        Value = dcValues[i], // 对应数组中的Value
                        Result = dcResults[i] // 对应数组中的Result
                    });
                }
            }

            // 构建组件列表（通过数组动态生成）
            var compList = new List<CompListItem>();
            /*
            if (compIds != null && compQtys != null)
            {
                // 取两个数组中长度最小的值作为循环次数，避免数组越界
                int compCount = Math.Min(compIds.Length, compQtys.Length);
                for (int i = 0; i < compCount; i++)
                {
                    compList.Add(new CompListItem
                    {
                        CompID = compIds[i],     // 对应数组中的CompID
                        Qty = compQtys[i]        // 对应数组中的Qty
                    });
                }
            }
            */
            // 调用发送函数
            string result_out = SN_CheckOutRequest_0209(
                Line, StationID, MachineID, Mold, OPID,
                TOKen, FixSN, apiUrl, sn, result, errCode,
                dcInfo, compList);

            return result_out;
        }


        public static string SN_CheckOutRequest_0209(
            string Line, string StationID, string MachineID, string Mold, string OPID,
            string TOKen, string FixSN,
            string apiUrl,
            string sn,
            string result,
            string errCode,
            List<DcInfoItem> dcInfoList,
            List<CompListItem> compList) {
            try {
                // 构建上传数据模型
                var uploadData = new SnCheckOutModel {
                    EventID = "SN_CheckOut",
                    Line = Line,
                    StationID = StationID,
                    MachineID = MachineID,
                    Mold = Mold,
                    OPID = OPID,
                    TOKen = TOKen,
                    FixSN = FixSN,
                    SNInfo = new List<SnInfoItem> {
                        new SnInfoItem {
                            SN = sn,
                            Result = result,
                            ErrCode = errCode,
                            DC_Info = dcInfoList,
                            CompList = compList
                        }
                    },
                    SendTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
                };

                // 序列化为JSON
                string json = JsonConvert.SerializeObject(uploadData, Formatting.Indented);

                // 同步发送HTTP请求
                using (var httpClient = new HttpClient()) {
                    // 1. 设置HttpClient的整体超时时间（核心修改点）
                    httpClient.Timeout = _requestTimeout;


                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = httpClient.PostAsync(apiUrl, content).Result;

                    if (response.IsSuccessStatusCode) {
                        return
                            $"Success: {response.StatusCode}, Response: {response.Content.ReadAsStringAsync().Result}";
                    }
                    else {
                        return $"Error: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                    }
                }
            }
            catch (TaskCanceledException ex) {
                // 单独捕获超时异常（TaskCanceledException包含超时场景）
                return
                    $"Timeout Error: Request timed out after {_requestTimeout.TotalSeconds} seconds. Details: {ex.Message}";
            }
            catch (Exception ex) {
                return
                    $"Timeout Error: Request timed out after {_requestTimeout.TotalSeconds} seconds. Details: {ex.Message}";
            }
        }


        /******************************************** 超时自动重传（最多3次）*********************************************************/

        // 内部哨兵值：标识单次请求发生了超时，不对外暴露
        private const string _timeoutMarker = "\x00TIMEOUT\x00";

        /// <summary>
        /// 执行单次 HTTP POST JSON 请求，区分"超时"与"其他结果"。
        /// </summary>
        /// <param name="apiUrl">目标接口地址。</param>
        /// <param name="json">已序列化的请求体 JSON 字符串。</param>
        /// <returns>
        /// 请求成功时返回 "Success: ..." 字符串；
        /// 服务端返回错误时返回 "Error: ..." 字符串；
        /// 请求超时时返回内部哨兵 <see cref="_timeoutMarker"/>（供重试方法判断）；
        /// 其他异常返回 "Exception: ..." 字符串。
        /// </returns>
        private static string TryPostOnce(string apiUrl, string json) {
            try {
                using (var httpClient = new HttpClient()) {
                    httpClient.Timeout = _requestTimeout;
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = httpClient.PostAsync(apiUrl, content).Result;

                    if (response.IsSuccessStatusCode) {
                        return $"Success: {response.StatusCode}, Response: {response.Content.ReadAsStringAsync().Result}";
                    }
                    return $"Error: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                }
            }
            catch (TaskCanceledException) {
                // 仅超时返回哨兵，由调用方决定是否重试
                return _timeoutMarker;
            }
            catch (Exception ex) {
                return $"Exception: {ex.Message}";
            }
        }

        private static void ArchiveFileUploadFailure(
            string sourceImagePath,
            string sn,
            int retryCount,
            string errorMessage,
            string apiUrl) {

            try {
                var failedImageArchiveService = new FailedImageArchiveService(FailedImageRootPath);
                failedImageArchiveService.ArchiveAfterMaxRetries(
                    sourceImagePath,
                    sn,
                    retryCount,
                    errorMessage,
                    apiUrl);
            }
            catch {
                // 失败图片归档不能影响上传接口的原有返回流程。
            }
        }

        /// <summary>
        /// 带超时自动重传的 SN_CheckOut 请求。
        /// 当请求超时时自动重试，总尝试次数最多 3 次（第 1 次 + 最多 2 次重试）。
        /// 任意一次成功则立即返回，不再继续重试；
        /// 3 次全部超时则返回明确的超时失败信息。
        /// 非超时的服务端错误或其他异常不重试，直接返回。
        /// </summary>
        /// <param name="Line">产线编号。</param>
        /// <param name="StationID">站点 ID。</param>
        /// <param name="MachineID">设备 ID。</param>
        /// <param name="Mold">治具编号。</param>
        /// <param name="OPID">操作人员 ID。</param>
        /// <param name="TOKen">认证 Token。</param>
        /// <param name="FixSN">修复序列号。</param>
        /// <param name="apiUrl">MES 系统 API 地址。</param>
        /// <param name="sn">序列号。</param>
        /// <param name="result">检测结果（PASS / FAIL）。</param>
        /// <param name="errCode">错误代码。</param>
        /// <param name="dcInfoList">DC 信息列表。</param>
        /// <param name="compList">组件列表。</param>
        /// <returns>
        /// 成功时返回 "Success: ..." 字符串；
        /// 服务端错误返回 "Error: ..." 字符串；
        /// 3 次全部超时返回 "Timeout Error: All 3 attempts timed out after Xs each."；
        /// 其他异常返回 "Exception: ..." 字符串。
        /// </returns>
        /// <remarks>
        /// 重试策略：最多 3 次，仅在 <see cref="TaskCanceledException"/>（超时）时重试。
        /// 超时阈值复用 <see cref="_requestTimeout"/>（当前 5 秒）。
        /// </remarks>
        public static string SN_CheckOutRequest_WithRetry(
            string Line, string StationID, string MachineID, string Mold, string OPID,
            string TOKen, string FixSN,
            string apiUrl, string sn, string result, string errCode,
            List<DcInfoItem> dcInfoList, List<CompListItem> compList) {

            var uploadData = new SnCheckOutModel {
                EventID    = "SN_CheckOut",
                Line       = Line,
                StationID  = StationID,
                MachineID  = MachineID,
                Mold       = Mold,
                OPID       = OPID,
                TOKen      = TOKen,
                FixSN      = FixSN,
                SNInfo     = new List<SnInfoItem> {
                    new SnInfoItem {
                        SN       = sn,
                        Result   = result,
                        ErrCode  = errCode,
                        DC_Info  = dcInfoList,
                        CompList = compList
                    }
                },
                SendTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
            };

            string json = JsonConvert.SerializeObject(uploadData, Formatting.Indented);

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++) {
                string response = TryPostOnce(apiUrl, json);
                if (response != _timeoutMarker)
                    return response;
                // 超时：更新 SendTime 后继续重试
                uploadData.SendTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                json = JsonConvert.SerializeObject(uploadData, Formatting.Indented);
            }

            return $"Timeout Error: All {maxAttempts} attempts timed out after {_requestTimeout.TotalSeconds}s each.";
        }

        /// <summary>
        /// 带超时自动重传的 SN_FileUpload 请求。
        /// 当请求超时时自动重试，总尝试次数最多 3 次（第 1 次 + 最多 2 次重试）。
        /// 任意一次成功则立即返回，不再继续重试；
        /// 3 次全部超时则返回明确的超时失败信息。
        /// 非超时的服务端错误或其他异常不重试，直接返回。
        /// </summary>
        /// <param name="Line">产线编号。</param>
        /// <param name="StationID">站点 ID。</param>
        /// <param name="MachineID">设备 ID。</param>
        /// <param name="OPID">操作人员 ID。</param>
        /// <param name="sn">序列号。</param>
        /// <param name="FileName">上传文件名。</param>
        /// <param name="FilePath">上传文件路径（MES 可访问的路径）。</param>
        /// <param name="apiUrl">MES 系统 API 地址。</param>
        /// <returns>
        /// 成功时返回 "Success: ..." 字符串；
        /// 服务端错误返回 "Error: ..." 字符串；
        /// 3 次全部超时返回 "Timeout Error: All 3 attempts timed out after Xs each."；
        /// 其他异常返回 "Exception: ..." 字符串。
        /// </returns>
        /// <remarks>
        /// 重试策略：最多 3 次，仅在 <see cref="TaskCanceledException"/>（超时）时重试。
        /// 超时阈值复用 <see cref="_requestTimeout"/>（当前 5 秒）。
        /// </remarks>
        public static string SN_FileUploadRequest_WithRetry(
            string Line, string StationID, string MachineID, string OPID,
            string sn, string FileName, string FilePath, string apiUrl) {

            var uploadData = new SnFileUploadModel {
                EventID   = "SN_FileUpload",
                Line      = Line,
                StationID = StationID,
                MachineID = MachineID,
                OPID      = OPID,
                SNList    = new List<SNList> {
                    new SNList {
                        SN       = sn,
                        FileInfo = new List<FileInfo> {
                            new FileInfo { FileName = FileName, FilePath = FilePath }
                        }
                    }
                },
                SendTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")
            };

            string json = JsonConvert.SerializeObject(uploadData, Formatting.Indented);

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++) {
                string response = TryPostOnce(apiUrl, json);
                if (response != _timeoutMarker)
                    return response;
                // 超时：更新 SendTime 后继续重试
                uploadData.SendTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                json = JsonConvert.SerializeObject(uploadData, Formatting.Indented);
            }

            string finalErrorMessage =
                $"Timeout Error: All {maxAttempts} attempts timed out after {_requestTimeout.TotalSeconds}s each.";
            ArchiveFileUploadFailure(FilePath, sn, maxAttempts, finalErrorMessage, apiUrl);
            return finalErrorMessage;
        }
    }
}

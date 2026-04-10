using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MyDll;
using static MyDll.ParameterManager;
using System.Drawing;
using System.Diagnostics;

namespace MainTestMydll
{
    internal class Program
    {
        static void PrintExceptionChain(Exception ex, int depth) {
            if (ex == null) return;
            string indent = new string(' ', depth * 2);
            Console.WriteLine($"{indent}[{ex.GetType().FullName}]");
            Console.WriteLine($"{indent}  Message: {ex.Message}");
            if (ex is AggregateException ae) {
                foreach (var inner in ae.Flatten().InnerExceptions)
                    PrintExceptionChain(inner, depth + 1);
            } else if (ex.InnerException != null) {
                PrintExceptionChain(ex.InnerException, depth + 1);
            }
        }

        // 启动一个本地 TCP 服务，接受连接但永不发送 HTTP 响应，使 HttpClient 触发超时。
        static string StartSilentServer(CancellationTokenSource cts) {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task.Run(() => {
                while (!cts.Token.IsCancellationRequested) {
                    try {
                        var client = listener.AcceptTcpClient(); // 接受连接但不回应
                        // 保持连接挂起，直到 HttpClient 超时
                        Task.Delay(60000, cts.Token).Wait(cts.Token);
                        client.Close();
                    } catch { break; }
                }
                listener.Stop();
            });
            return $"http://127.0.0.1:{port}/api";
        }

        static void Main(string[] args)
        {
            // ============================================================
            // 测试 1：超时重传 - SN_CheckOut（本地静默服务器，稳定触发超时）
            // ============================================================
            Console.WriteLine("========================================");
            Console.WriteLine("测试 1：SN_CheckOut 超时自动重传");
            Console.WriteLine("本地静默服务器，每次超时 5 秒，共 3 次，预计约 15 秒");
            Console.WriteLine("========================================");

            var cts1 = new CancellationTokenSource();
            string checkoutUrl = StartSilentServer(cts1);
            Console.WriteLine($"测试地址: {checkoutUrl}");

            var dcInfo = new List<DcInfoItem>
            {
                new DcInfoItem { Item = "FAI_5001", Value = "0",      Result = "PASS" },
                new DcInfoItem { Item = "FG_FAI_1", Value = "18.205", Result = "PASS" },
            };
            var compList = new List<CompListItem>
            {
                new CompListItem { CompID = "#10", Qty = "10" }
            };

            var sw = Stopwatch.StartNew();
            string checkoutResult = MesClass.SN_CheckOutRequest_WithRetry(
                Line:       "F-PA-02",
                StationID:  "OQC_AVI",
                MachineID:  "F-02-M9-AV-01",
                Mold:       "",
                OPID:       "12280738",
                TOKen:      "",
                FixSN:      "",
                apiUrl:     checkoutUrl,
                sn:         "SN_TEST_CHECKOUT_001",
                result:     "FAIL",
                errCode:    "TEST_ERR",
                dcInfoList: dcInfo,
                compList:   compList);
            sw.Stop();
            cts1.Cancel();

            Console.WriteLine($"结果: {checkoutResult}");
            Console.WriteLine($"耗时: {sw.Elapsed.TotalSeconds:F1} 秒（预期约 15 秒）");
            bool checkoutOk = checkoutResult.StartsWith("Timeout Error: All 3 attempts");
            Console.WriteLine($"判断: {(checkoutOk ? "[PASS] 正确触发 3 次重传" : "[FAIL] 未按预期重传")}");

            // ============================================================
            // 测试 2：超时重传 - SN_FileUpload（超时后触发归档）
            // ============================================================
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("测试 2：SN_FileUpload 超时重传 + 失败归档");
            Console.WriteLine("预计约 15 秒");
            Console.WriteLine("========================================");

            // 准备一个真实存在的测试图片文件
            string testImagePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "test_image.jpg");
            File.WriteAllBytes(testImagePath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }); // 最小 JPEG 头
            Console.WriteLine($"测试图片已创建: {testImagePath}");

            string archiveRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "FailedImages");
            MesClass.FailedImageRootPath = archiveRoot;

            var cts2 = new CancellationTokenSource();
            string uploadUrl = StartSilentServer(cts2);
            Console.WriteLine($"测试地址: {uploadUrl}");

            sw.Restart();
            string uploadResult = MesClass.SN_FileUploadRequest_WithRetry(
                Line:      "F-PA-02",
                StationID: "OQC_AVI",
                MachineID: "F-02-M9-AV-01",
                OPID:      "12280738",
                sn:        "SN_TEST_UPLOAD_001",
                FileName:  "test_image.jpg",
                FilePath:  testImagePath,
                apiUrl:    uploadUrl);
            sw.Stop();
            cts2.Cancel();

            Console.WriteLine($"结果: {uploadResult}");
            Console.WriteLine($"耗时: {sw.Elapsed.TotalSeconds:F1} 秒");
            bool uploadOk = uploadResult.StartsWith("Timeout Error: All 3 attempts");
            Console.WriteLine($"判断: {(uploadOk ? "[PASS] 正确触发 3 次重传 + 归档" : "[FAIL] 未按预期重传")}");

            // ============================================================
            // 测试 3：失败图片归档服务
            // ============================================================
            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("测试 3：FailedImageArchiveService 归档验证");
            Console.WriteLine("========================================");

            var archiveService = new FailedImageArchiveService(archiveRoot);

            // 3a：源文件存在 → 应成功复制
            Console.WriteLine("3a. 源文件存在:");
            archiveService.ArchiveAfterMaxRetries(
                sourceImagePath: testImagePath,
                sn:              "SN_ARCHIVE_001",
                retryCount:      3,
                errorMessage:    "Timeout Error: All 3 attempts timed out after 5s each.",
                apiUrl:          "http://10.255.255.1/api/upload");

            // 3b：源文件不存在 → 应只写日志不崩溃
            Console.WriteLine("3b. 源文件不存在（预期只写日志，不报错）:");
            archiveService.ArchiveAfterMaxRetries(
                sourceImagePath: @"D:\不存在的路径\fake_image.jpg",
                sn:              "SN_ARCHIVE_002",
                retryCount:      3,
                errorMessage:    "Timeout Error: All 3 attempts timed out after 5s each.",
                apiUrl:          "http://10.255.255.1/api/upload");

            // 验证归档结果
            Console.WriteLine();
            Console.WriteLine("归档目录内容:");
            string today = DateTime.Now.ToString("yyyyMMdd");
            string dayDir = Path.Combine(archiveRoot, today);
            if (Directory.Exists(dayDir))
            {
                // 列出日志文件
                foreach (var logFile in Directory.GetFiles(dayDir, "*.txt"))
                    Console.WriteLine($"  [日志] {Path.GetFileName(logFile)}");

                // 列出归档子目录
                foreach (var eventDir in Directory.GetDirectories(dayDir))
                {
                    Console.WriteLine($"  [目录] {Path.GetFileName(eventDir)}");
                    foreach (var f in Directory.GetFiles(eventDir))
                        Console.WriteLine($"    -> {Path.GetFileName(f)}");
                }

                bool hasLog  = Directory.GetFiles(dayDir, "*.txt").Length > 0;
                bool hasFile = false;
                foreach (var d in Directory.GetDirectories(dayDir))
                    if (Directory.GetFiles(d).Length > 0) { hasFile = true; break; }

                Console.WriteLine();
                Console.WriteLine($"判断: 日志存在={hasLog}, 图片已归档={hasFile}");
                Console.WriteLine(hasLog && hasFile ? "[PASS] 归档服务工作正常" : "[FAIL] 归档不完整");
            }
            else
            {
                Console.WriteLine($"  [FAIL] 归档目录不存在: {dayDir}");
            }

            Console.WriteLine();
            Console.WriteLine("========================================");
            Console.WriteLine("全部测试完成");
            Console.WriteLine("========================================");
        }
    }
}

using System;
using System.IO;
using System.Text;

namespace MyDll {
    /// <summary>
    /// 失败图片归档服务：集中处理失败图片复制、目录创建和失败日志追加。
    /// </summary>
    public sealed class FailedImageArchiveService {
        private const string DefaultFailedImageDirectoryName = "FailedImages";
        private static readonly object _copyLock = new object();
        private static readonly object _logLock = new object();

        private readonly string _failedImageRootPath;

        public FailedImageArchiveService(string failedImageRootPath) {
            _failedImageRootPath = ResolveRootPath(failedImageRootPath);
        }

        public string FailedImageRootPath {
            get { return _failedImageRootPath; }
        }

        /// <summary>
        /// 上传达到最大重试次数后归档失败图片，并追加失败日志。
        /// </summary>
        public void ArchiveAfterMaxRetries(
            string sourceImagePath,
            string sn,
            int retryCount,
            string errorMessage,
            string apiUrl) {

            DateTime failedTime = DateTime.Now;
            string dayText = failedTime.ToString("yyyyMMdd");
            string dayDirectory = Path.Combine(_failedImageRootPath, dayText);
            string archivePath = string.Empty;
            string finalErrorMessage = NormalizeForLog(errorMessage);

            try {
                Directory.CreateDirectory(dayDirectory);

                if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath)) {
                    string sourceMissingMessage = AppendMessage(
                        finalErrorMessage,
                        "Source image file does not exist.");

                    AppendFailureLogSafe(
                        dayDirectory,
                        failedTime,
                        sn,
                        sourceImagePath,
                        archivePath,
                        retryCount,
                        sourceMissingMessage,
                        apiUrl);

                    return;
                }

                string eventDirectory = Path.Combine(dayDirectory, failedTime.ToString("yyyyMMdd_HHmmss_fff"));
                Directory.CreateDirectory(eventDirectory);

                lock (_copyLock) {
                    archivePath = GetUniqueArchivePath(eventDirectory, sourceImagePath);
                    File.Copy(sourceImagePath, archivePath, false);
                }
            }
            catch (Exception ex) {
                finalErrorMessage = AppendMessage(
                    finalErrorMessage,
                    "Failed image archive internal error: " + NormalizeForLog(ex.ToString()));
            }

            AppendFailureLogSafe(
                dayDirectory,
                failedTime,
                sn,
                sourceImagePath,
                archivePath,
                retryCount,
                finalErrorMessage,
                apiUrl);
        }

        private static string ResolveRootPath(string failedImageRootPath) {
            if (!string.IsNullOrWhiteSpace(failedImageRootPath)) {
                return failedImageRootPath;
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultFailedImageDirectoryName);
        }

        private static string GetUniqueArchivePath(string eventDirectory, string sourceImagePath) {
            string fileName = Path.GetFileName(sourceImagePath);
            if (string.IsNullOrWhiteSpace(fileName)) {
                fileName = "unknown_image";
            }

            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string candidatePath = Path.Combine(eventDirectory, fileName);
            int index = 1;

            while (File.Exists(candidatePath)) {
                string numberedFileName = string.Format("{0}_{1}{2}", baseName, index, extension);
                candidatePath = Path.Combine(eventDirectory, numberedFileName);
                index++;
            }

            return candidatePath;
        }

        private static void AppendFailureLogSafe(
            string dayDirectory,
            DateTime failedTime,
            string sn,
            string sourceImagePath,
            string archivePath,
            int retryCount,
            string errorMessage,
            string apiUrl) {

            try {
                Directory.CreateDirectory(dayDirectory);
                string dayText = failedTime.ToString("yyyyMMdd");
                string logPath = Path.Combine(dayDirectory, string.Format("failed_upload_log_{0}.txt", dayText));
                string logEntry = BuildLogEntry(
                    failedTime,
                    sn,
                    sourceImagePath,
                    archivePath,
                    retryCount,
                    errorMessage,
                    apiUrl);

                lock (_logLock) {
                    File.AppendAllText(logPath, logEntry, Encoding.UTF8);
                }
            }
            catch {
                // 日志写入失败不能影响上传主流程。
            }
        }

        private static string BuildLogEntry(
            DateTime failedTime,
            string sn,
            string sourceImagePath,
            string archivePath,
            int retryCount,
            string errorMessage,
            string apiUrl) {

            var builder = new StringBuilder();
            builder.AppendLine("------------------------------------------------------------");
            builder.AppendLine("FailedTime: " + failedTime.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            builder.AppendLine("SN: " + NormalizeForLog(sn));
            builder.AppendLine("SourceImagePath: " + NormalizeForLog(sourceImagePath));
            builder.AppendLine("ArchivedImagePath: " + NormalizeForLog(archivePath));
            builder.AppendLine("RetryCount: " + retryCount);
            builder.AppendLine("ErrorMessage: " + NormalizeForLog(errorMessage));
            builder.AppendLine("ApiUrl: " + NormalizeForLog(apiUrl));
            builder.AppendLine();
            return builder.ToString();
        }

        private static string AppendMessage(string originalMessage, string appendedMessage) {
            if (string.IsNullOrWhiteSpace(originalMessage)) {
                return NormalizeForLog(appendedMessage);
            }

            if (string.IsNullOrWhiteSpace(appendedMessage)) {
                return NormalizeForLog(originalMessage);
            }

            return NormalizeForLog(originalMessage) + " | " + NormalizeForLog(appendedMessage);
        }

        private static string NormalizeForLog(string value) {
            if (string.IsNullOrWhiteSpace(value)) {
                return string.Empty;
            }

            return value.Replace("\r", " ").Replace("\n", " ");
        }
    }
}

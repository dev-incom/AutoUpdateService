using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Timers;
using Path = System.IO.Path;


namespace AutoUpdateService
{
    public partial class AutoUpdateService : ServiceBase
    {
        private readonly string downloadZipPath;
        private readonly string extractPath;
        private readonly string targetExePath;


        public AutoUpdateService()
        {
            InitializeComponent();


            string baseDir  = AppDomain.CurrentDomain.BaseDirectory;
            downloadZipPath = Path.Combine(baseDir, "update.zip");
            extractPath     = Path.Combine(baseDir, "update_extract");
            targetExePath   = Path.Combine(baseDir, "YourExecutable.exe");
        }


        protected override void OnStart(string[] args)
        {
            Console.WriteLine(ServiceName);
            Task.Run(() => CheckUpdate());
        }


        private async Task CheckUpdate()
        {
            try
            {
                // 1) version.xml 다운로드
                string versionXmlUrl = "https://your.server.com/version.xml";
                string localVersionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "version.xml");
                using (var client = new HttpClient())
                {
                    var xmlBytes = await client.GetByteArrayAsync(versionXmlUrl);
                    await WriteAllBytesAsync(localVersionFile, xmlBytes);
                }


                // 2) version.xml 파싱
                string newVersion = ParseVersionFromXml(localVersionFile);
                string currentVersion = GetCurrentVersion();

                // 3) 버전 비교
                if (!IsNewVersion(currentVersion, newVersion))
                {
                    EventLog.WriteEntry($"현재 버전이 최신입니다. 현재 {currentVersion}, 서버 {newVersion}");
                    return;
                }

                await DownloadZipAsync("https://your.server.com/update.zip", downloadZipPath);


                ExtractZip(downloadZipPath, extractPath);


                ReplaceExecutable(extractPath, targetExePath);


                RestartService();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry($"업데이트 중 오류: {ex.Message}");
            }
        }


        private async Task DownloadZipAsync(string url, string savePath)
        {
            using (var client = new HttpClient())
            {
                var data = await client.GetByteArrayAsync(url);
                //await File.WriteAllBytesAsync(savePath, data);
                await WriteAllBytesAsync(savePath, data);
                //using (var fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                //{
                //    await fs.WriteAsync(data, 0, data.Length);
                //    await fs.FlushAsync();
                //}

            }
        }

        private string ParseVersionFromXml(string path)
        {
            try
            {
                var doc = System.Xml.Linq.XDocument.Load(path);
                return doc.Root.Element("Version").Value.Trim();
            }
            catch
            {
                return "0.0.0.0";
            }
        }


        private string GetCurrentVersion()
        {
            try
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
            catch
            {
                return "0.0.0.0";
            }
        }


        private bool IsNewVersion(string currentVer, string serverVer)
        {
            try
            {
                Version cv = new Version(currentVer);
                Version sv = new Version(serverVer);
                return sv > cv;
            }
            catch
            {
                return false;
            }
        }

        private async Task WriteAllBytesAsync(string path, byte[] data)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await fs.WriteAsync(data, 0, data.Length);
                await fs.FlushAsync();
            }
        }

        private void ExtractZip(string zipPath, string extractTo)
        {
            if (Directory.Exists(extractTo))
                Directory.Delete(extractTo, true);


            Directory.CreateDirectory(extractTo);


            ZipFile.ExtractToDirectory(zipPath, extractTo);
        }


        private void ReplaceExecutable(string sourceDir, string targetExe)
        {
            string newExe = Path.Combine(sourceDir, Path.GetFileName(targetExe));
            string backupExe = targetExe + ".bak";
            string tempExe = targetExe + ".new";


            if (!File.Exists(newExe))
            {
                EventLog.WriteEntry("새 EXE 파일이 없습니다.");
                return;
            }


            if (File.Exists(tempExe)) File.Delete(tempExe);
            File.Copy(newExe, tempExe, true);


            if (File.Exists(targetExe))
            {
                File.Replace(tempExe, targetExe, backupExe);
            }
            else
            {
                File.Move(tempExe, targetExe);
            }
        }

        private void RestartService()
        {
            ServiceController sc = new ServiceController(ServiceName);
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));


            sc.Start();
        }


        protected override void OnStop() { }

        internal void TestStartupAndStop(string[] args)
        {
            Console.WriteLine($"Service Starting");
            this.OnStart(args);

        }
    }
}


/*
 * 
        private Timer _timer;


        public UpdateService()
        {
            ServiceName = "AutoUpdateService";
        }


        protected override void OnStart(string[] args)
        {
            _timer = new Timer(60000); // Check every 1 min
            _timer.Elapsed += CheckForUpdates;
            _timer.Start();
        }


        private void CheckForUpdates(object sender, ElapsedEventArgs e)
        {
            try
            {
                AutoUpdater.DownloadPath = @"C:\AutoUpdateService\Updates";
                AutoUpdater.InstallationPath = @"C:\AutoUpdateService";
                AutoUpdater.RunUpdateAsAdmin = true;
                AutoUpdater.CheckForUpdateEvent += AutoUpdaterOnCheckForUpdateEvent;
                AutoUpdater.Start("https://your-github-raw-link/update.xml");
            }
            catch (Exception ex)
            {
                // Handle logging
            }
        }


        //private void OnUpdateDownloaded(object sender, EventArgs e)
        //{
        //    try
        //    {
        //        string exePath = Path.Combine(AutoUpdater.DownloadPath, "UpdatedApp.exe");
        //        if (File.Exists(exePath))
        //        {
        //            Process.Start(exePath);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        // Handle logging
        //    }
        //}


        private async void AutoUpdaterOnCheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            if (args == null) return;


            if (args.IsUpdateAvailable && args.DownloadURL != null)
            {
                string downloadPath = Path.Combine(AutoUpdater.DownloadPath, "update.exe");


                try
                {
                    using (var http = new HttpClient())
                    {
                        var bytes = await http.GetByteArrayAsync(args.DownloadURL);
                        Directory.CreateDirectory(AutoUpdater.DownloadPath);
                        using (var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write,FileShare.None, 81920, useAsync: true))
                        {
                            await fs.WriteAsync(bytes, 0, bytes.Length);
                            await fs.FlushAsync();
                        }
                    }


                    if (File.Exists(downloadPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = downloadPath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    // logging 처리
                }
            }
        }


        protected override void OnStop()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }

        private void ExtractZipAndReplace(string zipPath, string extractPath)
        {
            try
            {
                if (Directory.Exists(extractPath))
                    Directory.Delete(extractPath, true);


                Directory.CreateDirectory(extractPath);


                ZipFile.ExtractToDirectory(zipPath, extractPath);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry($"ZIP 압축 해제 오류: {ex.Message}");
            }
        }

        private void ReplaceRunningExecutable(string sourceDir, string targetExePath)
        {
            try
            {
                string tempExe = targetExePath + ".new";
                string backupExe = targetExePath + ".bak";


                string newExePath = Path.Combine(sourceDir, Path.GetFileName(targetExePath));


                if (!File.Exists(newExePath))
                {
                    EventLog.WriteEntry($"새 실행 파일을 찾을 수 없습니다: {newExePath}");
                    return;
                }


                if (File.Exists(tempExe)) File.Delete(tempExe);
                File.Copy(newExePath, tempExe, true);

                // 대상 파일이 존재하면 File.Replace 사용 (원본은 삭제되고 대상은 백업됨)
                if (File.Exists(targetExePath))
                {
                    // File.Replace(sourceFileName, destinationFileName, destinationBackupFileName)
                    // source: tempExe, destination: targetExePath, backup: backupExe
                   File.Replace(tempExe, targetExePath, backupExe);
                }
                else
                {
                    // 대상이 없으면 단순 이동
                    File.Move(tempExe, targetExePath);
                }
                //File.Move(targetExePath, backupExe, true);
                //File.Move(tempExe, targetExePath);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry($"실행 파일 교체 오류: {ex.Message}");
            }
        }

 * 
 */
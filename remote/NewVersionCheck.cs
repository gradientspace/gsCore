// Copyright (c) Ryan Schmidt (rms@gradientspace.com) - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited. Proprietary and confidential.
using System;
using System.Text;
using System.Net;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics;
using f3;

namespace gs
{
    public interface VersionInfoSource
    {
        string VersionFileURL { get; }

        int MajorVersion { get; }
        int MinorVersion { get; }
        int BuildNumber { get; }
    }


    public interface NewVersionDownloadController
    {
        bool Completed { get; }
        void Cancel();
        void GetProgress(out long bytesReceived, out long bytesTotal, out int percentage);
    }


    /// <summary>
    /// This class implements version-checking based on a current-version file stored at a remote URL.
    /// Usage is basically:
    /// 
    ///    var check = new NewVersionCheck(version_info_object);
    ///    check.DoNewVersionCheck(new_version_action);
    ///    
    ///    new_version_action(download_url, force_install) {
    ///      if (force_install || user_wants_to_update_now) {
    ///         check.DownloadAndLaunchInstaller(download_url, quit_action);
    ///      }
    ///    }
    /// 
    /// The format of the version file is plain-text lines of the format:
    /// [platform] [version] [force] [url]
    /// 
    /// [platform] can be "win" or "osx"
    /// [version] is a 3-integer major.minor.build number, eg 1.2.307
    /// [force] can either be "force" or "noforce". This is just information for
    ///          your calling app code, NewVersionCheck does not actually force a call to DownloadAndLaunchInstaller()
    /// [url] link to installer binary
    /// 
    /// example:
    /// 
    /// win 1.1.2 force https://gradientspace.com/awesome_app/AwesomeInstaller_win_1p4p7.exe
    /// osx 1.0.1 noforce https://gradientspace.com/awesome_app/AwesomeInstaller_osx_1p4p7.dmg
    /// 
    /// </summary>
    public class NewVersionCheck
    {
        VersionInfoSource VersionInfo;

        Action<string, bool> OnNewVersionF;

        /// <summary>
        /// This will be called by internal functions if there is an error.
        /// Generally after an error we just abort/return, since this is background functionality
        /// </summary>
        public Action<string> OnErrorF = (code) => { };


        /// <summary>
        /// This action is called with the path to the downloaded installer.
        /// The default behavior is to launch this installer immediately.
        /// Replace this function if you would like to change this behavior.
        /// </summary>
        public Action<string> LaunchInstallerF = LaunchInstaller;


        /// <summary>
        /// Create a version-check object based on the version info you provide via NewVersionInfoSource
        /// </summary>
        public NewVersionCheck(VersionInfoSource info)
        {
            VersionInfo = info;
        }


        /// <summary>
        /// Launch version-file download and check against current version
        /// </summary>
        public void DoNewVersionCheck(Action<string, bool> onNewVersionF)
        {
            OnNewVersionF = onNewVersionF;

            Client.DownloadStringCompleted += OnDownloadStringCompleted;
            Client.DownloadStringAsync(new Uri(VersionInfo.VersionFileURL));
        }


        protected WebClient webclient;
        protected WebClient Client {
            get {
                if (webclient == null) {
                    webclient = new WebClient();
                    webclient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                    ServicePointManager.ServerCertificateValidationCallback = MyRemoteCertificateValidationCallback;
                }
                return webclient;
            }
        }



        private void OnDownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            Client.DownloadStringCompleted -= OnDownloadStringCompleted;

            if (e.Cancelled || e.Error != null) {
                DebugUtil.Log(2, "NewVersionCheck: string download returned error: " + e.Error.Message);
                return;
            }

            string[] lines = e.Result.Split(new char[] { '\n' });
            if (lines.Length < 1) {
                DebugUtil.Log(2, "NewVersionCheck: autoupdate text is formatted incorrectly: [{0}]", e.Result);
                return;
            }

            string platform = "win";
            switch (FPlatform.GetDeviceType()) {
                case FPlatform.fDeviceType.WindowsDesktop:
                    platform = "win";
                    break;
                case FPlatform.fDeviceType.OSXDesktop:
                    platform = "osx";
                    break;
                default:
                    platform = "unknown";
                    break;
            }
            if (platform == "unknown") {
                DebugUtil.Log(2, "NewVersionCheck: tried to run auto-update on unsupported platform {0}", FPlatform.GetDeviceType().ToString());
                return;
            }

            int nMajor = 0, nMinor = 0, nBuild = 0;
            bool force_update = true;
            string downloadURL = "";

            foreach (string line in lines) {
                string[] parts = line.Split(new char[] { ' ', '\r', '\n' });
                if (parts[0] != platform)
                    continue;
                if (parts.Length < 4)
                    continue;
                string[] version_tokens = parts[1].Split(new char[] { '.' });
                if (version_tokens.Length != 3)
                    continue;
                nMajor = int.Parse(version_tokens[0]);
                nMinor = int.Parse(version_tokens[1]);
                nBuild = int.Parse(version_tokens[2]);
                force_update = (parts[2] == "force");
                downloadURL = parts[3];
                break;
            }
            if (nMajor == 0 && nMinor == 0 && nBuild == 0) {
                DebugUtil.Log(2, "NewVersionCheck: could not find applicable platform string in auto-update file: [{0}]", e.Result);
                return;
            }

            DebugUtil.Log(2, "NewVersionCheck: current version is {0}.{1}.{2} force={3}, this build is {4}.{5}.{6}, url is {7}",
                nMajor, nMinor, nBuild, force_update, VersionInfo.MajorVersion, VersionInfo.MinorVersion, VersionInfo.BuildNumber, downloadURL);

            bool new_version_available =
                (nMajor > VersionInfo.MajorVersion) ||
                (nMajor == VersionInfo.MajorVersion && nMinor > VersionInfo.MinorVersion) ||
                (nMajor == VersionInfo.MajorVersion && nMinor == VersionInfo.MinorVersion && nBuild > VersionInfo.BuildNumber);

            if (new_version_available) {
                if (OnNewVersionF == null) {
                    DebugUtil.Log(2, "NewVersionCheck: no new-version handler was provided?");
                } else {
                    OnNewVersionF(downloadURL, force_update);
                }
            }
        }






        string TempFilePath;
        bool CancelInstall = false;
        Action QuitF;

        /// <summary>
        /// This function launches an update. First the installer is downloaded from the
        /// provided URL. When that is finished, QuitF() is called, and you should terminate
        /// your app here (eg call FPlatform.QuitApplication()). The installer will then be launched.
        /// 
        /// The returned object can be polled for progress events, and if you want
        /// to cancel the download, it has a .Cancel() function.
        /// </summary>
        public NewVersionDownloadController DownloadAndLaunchInstaller(string downloadURL, Action QuitF)
        {
            string[] tokens = downloadURL.Split(new char[] { '?' });
            string filename = Path.GetFileName(tokens[0]);

            TempFilePath = Path.Combine(Path.GetTempPath(), filename);
            DebugUtil.Log(2, "NewVersionCheck: downloading new version from {0} to {1}", downloadURL, TempFilePath);

            if (File.Exists(TempFilePath)) {
                try {
                    File.Delete(TempFilePath);
                } catch (Exception e) {
                    DebugUtil.Log(2, "NewVersionCheck: cannot delete existing file! {0}", e.Message);
                    OnErrorF?.Invoke("file_access_error");
                    return null;
                }
            }

            var controller = new InternalDownloadController() { Version = this };

            Client.DownloadProgressChanged += controller.DownloadProgressChanged;
            Client.DownloadFileCompleted += controller.OnDownloadCompleted;
            Client.DownloadFileCompleted += OnDownloadFileCompleted;
            Client.DownloadFileAsync(new Uri(downloadURL), TempFilePath);

            CancelInstall = false;
            this.QuitF = QuitF;
            return controller;
        }



        // allows caller to cancel download, track progress
        class InternalDownloadController : NewVersionDownloadController
        {
            public NewVersionCheck Version = null;

            bool completed = false;
            long received = 0, total = 1;
            int percentage = 0;
            public void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
            {
                received = e.BytesReceived;
                total = e.TotalBytesToReceive;
                percentage = e.ProgressPercentage;
            }
            public void OnDownloadCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
            {
                completed = true;
                Version.Client.DownloadFileCompleted -= this.OnDownloadCompleted;
                Version.Client.DownloadProgressChanged -= this.DownloadProgressChanged;
            }

            public bool Completed {
                get { return completed; }
            }

            public void Cancel() {
                Version.CancelInstall = true;
                Version.Client.CancelAsync();
            }
            public void GetProgress(out long bytesReceived, out long bytesTotal, out int percent)
            {
                bytesReceived = received;
                bytesTotal = total;
                percent = this.percentage;
            }
        }


        // called once download finishes, to launch installer
        private void OnDownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            Client.DownloadFileCompleted -= OnDownloadFileCompleted;
            if (CancelInstall)
                return;

            bool failed = false;
            if (e.Cancelled || e.Error != null) {
                DebugUtil.Log(2, "NewVersionCheck: file download returned error: " + e.Error.Message);
                failed = true;
            }
            if (!File.Exists(TempFilePath)) {
                DebugUtil.Log(2, "NewVersionCheck: Client did not return error, but output file {0} does not exist", TempFilePath);
                failed = true;
            }
            if (failed) {
                OnErrorF?.Invoke("download_failed");
                return;
            }

            DebugUtil.Log(2, "NewVersionCheck: download completed. Launching installer...");

            // tell caller to quit
            QuitF?.Invoke();

            LaunchInstallerF(TempFilePath);
        }


        
        public static void LaunchInstaller(string filePath)
        {
            Process p = new Process();
            p.StartInfo.FileName = filePath;
            p.StartInfo.UseShellExecute = true;
            p.Start();
        }




        // code from: https://stackoverflow.com/questions/4926676/mono-https-webrequest-fails-with-the-authentication-or-decryption-has-failed
        public static bool MyRemoteCertificateValidationCallback(System.Object sender,
            X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            bool isOk = true;
            // If there are errors in the certificate chain,
            // look at each error to determine the cause.
            if (sslPolicyErrors != SslPolicyErrors.None) {
                for (int i = 0; i < chain.ChainStatus.Length; i++) {
                    if (chain.ChainStatus[i].Status == X509ChainStatusFlags.RevocationStatusUnknown) {
                        continue;
                    }
                    chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
                    chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllFlags;
                    bool chainIsValid = chain.Build((X509Certificate2)certificate);
                    if (!chainIsValid) {
                        isOk = false;
                        break;
                    }
                }
            }
            return isOk;
        }

    }
}

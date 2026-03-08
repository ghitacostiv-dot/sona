using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace SONA.Services
{
    public static class WebViewService
    {
        private static CoreWebView2Environment? _environment;

        public static async Task InitializeWebViewAsync(WebView2 webView)
        {
            if (_environment == null)
            {
                var userDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SONA_Data", "WebView2");
                Directory.CreateDirectory(userDataFolder);

                var options = new CoreWebView2EnvironmentOptions(
                    additionalBrowserArguments: "--disable-web-security --enable-gpu-rasterization --ignore-gpu-blocklist --enable-zero-copy --enable-hardware-overlays --enable-features=SharedArrayBuffer --autoplay-policy=no-user-gesture-required --disable-features=WebContentsForceDark --max-connections-per-host=20 --enable-quic --num-raster-threads=4"
                );

                _environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            }

            await webView.EnsureCoreWebView2Async(_environment);
            
            // Disable forced dark mode if it's set in the profile (WebView2 1.0.1210.39+)
            try {
                webView.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Light;
            } catch { }
        }

        public static async Task InjectCssAsync(WebView2 webView, string css)
        {
            var escapedCss = css.Replace("'", "\\'").Replace("\n", " ");
            var script = $"(function() {{ " +
                         $"var style = document.createElement('style'); " +
                         $"style.innerHTML = '{escapedCss}'; " +
                         $"document.head.appendChild(style); " +
                         $"}})();";
            await webView.ExecuteScriptAsync(script);
        }
    }
}

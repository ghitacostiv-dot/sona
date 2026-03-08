using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SONA.Services;
using SONA.Controls;

namespace SONA
{
    public class EthicalHackingPage : UserControl
    {
        private readonly Dictionary<string, (string Category, Action<TextBox?> Action)> _tools = new();
        private TextBox? _outputBox;
        private TextBox? _inputBox;
        private readonly ContentControl _contentArea = new();
        private RadioButton _tabToolkit = new();
        private RadioButton _tabTerminal = new();
        private RadioButton _tabResources = new();
        private TextBox? _toolSearchBox;
        private Canvas? _matrixCanvas;
        private Grid _hostContainer = new();
        private NativeWindowHost? _host;
        private DispatcherTimer? _matrixTimer;
        private List<MatrixColumn> _matrixColumns = new();
        private Random _rnd = new();
        
        // Cyberpunk Cyan/Blue Accent
        private readonly SolidColorBrush _accentBrush = new(Color.FromRgb(0x0e, 0xb8, 0xeb)); 
        private readonly SolidColorBrush _textBrush = new(Color.FromRgb(0xda, 0xe4, 0xee)); 
        private readonly SolidColorBrush _dimBrush = new(Color.FromRgb(0x6b, 0x72, 0x80)); 

        public EthicalHackingPage(MainWindow mainWindow)
        {
            this.SetResourceReference(BackgroundProperty, "BgBrush");
            RegisterTools();

            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(450) });

            // LEFT PANEL
            var leftDock = new DockPanel();
            
            var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 24, 24, 0) };
            _tabToolkit = CreateTabButton("Toolkit", true);
            _tabTerminal = CreateTabButton("Terminal", false);
            _tabResources = CreateTabButton("Resources", false);
            
            tabRow.Children.Add(_tabToolkit);
            tabRow.Children.Add(_tabTerminal);
            tabRow.Children.Add(_tabResources);
 
            leftDock.Children.Add(tabRow);
            DockPanel.SetDock(tabRow, Dock.Top);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = _contentArea;
            
            var leftOverlayGrid = new Grid();
            leftOverlayGrid.Children.Add(scroll);
            
            _hostContainer = new Grid { Visibility = Visibility.Collapsed, Background = Brushes.Black };
            leftOverlayGrid.Children.Add(_hostContainer);
            
            leftDock.Children.Add(leftOverlayGrid);

            Grid.SetColumn(leftDock, 0);
            rootGrid.Children.Add(leftDock);

            // RIGHT PANEL
            var rightBorder = new Border { BorderThickness = new Thickness(1, 0, 0, 0) };
            rightBorder.SetResourceReference(Border.BackgroundProperty, "Bg2Brush");
            rightBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var sideDock = new DockPanel();
            
            var sideHeader = new Border { Background = new SolidColorBrush(Color.FromRgb(5, 10, 20)), Padding = new Thickness(12) };
            sideHeader.Child = new TextBlock { Text = "SYSTEM_LOG :: v2.0_NEXUS", Foreground = _accentBrush, FontSize = 13, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Consolas") };
            DockPanel.SetDock(sideHeader, Dock.Top);
            sideDock.Children.Add(sideHeader);

            _outputBox = new TextBox
            {
                Background = Brushes.Transparent,
                Foreground = _accentBrush,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(12),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                AcceptsReturn = true
            };
            sideDock.Children.Add(_outputBox);
            rightBorder.Child = sideDock;
            Grid.SetColumn(rightBorder, 1);
            rootGrid.Children.Add(rightBorder);

            Content = rootGrid;

            // Init Matrix AFTER the Content grid is set
            this.Loaded += (_, _) => InitMatrix(rootGrid);

            _tabToolkit.Checked += (_, _) => { if (IsLoaded) { CloseHost(); ShowToolkitView(); } };
            _tabTerminal.Checked += (_, _) => { if (IsLoaded) { CloseHost(); ShowTerminalView(); } };
            _tabResources.Checked += (_, _) => { if (IsLoaded) { CloseHost(); ShowResourcesView(); } };
            ShowToolkitView();
        }

        private RadioButton CreateTabButton(string text, bool isActive) => new RadioButton { Content = text, Style = (Style)Application.Current.FindResource("PageTab"), GroupName = "HackingTabs", IsChecked = isActive };

        private void ShowToolkitView()
        {
            var stack = new StackPanel { Margin = new Thickness(24) };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
            var icon = IconHelper.Img("nav/hacking", 40);
            icon.Margin = new Thickness(0, 0, 16, 0);
            headerRow.Children.Add(icon);
            headerRow.Children.Add(new TextBlock { Text = "Network Operations", Foreground = Brushes.White, FontSize = 32, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(headerRow);
            stack.Children.Add(new TextBlock { Text = "Execute system commands, mapping, and external reconnaissance.", Foreground = Brushes.Gray, FontSize = 14, Margin = new Thickness(0, 0, 0, 32) });

            var inputGrid = new Grid { Margin = new Thickness(0, 0, 0, 32) };
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            
            var inputStack = new StackPanel { Margin = new Thickness(0,0,16,0) };
            inputStack.Children.Add(new TextBlock { Text = "TARGET (IP / DOMAIN / HASH)", Foreground = _dimBrush, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(4, 0, 0, 8) });
            _inputBox = new TextBox
            {
                Style = (Style)Application.Current.FindResource("SearchBox"),
                Height = 44,
                VerticalContentAlignment = VerticalAlignment.Center,
                Text = "127.0.0.1",
                Foreground = _accentBrush,
                FontFamily = new FontFamily("Consolas"),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
                Padding = new Thickness(16, 0, 16, 0)
            };
            inputStack.Children.Add(_inputBox);
            Grid.SetColumn(inputStack, 0);
            inputGrid.Children.Add(inputStack);

            var searchStack = new StackPanel();
            searchStack.Children.Add(new TextBlock { Text = "FILTER TOOLS", Foreground = _dimBrush, FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(4, 0, 0, 8) });
            _toolSearchBox = new TextBox
            {
                Style = (Style)Application.Current.FindResource("SearchBox"),
                Height = 44,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = "Search Tools...",
                Foreground = _textBrush,
                FontFamily = new FontFamily("Consolas"),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
                Padding = new Thickness(16, 0, 16, 0)
            };
            _toolSearchBox.TextChanged += (_, _) => ShowToolkitView(); // simple re-render
            searchStack.Children.Add(_toolSearchBox);
            Grid.SetColumn(searchStack, 1);
            inputGrid.Children.Add(searchStack);
            
            stack.Children.Add(inputGrid);

            var filter = _toolSearchBox?.Text?.ToLower() ?? "";
            var wrap = new WrapPanel();
            var cats = _tools.Values.Select(t => t.Category).Distinct().OrderBy(c => c).ToList();

            foreach (var cat in cats)
            {
                var catTools = _tools.Where(x => x.Value.Category == cat && 
                    (string.IsNullOrEmpty(filter) || x.Key.ToLower().Contains(filter)));
                
                if (!catTools.Any()) continue;

                var catLabel = new Border { Background = new SolidColorBrush(Color.FromArgb(20, 14, 184, 235)), Padding = new Thickness(16, 8, 16, 8), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 32, 0, 16), Width = double.NaN, HorizontalAlignment = HorizontalAlignment.Left };
                var catTitle = new StackPanel { Orientation = Orientation.Horizontal };
                catTitle.Children.Add(new TextBlock { Text = GetCategoryIcon(cat), Margin = new Thickness(0, 0, 8, 0) });
                catTitle.Children.Add(new TextBlock { Text = cat.ToUpper(), Foreground = _accentBrush, FontWeight = FontWeights.Bold, FontSize = 14 });
                catLabel.Child = catTitle;
                wrap.Children.Add(catLabel);
                
                // Break wrap panel to new line after header
                wrap.Children.Add(new Border { Width = 2000, Height = 1 });

                foreach (var kv in catTools)
                {
                    var btn = new Button
                    {
                        Content = kv.Key,
                        Style = (Style)Application.Current.FindResource("DarkBtn"),
                        Margin = new Thickness(0, 0, 12, 12),
                        Padding = new Thickness(20, 10, 20, 10),
                        Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x14)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x26)),
                        Foreground = _textBrush,
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold
                    };
                    var key = kv.Key;
                    btn.MouseEnter += (_, _) => { btn.BorderBrush = _accentBrush; btn.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x1d, 0x24)); btn.Foreground = Brushes.White; };
                    btn.MouseLeave += (_, _) => { btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x26)); btn.Background = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x14)); btn.Foreground = _textBrush; };
                    btn.Click += (_, _) => ExecuteTool(key);
                    wrap.Children.Add(btn);
                }
            }
            stack.Children.Add(wrap);
            _contentArea.Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private string GetCategoryIcon(string cat) => cat switch {
            "Network" => "\ud83c\udf10",
            "System"  => "\ud83d\udda5",
            "Web"     => "\ud83d\udd78",
            "Recon"   => "\ud83d\udd0d",
            "Crypto"  => "\ud83d\udd10",
            "Utils"   => "\ud83d\udee0",
            _         => "\u2699"
        };

        private void ExecuteTool(string key)
        {
            try { Out($"[{DateTime.Now:HH:mm:ss}] > EXECUTING: {key}..."); _tools[key].Action(_inputBox); }
            catch (Exception ex) { Out($"[!] CRITICAL_ERROR: {ex.Message}"); }
        }

        private void ShowTerminalView()
        {
            var grid = new Grid();
            var bigOutput = new TextBox { Background = Brushes.Black, Foreground = _accentBrush, FontFamily = new FontFamily("Consolas"), FontSize = 14, IsReadOnly = true, BorderThickness = new Thickness(0), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0), AcceptsReturn = true, Text = _outputBox?.Text ?? "" };
            bigOutput.TextChanged += (s, e) => { if (_outputBox != null) _outputBox.Text = bigOutput.Text; bigOutput.ScrollToEnd(); };
            grid.Children.Add(bigOutput);
            _contentArea.Content = grid;
        }

        private void ShowResourcesView()
        {
            var stack = new StackPanel { Margin = new Thickness(24) };
            stack.Children.Add(new TextBlock { Text = "Operational Intel", Foreground = Brushes.White, FontSize = 32, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 24) });
            var resources = new (string Name, string Desc, string Url)[] {
                ("OWASP Top 10", "Web application security standards.", "https://owasp.org/www-project-top-ten/"),
                ("MITRE ATT&CK", "Globally-accessible knowledge base of adversary tactics.", "https://attack.mitre.org/"),
                ("NVD (NIST)", "U.S. vulnerability management data.", "https://nvd.nist.gov/"),
                ("Exploit-DB", "Archive of public exploits.", "https://www.exploit-db.com/"),
                ("HackerOne", "Bug bounty platform for professional researchers.", "https://www.hackerone.com/"),
                ("Bugcrowd", "Crowdsourced security testing.", "https://www.bugcrowd.com/"),
                ("Hack The Box", "Penetration testing lab.", "https://www.hackthebox.com/"),
                ("TryHackMe", "Cyber security training.", "https://tryhackme.com/"),
                ("PentesterLab", "Web security learning.", "https://pentesterlab.com/"),
                ("SANS Institute", "Information security training.", "https://www.sans.org/")
            };
            foreach (var res in resources) {
                var card = new Border { Background = new SolidColorBrush(Color.FromRgb(15, 15, 15)), CornerRadius = new CornerRadius(8), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 16), Cursor = Cursors.Hand, BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30)) };
                var inner = new StackPanel();
                inner.Children.Add(new TextBlock { Text = res.Name, Foreground = _accentBrush, FontSize = 18, FontWeight = FontWeights.Bold });
                inner.Children.Add(new TextBlock { Text = res.Desc, Foreground = Brushes.Gray, FontSize = 14, Margin = new Thickness(0, 4, 0, 0) });
                card.Child = inner;
                card.MouseEnter += (_, _) => card.BorderBrush = _accentBrush;
                card.MouseLeave += (_, _) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 30));
                card.MouseLeftButtonDown += (_, _) => Process.Start(new ProcessStartInfo(res.Url) { UseShellExecute = true });
                stack.Children.Add(card);
            }
            _contentArea.Content = new ScrollViewer { Content = stack };
        }

        private void Out(string msg) => Dispatcher.Invoke(() => { _outputBox!.AppendText(msg + "\n"); _outputBox.ScrollToEnd(); });
        private void ClearOut() => Dispatcher.Invoke(() => _outputBox!.Clear());

        private void RegisterTools()
        {
            // PRO TOOLS (Auto-Install via Winget)
            _tools["Launch Nmap"] = ("Network", _ => 
            {
                var exe = @"C:\Program Files (x86)\Nmap\zenmap.exe";
                if (System.IO.File.Exists(exe)) LaunchEmbedded(exe);
                else { Out("[*] Nmap not found. Initiating silent install..."); Task.Run(async () => await AppManagerService.InstallPackageAsync("Insecure.Nmap")); }
            });

            _tools["Launch Wireshark"] = ("Network", _ => 
            {
                var exe = @"C:\Program Files\Wireshark\Wireshark.exe";
                if (System.IO.File.Exists(exe)) LaunchEmbedded(exe);
                else { Out("[*] Wireshark not found. Initiating silent install..."); Task.Run(async () => await AppManagerService.InstallPackageAsync("WiresharkFoundation.Wireshark")); }
            });

            // NETWORK (25+)
            // NETWORK (20+)
            _tools["Ping"] = ("Network", _ => { try { var r = new Ping().Send(_inputBox!.Text.Trim()); Out($"[PING] Reply from {_inputBox!.Text}: {r?.Status} ({r?.RoundtripTime}ms)"); } catch (Exception ex) { Out("[!] " + ex.Message); } });
            _tools["Traceroute"] = ("Network", _ => RunCmd("tracert " + _inputBox!.Text.Trim(), Out));
            _tools["NSLookup"] = ("Network", _ => { try { var h = Dns.GetHostEntry(_inputBox!.Text.Trim()); Out("[DNS] " + string.Join(", ", h.AddressList.Select(a => a.ToString()))); } catch (Exception ex) { Out("[!] " + ex.Message); } });
            _tools["IP Configuration"] = ("Network", _ => { var sb = new StringBuilder(); foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) { foreach (var ua in ni.GetIPProperties().UnicastAddresses) sb.AppendLine($"[{ni.Name}] {ua.Address}"); } Out(sb.ToString()); });
            _tools["Netstat Connections"] = ("Network", _ => RunCmd("netstat -an", Out));
            _tools["Flush DNS Cache"] = ("Network", _ => RunCmd("ipconfig /flushdns", Out));
            _tools["Active Ports (TCP)"] = ("Network", _ => RunCmd("netstat -p tcp", Out));
            _tools["Active Ports (UDP)"] = ("Network", _ => RunCmd("netstat -p udp", Out));
            _tools["Adapter Stats"] = ("Network", _ => RunCmd("netstat -e", Out));
            _tools["Port Scan (Common)"] = ("Network", async _ => { int[] ps = { 80, 443, 21, 22, 3389, 8080 }; foreach (var p in ps) { try { using var c = new System.Net.Sockets.TcpClient(); var t = c.ConnectAsync(_inputBox!.Text, p); if (await Task.WhenAny(t, Task.Delay(1000)) == t) { await t; Out($"[+] Port {p} OPEN"); } else Out($"[-] Port {p} CLOSED"); } catch { Out($"[-] Port {p} CLOSED"); } } });
            _tools["Get MAC Address"] = ("Network", _ => RunCmd("getmac", Out));
            _tools["Pathping"] = ("Network", _ => RunCmd("pathping " + _inputBox!.Text.Trim(), Out));
            _tools["Proxy Settings"] = ("Network", _ => RunCmd("netsh winhttp show proxy", Out));
            _tools["WLAN Profiles"] = ("Network", _ => RunCmd("netsh wlan show profiles", Out));
            _tools["WLAN Passwords"] = ("Network", _ => RunCmd("netsh wlan show profiles name=* key=clear", Out));
            _tools["DNS Server List"] = ("Network", _ => RunCmd("netsh int ip show dns", Out));
            _tools["Interface Stats"] = ("Network", _ => RunCmd("netsh interface ip show statistics", Out));
            _tools["ARP Table"] = ("Network", _ => RunCmd("arp -a", Out));
            _tools["Route Print"] = ("Network", _ => RunCmd("route print", Out));

            // SYSTEM (20+)
            _tools["Running Processes"] = ("System", _ => RunCmd("tasklist /v", Out));
            _tools["System Metadata"] = ("System", _ => RunCmd("systeminfo", Out));
            _tools["Local User List"] = ("System", _ => RunCmd("net user", Out));
            _tools["Local Groups"] = ("System", _ => RunCmd("net localgroup", Out));
            _tools["OS Hostname"] = ("System", _ => Out($"[OS] {Environment.MachineName} ({Environment.OSVersion})"));
            _tools["Startup Commands"] = ("System", _ => RunCmd("wmic startup get caption,command", Out));
            _tools["Environment Vars"] = ("System", _ => { var sb = new StringBuilder(); foreach (System.Collections.DictionaryEntry de in Environment.GetEnvironmentVariables()) sb.AppendLine($"{de.Key} = {de.Value}"); Out(sb.ToString()); });
            _tools["Disk Usage"] = ("System", _ => RunCmd("wmic logicaldisk get caption,description,freespace,size", Out));
            _tools["CPU Topology"] = ("System", _ => RunCmd("wmic cpu get name,numberofcores,maxclockspeed", Out));
            _tools["Memory Stats"] = ("System", _ => RunCmd("wmic OS get FreePhysicalMemory,TotalVisibleMemorySize /Value", Out));
            _tools["Driver Query"] = ("System", _ => RunCmd("driverquery", Out));
            _tools["Installed Software"] = ("System", _ => RunCmd("wmic product get name,version", Out));
            _tools["Active Services"] = ("System", _ => RunCmd("sc query state= all", Out));
            _tools["Registry Registry"] = ("System", _ => RunCmd("reg query HKLM\\Software", Out));
            _tools["Security Patches"] = ("System", _ => RunCmd("wmic qfe list brief", Out));
            _tools["Bios Version"] = ("System", _ => RunCmd("wmic bios get serialnumber,smbiosbiosversion", Out));
            _tools["Motherboard Info"] = ("System", _ => RunCmd("wmic baseboard get product,manufacturer", Out));
            _tools["GPU Details"] = ("System", _ => RunCmd("wmic path win32_VideoController get name", Out));
            _tools["User Privileges"] = ("System", _ => RunCmd("whoami /priv", Out));
            _tools["Group Memberships"] = ("System", _ => RunCmd("whoami /groups", Out));
            _tools["Schedule Tasks"] = ("System", _ => RunCmd("schtasks /query /fo LIST", Out));
            _tools["Power Config"] = ("System", _ => RunCmd("powercfg /list", Out));
            _tools["System Drivers"] = ("System", _ => RunCmd("wmic sysdriver get name,status", Out));
            _tools["Volume Info"] = ("System", _ => RunCmd("wmic volume list brief", Out));
            _tools["Event Log (Security)"] = ("System", _ => RunCmd("wevtutil qe Security /c:10 /f:text", Out));
            _tools["Hotfixes"] = ("System", _ => RunCmd("wmic qfe list brief", Out));
            _tools["Network Adapter Config"] = ("System", _ => RunCmd("wmic nicconfig get caption,ipaddress,macaddress", Out));

            // WEB (15+)
            _tools["HTTP Headers"] = ("Web", async _ => { try { using var h = new HttpClient(); var r = await h.GetAsync(_inputBox!.Text); Out($"[WEB] Headers:\n{r.Headers}"); } catch (Exception ex) { Out("[!] " + ex.Message); } });
            _tools["SSL Check"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://www.ssllabs.com/ssltest/analyze.html?d=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["Wayback Machine"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://web.archive.org/web/*/" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["DNS Checker"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://dnschecker.org/#A/" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["What CMS"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://whatcms.org/?s=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["BuiltWith Tech"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://builtwith.com/" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["Subdomain Finder"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://subdomainfinder.c99.nl/index.php?domain=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["Security Headers"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://securityheaders.com/?q=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["robots.txt Scrape"] = ("Web", async _ => { try { using var h = new HttpClient(); var url = _inputBox!.Text.Trim(); if(!url.StartsWith("http")) url = "https://" + url; var r = await h.GetStringAsync(url.TrimEnd('/') + "/robots.txt"); Out($"[ROBOTS.TXT]\n{r}"); } catch { Out("[!] No robots.txt found."); } });
            _tools["Sitemap Scrape"] = ("Web", async _ => { try { using var h = new HttpClient(); var url = _inputBox!.Text.Trim(); if(!url.StartsWith("http")) url = "https://" + url; var r = await h.GetStringAsync(url.TrimEnd('/') + "/sitemap.xml"); Out($"[SITEMAP.XML]\n{r.Substring(0, Math.Min(r.Length, 1000))}..."); } catch { Out("[!] No sitemap.xml found."); } });
            _tools["DNS Dumpster"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://dnsdumpster.com/") { UseShellExecute = true }));
            _tools["crt.sh Logs"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://crt.sh/?q=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["Pentest-Tools"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://pentest-tools.com/") { UseShellExecute = true }));
            _tools["SecurityTrails"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://securitytrails.com/domain/" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["Page Speed Insights"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://pagespeed.web.dev/report?url=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["W3C Validator"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://validator.w3.org/nu/?doc=https%3A%2F%2F" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["SecurityHeaders.io"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://securityheaders.com/?q=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["HSTS Preload"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://hstspreload.org/?domain=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["Cloudflare Check"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://www.cloudflare.com/diagnostic-center/?url=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["ImmuniWeb SSL"] = ("Web", _ => Process.Start(new ProcessStartInfo("https://www.immuniweb.com/ssl/?id=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));

            // RECON (20+)
            _tools["CVE Search (MITRE)"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://cve.mitre.org/cgi-bin/cvename.cgi?name=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["VirusTotal Scan"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://www.virustotal.com/gui/search/" + Uri.EscapeDataString(_inputBox!.Text.Trim())) { UseShellExecute = true }));
            _tools["Shodan Recon"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://www.shodan.io/search?query=" + Uri.EscapeDataString(_inputBox!.Text.Trim())) { UseShellExecute = true }));
            _tools["Censys Search"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://search.censys.io/search?resource=hosts&q=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["GreyNoise Intel"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://viz.greynoise.io/ip/" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["Have I Been Pwned"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://haveibeenpwned.com/") { UseShellExecute = true }));
            _tools["IntelX Search"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://intelx.io/?s=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["Public Buckets"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://grayhatwarfare.com/buckets?q=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["GitHub Dorking"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://github.com/search?q=" + Uri.EscapeDataString(_inputBox!.Text.Trim()) + " \"password\"") { UseShellExecute = true }));
            _tools["Google Dorking"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://www.google.com/search?q=site:" + _inputBox!.Text.Trim() + " intitle:index.of") { UseShellExecute = true }));
            _tools["Hunter.io Emails"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://hunter.io/search/" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["BinaryEdge"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://www.binaryedge.io/") { UseShellExecute = true }));
            _tools["Wiggle WiFi"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://wigle.net/") { UseShellExecute = true }));
            _tools["Onyphe.io"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://www.onyphe.io/") { UseShellExecute = true }));
            _tools["Zoomeye Search"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://www.zoomeye.org/") { UseShellExecute = true }));
            _tools["Spyse Intel"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://spyse.com/") { UseShellExecute = true }));
            _tools["FullHunt.io"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://fullhunt.io/") { UseShellExecute = true }));
            _tools["AlienVault OTX"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://otx.alienvault.com/indicator/ip/" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["AbuseIPDB"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://www.abuseipdb.com/check/" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["Criminal IP"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://www.criminalip.io/asset/report/" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["LeakCheck"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://leakcheck.io/") { UseShellExecute = true }));
            _tools["Social Searcher"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://www.social-searcher.com/search-users/?q=" + _inputBox!.Text.Trim()) { UseShellExecute = true }));
            _tools["Username Search"] = ("Recon", _ => Process.Start(new ProcessStartInfo("https://whatsmyname.app/") { UseShellExecute = true }));

            // CRYPTO (15+)
            _tools["MD5 Gen"] = ("Crypto", _ => { var s = _inputBox!.Text; if (string.IsNullOrEmpty(s)) return; using var md5 = MD5.Create(); var b = md5.ComputeHash(Encoding.UTF8.GetBytes(s)); Out($"[MD5] {BitConverter.ToString(b).Replace("-", "").ToLowerInvariant()}"); });
            _tools["SHA1 Gen"] = ("Crypto", _ => { var s = _inputBox!.Text; if (string.IsNullOrEmpty(s)) return; using var sha = SHA1.Create(); var b = sha.ComputeHash(Encoding.UTF8.GetBytes(s)); Out($"[SHA1] {BitConverter.ToString(b).Replace("-", "").ToLowerInvariant()}"); });
            _tools["SHA256 Gen"] = ("Crypto", _ => { var s = _inputBox!.Text; if (string.IsNullOrEmpty(s)) return; var b = SHA256.HashData(Encoding.UTF8.GetBytes(s)); Out($"[SHA256] {Convert.ToHexString(b).ToLowerInvariant()}"); });
            _tools["SHA512 Gen"] = ("Crypto", _ => { var s = _inputBox!.Text; if (string.IsNullOrEmpty(s)) return; var b = SHA512.HashData(Encoding.UTF8.GetBytes(s)); Out($"[SHA512] {Convert.ToHexString(b).ToLowerInvariant()}"); });
            _tools["Base64 Encode"] = ("Crypto", _ => { try { Out("[B64_ENC] " + Convert.ToBase64String(Encoding.UTF8.GetBytes(_inputBox!.Text))); } catch (Exception ex) { Out("[!] " + ex.Message); } });
            _tools["Base64 Decode"] = ("Crypto", _ => { try { Out("[B64_DEC] " + Encoding.UTF8.GetString(Convert.FromBase64String(_inputBox!.Text.Trim()))); } catch (Exception ex) { Out("[!] " + ex.Message); } });
            _tools["Hex Encoder"] = ("Crypto", _ => Out("[HEX] " + string.Join("", _inputBox!.Text.Select(c => ((int)c).ToString("X2")))));
            _tools["Hex Decoder"] = ("Crypto", _ => { try { var r = ""; for (int i = 0; i < _inputBox!.Text.Length; i += 2) r += (char)Convert.ToInt32(_inputBox.Text.Substring(i, 2), 16); Out("[UTF8] " + r); } catch { Out("[!] Invalid Hex"); } });
            _tools["URL Encoder"] = ("Crypto", _ => Out("[URL_ENC] " + Uri.EscapeDataString(_inputBox!.Text)));
            _tools["URL Decoder"] = ("Crypto", _ => Out("[URL_DEC] " + Uri.UnescapeDataString(_inputBox!.Text)));
            _tools["JWT Decoder"] = ("Crypto", _ => Process.Start(new ProcessStartInfo("https://jwt.io/") { UseShellExecute = true }));
            _tools["CyberChef"] = ("Crypto", _ => Process.Start(new ProcessStartInfo("https://gchq.github.io/CyberChef/") { UseShellExecute = true }));
            _tools["Password Gen 16"] = ("Crypto", _ => { var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*"; var res = new string(Enumerable.Repeat(chars, 16).Select(s => s[new Random().Next(s.Length)]).ToArray()); Out("[PASS] " + res); });
            _tools["UUID/GUID Gen"] = ("Crypto", _ => Out("[UUID] " + Guid.NewGuid().ToString()));
            _tools["Bcrypt Tester"] = ("Crypto", _ => Process.Start(new ProcessStartInfo("https://www.browserling.com/tools/bcrypt") { UseShellExecute = true }));
            _tools["Vigenere Cipher"] = ("Crypto", _ => Process.Start(new ProcessStartInfo("https://www.dcode.fr/vigenere-cipher") { UseShellExecute = true }));

            // UTILS (5+)
            _tools["Clear CLI"] = ("Utils", _ => ClearOut());
            _tools["System Uptime"] = ("Utils", _ => Out($"[UPTIME] {TimeSpan.FromMilliseconds(Environment.TickCount64):dd\\:hh\\:mm\\:ss}"));
            _tools["Binary View"] = ("Utils", _ => Out("[BIN] " + string.Join(" ", _inputBox!.Text.Select(c => Convert.ToString(c, 2).PadLeft(8, '0')))));
            _tools["ROT13 Cipher"] = ("Utils", _ => { var s = _inputBox!.Text; Out("[ROT13] " + new string(s.Select(c => { if (c >= 'a' && c <= 'z') return (char)((c - 'a' + 13) % 26 + 'a'); if (c >= 'A' && c <= 'Z') return (char)((c - 'A' + 13) % 26 + 'A'); return c; }).ToArray())); });
            _tools["Morse Code"] = ("Utils", _ => Out("[INFO] Morse converter planned for v2.0"));
            _tools["Morse Encoder"] = ("Utils", _ => Process.Start(new ProcessStartInfo("https://morsedecoder.com/") { UseShellExecute = true }));
            _tools["JSON Formatter"] = ("Utils", _ => Process.Start(new ProcessStartInfo("https://jsonformatter.org/") { UseShellExecute = true }));
            _tools["QR Code Gen"] = ("Utils", _ => Process.Start(new ProcessStartInfo("https://www.qr-code-generator.com/") { UseShellExecute = true }));
            _tools["ASCII Art Gen"] = ("Utils", _ => Process.Start(new ProcessStartInfo("https://patorjk.com/software/taag/") { UseShellExecute = true }));
            _tools["Unix Timestamp"] = ("Utils", _ => Out($"[TS] {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"));
            _tools["Cron Expression"] = ("Utils", _ => Process.Start(new ProcessStartInfo("https://crontab.guru/") { UseShellExecute = true }));
        }

        private async void LaunchEmbedded(string exe)
        {
            try
            {
                _hostContainer.Visibility = Visibility.Visible;
                if (_host == null)
                {
                    _host = new NativeWindowHost
                    {
                        ProcessNameToKill = System.IO.Path.GetFileNameWithoutExtension(exe),
                        IsFloating = true,
                        KeepNativeControls = true,
                        IsSticky = false
                    };
                    _hostContainer.Children.Add(_host);
                }
                else
                {
                    _host.ProcessNameToKill = System.IO.Path.GetFileNameWithoutExtension(exe);
                }

                await _host.LoadAppAsync(exe, "");
            }
            catch (Exception ex)
            {
                Out("[!] FAILED_TO_EMBED: " + ex.Message);
                CloseHost();
            }
        }

        private void CloseHost()
        {
            _hostContainer.Visibility = Visibility.Collapsed;
            _host?.Dispose();
            _host = null;
        }

        private static void RunCmd(string cmd, Action<string> outAction)
        {
            try { var psi = new ProcessStartInfo("cmd.exe") { Arguments = "/c " + cmd, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }; using var p = Process.Start(psi); if (p != null) { outAction(p.StandardOutput.ReadToEnd()); } }
            catch (Exception ex) { outAction("[!] ERR: " + ex.Message); }
        }

        private void InitMatrix(Grid mainGrid)
        {
            _matrixCanvas = new Canvas { Background = Brushes.Transparent, IsHitTestVisible = false, Opacity = 0.08 };
            Grid.SetColumnSpan(_matrixCanvas, 2);
            mainGrid.Children.Insert(0, _matrixCanvas);

            this.SizeChanged += (s, e) => {
                _matrixCanvas.Children.Clear();
                _matrixColumns.Clear();
                int cols = (int)(e.NewSize.Width / 20);
                for (int i = 0; i < cols; i++) {
                    _matrixColumns.Add(new MatrixColumn { 
                        X = i * 20, 
                        Y = _rnd.Next(-500, 0), 
                        Speed = _rnd.Next(2, 8),
                        Chars = new List<TextBlock>()
                    });
                }
            };

            _matrixTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _matrixTimer.Tick += (s, e) => UpdateMatrix();
            _matrixTimer.Start();
        }

        private void UpdateMatrix()
        {
            if (_matrixCanvas == null) return;
            var chars = "0123456789ABCDEFHIJKLMNOPQRSTUVWXYZアイウエオカキクケコサシスセソタチツテトナニヌネノ";
            
            foreach (var col in _matrixColumns)
            {
                col.Y += col.Speed;
                if (col.Y > _matrixCanvas.ActualHeight + 100)
                {
                    col.Y = -100;
                    col.Speed = _rnd.Next(2, 8);
                }

                if (col.Chars.Count > 15) { _matrixCanvas.Children.Remove(col.Chars[0]); col.Chars.RemoveAt(0); }

                var tb = new TextBlock { 
                    Text = chars[_rnd.Next(chars.Length)].ToString(),
                    Foreground = _accentBrush,
                    FontSize = 14,
                    FontFamily = new FontFamily("Consolas"),
                    Opacity = 0.8
                };
                Canvas.SetLeft(tb, col.X);
                Canvas.SetTop(tb, col.Y);
                _matrixCanvas.Children.Add(tb);
                col.Chars.Add(tb);

                // Fade existing
                for (int i = 0; i < col.Chars.Count; i++) col.Chars[i].Opacity = (double)i / col.Chars.Count;
            }
        }
    }

    public class MatrixColumn
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Speed { get; set; }
        public List<TextBlock> Chars { get; set; } = new();
    }
}

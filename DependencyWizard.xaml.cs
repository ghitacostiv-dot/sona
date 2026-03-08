using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SONA.Models;
using SONA.Services;

namespace SONA
{
    public partial class DependencyWizard : Window
    {
        private List<DependencyItem> _dependencies = null!;

        public DependencyWizard()
        {
            InitializeComponent();
            LoadDependencies();
        }

        private void LoadDependencies()
        {
            _dependencies = DependencyChecker.GetDefaultDependencies();
            DependencyChecker.CheckInstalledStatus(_dependencies);

            var selectedJson = AppConfig.GetString("selected_packages", "[]");
            var selectedList = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(selectedJson) ?? new List<string>();

            foreach (var dep in _dependencies)
                dep.IsSelected = selectedList.Contains(dep.PackageId) || !dep.IsInstalled;

            DependencyListBox.ItemsSource = _dependencies;
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            DependencyListBox.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            InstallButton.IsEnabled = false;

            var selected = _dependencies.Where(d => d.IsSelected).Select(d => d.PackageId).ToList();

            if (selected.Count == 0)
            {
                MessageBox.Show("No packages selected.", "Information");
                Close();
                return;
            }

            var progress = new Progress<string>(msg =>
            {
                LogTextBox.AppendText(msg + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            });

            await WingetInstaller.InstallPackagesAsync(selected, progress);

            AppConfig.Set("selected_packages", Newtonsoft.Json.JsonConvert.SerializeObject(selected));

            MessageBox.Show("Installation completed!", "Done");
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}

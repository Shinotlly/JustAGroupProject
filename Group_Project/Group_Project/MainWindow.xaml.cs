using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Security.AccessControl;
using System.Windows;
using System.Management;
using System;

namespace Group_Project
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<string> LocalUsers { get; set; } = new();
        private string _selectedPath;

        public MainWindow()
        {
            InitializeComponent();
            InitializeLocalUsers();
        }

        private void InitializeLocalUsers()
        {
            try
            {
                LoadLocalUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kullanıcılar listelenemedi:\n" + ex.Message,
                                "Hata",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void LoadLocalUsers()
        {
            LocalUsers.Clear();

            using (var context = new PrincipalContext(ContextType.Machine))
            using (var searcher = new PrincipalSearcher(new UserPrincipal(context)))
            {
                foreach (var result in searcher.FindAll())
                {
                    if (result is UserPrincipal user)
                    {
                        LocalUsers.Add(user.SamAccountName);
                    }
                }
            }

            UserListBox.ItemsSource = LocalUsers;
        }

        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == true)
            {
                _selectedPath = dialog.FileName; // ✅ Artık dosya yolu kaydediliyor

                FilePathText.Text = $"Seçilen dosya: {_selectedPath}";

                var info = new FileInfo(_selectedPath);
                FileInfoText.Text = $"Boyut: {info.Length / 1024.0:F2} KB\n" +
                                    $"Oluşturulma: {info.CreationTime}\n" +
                                    $"Değiştirilme: {info.LastWriteTime}\n" +
                                    $"Sahip: {GetFileOwner(_selectedPath)}";

                ShowFileAcl(_selectedPath);
                UpdateCheckBoxes(_selectedPath); // ✅ Dosya seçilince checkbox’lar güncelleniyor
            }
        }

        private void ShowFileAcl(string path)
        {
            try
            {
                AclListBox.Items.Clear();
                var security = new FileInfo(path).GetAccessControl();
                foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(System.Security.Principal.NTAccount)))
                {
                    AclListBox.Items.Add($"{rule.IdentityReference.Value} — {rule.FileSystemRights}");
                }
            }
            catch (Exception ex)
            {
                AclListBox.Items.Add("İzinler okunamadı: " + ex.Message);
            }
        }

        private string GetFileOwner(string path)
        {
            try
            {
                string wmiPath = path.Replace("\\", "\\\\");
                string query = $"ASSOCIATORS OF {{Win32_LogicalFileSecuritySetting='{wmiPath}'}} WHERE AssocClass=Win32_LogicalFileOwner ResultRole=Owner";
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        return $"{mo["Domain"]}\\{mo["Name"]}";
                    }
                }
            }
            catch { }

            return "Sahip bulunamadı";
        }

        // CheckBox durumlarını güncelle
        private void UpdateCheckBoxes(string path)
        {
            try
            {
                var info = new FileInfo(path);
                ReadOnlyCheck.IsChecked = info.IsReadOnly;
                WritableCheck.IsChecked = !info.IsReadOnly;
            }
            catch
            {
                ReadOnlyCheck.IsChecked = false;
                WritableCheck.IsChecked = false;
            }
        }

        // "Sadece okunabilir" seçilince dosya ReadOnly yap
        private void ReadOnlyCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPath)) return;

            try
            {
                var info = new FileInfo(_selectedPath);
                info.IsReadOnly = true;
                WritableCheck.IsChecked = false;
                ShowFileAcl(_selectedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İzin değiştirilemedi: {ex.Message}");
            }
        }

        private void ReadOnlyCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPath)) return;

            try
            {
                var info = new FileInfo(_selectedPath);
                info.IsReadOnly = false;
                WritableCheck.IsChecked = true;
                ShowFileAcl(_selectedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İzin değiştirilemedi: {ex.Message}");
            }
        }

        private void WritableCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPath)) return;

            try
            {
                var info = new FileInfo(_selectedPath);
                info.IsReadOnly = false;
                ReadOnlyCheck.IsChecked = false;
                ShowFileAcl(_selectedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İzin değiştirilemedi: {ex.Message}");
            }
        }

        private void WritableCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPath)) return;

            try
            {
                var info = new FileInfo(_selectedPath);
                info.IsReadOnly = true;
                ReadOnlyCheck.IsChecked = true;
                ShowFileAcl(_selectedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İzin değiştirilemedi: {ex.Message}");
            }
        }
    }
}

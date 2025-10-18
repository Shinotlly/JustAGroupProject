using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Security.AccessControl;
using System.Windows;
using System.Management;
using System;
using System.Security.Principal;

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

        private void ReadOnlyCheck_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPath)) return;

            try
            {
                // Her zaman checked olacak şekilde bırak
                ReadOnlyCheck.IsChecked = true;
                WritableCheck.IsChecked = false;

                // Dosyayı sadece okunabilir yap
                SetEveryoneReadOnly(_selectedPath);

                // ACL listesini güncelle
                ShowFileAcl(_selectedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İzin değiştirilemedi: {ex.Message}");
            }
        }

        private void WritableCheck_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPath)) return;

            try
            {
                // Her zaman checked olacak şekilde bırak
                WritableCheck.IsChecked = true;
                ReadOnlyCheck.IsChecked = false;

                // Dosyayı yazılabilir yap
                SetEveryoneWritable(_selectedPath);

                // ACL listesini güncelle
                ShowFileAcl(_selectedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İzin değiştirilemedi: {ex.Message}");
            }
        }


        private void SetEveryoneReadOnly(string path)
        {

            try
            {
                var fileInfo = new FileInfo(path);
                var security = fileInfo.GetAccessControl();
                SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

                // Önce Everyone için tüm izinleri kaldır
                security.PurgeAccessRules(everyone);

                // Sadece okuma izinleri ekle
                security.AddAccessRule(new FileSystemAccessRule(
                    everyone,
                    FileSystemRights.Read | FileSystemRights.ReadAndExecute,
                    AccessControlType.Allow));

                // Uygula
                fileInfo.SetAccessControl(security);
                MessageBox.Show("Everyone için dosya 'sadece okunabilir' hale getirildi.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İzin değiştirilemedi: {ex.Message}");
            }
        }

        private void SetEveryoneWritable(string path)
        {
            try
            {
                var fileInfo = new FileInfo(path);
                var security = fileInfo.GetAccessControl();
                SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

                // Önce Everyone için tüm izinleri kaldır
                security.PurgeAccessRules(everyone);

                // FullControl izinleri ekle
                security.AddAccessRule(new FileSystemAccessRule(
                    everyone,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));

                // Uygula
                fileInfo.SetAccessControl(security);
                MessageBox.Show("Everyone için dosya yazılabilir hale getirildi.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İzin değiştirilemedi: {ex.Message}");
            }
        }
    }
}

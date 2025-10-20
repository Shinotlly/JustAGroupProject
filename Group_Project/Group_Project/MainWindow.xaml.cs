using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Security.AccessControl;
using System.Windows;
using System.Management;
using System;
using System.Security.Principal;
using System.Diagnostics;
using System.Windows.Controls;

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
                var fileInfo = new FileInfo(path);
                var security = fileInfo.GetAccessControl();

                bool canWrite = false;

                var rules = security.GetAccessRules(true, true, typeof(NTAccount));
                foreach (FileSystemAccessRule rule in rules)
                {
                    if (rule.AccessControlType == AccessControlType.Allow)
                    {
                        // Eğer herhangi bir izin yazmaya izin veriyorsa
                        if ((rule.FileSystemRights & FileSystemRights.Write) != 0 ||
                            (rule.FileSystemRights & FileSystemRights.Modify) != 0 ||
                            (rule.FileSystemRights & FileSystemRights.FullControl) != 0)
                        {
                            canWrite = true;
                            break; // Bir kez yazma izni bulduğunda yeter
                        }
                    }
                }

                // Checkbox "Sadece Okunabilir" olacak şekilde ayarla
                ReadOnlyToggle.IsChecked = !canWrite;
            }
            catch
            {
                ReadOnlyToggle.IsChecked = false;
            }
        }


        private void ReadOnlyToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedPath))
                return;

            var checkBox = sender as CheckBox;
            bool makeReadOnly = checkBox?.IsChecked == true;

            try
            {
                if (makeReadOnly)
                {
                    SetFileAccess(_selectedPath, readOnly: true);
                    MessageBox.Show("Dosya sadece okunabilir hale getirildi.");
                }
                else
                {
                    SetFileAccess(_selectedPath, readOnly: false);
                    MessageBox.Show("Dosya yazılabilir hale getirildi.");
                }

                ShowFileAcl(_selectedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İzin değiştirilemedi: {ex.Message}");
            }
        }

        private void SetFileAccess(string path, bool readOnly)
        {
            var fileInfo = new FileInfo(path);

            // Dosya sisteminin readonly flag’ini ayarla
            fileInfo.IsReadOnly = readOnly;

            try
            {
                var security = new FileSecurity();

                var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

                FileSystemRights rights = readOnly
                    ? FileSystemRights.Read | FileSystemRights.ReadAndExecute
                    : FileSystemRights.FullControl;

                security.SetAccessRuleProtection(true, false);
                security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(users, rights, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(everyone, rights, AccessControlType.Allow));

                fileInfo.SetAccessControl(security);
            }
            catch (UnauthorizedAccessException)
            {
                string mode = readOnly ? "R" : "F";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "icacls",
                    Arguments = $"\"{path}\" /reset /grant Everyone:{mode}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                })?.WaitForExit();
            }
        }


    }
}

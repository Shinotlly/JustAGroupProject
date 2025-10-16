using System.Collections.ObjectModel;
using System.DirectoryServices.AccountManagement;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Group_Project
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<string> LocalUsers { get; set; } = new();
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
            {
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
            }

            UserListBox.ItemsSource = LocalUsers;
        }
    }
}
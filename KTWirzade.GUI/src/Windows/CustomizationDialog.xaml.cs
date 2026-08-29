using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KTWirzade.Shared.Customization;
using KTWirzade.GUI.Controls;
using Microsoft.Win32;

namespace KTWirzade.GUI.Windows
{
    /// <summary>
    /// Interaction logic for CustomizationDialog.xaml
    /// Shows APBX customization options and lets the user override them.
    /// </summary>
    public partial class CustomizationDialog : AcrylicWindow
    {
        private readonly CustomizationProfile _profile;
        private readonly string _playbookPath;
        public UserCustomizationChoices Choices { get; private set; }

        // UI Controls
        private TextBox _usernameTextBox;
        private CheckBox _usernameCheckBox;
        private Image _profilePicturePreview;
        private Button _profilePictureButton;
        private CheckBox _profilePictureCheckBox;
        private Image _wallpaperPreview;
        private Button _wallpaperButton;
        private CheckBox _wallpaperCheckBox;
        private Image _lockscreenPreview;
        private Button _lockscreenButton;
        private CheckBox _lockscreenCheckBox;
        private TextBox _computerNameTextBox;
        private CheckBox _computerNameCheckBox;
        private TextBox _descriptionTextBox;
        private CheckBox _descriptionCheckBox;
        private PasswordBox _passwordBox;
        private CheckBox _passwordCheckBox;
        private CheckBox _elevateCheckBox;
        private CheckBox _autoLogonCheckBox;
        private ComboBox _themeComboBox;
        private TextBox _accentColorTextBox;
        private CheckBox _accentColorCheckBox;

        public CustomizationDialog(CustomizationProfile profile, string playbookPath)
        {
            InitializeComponent();
            _profile = profile;
            _playbookPath = playbookPath;
            Choices = new UserCustomizationChoices();

            BuildUI();
        }

        private void BuildUI()
        {
            if (_profile == null || !_profile.HasCustomizations)
            {
                NoCustomizationsText.Visibility = Visibility.Visible;
                CustomizationsPanel.Children.Clear();
                return;
            }

            NoCustomizationsText.Visibility = Visibility.Collapsed;

            // Account Section
            if (!string.IsNullOrEmpty(_profile.Username) ||
                !string.IsNullOrEmpty(_profile.AccountPassword) ||
                !string.IsNullOrEmpty(_profile.AccountDescription) ||
                _profile.ElevateToAdmin.HasValue)
            {
                AddSectionHeader("Conta do Usuário");

                if (!string.IsNullOrEmpty(_profile.Username))
                {
                    AddCustomizableField("Nome de usuário:", _profile.Username,
                        out _usernameCheckBox, out _usernameTextBox);
                    _usernameTextBox.Text = _profile.Username;
                    _usernameCheckBox.IsChecked = true;
                }

                if (!string.IsNullOrEmpty(_profile.AccountDescription))
                {
                    AddCustomizableField("Descrição da conta:", _profile.AccountDescription,
                        out _descriptionCheckBox, out _descriptionTextBox, true);
                    _descriptionTextBox.Text = _profile.AccountDescription;
                    _descriptionCheckBox.IsChecked = true;
                }

                if (!string.IsNullOrEmpty(_profile.AccountPassword))
                {
                    AddPasswordField("Senha:", out _passwordCheckBox, out _passwordBox);
                    _passwordCheckBox.IsChecked = true;
                }

                if (_profile.ElevateToAdmin.HasValue)
                {
                    AddCheckBoxField("Elevar para Administrador:", _profile.ElevateToAdmin.Value,
                        out _elevateCheckBox);
                }
            }

            // Personalization Section
            if (!string.IsNullOrEmpty(_profile.ProfilePicturePath) ||
                !string.IsNullOrEmpty(_profile.WallpaperPath) ||
                !string.IsNullOrEmpty(_profile.LockscreenPath) ||
                !string.IsNullOrEmpty(_profile.ThemeMode) ||
                !string.IsNullOrEmpty(_profile.AccentColor))
            {
                AddSectionHeader("Personalização");

                if (!string.IsNullOrEmpty(_profile.ProfilePicturePath))
                {
                    AddImageField("Foto do perfil:", _profile.ProfilePicturePath,
                        out _profilePictureCheckBox, out _profilePicturePreview, out _profilePictureButton);
                    _profilePictureCheckBox.IsChecked = true;
                }

                if (!string.IsNullOrEmpty(_profile.WallpaperPath))
                {
                    AddImageField("Wallpaper:", _profile.WallpaperPath,
                        out _wallpaperCheckBox, out _wallpaperPreview, out _wallpaperButton);
                    _wallpaperCheckBox.IsChecked = true;
                }

                if (!string.IsNullOrEmpty(_profile.LockscreenPath))
                {
                    AddImageField("Lockscreen:", _profile.LockscreenPath,
                        out _lockscreenCheckBox, out _lockscreenPreview, out _lockscreenButton);
                    _lockscreenCheckBox.IsChecked = true;
                }

                if (!string.IsNullOrEmpty(_profile.ThemeMode))
                {
                    AddThemeField("Tema:", _profile.ThemeMode, out _themeComboBox);
                }

                if (!string.IsNullOrEmpty(_profile.AccentColor))
                {
                    AddCustomizableField("Cor de destaque:", _profile.AccentColor,
                        out _accentColorCheckBox, out _accentColorTextBox);
                    _accentColorTextBox.Text = _profile.AccentColor;
                    _accentColorCheckBox.IsChecked = true;
                }
            }

            // Computer Section
            if (!string.IsNullOrEmpty(_profile.ComputerName) || _profile.AutoLogon.HasValue)
            {
                AddSectionHeader("Computador");

                if (!string.IsNullOrEmpty(_profile.ComputerName))
                {
                    AddCustomizableField("Nome do computador:", _profile.ComputerName,
                        out _computerNameCheckBox, out _computerNameTextBox);
                    _computerNameTextBox.Text = _profile.ComputerName;
                    _computerNameCheckBox.IsChecked = true;
                }

                if (_profile.AutoLogon.HasValue)
                {
                    AddCheckBoxField("Auto-logon:", _profile.AutoLogon.Value,
                        out _autoLogonCheckBox);
                }
            }
        }

        #region UI Building Helpers

        private void AddSectionHeader(string title)
        {
            var header = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 15, 0, 10),
                Foreground = FindResource("TextPrimaryBrush") as System.Windows.Media.Brush
            };
            CustomizationsPanel.Children.Add(header);
        }

        private void AddCustomizableField(string label, string defaultValue,
            out CheckBox checkBox, out TextBox textBox, bool multiline = false)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };

            var labelPanel = new DockPanel();
            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = FindResource("TextSecondaryBrush") as System.Windows.Media.Brush
            };
            DockPanel.SetDock(labelBlock, Dock.Left);

            checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = true
            };

            labelPanel.Children.Add(checkBox);
            labelPanel.Children.Add(labelBlock);
            panel.Children.Add(labelPanel);

            textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(25, 5, 0, 0),
                TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
                AcceptsReturn = multiline,
                Height = multiline ? 60 : 25
            };

            panel.Children.Add(textBox);
            CustomizationsPanel.Children.Add(panel);
        }

        private void AddPasswordField(string label, out CheckBox checkBox, out PasswordBox passwordBox)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };

            var labelPanel = new DockPanel();
            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = FindResource("TextSecondaryBrush") as System.Windows.Media.Brush
            };
            DockPanel.SetDock(labelBlock, Dock.Left);

            checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = true
            };

            labelPanel.Children.Add(checkBox);
            labelPanel.Children.Add(labelBlock);
            panel.Children.Add(labelPanel);

            passwordBox = new PasswordBox
            {
                Margin = new Thickness(25, 5, 0, 0),
                Height = 25
            };

            panel.Children.Add(passwordBox);
            CustomizationsPanel.Children.Add(panel);
        }

        private void AddCheckBoxField(string label, bool defaultValue, out CheckBox checkBox)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };

            checkBox = new CheckBox
            {
                Content = label,
                IsChecked = defaultValue,
                Foreground = FindResource("TextSecondaryBrush") as System.Windows.Media.Brush
            };

            panel.Children.Add(checkBox);
            CustomizationsPanel.Children.Add(panel);
        }

        private void AddImageField(string label, string imagePath,
            out CheckBox checkBox, out Image preview, out Button changeButton)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };

            var labelPanel = new DockPanel();
            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = FindResource("TextSecondaryBrush") as System.Windows.Media.Brush
            };
            DockPanel.SetDock(labelBlock, Dock.Left);

            checkBox = new CheckBox
            {
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = true
            };

            labelPanel.Children.Add(checkBox);
            labelPanel.Children.Add(labelBlock);
            panel.Children.Add(labelPanel);

            var contentPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(25, 5, 0, 0) };

            var localPreview = new Image
            {
                Width = 80,
                Height = 80,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Margin = new Thickness(0, 0, 10, 0)
            };

            // Try to load preview
            try
            {
                if (File.Exists(imagePath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    localPreview.Source = bitmap;
                }
            }
            catch { }

            var localButton = new Button
            {
                Content = "Escolher...",
                Height = 30,
                Padding = new Thickness(10, 0, 10, 0),
                Style = FindResource("ContentBoxButton") as Style
            };

            var selectDialog = new OpenFileDialog
            {
                Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp|Todos|*.*"
            };

            localButton.Click += (s, e) =>
            {
                if (selectDialog.ShowDialog() == true)
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(selectDialog.FileName);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        localPreview.Source = bitmap;
                        // Store path in tag for later retrieval
                        localButton.Tag = selectDialog.FileName;
                    }
                    catch { }
                }
            };

            contentPanel.Children.Add(localPreview);
            contentPanel.Children.Add(localButton);
            panel.Children.Add(contentPanel);

            CustomizationsPanel.Children.Add(panel);

            // Assign out parameters
            preview = localPreview;
            changeButton = localButton;
        }

        private void AddThemeField(string label, string defaultValue, out ComboBox comboBox)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };

            var labelPanel = new DockPanel();
            var labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = FindResource("TextSecondaryBrush") as System.Windows.Media.Brush
            };
            DockPanel.SetDock(labelBlock, Dock.Left);
            labelPanel.Children.Add(labelBlock);
            panel.Children.Add(labelPanel);

            comboBox = new ComboBox
            {
                Margin = new Thickness(25, 5, 0, 0),
                Width = 150,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            comboBox.Items.Add(new ComboBoxItem { Content = "Escuro", Tag = "dark" });
            comboBox.Items.Add(new ComboBoxItem { Content = "Claro", Tag = "light" });
            comboBox.Items.Add(new ComboBoxItem { Content = "Sistema", Tag = "system" });

            // Select default
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                var item = (ComboBoxItem)comboBox.Items[i];
                if (item.Tag.ToString() == defaultValue?.ToLowerInvariant())
                {
                    comboBox.SelectedIndex = i;
                    break;
                }
            }

            panel.Children.Add(comboBox);
            CustomizationsPanel.Children.Add(panel);
        }

        #endregion

        #region Event Handlers

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void UseDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            // Use all defaults - don't override anything
            Choices = null;
            DialogResult = true;
            Close();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            Choices = new UserCustomizationChoices();

            // Username
            if (_usernameCheckBox != null)
            {
                Choices.UseUsername = _usernameCheckBox.IsChecked == true;
                Choices.Username = _usernameTextBox?.Text;
            }

            // Profile Picture
            if (_profilePictureCheckBox != null)
            {
                Choices.UseProfilePicture = _profilePictureCheckBox.IsChecked == true;
                // Use custom path if button was clicked, otherwise use default
                Choices.ProfilePicturePath = _profilePictureButton?.Tag as string ?? _profile.ProfilePicturePath;
            }

            // Wallpaper
            if (_wallpaperCheckBox != null)
            {
                Choices.UseWallpaper = _wallpaperCheckBox.IsChecked == true;
                Choices.WallpaperPath = _wallpaperButton?.Tag as string ?? _profile.WallpaperPath;
            }

            // Lockscreen
            if (_lockscreenCheckBox != null)
            {
                Choices.UseLockscreen = _lockscreenCheckBox.IsChecked == true;
                Choices.LockscreenPath = _lockscreenButton?.Tag as string ?? _profile.LockscreenPath;
            }

            // Description
            if (_descriptionCheckBox != null)
            {
                Choices.UseAccountDescription = _descriptionCheckBox.IsChecked == true;
                Choices.AccountDescription = _descriptionTextBox?.Text;
            }

            // Password
            if (_passwordCheckBox != null)
            {
                Choices.UseAccountPassword = _passwordCheckBox.IsChecked == true;
                Choices.AccountPassword = _passwordBox?.Password;
            }

            // Elevate
            if (_elevateCheckBox != null)
            {
                Choices.UseElevateToAdmin = true;
                Choices.ElevateToAdmin = _elevateCheckBox.IsChecked == true;
            }

            // Computer Name
            if (_computerNameCheckBox != null)
            {
                Choices.UseComputerName = _computerNameCheckBox.IsChecked == true;
                Choices.ComputerName = _computerNameTextBox?.Text;
            }

            // Accent Color
            if (_accentColorCheckBox != null)
            {
                Choices.UseAccentColor = _accentColorCheckBox.IsChecked == true;
                Choices.AccentColor = _accentColorTextBox?.Text;
            }

            // Auto-logon
            if (_autoLogonCheckBox != null)
            {
                Choices.UseAutoLogon = true;
                Choices.AutoLogon = _autoLogonCheckBox.IsChecked == true;
            }

            // Theme
            if (_themeComboBox?.SelectedItem is ComboBoxItem themeItem)
            {
                Choices.UseThemeMode = true;
                Choices.ThemeMode = themeItem.Tag as string;
            }

            DialogResult = true;
            Close();
        }

        #endregion
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace EasyWords
{
    public static class SettingsMenu
    {
        // Template tùy chỉnh cho MenuItem: tự vẽ toàn bộ vùng (kể cả cột check-mark)
        // để không bị "hở" ra nền trắng mặc định của Windows theme.
        private static readonly string MenuItemTemplateXaml = @"
<ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                  xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                  TargetType=""MenuItem"">
    <Border x:Name=""Bd""
            Background=""{TemplateBinding Background}""
            BorderBrush=""{TemplateBinding BorderBrush}""
            BorderThickness=""{TemplateBinding BorderThickness}""
            CornerRadius=""3"">
        <Grid Margin=""{TemplateBinding Padding}"">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width=""22""/>
                <ColumnDefinition Width=""*""/>
            </Grid.ColumnDefinitions>

            <Path x:Name=""CheckMark""
                  Grid.Column=""0""
                  Width=""9"" Height=""7""
                  Stretch=""Uniform""
                  Data=""M0,3.5 L3.2,7 L9,0""
                  Stroke=""{TemplateBinding Foreground}""
                  StrokeThickness=""1.6""
                  StrokeStartLineCap=""Round""
                  StrokeEndLineCap=""Round""
                  StrokeLineJoin=""Round""
                  HorizontalAlignment=""Center""
                  VerticalAlignment=""Center""
                  Visibility=""Collapsed""/>

            <ContentPresenter Grid.Column=""1""
                               ContentSource=""Header""
                               RecognizesAccessKey=""True""
                               VerticalAlignment=""Center""/>
        </Grid>
    </Border>
    <ControlTemplate.Triggers>
        <Trigger Property=""IsChecked"" Value=""True"">
            <Setter TargetName=""CheckMark"" Property=""Visibility"" Value=""Visible""/>
        </Trigger>
        <Trigger Property=""IsHighlighted"" Value=""True"">
            <Setter TargetName=""Bd"" Property=""Background"" Value=""#3F3F46""/>
        </Trigger>
        <Trigger Property=""IsEnabled"" Value=""False"">
            <Setter Property=""Foreground"" Value=""#777777""/>
        </Trigger>
    </ControlTemplate.Triggers>
</ControlTemplate>";

        // Template tùy chỉnh cho chính ContextMenu: bỏ hẳn "gutter" icon mặc định
        // (dải màu sáng hệ thống chừa chỗ cho icon) mà theme Windows tự vẽ ở khung ngoài.
        private static readonly string ContextMenuTemplateXaml = @"
<ControlTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                  xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                  TargetType=""ContextMenu"">
    <Border Background=""{TemplateBinding Background}""
            BorderBrush=""{TemplateBinding BorderBrush}""
            BorderThickness=""{TemplateBinding BorderThickness}""
            CornerRadius=""4"">
        <Border.Effect>
            <DropShadowEffect BlurRadius=""12"" Opacity=""0.4"" ShadowDepth=""2"" Color=""Black""/>
        </Border.Effect>
        <ScrollViewer CanContentScroll=""True"" VerticalScrollBarVisibility=""Auto"">
            <ItemsPresenter Margin=""{TemplateBinding Padding}"" KeyboardNavigation.DirectionalNavigation=""Cycle""/>
        </ScrollViewer>
    </Border>
</ControlTemplate>";

        public static void OpenMenu(System.Windows.Controls.Button anchorButton)
        {
            if (anchorButton == null) return;

            var menuItemTemplate = (ControlTemplate)XamlReader.Parse(MenuItemTemplateXaml);
            var contextMenuTemplate = (ControlTemplate)XamlReader.Parse(ContextMenuTemplateXaml);

            var menu = new System.Windows.Controls.ContextMenu
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1),
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                HasDropShadow = true,
                Padding = new Thickness(2),
                Template = contextMenuTemplate
            };

            // Hàm tạo Style mới cho từng MenuItem để tránh đụng độ Visual Tree
            Style GetItemStyle()
            {
                var style = new Style(typeof(System.Windows.Controls.MenuItem));
                style.Setters.Add(new Setter(System.Windows.Controls.MenuItem.ForegroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240))));
                style.Setters.Add(new Setter(System.Windows.Controls.MenuItem.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
                style.Setters.Add(new Setter(System.Windows.Controls.MenuItem.PaddingProperty, new Thickness(8, 6, 8, 6)));
                style.Setters.Add(new Setter(System.Windows.Controls.MenuItem.TemplateProperty, menuItemTemplate));
                return style;
            }

            // 1. Show / Hide Suggestions
            var itemShowHide = new System.Windows.Controls.MenuItem
            {
                Header = "Show Suggestions",
                IsCheckable = true,
                IsChecked = AppSettings.IsAutoCompleteEnabled,
                Style = GetItemStyle()
            };
            itemShowHide.Checked += (s, e) => AppSettings.IsAutoCompleteEnabled = true;
            itemShowHide.Unchecked += (s, e) => AppSettings.IsAutoCompleteEnabled = false;

            // 1b. Tự động chọn từ đầu
            var itemAutoSelectFirst = new System.Windows.Controls.MenuItem
            {
                Header = "Auto-select first suggestion",
                IsCheckable = true,
                IsChecked = AppSettings.IsAutoSelectFirstEnabled,
                Style = GetItemStyle()
            };
            itemAutoSelectFirst.Checked += (s, e) => AppSettings.IsAutoSelectFirstEnabled = true;
            itemAutoSelectFirst.Unchecked += (s, e) => AppSettings.IsAutoSelectFirstEnabled = false;

            // 2. Khởi động cùng Windows
            var itemStartup = new System.Windows.Controls.MenuItem
            {
                Header = "Run at startup",
                IsCheckable = true,
                IsChecked = AppSettings.RunAtStartup,
                Style = GetItemStyle()
            };
            itemStartup.Checked += (s, e) => AppSettings.RunAtStartup = true;
            itemStartup.Unchecked += (s, e) => AppSettings.RunAtStartup = false;

            // 3. Thêm từ mới
            var itemAddWord = new System.Windows.Controls.MenuItem { Header = "Add new word...", Style = GetItemStyle() };
            itemAddWord.Click += (s, e) =>
            {
                var addWindow = new AddWordWindow();
                addWindow.ShowDialog();
            };

            // 3b. Đổi tên gọi sang WordsManagerWindow
            var itemCustomList = new System.Windows.Controls.MenuItem { Header = "Manage words", Style = GetItemStyle() };
            itemCustomList.Click += (s, e) =>
            {
                var customWindow = new CustomWordsWindow();
                customWindow.ShowDialog();
            };

            // 4. Thoát
            var itemExit = new System.Windows.Controls.MenuItem { Header = "Exit", Style = GetItemStyle() };
            itemExit.Click += (s, e) =>
            {
                System.Windows.Application.Current.Shutdown();
            };

            var itemAutoHide = new System.Windows.Controls.MenuItem
            {
                Header = "Auto-hide after 5s",
                IsCheckable = true,
                IsChecked = AppSettings.IsAutoHideEnabled,
                Style = GetItemStyle()
            };
            // 5. Thêm mục Toggle Auto Hide 5s trong SettingsMenu.cs
            itemAutoHide.Click += (s, e) =>
            {
                AppSettings.IsAutoHideEnabled = itemAutoHide.IsChecked;
            };

            menu.Items.Add(itemAutoHide);
            menu.Items.Add(itemShowHide);
            menu.Items.Add(itemAutoSelectFirst);
            menu.Items.Add(itemStartup);
            menu.Items.Add(new System.Windows.Controls.Separator { Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 50)), Margin = new Thickness(4, 2, 4, 2) });
            menu.Items.Add(itemAddWord);
            menu.Items.Add(itemCustomList);
            menu.Items.Add(new System.Windows.Controls.Separator { Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 50)), Margin = new Thickness(4, 2, 4, 2) });
            menu.Items.Add(itemExit);

            menu.PlacementTarget = anchorButton;
            menu.IsOpen = true;
        }
    }
}
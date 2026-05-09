using System.Windows;
using System.Windows.Controls;

namespace SpaceMousePilot.Views;

public sealed class SpaceMouseDialog : Window
{
    public SpaceMouseDialog(Window owner, string message)
    {
        Owner                 = owner;
        Title                 = "SpaceMouse not found";
        Width                 = 380;
        SizeToContent         = SizeToContent.Height;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background            = (System.Windows.Media.Brush)Application.Current.Resources["BrBg"];

        var stack = new StackPanel { Margin = new Thickness(24) };

        stack.Children.Add(new TextBlock
        {
            Text       = "SpaceMouse not found",
            FontSize   = 14, FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["BrText"],
            Margin     = new Thickness(0, 0, 0, 8),
        });

        stack.Children.Add(new TextBlock
        {
            Text         = message, FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground   = (System.Windows.Media.Brush)Application.Current.Resources["BrMuted"],
            Margin       = new Thickness(0, 0, 0, 20),
        });

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };

        var retry = new Button
        {
            Content = "Retry", Width = 100, Height = 34,
            Style   = Application.Current.Resources["BtnAccent"] as Style,
            Margin  = new Thickness(0, 0, 8, 0),
        };
        retry.Click += (_, _) => { DialogResult = true; };

        var cancel = new Button
        {
            Content = "Cancel", Width = 100, Height = 34,
            Style   = Application.Current.Resources["BtnSecondary"] as Style,
        };
        cancel.Click += (_, _) => { DialogResult = false; };

        row.Children.Add(retry);
        row.Children.Add(cancel);
        stack.Children.Add(row);
        Content = stack;
    }
}

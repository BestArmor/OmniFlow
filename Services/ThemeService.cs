using System.Windows;
using System.Windows.Media;

namespace OmniFlow.Services;

public class ThemeService
{
    public void ApplyCyberpunkTheme()
    {
        Application.Current.Resources["AccentColorKey"] = (Color)ColorConverter.ConvertFromString("#7C3AED");
        Application.Current.Resources["Accent2ColorKey"] = (Color)ColorConverter.ConvertFromString("#EC4899");
    }

    public void ApplyOceanTheme()
    {
        Application.Current.Resources["AccentColorKey"] = (Color)ColorConverter.ConvertFromString("#0EA5E9");
        Application.Current.Resources["Accent2ColorKey"] = (Color)ColorConverter.ConvertFromString("#06B6D4");
    }

    public void ApplyMatrixTheme()
    {
        Application.Current.Resources["AccentColorKey"] = (Color)ColorConverter.ConvertFromString("#22C55E");
        Application.Current.Resources["Accent2ColorKey"] = (Color)ColorConverter.ConvertFromString("#84CC16");
    }
}
using MudBlazor;

namespace BlazorWebApp.Services
{
    public class ThemeService
    {
        public Dictionary<string, MudTheme> Themes { get; }
        public string[] ThemeNames { get; }

        public ThemeService()
        {
            Themes = new Dictionary<string, MudTheme>
            {
                { "Default", CreateDefaultTheme() },
                { "Custom", CreateCustomTheme() },
                { "Ocean", CreateOceanTheme() },
                { "Twilight", CreateTwilightTheme() },
                { "Emerald", CreateEmeraldTheme() },
                { "Sunset", CreateSunsetTheme() }
            };

            ThemeNames = Themes.Keys.ToArray();
        }

        public MudTheme GetTheme(string themeName)
        {
            return Themes.TryGetValue(themeName, out var theme) ? theme : Themes["Default"];
        }

        private static MudTheme CreateDefaultTheme()
        {
            return new MudTheme();
        }

        private static MudTheme CreateCustomTheme()
        {
            return new MudTheme
            {
                Palette = new Palette
                {
                    Primary = "#2196F3",
                    Secondary = "#9C27B0",
                    Tertiary = "#FF9800",
                    Info = "#00BCD4",
                    Success = "#4CAF50",
                    Warning = "#FF9800",
                    Error = "#F44336",
                    Dark = "#212121",
                    AppbarBackground = "#1E1E1E"
                },
                PaletteDark = new PaletteDark
                {
                    Primary = "#64B5F6",
                    Secondary = "#BA68C8",
                    Tertiary = "#FFB74D",
                    Info = "#4DD0E1",
                    Success = "#81C784",
                    Warning = "#FFB74D",
                    Error = "#E57373",
                    AppbarBackground = "#1A1A1A",
                    Surface = "#1E1E1E",
                    Background = "#121212",
                    DrawerBackground = "#1A1A1A"
                }
            };
        }

        private static MudTheme CreateOceanTheme()
        {
            return new MudTheme
            {
                Palette = new Palette
                {
                    Primary = "#006BA6",
                    Secondary = "#0496FF",
                    Tertiary = "#FFBC42",
                    Info = "#118AB2",
                    Success = "#06D6A0",
                    Warning = "#FFD60A",
                    Error = "#EF476F",
                    Dark = "#001D3D",
                    AppbarBackground = "#003459"
                },
                PaletteDark = new PaletteDark
                {
                    Primary = "#0496FF",
                    Secondary = "#118AB2",
                    Tertiary = "#FFD60A",
                    Info = "#06D6A0",
                    Success = "#06D6A0",
                    Warning = "#FFBC42",
                    Error = "#EF476F",
                    AppbarBackground = "#001D3D",
                    Surface = "#003459",
                    Background = "#000814",
                    DrawerBackground = "#001D3D"
                }
            };
        }

        private static MudTheme CreateTwilightTheme()
        {
            return new MudTheme
            {
                Palette = new Palette
                {
                    Primary = "#6A4C93",
                    Secondary = "#C77DFF",
                    Tertiary = "#E0AAFF",
                    Info = "#7209B7",
                    Success = "#10B981",
                    Warning = "#F59E0B",
                    Error = "#EF4444",
                    Dark = "#240046",
                    AppbarBackground = "#3C096C"
                },
                PaletteDark = new PaletteDark
                {
                    Primary = "#C77DFF",
                    Secondary = "#E0AAFF",
                    Tertiary = "#F72585",
                    Info = "#B5179E",
                    Success = "#10B981",
                    Warning = "#FBBF24",
                    Error = "#F87171",
                    AppbarBackground = "#10002B",
                    Surface = "#240046",
                    Background = "#10002B",
                    DrawerBackground = "#240046"
                }
            };
        }

        private static MudTheme CreateEmeraldTheme()
        {
            return new MudTheme
            {
                Palette = new Palette
                {
                    Primary = "#047857",
                    Secondary = "#10B981",
                    Tertiary = "#34D399",
                    Info = "#0891B2",
                    Success = "#22C55E",
                    Warning = "#F59E0B",
                    Error = "#DC2626",
                    Dark = "#064E3B",
                    AppbarBackground = "#065F46"
                },
                PaletteDark = new PaletteDark
                {
                    Primary = "#10B981",
                    Secondary = "#34D399",
                    Tertiary = "#6EE7B7",
                    Info = "#06B6D4",
                    Success = "#4ADE80",
                    Warning = "#FBBF24",
                    Error = "#EF4444",
                    AppbarBackground = "#022C22",
                    Surface = "#064E3B",
                    Background = "#022C22",
                    DrawerBackground = "#064E3B"
                }
            };
        }

        private static MudTheme CreateSunsetTheme()
        {
            return new MudTheme
            {
                Palette = new Palette
                {
                    Primary = "#EA580C",
                    Secondary = "#F97316",
                    Tertiary = "#FB923C",
                    Info = "#0EA5E9",
                    Success = "#22C55E",
                    Warning = "#EAB308",
                    Error = "#DC2626",
                    Dark = "#7C2D12",
                    AppbarBackground = "#9A3412"
                },
                PaletteDark = new PaletteDark
                {
                    Primary = "#FB923C",
                    Secondary = "#FDBA74",
                    Tertiary = "#FED7AA",
                    Info = "#38BDF8",
                    Success = "#4ADE80",
                    Warning = "#FDE047",
                    Error = "#F87171",
                    AppbarBackground = "#431407",
                    Surface = "#7C2D12",
                    Background = "#431407",
                    DrawerBackground = "#7C2D12"
                }
            };
        }
    }
}
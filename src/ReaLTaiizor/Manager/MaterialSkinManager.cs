#region Imports

using ReaLTaiizor.Colors;
using ReaLTaiizor.Controls;
using ReaLTaiizor.Extension;
using ReaLTaiizor.Forms;
using ReaLTaiizor.Properties;
using ReaLTaiizor.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static ReaLTaiizor.Util.MaterialNativeTextRenderer;

#endregion

namespace ReaLTaiizor.Manager
{
    #region MaterialSkinManager

    public class MaterialSkinManager
    {
        private sealed class FontDescriptor
        {
            public string FaceName { get; }
            public int PixelSize { get; } // Unscaled
            public logFontWeight Weight { get; }
            public byte Italic { get; } = 0;

            public FontDescriptor(string faceName, int pixelSize,
                          logFontWeight weight, byte italic = 0)
            {
                FaceName = faceName;
                PixelSize = pixelSize;
                Weight = weight;
                Italic = italic;
            }

            public IntPtr Create(float scaleRatio)
            {
                int scaledSize = (int)Math.Round(PixelSize * scaleRatio);
                IntPtr fontHandle = createLogicalFont(
                    FaceName,
                    scaledSize,
                    Weight,
                    Italic
                );

                if (fontHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create logical font using CreateFontIndirect.");
                }

                return fontHandle;
            }
        }

        /// <summary>
        /// Create LFont Descriptor Object for TextBox Rendering
        /// </summary>
        /// <param name="size">Should be in [12, 16]</param>
        /// <returns></returns>
        private FontDescriptor CreateTextBoxDescriptor(int size)
        {
            if (size > 16) size = 16;
            if (size < 12) size = 12;
            return new FontDescriptor
            (
                size <= 13 ? "Roboto Medium" : "Roboto",
                size,
                size <= 13
                    ? logFontWeight.FW_MEDIUM
                    : logFontWeight.FW_REGULAR,
                0
            );
        }


        private readonly Dictionary<FontType, FontDescriptor> fontDescriptors;


        private readonly List<MaterialForm> _formsToManage = [];

        public delegate void SkinManagerEventHandler(object sender);

        public event SkinManagerEventHandler ColorSchemeChanged;

        public event SkinManagerEventHandler ThemeChanged;

        public bool EnforceBackcolorOnAllComponents = true;

        public static MaterialSkinManager Instance => field ?? (field = new MaterialSkinManager());

        public int FORM_PADDING = 14;

        private readonly object _fontLock = new(); // To avoid cross-thread unexpected behaviour.
        private readonly object _fontCacheLock = new();

        // Constructor
        private MaterialSkinManager()
        {
            Theme = Themes.LIGHT;
            ColorScheme = new MaterialColorScheme(MaterialPrimary.Indigo500, MaterialPrimary.Indigo700, MaterialPrimary.Indigo100, MaterialAccent.Pink200, MaterialTextShade.WHITE);

            // Create and cache Roboto fonts
            // Thanks https://www.codeproject.com/Articles/42041/How-to-Use-a-Font-Without-Installing-it
            // And https://www.codeproject.com/Articles/107376/Embedding-Font-To-Resources

            // Add font to system table in memory and save the font family
            addFont(Resources.Roboto_Thin);
            addFont(Resources.Roboto_Light);
            addFont(Resources.Roboto_Regular);
            addFont(Resources.Roboto_Medium);
            addFont(Resources.Roboto_Bold);
            addFont(Resources.Roboto_Black);

            RobotoFontFamilies = [];
            foreach (FontFamily ff in privateFontCollection.Families.ToArray())
            {
                RobotoFontFamilies.Add(ff.Name.Replace(' ', '_'), ff);
            }

            fontDescriptors = new()
            {
                { FontType.H1, new FontDescriptor ("Roboto Light", 96, logFontWeight.FW_LIGHT) },
                { FontType.H2, new FontDescriptor ("Roboto Light", 60, logFontWeight.FW_LIGHT) },
                { FontType.H3, new FontDescriptor ("Roboto", 48, logFontWeight.FW_REGULAR) },
                { FontType.H4, new FontDescriptor ("Roboto", 34, logFontWeight.FW_REGULAR) },
                { FontType.H5, new FontDescriptor ("Roboto", 24, logFontWeight.FW_REGULAR) },
                { FontType.H6, new FontDescriptor ("Roboto Medium", 20, logFontWeight.FW_MEDIUM) },

                { FontType.Subtitle1, new FontDescriptor ("Roboto", 16, logFontWeight.FW_REGULAR) },
                { FontType.Subtitle2, new FontDescriptor ("Roboto Medium", 14, logFontWeight.FW_MEDIUM) },
                { FontType.SubtleEmphasis, new FontDescriptor ("Roboto", 12, logFontWeight.FW_NORMAL, 1) },

                { FontType.Body1, new FontDescriptor ("Roboto", 16, logFontWeight.FW_REGULAR) },
                { FontType.Body2, new FontDescriptor ("Roboto", 14, logFontWeight.FW_REGULAR) },

                { FontType.Button, new FontDescriptor ("Roboto Medium", 14, logFontWeight.FW_MEDIUM) },
                { FontType.Caption, new FontDescriptor ("Roboto", 12, logFontWeight.FW_REGULAR) },
                { FontType.Overline, new FontDescriptor ("Roboto", 10, logFontWeight.FW_REGULAR) },
            };

            // NOTE:
            // fontDescriptors is intentionally limited to a predefined set of FontType values
            // (H1–H6, Subtitle1/2, SubtleEmphasis, Body1/2, Button, Caption, Overline).
            // Textbox and other dynamic font sizes are created on-the-fly and are not stored
            // in this dictionary. They may be cached separately (for example, in logicalFonts)
            // to avoid growing this dictionary without bound and to preserve the current behavior
            // of the public API. If additional FontType values are introduced in the future, they
            // should be added to this initialization explicitly.

            // create and save font handles for GDI
            logicalFonts = [];
            foreach (var item in fontDescriptors.Keys)
            {
                GetLogFontByType(item);
                // Preload logical fonts for the default scale factor (1.0).
                // This restores the original eager-initialization behavior for
                // standard DPI environments (100%).
                //
                // In PerMonitorV2 DPI scenarios, the actual ScaleFactor may not be
                // known at construction time. In those cases, fonts for other scale
                // ratios will be created lazily on first request.
                //
                // This avoids breaking the previous initialization contract
                // while still supporting dynamic DPI changes.

            }
        }

        // Destructor
        ~MaterialSkinManager()
        {
            // RemoveFontMemResourceEx
            foreach (IntPtr handle in logicalFonts.Values)
            {
                MaterialNativeTextRenderer.DeleteObject(handle);
            }

            foreach (Font font in _fonts.Values)
                font.Dispose();

            _fonts.Clear();
        }

        // Themes

        public Themes Theme
        {
            get;
            set
            {
                field = value;
                UpdateBackgrounds();
                ThemeChanged?.Invoke(this);
            }
        }

        public MaterialColorScheme ColorScheme
        {
            get;
            set
            {
                field = value;
                UpdateBackgrounds();
                ColorSchemeChanged?.Invoke(this);
            }
        }

        public enum Themes : byte
        {
            LIGHT,
            DARK
        }

        // Text
        private static readonly Color TEXT_HIGH_EMPHASIS_LIGHT = Color.FromArgb(222, 255, 255, 255); // Alpha 87%
        private static readonly Brush TEXT_HIGH_EMPHASIS_LIGHT_BRUSH = new SolidBrush(TEXT_HIGH_EMPHASIS_LIGHT);
        private static readonly Color TEXT_HIGH_EMPHASIS_DARK = Color.FromArgb(222, 0, 0, 0); // Alpha 87%
        private static readonly Brush TEXT_HIGH_EMPHASIS_DARK_BRUSH = new SolidBrush(TEXT_HIGH_EMPHASIS_DARK);

        private static readonly Color TEXT_HIGH_EMPHASIS_LIGHT_NOALPHA = Color.FromArgb(255, 255, 255, 255); // Alpha 100%
        private static readonly Brush TEXT_HIGH_EMPHASIS_LIGHT_NOALPHA_BRUSH = new SolidBrush(TEXT_HIGH_EMPHASIS_LIGHT_NOALPHA);
        private static readonly Color TEXT_HIGH_EMPHASIS_DARK_NOALPHA = Color.FromArgb(255, 0, 0, 0); // Alpha 100%
        private static readonly Brush TEXT_HIGH_EMPHASIS_DARK_NOALPHA_BRUSH = new SolidBrush(TEXT_HIGH_EMPHASIS_DARK_NOALPHA);

        private static readonly Color TEXT_MEDIUM_EMPHASIS_LIGHT = Color.FromArgb(153, 255, 255, 255); // Alpha 60%
        private static readonly Brush TEXT_MEDIUM_EMPHASIS_LIGHT_BRUSH = new SolidBrush(TEXT_MEDIUM_EMPHASIS_LIGHT);
        private static readonly Color TEXT_MEDIUM_EMPHASIS_DARK = Color.FromArgb(153, 0, 0, 0); // Alpha 60%
        private static readonly Brush TEXT_MEDIUM_EMPHASIS_DARK_BRUSH = new SolidBrush(TEXT_MEDIUM_EMPHASIS_DARK);

        private static readonly Color TEXT_DISABLED_OR_HINT_LIGHT = Color.FromArgb(97, 255, 255, 255); // Alpha 38%
        private static readonly Brush TEXT_DISABLED_OR_HINT_LIGHT_BRUSH = new SolidBrush(TEXT_DISABLED_OR_HINT_LIGHT);
        private static readonly Color TEXT_DISABLED_OR_HINT_DARK = Color.FromArgb(97, 0, 0, 0); // Alpha 38%
        private static readonly Brush TEXT_DISABLED_OR_HINT_DARK_BRUSH = new SolidBrush(TEXT_DISABLED_OR_HINT_DARK);

        // Dividers and thin lines
        private static readonly Color DIVIDERS_LIGHT = Color.FromArgb(30, 255, 255, 255); // Alpha 30%
        private static readonly Brush DIVIDERS_LIGHT_BRUSH = new SolidBrush(DIVIDERS_LIGHT);
        private static readonly Color DIVIDERS_DARK = Color.FromArgb(30, 0, 0, 0); // Alpha 30%
        private static readonly Brush DIVIDERS_DARK_BRUSH = new SolidBrush(DIVIDERS_DARK);
        private static readonly Color DIVIDERS_ALTERNATIVE_LIGHT = Color.FromArgb(153, 255, 255, 255); // Alpha 60%
        private static readonly Brush DIVIDERS_ALTERNATIVE_LIGHT_BRUSH = new SolidBrush(DIVIDERS_ALTERNATIVE_LIGHT);
        private static readonly Color DIVIDERS_ALTERNATIVE_DARK = Color.FromArgb(153, 0, 0, 0); // Alpha 60%
        private static readonly Brush DIVIDERS_ALTERNATIVE_DARK_BRUSH = new SolidBrush(DIVIDERS_ALTERNATIVE_DARK);

        // Checkbox / Radio / Switches
        private static readonly Color CHECKBOX_OFF_LIGHT = Color.FromArgb(138, 0, 0, 0);
        private static readonly Brush CHECKBOX_OFF_LIGHT_BRUSH = new SolidBrush(CHECKBOX_OFF_LIGHT);
        private static readonly Color CHECKBOX_OFF_DARK = Color.FromArgb(179, 255, 255, 255);
        private static readonly Brush CHECKBOX_OFF_DARK_BRUSH = new SolidBrush(CHECKBOX_OFF_DARK);
        private static readonly Color CHECKBOX_OFF_DISABLED_LIGHT = Color.FromArgb(66, 0, 0, 0);
        private static readonly Brush CHECKBOX_OFF_DISABLED_LIGHT_BRUSH = new SolidBrush(CHECKBOX_OFF_DISABLED_LIGHT);
        private static readonly Color CHECKBOX_OFF_DISABLED_DARK = Color.FromArgb(77, 255, 255, 255);
        private static readonly Brush CHECKBOX_OFF_DISABLED_DARK_BRUSH = new SolidBrush(CHECKBOX_OFF_DISABLED_DARK);

        // Switch specific
        private static readonly Color SWITCH_OFF_THUMB_LIGHT = Color.FromArgb(255, 255, 255, 255);
        private static readonly Color SWITCH_OFF_THUMB_DARK = Color.FromArgb(255, 190, 190, 190);
        private static readonly Color SWITCH_OFF_TRACK_LIGHT = Color.FromArgb(100, 0, 0, 0);
        private static readonly Color SWITCH_OFF_TRACK_DARK = Color.FromArgb(100, 255, 255, 255);
        private static readonly Color SWITCH_OFF_DISABLED_THUMB_LIGHT = Color.FromArgb(255, 230, 230, 230);
        private static readonly Color SWITCH_OFF_DISABLED_THUMB_DARK = Color.FromArgb(255, 150, 150, 150);

        // Generic back colors - for user controls
        private static readonly Color BACKGROUND_LIGHT = Color.FromArgb(255, 255, 255, 255);
        private static readonly Brush BACKGROUND_LIGHT_BRUSH = new SolidBrush(BACKGROUND_LIGHT);
        private static readonly Color BACKGROUND_DARK = Color.FromArgb(255, 80, 80, 80);
        private static readonly Brush BACKGROUND_DARK_BRUSH = new SolidBrush(BACKGROUND_DARK);
        private static readonly Color BACKGROUND_ALTERNATIVE_LIGHT = Color.FromArgb(10, 0, 0, 0);
        private static readonly Brush BACKGROUND_ALTERNATIVE_LIGHT_BRUSH = new SolidBrush(BACKGROUND_ALTERNATIVE_LIGHT);
        private static readonly Color BACKGROUND_ALTERNATIVE_DARK = Color.FromArgb(10, 255, 255, 255);
        private static readonly Brush BACKGROUND_ALTERNATIVE_DARK_BRUSH = new SolidBrush(BACKGROUND_ALTERNATIVE_DARK);
        private static readonly Color BACKGROUND_HOVER_LIGHT = Color.FromArgb(20, 0, 0, 0);
        private static readonly Brush BACKGROUND_HOVER_LIGHT_BRUSH = new SolidBrush(BACKGROUND_HOVER_LIGHT);
        private static readonly Color BACKGROUND_HOVER_DARK = Color.FromArgb(20, 255, 255, 255);
        private static readonly Brush BACKGROUND_HOVER_DARK_BRUSH = new SolidBrush(BACKGROUND_HOVER_DARK);
        private static readonly Color BACKGROUND_HOVER_RED = Color.FromArgb(255, 255, 0, 0);
        private static readonly Brush BACKGROUND_HOVER_RED_BRUSH = new SolidBrush(BACKGROUND_HOVER_RED);
        private static readonly Color BACKGROUND_DOWN_RED = Color.FromArgb(255, 255, 84, 54);
        private static readonly Brush BACKGROUND_DOWN_RED_BRUSH = new SolidBrush(BACKGROUND_DOWN_RED);
        private static readonly Color BACKGROUND_FOCUS_LIGHT = Color.FromArgb(30, 0, 0, 0);
        private static readonly Brush BACKGROUND_FOCUS_LIGHT_BRUSH = new SolidBrush(BACKGROUND_FOCUS_LIGHT);
        private static readonly Color BACKGROUND_FOCUS_DARK = Color.FromArgb(30, 255, 255, 255);
        private static readonly Brush BACKGROUND_FOCUS_DARK_BRUSH = new SolidBrush(BACKGROUND_FOCUS_DARK);
        private static readonly Color BACKGROUND_DISABLED_LIGHT = Color.FromArgb(25, 0, 0, 0);
        private static readonly Brush BACKGROUND_DISABLED_LIGHT_BRUSH = new SolidBrush(BACKGROUND_DISABLED_LIGHT);
        private static readonly Color BACKGROUND_DISABLED_DARK = Color.FromArgb(25, 255, 255, 255);
        private static readonly Brush BACKGROUND_DISABLED_DARK_BRUSH = new SolidBrush(BACKGROUND_DISABLED_DARK);

        //Expansion Panel colors
        private static readonly Color EXPANSIONPANEL_FOCUS_LIGHT = Color.FromArgb(255, 242, 242, 242);
        private static readonly Brush EXPANSIONPANEL_FOCUS_LIGHT_BRUSH = new SolidBrush(EXPANSIONPANEL_FOCUS_LIGHT);
        private static readonly Color EXPANSIONPANEL_FOCUS_DARK = Color.FromArgb(255, 50, 50, 50);
        private static readonly Brush EXPANSIONPANEL_FOCUS_DARK_BRUSH = new SolidBrush(EXPANSIONPANEL_FOCUS_DARK);

        // Backdrop colors - for containers, like forms or panels
        private static readonly Color BACKDROP_LIGHT = Color.FromArgb(255, 242, 242, 242);
        private static readonly Brush BACKDROP_LIGHT_BRUSH = new SolidBrush(BACKDROP_LIGHT);
        private static readonly Color BACKDROP_DARK = Color.FromArgb(255, 50, 50, 50);
        private static readonly Brush BACKDROP_DARK_BRUSH = new SolidBrush(BACKDROP_DARK);

        //Other colors
        private static readonly Color CARD_BLACK = Color.FromArgb(255, 42, 42, 42);
        private static readonly Color CARD_WHITE = Color.White;

        // Getters - Using these makes handling the dark theme switching easier
        // Text
        public Color TextHighEmphasisColor => Theme == Themes.LIGHT ? TEXT_HIGH_EMPHASIS_DARK : TEXT_HIGH_EMPHASIS_LIGHT;
        public Brush TextHighEmphasisBrush => Theme == Themes.LIGHT ? TEXT_HIGH_EMPHASIS_DARK_BRUSH : TEXT_HIGH_EMPHASIS_LIGHT_BRUSH;
        public Color TextHighEmphasisNoAlphaColor => Theme == Themes.LIGHT ? TEXT_HIGH_EMPHASIS_DARK_NOALPHA : TEXT_HIGH_EMPHASIS_LIGHT_NOALPHA;
        public Brush TextHighEmphasisNoAlphaBrush => Theme == Themes.LIGHT ? TEXT_HIGH_EMPHASIS_DARK_NOALPHA_BRUSH : TEXT_HIGH_EMPHASIS_LIGHT_NOALPHA_BRUSH;
        public Color TextMediumEmphasisColor => Theme == Themes.LIGHT ? TEXT_MEDIUM_EMPHASIS_DARK : TEXT_MEDIUM_EMPHASIS_LIGHT;
        public Brush TextMediumEmphasisBrush => Theme == Themes.LIGHT ? TEXT_MEDIUM_EMPHASIS_DARK_BRUSH : TEXT_MEDIUM_EMPHASIS_LIGHT_BRUSH;
        public Color TextDisabledOrHintColor => Theme == Themes.LIGHT ? TEXT_DISABLED_OR_HINT_DARK : TEXT_DISABLED_OR_HINT_LIGHT;
        public Brush TextDisabledOrHintBrush => Theme == Themes.LIGHT ? TEXT_DISABLED_OR_HINT_DARK_BRUSH : TEXT_DISABLED_OR_HINT_LIGHT_BRUSH;

        // Divider
        public Color DividersColor => Theme == Themes.LIGHT ? DIVIDERS_DARK : DIVIDERS_LIGHT;
        public Brush DividersBrush => Theme == Themes.LIGHT ? DIVIDERS_DARK_BRUSH : DIVIDERS_LIGHT_BRUSH;
        public Color DividersAlternativeColor => Theme == Themes.LIGHT ? DIVIDERS_ALTERNATIVE_DARK : DIVIDERS_ALTERNATIVE_LIGHT;
        public Brush DividersAlternativeBrush => Theme == Themes.LIGHT ? DIVIDERS_ALTERNATIVE_DARK_BRUSH : DIVIDERS_ALTERNATIVE_LIGHT_BRUSH;

        // Checkbox / Radio / Switch
        public Color CheckboxOffColor => Theme == Themes.LIGHT ? CHECKBOX_OFF_LIGHT : CHECKBOX_OFF_DARK;
        public Brush CheckboxOffBrush => Theme == Themes.LIGHT ? CHECKBOX_OFF_LIGHT_BRUSH : CHECKBOX_OFF_DARK_BRUSH;
        public Color CheckBoxOffDisabledColor => Theme == Themes.LIGHT ? CHECKBOX_OFF_DISABLED_LIGHT : CHECKBOX_OFF_DISABLED_DARK;
        public Brush CheckBoxOffDisabledBrush => Theme == Themes.LIGHT ? CHECKBOX_OFF_DISABLED_LIGHT_BRUSH : CHECKBOX_OFF_DISABLED_DARK_BRUSH;

        // Switch
        public Color SwitchOffColor => Theme == Themes.LIGHT ? CHECKBOX_OFF_DARK : CHECKBOX_OFF_LIGHT; // yes, I re-use the checkbox color, sue me
        public Color SwitchOffThumbColor => Theme == Themes.LIGHT ? SWITCH_OFF_THUMB_LIGHT : SWITCH_OFF_THUMB_DARK;
        public Color SwitchOffTrackColor => Theme == Themes.LIGHT ? SWITCH_OFF_TRACK_LIGHT : SWITCH_OFF_TRACK_DARK;
        public Color SwitchOffDisabledThumbColor => Theme == Themes.LIGHT ? SWITCH_OFF_DISABLED_THUMB_LIGHT : SWITCH_OFF_DISABLED_THUMB_DARK;

        // Control Back colors
        public Color BackgroundColor => Theme == Themes.LIGHT ? BACKGROUND_LIGHT : BACKGROUND_DARK;
        public Brush BackgroundBrush => Theme == Themes.LIGHT ? BACKGROUND_LIGHT_BRUSH : BACKGROUND_DARK_BRUSH;
        public Color BackgroundAlternativeColor => Theme == Themes.LIGHT ? BACKGROUND_ALTERNATIVE_LIGHT : BACKGROUND_ALTERNATIVE_DARK;
        public Brush BackgroundAlternativeBrush => Theme == Themes.LIGHT ? BACKGROUND_ALTERNATIVE_LIGHT_BRUSH : BACKGROUND_ALTERNATIVE_DARK_BRUSH;
        public Color BackgroundDisabledColor => Theme == Themes.LIGHT ? BACKGROUND_DISABLED_LIGHT : BACKGROUND_DISABLED_DARK;
        public Brush BackgroundDisabledBrush => Theme == Themes.LIGHT ? BACKGROUND_DISABLED_LIGHT_BRUSH : BACKGROUND_DISABLED_DARK_BRUSH;
        public Color BackgroundHoverColor => Theme == Themes.LIGHT ? BACKGROUND_HOVER_LIGHT : BACKGROUND_HOVER_DARK;
        public Brush BackgroundHoverBrush => Theme == Themes.LIGHT ? BACKGROUND_HOVER_LIGHT_BRUSH : BACKGROUND_HOVER_DARK_BRUSH;
        public Color BackgroundHoverRedColor => Theme == Themes.LIGHT ? BACKGROUND_HOVER_RED : BACKGROUND_HOVER_RED;
        public Brush BackgroundHoverRedBrush => Theme == Themes.LIGHT ? BACKGROUND_HOVER_RED_BRUSH : BACKGROUND_HOVER_RED_BRUSH;
        public Brush BackgroundDownRedBrush => Theme == Themes.LIGHT ? BACKGROUND_DOWN_RED_BRUSH : BACKGROUND_DOWN_RED_BRUSH;
        public Color BackgroundFocusColor => Theme == Themes.LIGHT ? BACKGROUND_FOCUS_LIGHT : BACKGROUND_FOCUS_DARK;
        public Brush BackgroundFocusBrush => Theme == Themes.LIGHT ? BACKGROUND_FOCUS_LIGHT_BRUSH : BACKGROUND_FOCUS_DARK_BRUSH;

        // Other color
        public Color CardsColor => Theme == Themes.LIGHT ? CARD_WHITE : CARD_BLACK;

        // Expansion Panel color/brush
        public Brush ExpansionPanelFocusBrush => Theme == Themes.LIGHT ? EXPANSIONPANEL_FOCUS_LIGHT_BRUSH : EXPANSIONPANEL_FOCUS_DARK_BRUSH;

        // SnackBar
        public Color SnackBarTextHighEmphasisColor => Theme != Themes.LIGHT ? TEXT_HIGH_EMPHASIS_DARK : TEXT_HIGH_EMPHASIS_LIGHT;
        public Color SnackBarBackgroundColor => Theme != Themes.LIGHT ? BACKGROUND_LIGHT : BACKGROUND_DARK;
        public Color SnackBarTextButtonNoAccentTextColor => Theme != Themes.LIGHT ? ColorScheme.PrimaryColor : ColorScheme.LightPrimaryColor;

        // Backdrop color
        public Color BackdropColor => Theme == Themes.LIGHT ? BACKDROP_LIGHT : BACKDROP_DARK;
        public Brush BackdropBrush => Theme == Themes.LIGHT ? BACKDROP_LIGHT_BRUSH : BACKDROP_DARK_BRUSH;

        // Font Handling
        public enum FontType
        {
            H1,
            H2,
            H3,
            H4,
            H5,
            H6,
            Subtitle1,
            Subtitle2,
            SubtleEmphasis,
            Body1,
            Body2,
            Button,
            Caption,
            Overline
        }

        public Font GetFontByType(FontType type)
        {
            return GetFontByType(type, 1);
        }
        private readonly Dictionary<string, Font> _fonts = new Dictionary<string, Font>();
        public Font GetFontByType(FontType type, float scaleRatio)
        {
            return type switch
            {
                FontType.H1 => new Font(RobotoFontFamilies["Roboto_Light"], 96f * scaleRatio, FontStyle.Regular, GraphicsUnit.Pixel),
                FontType.H2 => new Font(RobotoFontFamilies["Roboto_Light"], 60f * scaleRatio, FontStyle.Regular, GraphicsUnit.Pixel),
                FontType.H3 => new Font(RobotoFontFamilies["Roboto"], 48f * scaleRatio, FontStyle.Bold, GraphicsUnit.Pixel),
                FontType.H4 => new Font(RobotoFontFamilies["Roboto"], 34f * scaleRatio, FontStyle.Bold, GraphicsUnit.Pixel),
                FontType.H5 => new Font(RobotoFontFamilies["Roboto"], 24f * scaleRatio, FontStyle.Bold, GraphicsUnit.Pixel),
                FontType.H6 => new Font(RobotoFontFamilies["Roboto_Medium"], 20f * scaleRatio, FontStyle.Bold, GraphicsUnit.Pixel),
                FontType.Subtitle1 => new Font(RobotoFontFamilies["Roboto"], 16f * scaleRatio, FontStyle.Regular, GraphicsUnit.Pixel),
                FontType.Subtitle2 => new Font(RobotoFontFamilies["Roboto_Medium"], 14f * scaleRatio, FontStyle.Bold, GraphicsUnit.Pixel),
                FontType.SubtleEmphasis => new Font(RobotoFontFamilies["Roboto"], 12f * scaleRatio, FontStyle.Italic, GraphicsUnit.Pixel),
                FontType.Body1 => new Font(RobotoFontFamilies["Roboto"], 14f * scaleRatio, FontStyle.Regular, GraphicsUnit.Pixel),
                FontType.Body2 => new Font(RobotoFontFamilies["Roboto"], 12f * scaleRatio, FontStyle.Regular, GraphicsUnit.Pixel),
                FontType.Button => new Font(RobotoFontFamilies["Roboto"], 14f * scaleRatio, FontStyle.Bold, GraphicsUnit.Pixel),
                FontType.Caption => new Font(RobotoFontFamilies["Roboto"], 12f * scaleRatio, FontStyle.Regular, GraphicsUnit.Pixel),
                FontType.Overline => new Font(RobotoFontFamilies["Roboto"], 10f * scaleRatio, FontStyle.Regular, GraphicsUnit.Pixel),
                _ => new Font(RobotoFontFamilies["Roboto"], 14f * scaleRatio, FontStyle.Regular, GraphicsUnit.Pixel),
            };
        }

        public IntPtr GetTextBoxFontBySize(int size)
        {
            return GetTextBoxFontBySize(size, 1f);
        }

        public IntPtr GetTextBoxFontBySize(int size, float scaleRatio)
        {
            int scaleKey = (int)Math.Round(scaleRatio * 100);
            string cacheKey = $"TextBox-{size}-scale{scaleKey}";

            if (logicalFonts.TryGetValue(cacheKey, out IntPtr cached))
            {
                return cached;
            }

            lock (_fontLock)
            {
                PreloadSpecifiedScaleLFonts(scaleRatio);
                if (logicalFonts.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }

                FontDescriptor descriptor = CreateTextBoxDescriptor(size);
                IntPtr hFont = descriptor.Create(scaleRatio);
                logicalFonts[cacheKey] = hFont;
                return hFont;
            }
        }

        public IntPtr GetLogFontByType(FontType type)
        {
            return GetLogFontByType(type, 1f);
        }

        public IntPtr GetLogFontByType(FontType type, float scaleRatio)
        {
            int scaleKey = (int)Math.Round(scaleRatio * 100);
            string cacheKey = $"{type}-scale{scaleKey}";

            if (logicalFonts.TryGetValue(cacheKey, out IntPtr cached))
            {
                return cached;
            }

            lock (_fontLock)
            {
                PreloadSpecifiedScaleLFonts(scaleRatio);
                if (logicalFonts.TryGetValue(cacheKey, out cached))
                {
                    return cached;
                }

                if (!fontDescriptors.TryGetValue(type, out FontDescriptor descriptor))
                {
                    throw new ArgumentException($"No font descriptor is registered for font type '{type}'.", nameof(type));
                }
                IntPtr hFont = descriptor.Create(scaleRatio);
                logicalFonts[cacheKey] = hFont;
                return hFont;
            }
        }

        private void PreloadSpecifiedScaleLFonts(float scaleRatio)
        {
            int scaleKey = (int)Math.Round(scaleRatio * 100);
            foreach (var type in fontDescriptors.Keys)
            {
                string cacheKey = $"{type}-scale{scaleKey}";
                if (logicalFonts.TryGetValue(cacheKey, out IntPtr cached))
                {
                    continue;
                }
                var descriptor = fontDescriptors[type];
                IntPtr hFont = descriptor.Create(scaleRatio);
                logicalFonts[cacheKey] = hFont;
            }
            for (int i = 12; i < 17; i++)
            {
                string cacheKey = $"TextBox-{i}-scale{scaleKey}";
                if (logicalFonts.TryGetValue(cacheKey, out IntPtr cached))
                {
                    continue;
                }
                FontDescriptor descriptor = CreateTextBoxDescriptor(i);
                IntPtr hFont = descriptor.Create(scaleRatio);
                logicalFonts[cacheKey] = hFont;
            }
        }

        // Font stuff
        private Dictionary<string, IntPtr> logicalFonts;

        private Dictionary<string, FontFamily> RobotoFontFamilies;

        private PrivateFontCollection privateFontCollection = new();

        private void addFont(byte[] fontdata)
        {
            // Add font to system table in memory
            int dataLength = fontdata.Length;

            IntPtr ptrFont = Marshal.AllocCoTaskMem(dataLength);
            Marshal.Copy(fontdata, 0, ptrFont, dataLength);

            // GDI Font
            MaterialNativeTextRenderer.AddFontMemResourceEx(fontdata, dataLength, IntPtr.Zero, out _);

            // GDI+ Font
            privateFontCollection.AddMemoryFont(ptrFont, dataLength);
        }

        private static IntPtr createLogicalFont(string fontName, int size, MaterialNativeTextRenderer.logFontWeight weight, byte lfItalic = 0)
        {
            // Logical font:
            MaterialNativeTextRenderer.LogFont lfont = new()
            {
                lfFaceName = fontName,
                lfHeight = -size,
                lfWeight = (int)weight,
                lfItalic = lfItalic
            };
            return MaterialNativeTextRenderer.CreateFontIndirect(lfont);
        }

        // Dyanmic Themes
        public void AddFormToManage(MaterialForm materialForm)
        {
            _formsToManage.Add(materialForm);
            UpdateBackgrounds();

            // Set background on newly added controls
            materialForm.ControlAdded += (sender, e) =>
            {
                UpdateControlBackColor(e.Control, BackdropColor);
            };
        }

        public void RemoveFormToManage(MaterialForm materialForm)
        {
            _formsToManage.Remove(materialForm);
        }

        private void UpdateBackgrounds()
        {
            Color newBackColor = BackdropColor;
            foreach (MaterialForm materialForm in _formsToManage)
            {
                materialForm.BackColor = newBackColor;
                UpdateControlBackColor(materialForm, newBackColor);
            }
        }

        public float GetDeviceScaleFactor(Control control)
        {
            // 96 is Windows default scaling DPI（100% scale）
            float scalingFactor = (float)control.DeviceDpi / 96f;
            return scalingFactor;
        }

        public float GetDeviceScaleFactorSqrt(Control control)
        {
            // 96 is Windows default scaling DPI（100% scale）
            float scalingFactor = (float)Math.Sqrt((float)control.DeviceDpi / 96f);
            // Since a 'replace' to code will lead to operate scaling twice in some scenes, this method provide the sqrt value of a factor.
            return scalingFactor;
        }

        private void UpdateControlBackColor(Control controlToUpdate, Color newBackColor)
        {
            // No control
            if (controlToUpdate == null)
            {
                return;
            }

            // Control's Context menu
            if (controlToUpdate.ContextMenuStrip != null)
            {
                UpdateToolStrip(controlToUpdate.ContextMenuStrip, newBackColor);
            }

            // Material Tabcontrol pages
            if (controlToUpdate is System.Windows.Forms.TabPage)
            {
                ((System.Windows.Forms.TabPage)controlToUpdate).BackColor = newBackColor;
            }

            // Material Divider
            else if (controlToUpdate is MaterialDivider)
            {
                controlToUpdate.BackColor = DividersColor;
            }

            // Other Material Skin control
            else if (controlToUpdate.IsMaterialControl())
            {
                controlToUpdate.BackColor = newBackColor;
                controlToUpdate.ForeColor = TextHighEmphasisColor;
            }

            // Other Generic control not part of material skin
            else if (EnforceBackcolorOnAllComponents && controlToUpdate.HasProperty("BackColor") && !controlToUpdate.IsMaterialControl() && controlToUpdate.Parent != null)
            {
                controlToUpdate.BackColor = controlToUpdate.Parent.BackColor;
                controlToUpdate.ForeColor = TextHighEmphasisColor;
                controlToUpdate.Font = GetFontByType(MaterialSkinManager.FontType.Body1);
            }

            // Recursive call to control's children
            foreach (Control control in controlToUpdate.Controls)
            {
                UpdateControlBackColor(control, newBackColor);
            }
        }

        private void UpdateToolStrip(ToolStrip toolStrip, Color newBackColor)
        {
            if (toolStrip == null)
            {
                return;
            }

            toolStrip.BackColor = newBackColor;
            foreach (ToolStripItem control in toolStrip.Items)
            {
                control.BackColor = newBackColor;
                if (control is MaterialToolStripMenuItem && (control as MaterialToolStripMenuItem).HasDropDown)
                {
                    //recursive call
                    UpdateToolStrip((control as MaterialToolStripMenuItem).DropDown, newBackColor);
                }
            }
        }

    }

    #endregion
}
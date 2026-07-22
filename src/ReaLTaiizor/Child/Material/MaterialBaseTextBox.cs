#region Imports

using ReaLTaiizor.Controls;
using ReaLTaiizor.Helper;
using ReaLTaiizor.Manager;
using ReaLTaiizor.Util;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static ReaLTaiizor.Helper.MaterialDrawHelper;

#endregion

namespace ReaLTaiizor.Child.Material
{
    #region MaterialBaseTextBox

    [ToolboxItem(false)]
    public class MaterialBaseTextBox : TextBox, MaterialControlI
    {
        #region "Public Properties"

        //Properties for managing the material design properties
        private Regex _inputRegex;
        [Category("Behavior")]
        [DefaultValue("")]
        public string InputRegex
        {
            get => _inputRegex?.ToString() ?? "";
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _inputRegex = null;
                }
                else
                {
                    _inputRegex = new Regex(value, RegexOptions.Compiled);
                }
            }
        }

        [Browsable(false)]
        public int Depth { get; set; }

        [Browsable(false)]
        public MaterialSkinManager SkinManager => MaterialSkinManager.Instance;

        [Browsable(false)]
        public MaterialMouseState MouseState { get; set; }

        public string Hint
        {
            get;
            set
            {
                field = value;
                Invalidate();
            }
        } = string.Empty;

        public new void SelectAll()
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                base.Focus();
                base.SelectAll();
            });
        }

        #endregion

        public MaterialBaseTextBox()
        {
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        private const int WM_ENABLE = 0x0A;
        private const int WM_PAINT = 0xF;
        private const uint WM_USER = 0x0400;
        private const uint EM_SETBKGNDCOLOR = WM_USER + 67;
        private const uint WM_KILLFOCUS = 0x0008;
        private const int WM_CHAR = 0x0102;
        private const int WM_PASTE = 0x0302;

        private float? _scaleRatio; // Cache
        private float ScaleFactor
        {
            get
            {
                if (!_scaleRatio.HasValue)
                {
                    _scaleRatio = SkinManager.GetDeviceScaleFactor(this);
                }
                return _scaleRatio.Value;
            }

            set => _scaleRatio = value;
        }
        private float? _scaleRatioSqrt; // Cache
        private float ScaleFactorSqrt
        {
            get
            {
                if (!_scaleRatioSqrt.HasValue)
                {
                    _scaleRatioSqrt = SkinManager.GetDeviceScaleFactorSqrt(this);
                }
                return _scaleRatioSqrt.Value;
            }

            set => _scaleRatioSqrt = value;
        }
        private bool IsInputValid(ref Message m)
        {
            if (m.Msg == WM_CHAR && _inputRegex != null)
            {
                char c = (char)m.WParam;

                if (!char.IsControl(c))
                {
                    string newText =
                        Text.Remove(SelectionStart, SelectionLength)
                            .Insert(SelectionStart, c.ToString());

                    if (!_inputRegex.IsMatch(newText))
                    {
                        return false;
                    }
                }
            }
            if (m.Msg == WM_PASTE && _inputRegex != null)
            {
                string paste = Clipboard.GetText();

                string newText =
                    Text.Remove(SelectionStart, SelectionLength)
                        .Insert(SelectionStart, paste);

                if (!_inputRegex.IsMatch(newText))
                {
                    return false;
                }
            }
            return true;
        }
        protected override void WndProc(ref Message m)
        {
            if (!IsInputValid(ref m))
                return;

            ScaleFactor = SkinManager.GetDeviceScaleFactor(this);
            ScaleFactorSqrt = SkinManager.GetDeviceScaleFactorSqrt(this);

            base.WndProc(ref m);


            if (m.Msg == WM_PAINT)
            {
                if (m.Msg == WM_ENABLE)
                {
                    Graphics g = Graphics.FromHwnd(Handle);
                    Rectangle bounds = new(0, 0, Width, Height);
                    g.FillRectangle(SkinManager.BackgroundDisabledBrush, bounds);
                }
            }

            if (m.Msg == WM_PAINT && string.IsNullOrEmpty(Text) && !Focused)
            {
                using MaterialNativeTextRenderer NativeText = new(Graphics.FromHwnd(m.HWnd));
                NativeText.DrawTransparentText(
                Hint,
                SkinManager.GetFontByType(MaterialSkinManager.FontType.Subtitle1, ScaleFactor),
                Enabled ?
                MaterialColorHelper.RemoveAlpha(SkinManager.TextMediumEmphasisColor, BackColor) : // not focused
                MaterialColorHelper.RemoveAlpha(SkinManager.TextDisabledOrHintColor, BackColor), // Disabled
                ClientRectangle.Location,
                ClientRectangle.Size,
                MaterialNativeTextRenderer.TextAlignFlags.Left | MaterialNativeTextRenderer.TextAlignFlags.Top);
            }

            if (m.Msg == EM_SETBKGNDCOLOR)
            {
                Invalidate();
            }

            if (m.Msg == WM_KILLFOCUS) //set border back to normal on lost focus
            {
                Invalidate();
            }

        }


    }

    #endregion

    #region MaterialBaseMaskedTextBox

    [ToolboxItem(false)]
    public class MaterialBaseMaskedTextBox : MaskedTextBox, MaterialControlI
    {
        //Properties for managing the material design properties
        [Browsable(false)]
        public int Depth { get; set; }

        [Browsable(false)]
        public MaterialSkinManager SkinManager => MaterialSkinManager.Instance;

        [Browsable(false)]
        public MaterialMouseState MouseState { get; set; }

        public string Hint
        {
            get;
            set
            {
                field = value;
                Invalidate();
            }
        } = string.Empty;

        public new void SelectAll()
        {
            BeginInvoke((MethodInvoker)delegate ()
            {
                base.Focus();
                base.SelectAll();
            });
        }

        public MaterialBaseMaskedTextBox()
        {
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        private const int WM_ENABLE = 0x0A;
        private const int WM_PAINT = 0xF;
        private const uint WM_USER = 0x0400;
        private const uint EM_SETBKGNDCOLOR = WM_USER + 67;
        private const uint WM_KILLFOCUS = 0x0008;


        private float? _scaleRatio; // Cache
        private float ScaleFactor
        {
            get
            {
                if (!_scaleRatio.HasValue)
                {
                    _scaleRatio = SkinManager.GetDeviceScaleFactor(this);
                }
                return _scaleRatio.Value;
            }

            set => _scaleRatio = value;
        }
        private float? _scaleRatioSqrt; // Cache
        private float ScaleFactorSqrt
        {
            get
            {
                if (!_scaleRatioSqrt.HasValue)
                {
                    _scaleRatioSqrt = SkinManager.GetDeviceScaleFactorSqrt(this);
                }
                return _scaleRatioSqrt.Value;
            }

            set => _scaleRatioSqrt = value;
        }

        protected override void WndProc(ref Message m)
        {
            ScaleFactor = SkinManager.GetDeviceScaleFactor(this);
            ScaleFactorSqrt = SkinManager.GetDeviceScaleFactorSqrt(this);

            base.WndProc(ref m);


            if (m.Msg == WM_PAINT)
            {
                if (m.Msg == WM_ENABLE)
                {
                    Graphics g = Graphics.FromHwnd(Handle);
                    Rectangle bounds = new(0, 0, Width, Height);
                    g.FillRectangle(SkinManager.BackgroundDisabledBrush, bounds);
                }
            }

            if (m.Msg == WM_PAINT && string.IsNullOrEmpty(Text) && !Focused)
            {
                using MaterialNativeTextRenderer NativeText = new(Graphics.FromHwnd(m.HWnd));
                NativeText.DrawTransparentText(
                Hint,
                SkinManager.GetFontByType(MaterialSkinManager.FontType.Subtitle1, ScaleFactor),
                Enabled ?
                MaterialColorHelper.RemoveAlpha(SkinManager.TextMediumEmphasisColor, BackColor) : // not focused
                MaterialColorHelper.RemoveAlpha(SkinManager.TextDisabledOrHintColor, BackColor), // Disabled
                ClientRectangle.Location,
                ClientRectangle.Size,
                MaterialNativeTextRenderer.TextAlignFlags.Left | MaterialNativeTextRenderer.TextAlignFlags.Top);
            }

            if (m.Msg == EM_SETBKGNDCOLOR)
            {
                Invalidate();
            }

            if (m.Msg == WM_KILLFOCUS) //set border back to normal on lost focus
            {
                Invalidate();
            }

        }


    }

    #endregion

    #region MaterialBaseTextBoxContextMenuStrip

    [ToolboxItem(false)]
    public class MaterialBaseTextBoxContextMenuStrip : MaterialContextMenuStrip
    {
        public readonly ToolStripItem undo = new MaterialToolStripMenuItem { Text = "Undo" };
        public readonly ToolStripItem seperator1 = new ToolStripSeparator();
        public readonly ToolStripItem cut = new MaterialToolStripMenuItem { Text = "Cut" };
        public readonly ToolStripItem copy = new MaterialToolStripMenuItem { Text = "Copy" };
        public readonly ToolStripItem paste = new MaterialToolStripMenuItem { Text = "Paste" };
        public readonly ToolStripItem delete = new MaterialToolStripMenuItem { Text = "Delete" };
        public readonly ToolStripItem seperator2 = new ToolStripSeparator();
        public readonly ToolStripItem selectAll = new MaterialToolStripMenuItem { Text = "Select All" };

        public MaterialBaseTextBoxContextMenuStrip()
        {
            Items.AddRange(new[]
            {
                    undo,
                    seperator1,
                    cut,
                    copy,
                    paste,
                    delete,
                    seperator2,
                    selectAll
                });
        }
    }

    #endregion
}
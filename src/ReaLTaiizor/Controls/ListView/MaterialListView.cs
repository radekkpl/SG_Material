#region Imports

using ReaLTaiizor.Manager;
using ReaLTaiizor.Util;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static ReaLTaiizor.Helper.MaterialDrawHelper;

#endregion

namespace ReaLTaiizor.Controls
{
    #region MaterialListView

    public class MaterialListView : ListView, MaterialControlI
    {
        [Browsable(false)]
        public int Depth { get; set; }

        [Browsable(false)]
        public MaterialSkinManager SkinManager => MaterialSkinManager.Instance;

        [Browsable(false)]
        public MaterialMouseState MouseState { get; set; }

        [Browsable(false)]
        public Point MouseLocation { get; set; }

        [Category("Appearance"), Browsable(true)]
        public bool AutoSizeTable
        {
            get;
            set
            {
                field = value;
                Scrollable = !value;
            }
        }

        [Browsable(false)]
        private ListViewItem HoveredItem { get; set; }

        private const int PAD = 16;
        private const int ITEMS_HEIGHT = 52;


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

        public MaterialListView()
        {
            GridLines = false;
            FullRowSelect = true;
            View = View.Details;
            OwnerDraw = true;
            ResizeRedraw = true;
            BorderStyle = BorderStyle.None;
            MinimumSize = new Size(200, 100);

            SetStyle(ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = SkinManager.BackgroundColor;

            // Fix for hovers, by default it doesn't redraw
            MouseLocation = new Point(-1, -1);
            MouseState = MaterialMouseState.OUT;
            MouseEnter += delegate
            {
                MouseState = MaterialMouseState.HOVER;
            };
            MouseLeave += delegate
            {
                MouseState = MaterialMouseState.OUT;
                MouseLocation = new Point(-1, -1);
                HoveredItem = null;
                Invalidate();
            };
            MouseDown += delegate
            {
                MouseState = MaterialMouseState.DOWN;
            };
            MouseUp += delegate
            {
                MouseState = MaterialMouseState.HOVER;
            };
            MouseMove += delegate (object sender, MouseEventArgs args)
            {
                MouseLocation = args.Location;
                ListViewItem currentHoveredItem = GetItemAt(MouseLocation.X, MouseLocation.Y);
                if (HoveredItem != currentHoveredItem)
                {
                    HoveredItem = currentHoveredItem;
                    Invalidate();
                }
            };
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            Graphics g = e.Graphics;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush brush = new SolidBrush(BackColor))
            {
                g.FillRectangle(brush, e.Bounds);
            }

            // Draw Text
            using MaterialNativeTextRenderer NativeText = new(g);
            NativeText.DrawTransparentText(
                e.Header.Text,
                SkinManager.GetLogFontByType(MaterialSkinManager.FontType.Subtitle2, ScaleFactor),
                Enabled ? SkinManager.TextHighEmphasisNoAlphaColor : SkinManager.TextDisabledOrHintColor,
                new Point(e.Bounds.Location.X + (int)(PAD * ScaleFactor), e.Bounds.Location.Y),
                new Size(e.Bounds.Size.Width - ((int)(PAD * ScaleFactor) * 2), e.Bounds.Size.Height),
                e.Header.TextAlign == HorizontalAlignment.Left ? MaterialNativeTextRenderer.TextAlignFlags.Left | MaterialNativeTextRenderer.TextAlignFlags.Middle
                : e.Header.TextAlign == HorizontalAlignment.Right ? MaterialNativeTextRenderer.TextAlignFlags.Right | MaterialNativeTextRenderer.TextAlignFlags.Middle
                : MaterialNativeTextRenderer.TextAlignFlags.Center | MaterialNativeTextRenderer.TextAlignFlags.Middle);
        }

        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            Graphics g = e.Graphics;

            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Always draw default background
            g.FillRectangle(SkinManager.BackgroundBrush, e.Bounds);

            if (e.Item.Selected)
            {
                // Selected background
                g.FillRectangle(SkinManager.BackgroundFocusBrush, e.Bounds);
            }
            else if (e.Bounds.Contains(MouseLocation) && MouseState == MaterialMouseState.HOVER)
            {
                // Hover background
                g.FillRectangle(SkinManager.BackgroundHoverBrush, e.Bounds);
            }
            using (Pen pen = new Pen(SkinManager.DividersColor)) 
            {
                // Draw separator line
                g.DrawLine(new Pen(SkinManager.DividersColor), e.Bounds.Left, e.Bounds.Y, e.Bounds.Right, e.Bounds.Y);
            }

            int id = 0;
            foreach (ListViewItem.ListViewSubItem subItem in e.Item.SubItems)
            {
                // Draw Text
                using MaterialNativeTextRenderer NativeText = new(g);
                NativeText.DrawTransparentText(
                    subItem.Text,
                    SkinManager.GetLogFontByType(MaterialSkinManager.FontType.Body2, ScaleFactor),
                    Enabled ? SkinManager.TextHighEmphasisNoAlphaColor : SkinManager.TextDisabledOrHintColor,
                    new Point(subItem.Bounds.X + (int)(PAD * ScaleFactor), subItem.Bounds.Y),
                    new Size(subItem.Bounds.Width - ((int)(PAD * ScaleFactor) * 2), subItem.Bounds.Height),
                    Columns[id].TextAlign == HorizontalAlignment.Left
                            ? MaterialNativeTextRenderer.TextAlignFlags.Left | MaterialNativeTextRenderer.TextAlignFlags.Middle
                            : Columns[id].TextAlign == HorizontalAlignment.Right
                            ? MaterialNativeTextRenderer.TextAlignFlags.Right | MaterialNativeTextRenderer.TextAlignFlags.Middle
                            : MaterialNativeTextRenderer.TextAlignFlags.Center | MaterialNativeTextRenderer.TextAlignFlags.Middle);
                ++id;
            }
        }
        
        // Resize
        protected override void OnColumnWidthChanging(ColumnWidthChangingEventArgs e)
        {
            ScaleFactor = SkinManager.GetDeviceScaleFactor(this);
            ScaleFactorSqrt = SkinManager.GetDeviceScaleFactorSqrt(this);

            base.OnColumnWidthChanging(e);
           // AutoResize();
        }

        protected override void OnColumnWidthChanged(ColumnWidthChangedEventArgs e)
        {
            ScaleFactor = SkinManager.GetDeviceScaleFactor(this);
            ScaleFactorSqrt = SkinManager.GetDeviceScaleFactorSqrt(this);

            base.OnColumnWidthChanged(e);
            //  AutoResize();
        }

        protected override void OnResize(EventArgs e)
        {
            ScaleFactor = SkinManager.GetDeviceScaleFactor(this);
            ScaleFactorSqrt = SkinManager.GetDeviceScaleFactorSqrt(this);

            base.OnResize(e);
            AutoResize();
        }

        private void AutoResize()
        {
            if (!AutoSizeTable)
            {
                return;
            }

            // Width
            int w = 0;
            foreach (ColumnHeader col in Columns)
            {
                w += col.Width;
            }

            // Height
            int h = 50; //Header size
            if (Items.Count > 0)
            {
                h = TopItem.Bounds.Top;
            }

            foreach (ListViewItem item in Items)
            {
                h += item.Bounds.Height;
            }

            Size = new Size(w, h);
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            ScaleFactor = SkinManager.GetDeviceScaleFactor(this);
            ScaleFactorSqrt = SkinManager.GetDeviceScaleFactorSqrt(this);
            base.OnDpiChangedAfterParent(e);
        }

        protected override void InitLayout()
        {
            ScaleFactor = SkinManager.GetDeviceScaleFactor(this);
            ScaleFactorSqrt = SkinManager.GetDeviceScaleFactorSqrt(this);

            base.InitLayout();

            // enforce settings
            GridLines = false;
            FullRowSelect = true;
            View = View.Details;
            OwnerDraw = true;
            ResizeRedraw = true;
            BorderStyle = BorderStyle.None;
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
        }

        protected override void OnBackColorChanged(EventArgs e)
        {
            base.OnBackColorChanged(e);
            BackColor = SkinManager.BackgroundColor;
        }
    }

    #endregion
}
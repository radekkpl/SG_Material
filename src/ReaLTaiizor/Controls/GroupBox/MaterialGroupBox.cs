#region Imports

using ReaLTaiizor.Manager;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static ReaLTaiizor.Helper.MaterialDrawHelper;

#endregion

namespace ReaLTaiizor.Controls
{
    #region GroupBox

    public class MaterialGroupBox : ContainerControl , MaterialControlI
    {
        #region MaterialProperties
        private MaterialMouseState _mouseState = MaterialMouseState.OUT;
        /// <summary>
        /// Gets or sets the Depth
        /// </summary>
        [Browsable(false)]
        public int Depth { get; set; }

        /// <summary>
        /// Gets the SkinManager
        /// </summary>
        [Browsable(false)]
        public MaterialSkinManager SkinManager => MaterialSkinManager.Instance;

        /// <summary>
        /// Gets or sets the MouseState
        /// </summary>
        [Browsable(false)]
        public MaterialMouseState MouseState { get; set; }
        [Category("Material"),
        DefaultValue(false)]
        public bool HighEmphasis { get; set; }

        [Category("Material"),
        DefaultValue(false)]
        public bool UseAccent { get; set; }
        private MaterialSkinManager.FontType _fontType = MaterialSkinManager.FontType.Body1;
        [Category("Material"),
        DefaultValue(typeof(MaterialSkinManager.FontType), "Body1")]
        public MaterialSkinManager.FontType FontType
        {
            get => _fontType;
            set
            {
                _fontType = value;
                Font = SkinManager.GetFontByType(_fontType);
                Refresh();
            }
        }

        #endregion
        #region Custom Properties
        private Padding _CornerRadius = new Padding(10);
        /// <summary>Gets or sets the radius of the corners of the control.</summary>
        [Category("Appearance"), Description("This feature will round the corners of the control.")]
        public Padding CornerRadius
        {
            get => _CornerRadius;
            set
            {
                if (value.Left < 0) value.Left = 0;
                if (value.Right < 0) value.Right = 0;
                if (value.Top < 0) value.Top = 0;
                if (value.Bottom < 0) value.Bottom = 0;
                _CornerRadius = value;
                Invalidate();
            }
        }

        /// <summary>Gets or sets the alignment of the text</summary>
        [Category("Appearance"), Description("Gets or sets the alignment of the text.")]
        [DefaultValue(GroupBoxCaptionAlignment.Center)]
        public GroupBoxCaptionAlignment CaptionAlignment
        {
            get => _CaptionAlignment;
            set
            {
                _CaptionAlignment = value;
                Invalidate();
            }
        }

        /// <summary>Gets or sets the orientation of the text</summary>
        [Category("Appearance"), Description("Gets or sets the orientation of the text.")]
        [DefaultValue(GroupBoxCaptionOrientation.Top)]
        public GroupBoxCaptionOrientation CaptionOrientation
        {
            get => _CaptionOrientation;
            set
            {
                _CaptionOrientation = value;
                Invalidate();
            }
        }
        #endregion

        public MaterialGroupBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = SkinManager.BackdropColor;
            ForeColor = SkinManager.ColorScheme.TextColor;
            Size = new(200, 200);
            MinimumSize = new(130, 50);
            Padding = new Padding(5, 28, 5, 5);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.HighQuality;

            g.Clear(SkinManager.BackdropColor);
            var velikost = e.Graphics.MeasureString(Text, Font);
            var s = (int)velikost.Width / 2;
            var v = (int)velikost.Height / 2; // +1?

            //first lets set the rectangles we will be drawing in.
            Rectangle frameRec = new Rectangle();
            switch (CaptionOrientation)
            {
                case MaterialGroupBox.GroupBoxCaptionOrientation.Top:
                    frameRec = Rectangle.FromLTRB(ClientRectangle.Left, ClientRectangle.Top + v, ClientRectangle.Right - 1, ClientRectangle.Bottom - 1);
                    break;
                case MaterialGroupBox.GroupBoxCaptionOrientation.Left:
                    frameRec = Rectangle.FromLTRB(ClientRectangle.Left + v, ClientRectangle.Top, ClientRectangle.Right - 1, ClientRectangle.Bottom - 1);
                    break;
                case MaterialGroupBox.GroupBoxCaptionOrientation.Right:
                    frameRec = Rectangle.FromLTRB(ClientRectangle.Left, ClientRectangle.Top, ClientRectangle.Right - (v + 1), ClientRectangle.Bottom - 1);
                    break;
                case MaterialGroupBox.GroupBoxCaptionOrientation.Bottom:
                    frameRec = Rectangle.FromLTRB(ClientRectangle.Left, ClientRectangle.Top, ClientRectangle.Right - 1, ClientRectangle.Bottom - (v + 1));
                    break;
                default:
                    break;
            }

            if (frameRec.Height <= 0 || frameRec.Width <= 0) return;

            using (GraphicsPath gp = RoundRectangle(frameRec, _CornerRadius))
            {
                e.Graphics.FillPath(SkinManager.BackdropBrush, gp);
                e.Graphics.DrawPath(SkinManager.ColorScheme.AccentPen, gp);
            }
            e.Graphics.SmoothingMode = SmoothingMode.None;

            float x = 0;
            float y = 0;
            bool rotate = false;

            switch (CaptionOrientation)
            {
                case GroupBoxCaptionOrientation.Top:

                    switch (CaptionAlignment)
                    {
                        case GroupBoxCaptionAlignment.Left:
                            x = CornerRadius.Top + 2;
                            break;

                        case GroupBoxCaptionAlignment.Center:
                            x = Width / 2f - s;
                            break;

                        case GroupBoxCaptionAlignment.Right:
                            x = Width - CornerRadius.Right - 6 - (2 * s);
                            break;
                    }

                    break;

                case GroupBoxCaptionOrientation.Bottom:

                    y = Height - (2 * v) - 2;

                    switch (CaptionAlignment)
                    {
                        case GroupBoxCaptionAlignment.Left:
                            x = CornerRadius.Left + 2;
                            break;

                        case GroupBoxCaptionAlignment.Center:
                            x = Width / 2f - s;
                            break;

                        case GroupBoxCaptionAlignment.Right:
                            x = Width - CornerRadius.Bottom - 6 - (2 * s);
                            break;
                    }

                    break;

                case GroupBoxCaptionOrientation.Left:

                    rotate = true;

                    switch (CaptionAlignment)
                    {
                        case GroupBoxCaptionAlignment.Left:
                            y = Height - CornerRadius.Left - 2;
                            break;

                        case GroupBoxCaptionAlignment.Center:
                            y = Height / 2f + s;
                            break;

                        case GroupBoxCaptionAlignment.Right:
                            y = CornerRadius.Top + 2 + (2 * s);
                            break;
                    }

                    break;

                case GroupBoxCaptionOrientation.Right:

                    rotate = true;
                    x = Width - (2 * v) - 2;

                    switch (CaptionAlignment)
                    {
                        case GroupBoxCaptionAlignment.Left:
                            y = Height - CornerRadius.Bottom - 2;
                            break;

                        case GroupBoxCaptionAlignment.Center:
                            y = Height / 2f + s;
                            break;

                        case GroupBoxCaptionAlignment.Right:
                            y = CornerRadius.Right + 2 + (2 * s);
                            break;
                    }

                    break;
            }
            e.Graphics.TranslateTransform(x, y);
            if (rotate) e.Graphics.RotateTransform(-90);
            e.Graphics.FillRectangle(SkinManager.BackdropBrush, 0, 0, velikost.Width + 2, velikost.Height + 2);
            e.Graphics.DrawString(Text, Font, SkinManager.ColorScheme.TextBrush, 1, 1);

            e.Graphics.ResetTransform();
        }
  
        static GraphicsPath RoundRectangle(Rectangle r, Padding radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) return path;
            path.AddLine(r.Left + radius.Top, r.Top, r.Right - radius.Right, r.Top); //Horní
            if (radius.Right > 0) path.AddArc(Rectangle.FromLTRB(r.Right - radius.Right, r.Top, r.Right, r.Top + radius.Right), -90, 90);
            path.AddLine(r.Right, r.Top + radius.Right, r.Right, r.Bottom - radius.Bottom); //pravá
            if (radius.Bottom > 0) path.AddArc(Rectangle.FromLTRB(r.Right - radius.Bottom, r.Bottom - radius.Bottom, r.Right, r.Bottom), 0, 90);
            path.AddLine(r.Right - radius.Bottom, r.Bottom, r.Left + radius.Left, r.Bottom); //spodní
            if (radius.Left > 0) path.AddArc(Rectangle.FromLTRB(r.Left, r.Bottom - radius.Left, r.Left + radius.Left, r.Bottom), 90, 90);
            path.AddLine(r.Left, r.Bottom - radius.Left, r.Left, r.Top + radius.Top); //levá
            if (radius.Top > 0) path.AddArc(Rectangle.FromLTRB(r.Left, r.Top, r.Left + radius.Top, r.Top + radius.Top), 180, 90);
            path.CloseFigure();
            return path;
        }
        private GroupBoxCaptionAlignment _CaptionAlignment = GroupBoxCaptionAlignment.Center;
        private GroupBoxCaptionOrientation _CaptionOrientation = GroupBoxCaptionOrientation.Top;
        public enum GroupBoxCaptionAlignment
        {
            Left = StringAlignment.Near,
            Right = StringAlignment.Far,
            Center = StringAlignment.Center
        }
        public enum GroupBoxCaptionOrientation
        {
            Top,
            Left,
            Right,
            Bottom
        }
    }

    #endregion
}
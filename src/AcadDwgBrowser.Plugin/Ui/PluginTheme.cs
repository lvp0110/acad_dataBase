using System;
using System.Drawing;
using System.Windows.Forms;

namespace AcadDwgBrowser.Plugin.Ui
{
    /// <summary>
    /// WinForms palette matching AG CAD Calc (CalcView.xaml):
    /// page #F4F7F9, accent #6CABC8, text #1E2A32, muted #5B6B75.
    /// </summary>
    internal static class PluginTheme
    {
        public static readonly Color Page = Color.FromArgb(0xF4, 0xF7, 0xF9);
        public static readonly Color Header = Color.FromArgb(0x1E, 0x2A, 0x32);
        public static readonly Color Accent = Color.FromArgb(0x6C, 0xAB, 0xC8);
        public static readonly Color AccentDark = Color.FromArgb(0x3E, 0x7A, 0x96);
        public static readonly Color Text = Color.FromArgb(0x1E, 0x2A, 0x32);
        public static readonly Color Muted = Color.FromArgb(0x5B, 0x6B, 0x75);
        public static readonly Color Ghost = Color.FromArgb(0xE4, 0xEE, 0xF3);
        public static readonly Color Border = Color.FromArgb(0xC5, 0xD5, 0xDE);
        public static readonly Color Footer = Color.FromArgb(0xE8, 0xEE, 0xF2);
        public static readonly Color Card = Color.White;
        public static readonly Color AltRow = Color.FromArgb(0xF7, 0xFB, 0xFD);
        public static readonly Color Danger = Color.FromArgb(0xB0, 0x3A, 0x3A);
        public static readonly Color InputDisabled = Color.FromArgb(0xE8, 0xEE, 0xF2);

        public static readonly Font TitleFont = new Font("Segoe UI", 12f, FontStyle.Bold);
        public static readonly Font HeaderFont = new Font("Segoe UI", 11f, FontStyle.Bold);
        public static readonly Font UiFont = new Font("Segoe UI", 9f);
        public static readonly Font SmallFont = new Font("Segoe UI", 8.5f);
        public static readonly Font CaptionFont = new Font("Segoe UI", 8.5f);

        public static void ApplyPage(Control control)
        {
            if (control == null)
                return;
            control.BackColor = Page;
            control.ForeColor = Text;
            control.Font = UiFont;
        }

        public static void ApplyForm(Form form)
        {
            ApplyPage(form);
            form.Font = UiFont;
        }

        public static Panel CreateHeader(string title)
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Header,
                Padding = new Padding(12, 8, 12, 8)
            };
            header.Controls.Add(new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                Font = HeaderFont,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Header
            });
            return header;
        }

        public static void ApplyPrimaryButton(Button button)
        {
            if (button == null)
                return;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AccentDark;
            button.FlatAppearance.MouseDownBackColor = AccentDark;
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.Font = UiFont;
            WireEnabledTint(button, Accent, Color.White, ghost: false);
        }

        public static void ApplyGhostButton(Button button)
        {
            if (button == null)
                return;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Accent;
            button.FlatAppearance.MouseDownBackColor = AccentDark;
            button.BackColor = Ghost;
            button.ForeColor = Text;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.Font = UiFont;
            WireEnabledTint(button, Ghost, Text, ghost: true);
        }

        public static void ApplyDangerButton(Button button)
        {
            ApplyGhostButton(button);
            button.ForeColor = Danger;
        }

        public static void ApplyTextBox(TextBox box)
        {
            if (box == null)
                return;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Card;
            box.ForeColor = Text;
            box.Font = UiFont;
        }

        public static void ApplyCombo(ComboBox combo)
        {
            if (combo == null)
                return;
            combo.FlatStyle = FlatStyle.Flat;
            combo.BackColor = Card;
            combo.ForeColor = Text;
            combo.Font = SmallFont;
        }

        public static void ApplyLabel(Label label, bool muted = false)
        {
            if (label == null)
                return;
            label.ForeColor = muted ? Muted : Text;
            label.BackColor = Color.Transparent;
            label.Font = muted ? SmallFont : UiFont;
        }

        public static void ApplyGroup(GroupBox box)
        {
            if (box == null)
                return;
            box.ForeColor = AccentDark;
            box.BackColor = Page;
            box.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        }

        public static void ApplyListView(ListView list)
        {
            if (list == null)
                return;
            list.BackColor = Card;
            list.ForeColor = Text;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.Font = SmallFont;
            list.OwnerDraw = true;
            list.DrawColumnHeader -= OnDrawColumnHeader;
            list.DrawItem -= OnDrawItem;
            list.DrawSubItem -= OnDrawSubItem;
            list.DrawColumnHeader += OnDrawColumnHeader;
            list.DrawItem += OnDrawItem;
            list.DrawSubItem += OnDrawSubItem;
        }

        public static void ApplyCheckedList(CheckedListBox list)
        {
            if (list == null)
                return;
            list.BackColor = Card;
            list.ForeColor = Text;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.Font = SmallFont;
        }

        private static void OnDrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var fill = new SolidBrush(Footer))
            using (var line = new Pen(Border))
            {
                e.Graphics.FillRectangle(fill, e.Bounds);
                e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                TextRenderer.DrawText(e.Graphics, e.Header.Text, SmallFont,
                    Rectangle.Inflate(e.Bounds, -4, 0), Muted, flags);
            }
        }

        private static void OnDrawItem(object? sender, DrawListViewItemEventArgs e)
        {
            e.DrawDefault = false;
        }

        private static void OnDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.Item == null || e.SubItem == null)
                return;

            var selected = e.Item.Selected;
            var alt = e.ItemIndex % 2 == 1;
            var bg = selected ? Accent : (alt ? AltRow : Card);
            var fg = selected ? Color.White : Text;
            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

            var isStatus = e.Header != null
                && string.Equals(e.Header.Text, "Статус", StringComparison.Ordinal);
            if (isStatus)
            {
                var raw = e.SubItem.Tag as string ?? e.SubItem.Text;
                if (ContentStatusUi.TryGetFill(raw, out var fillColor, out var textColor))
                {
                    bg = fillColor;
                    fg = textColor;
                }

                flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.EndEllipsis;
            }

            using (var fill = new SolidBrush(bg))
                e.Graphics.FillRectangle(fill, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, SmallFont,
                Rectangle.Inflate(e.Bounds, -4, 0), fg, flags);
        }

        private static void WireEnabledTint(Button button, Color enabledBack, Color enabledFore, bool ghost)
        {
            void Apply()
            {
                if (button.Enabled)
                {
                    button.BackColor = enabledBack;
                    button.ForeColor = enabledFore;
                }
                else
                {
                    button.BackColor = ghost ? Color.FromArgb(0xF0, 0xF4, 0xF6) : Color.FromArgb(0xB7, 0xD0, 0xDC);
                    button.ForeColor = ghost ? Color.FromArgb(0x9A, 0xA8, 0xB0) : Color.White;
                }
            }

            button.EnabledChanged -= ButtonOnEnabledChanged;
            button.EnabledChanged += ButtonOnEnabledChanged;
            Apply();

            void ButtonOnEnabledChanged(object? sender, EventArgs e) => Apply();
        }
    }
}

using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Plugin.Ui
{
    /// <summary>WinForms dialogs mirroring constr-todo-web create flows for size / perforation / edge.</summary>
    internal static class ProductionDrawingCreateDialogs
    {
        public static bool TryCreatePanelSize(IWin32Window owner, out PanelSizeCreateRequest request)
        {
            request = new PanelSizeCreateRequest();
            using (var form = new Form())
            {
                form.Text = "Новый размер панели";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(360, 220);
                form.Font = new Font("Segoe UI", 9f);

                var hint = new Label
                {
                    Text = "Код и название формируются автоматически по размерам.",
                    Dock = DockStyle.Top,
                    Height = 36,
                    Padding = new Padding(8, 8, 8, 0)
                };

                var grid = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 3,
                    Padding = new Padding(8, 4, 8, 4)
                };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (var i = 0; i < 3; i++)
                    grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

                var xBox = AddNumRow(grid, 0, "Длина X (мм) *");
                var zBox = AddNumRow(grid, 1, "Длина Z (мм) *");
                var yBox = AddNumRow(grid, 2, "Длина Y (мм) *");

                var buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    FlowDirection = FlowDirection.RightToLeft,
                    Height = 40,
                    Padding = new Padding(8, 4, 8, 8)
                };
                var ok = new Button { Text = "Создать", DialogResult = DialogResult.None, Width = 90 };
                var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 90 };
                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);
                form.CancelButton = cancel;

                var result = new PanelSizeCreateRequest();
                ok.Click += (_, __) =>
                {
                    if (!TryParsePositive(xBox.Text, integer: true, out var x, out var err)
                        || !TryParsePositive(zBox.Text, integer: true, out var z, out err)
                        || !TryParsePositive(yBox.Text, integer: false, out var y, out err))
                    {
                        MessageBox.Show(form, err, form.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    result.LenX = x;
                    result.LenZ = z;
                    result.LenY = y;
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };

                form.Controls.Add(grid);
                form.Controls.Add(hint);
                form.Controls.Add(buttons);
                form.AcceptButton = ok;

                if (form.ShowDialog(owner) != DialogResult.OK)
                    return false;
                request = result;
                return true;
            }
        }

        public static bool TryCreateBrandEntity(
            IWin32Window owner,
            string title,
            string brandCode,
            out BrandEntityCreateRequest request)
        {
            request = new BrandEntityCreateRequest();
            using (var form = new Form())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ShowInTaskbar = false;
                form.ClientSize = new Size(400, 280);
                form.Font = new Font("Segoe UI", 9f);

                var hint = new Label
                {
                    Text = "Будет создана запись для бренда «" + brandCode + "».",
                    Dock = DockStyle.Top,
                    Height = 36,
                    Padding = new Padding(8, 8, 8, 0)
                };

                var grid = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 3,
                    Padding = new Padding(8, 4, 8, 4)
                };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                var codeBox = AddTextRow(grid, 0, "Код *");
                var nameBox = AddTextRow(grid, 1, "Название *");
                grid.Controls.Add(new Label
                {
                    Text = "Описание",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.TopLeft,
                    Padding = new Padding(0, 6, 0, 0)
                }, 0, 2);
                var descBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    AcceptsReturn = true
                };
                grid.Controls.Add(descBox, 1, 2);

                var buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    FlowDirection = FlowDirection.RightToLeft,
                    Height = 40,
                    Padding = new Padding(8, 4, 8, 8)
                };
                var ok = new Button { Text = "Создать", Width = 90 };
                var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Width = 90 };
                buttons.Controls.Add(ok);
                buttons.Controls.Add(cancel);
                form.CancelButton = cancel;

                var result = new BrandEntityCreateRequest { BrandCode = brandCode };
                ok.Click += (_, __) =>
                {
                    var code = (codeBox.Text ?? string.Empty).Trim();
                    var name = (nameBox.Text ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        MessageBox.Show(form, "Заполните поле «Код».", form.Text,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        MessageBox.Show(form, "Заполните поле «Название».", form.Text,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var desc = (descBox.Text ?? string.Empty).Trim();
                    result.Code = code;
                    result.Name = name;
                    result.Description = string.IsNullOrWhiteSpace(desc) ? null : desc;
                    result.BrandCode = brandCode;
                    form.DialogResult = DialogResult.OK;
                    form.Close();
                };

                form.Controls.Add(grid);
                form.Controls.Add(hint);
                form.Controls.Add(buttons);
                form.AcceptButton = ok;

                if (form.ShowDialog(owner) != DialogResult.OK)
                    return false;
                request = result;
                return true;
            }
        }

        private static TextBox AddNumRow(TableLayoutPanel grid, int row, string caption)
        {
            grid.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);
            var box = new TextBox { Dock = DockStyle.Fill };
            grid.Controls.Add(box, 1, row);
            return box;
        }

        private static TextBox AddTextRow(TableLayoutPanel grid, int row, string caption)
        {
            grid.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);
            var box = new TextBox { Dock = DockStyle.Fill };
            grid.Controls.Add(box, 1, row);
            return box;
        }

        private static bool TryParsePositive(
            string? raw,
            bool integer,
            out double value,
            out string error)
        {
            value = 0;
            error = string.Empty;
            var text = (raw ?? string.Empty).Trim().Replace(',', '.');
            if (string.IsNullOrWhiteSpace(text)
                || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                error = "Заполните все размеры числом.";
                return false;
            }

            if (value <= 0)
            {
                error = "Размеры должны быть больше нуля.";
                return false;
            }

            if (integer && Math.Abs(value - Math.Round(value)) > 0.0000001)
            {
                error = "Длины X и Z должны быть целыми числами.";
                return false;
            }

            if (integer)
                value = Math.Round(value);

            return true;
        }
    }
}

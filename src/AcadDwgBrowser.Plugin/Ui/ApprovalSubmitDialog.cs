using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Plugin.Ui
{
    /// <summary>
    /// Compact dialog to pick approvers per policy step before POST /content/toApproval/{id}.
    /// </summary>
    internal sealed class ApprovalSubmitDialog : Form
    {
        private readonly List<StepUi> _steps = new List<StepUi>();

        private ApprovalSubmitDialog()
        {
            Text = "Отправка на согласование";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 460);
            Font = new Font("Segoe UI", 9f);
        }

        public StartApprovalProcessRequest? Result { get; private set; }

        public static ApprovalSubmitDialog Create(IReadOnlyList<ApprovalPreviewStep> steps, string drawingName)
        {
            var dlg = new ApprovalSubmitDialog();
            dlg.BuildUi(steps, drawingName);
            return dlg;
        }

        private void BuildUi(IReadOnlyList<ApprovalPreviewStep> steps, string drawingName)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Чертёж: " + (drawingName ?? "—") + "\nОтметьте согласующих по шагам политики.",
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            root.Controls.Add(scroll, 0, 1);

            var stack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(6)
            };
            scroll.Controls.Add(stack);

            var ordered = (steps ?? Array.Empty<ApprovalPreviewStep>())
                .OrderBy(s => s.StepOrder)
                .ToList();

            if (ordered.Count == 0)
            {
                stack.Controls.Add(new Label
                {
                    AutoSize = true,
                    Text = "Нет шагов согласования для этого чертежа.",
                    ForeColor = Color.DarkRed
                });
            }

            foreach (var step in ordered)
            {
                var required = Math.Max(1, step.RequiredSignatures);
                var users = step.ApprovalUsers ?? new List<ApprovalUser>();
                var box = new GroupBox
                {
                    Text = "Шаг " + step.StepOrder
                           + " — нужно подписей: " + required
                           + " (доступно: " + users.Count + ")",
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(8),
                    Margin = new Padding(0, 0, 0, 8),
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
                };

                var list = new CheckedListBox
                {
                    Dock = DockStyle.Top,
                    CheckOnClick = true,
                    IntegralHeight = false,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                    Height = Math.Min(180, Math.Max(56, users.Count * 18 + 8))
                };

                foreach (var user in users)
                {
                    if (user == null || string.IsNullOrWhiteSpace(user.UserId))
                        continue;
                    var index = list.Items.Add(user);
                    // Pre-check first required_signatures users.
                    if (index < required)
                        list.SetItemChecked(index, true);
                }

                if (list.Items.Count == 0)
                {
                    box.Controls.Add(new Label
                    {
                        Dock = DockStyle.Top,
                        Height = 24,
                        Text = "Нет доступных согласующих",
                        ForeColor = Color.DarkRed,
                        Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
                    });
                }
                else
                {
                    box.Controls.Add(list);
                }

                stack.RowCount++;
                stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                stack.Controls.Add(box, 0, stack.RowCount - 1);

                _steps.Add(new StepUi
                {
                    PolicyStepId = step.PolicyStepId,
                    RequiredSignatures = required,
                    List = list
                });
            }

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 4, 0, 0)
            };
            var ok = new Button
            {
                Text = "Отправить",
                Width = 100,
                Height = 28,
                DialogResult = DialogResult.None
            };
            ok.Click += (_, __) => TryAccept();
            var cancel = new Button
            {
                Text = "Отмена",
                Width = 88,
                Height = 28,
                DialogResult = DialogResult.Cancel
            };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 2);
            CancelButton = cancel;
            AcceptButton = ok;
        }

        private void TryAccept()
        {
            var request = new StartApprovalProcessRequest();
            foreach (var step in _steps)
            {
                var selected = new List<string>();
                for (var i = 0; i < step.List.Items.Count; i++)
                {
                    if (!step.List.GetItemChecked(i))
                        continue;
                    if (step.List.Items[i] is ApprovalUser user && !string.IsNullOrWhiteSpace(user.UserId))
                        selected.Add(user.UserId.Trim());
                }

                if (selected.Count < step.RequiredSignatures)
                {
                    MessageBox.Show(
                        this,
                        "На шаге политики " + step.PolicyStepId
                        + " нужно выбрать не меньше " + step.RequiredSignatures
                        + " согласующих.",
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                request.Steps.Add(new ApprovalStepAssignInput
                {
                    PolicyStepId = step.PolicyStepId,
                    UserIds = selected
                });
            }

            if (request.Steps.Count == 0)
            {
                MessageBox.Show(this, "Нет шагов для отправки.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Result = request;
            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed class StepUi
        {
            public int PolicyStepId { get; set; }
            public int RequiredSignatures { get; set; }
            public CheckedListBox List { get; set; } = null!;
        }
    }
}

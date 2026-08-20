using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Plugin.Ui
{
    /// <summary>
    /// Pick replacement unsigned assignees for PUT /content/toApproval/assignees/{id}.
    /// </summary>
    internal sealed class ApprovalAssigneesDialog : Form
    {
        private readonly List<StepUi> _steps = new List<StepUi>();

        private ApprovalAssigneesDialog()
        {
            Text = "Замена согласующих";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(420, 460);
            PluginTheme.ApplyForm(this);
        }

        public IReadOnlyList<UpdateApprovalAssigneesRequest> Result { get; private set; } =
            Array.Empty<UpdateApprovalAssigneesRequest>();

        public static ApprovalAssigneesDialog Create(
            IReadOnlyList<ContentApprovalStep> steps,
            string drawingName)
        {
            var dlg = new ApprovalAssigneesDialog();
            dlg.BuildUi(steps, drawingName);
            return dlg;
        }

        private void BuildUi(IReadOnlyList<ContentApprovalStep> steps, string drawingName)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            Controls.Add(root);

            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Чертёж: " + (drawingName ?? "—")
                       + "\nМожно заменить тех, кто ещё не принял решение. Уже подписавшие шаги недоступны.",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = PluginTheme.Muted,
                Font = PluginTheme.SmallFont
            }, 0, 0);

            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = PluginTheme.Card
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

            var ordered = (steps ?? Array.Empty<ContentApprovalStep>())
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s.ProcessStepId))
                .OrderBy(s => s.StepOrder)
                .ToList();

            if (ordered.Count == 0)
            {
                stack.Controls.Add(new Label
                {
                    AutoSize = true,
                    Text = "Нет незавершённых шагов: все согласующие уже приняли решение.",
                    ForeColor = PluginTheme.Danger
                });
            }

            foreach (var step in ordered)
            {
                var required = Math.Max(1, step.RequiredSignatures);
                var users = step.ApprovalUsers ?? new List<ApprovalUser>();
                var pending = new HashSet<string>(
                    step.PendingAssigneeIds ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

                var box = new GroupBox
                {
                    Text = "Шаг " + step.StepOrder
                           + " — нужно подписей: " + required
                           + " (доступно: " + users.Count + ")",
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Padding = new Padding(8),
                    Margin = new Padding(0, 0, 0, 8)
                };
                PluginTheme.ApplyGroup(box);

                var list = new CheckedListBox
                {
                    Dock = DockStyle.Top,
                    CheckOnClick = true,
                    IntegralHeight = false,
                    Height = Math.Min(180, Math.Max(56, users.Count * 18 + 8))
                };
                PluginTheme.ApplyCheckedList(list);

                foreach (var user in users)
                {
                    if (user == null || string.IsNullOrWhiteSpace(user.UserId))
                        continue;
                    var index = list.Items.Add(user);
                    if (pending.Contains(user.UserId.Trim()))
                        list.SetItemChecked(index, true);
                }

                if (list.Items.Count == 0)
                {
                    box.Controls.Add(new Label
                    {
                        Dock = DockStyle.Top,
                        Height = 24,
                        Text = "Нет доступных согласующих этого отдела",
                        ForeColor = PluginTheme.Danger,
                        Font = PluginTheme.SmallFont
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
                    ProcessStepId = step.ProcessStepId!.Trim(),
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
                Text = "Сохранить",
                Width = 100,
                Height = 28,
                DialogResult = DialogResult.None,
                Enabled = _steps.Count > 0
            };
            PluginTheme.ApplyPrimaryButton(ok);
            ok.Click += (_, __) => TryAccept();
            var cancel = new Button
            {
                Text = "Отмена",
                Width = 88,
                Height = 28,
                DialogResult = DialogResult.Cancel
            };
            PluginTheme.ApplyGhostButton(cancel);
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            root.Controls.Add(buttons, 0, 2);
            CancelButton = cancel;
            AcceptButton = ok;
        }

        private void TryAccept()
        {
            var requests = new List<UpdateApprovalAssigneesRequest>();
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
                        "На шаге нужно выбрать не меньше " + step.RequiredSignatures
                        + " согласующих.",
                        Text,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                requests.Add(new UpdateApprovalAssigneesRequest
                {
                    ProcessStepId = step.ProcessStepId,
                    UserIds = selected
                });
            }

            if (requests.Count == 0)
            {
                MessageBox.Show(this, "Нет шагов для изменения.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Result = requests;
            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed class StepUi
        {
            public string ProcessStepId { get; set; } = string.Empty;
            public int RequiredSignatures { get; set; }
            public CheckedListBox List { get; set; } = null!;
        }
    }
}

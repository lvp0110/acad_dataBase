using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AcadDwgBrowser.Core.Models;
using AcadDwgBrowser.Core.Services;
using AcadDwgBrowser.Plugin.Services;

namespace AcadDwgBrowser.Plugin.Ui
{
    internal sealed class DwgBrowserControl : UserControl
    {
        private readonly Panel _loginPanel;
        private readonly Panel _catalogPanel;
        private TextBox _apiUrlBox = null!;
        private TextBox _emailBox = null!;
        private TextBox _passwordBox = null!;
        private Button _loginButton = null!;
        private Label _loginStatus = null!;
        private Label _userLabel = null!;
        private Button _logoutButton = null!;
        private ListView _list = null!;
        private Button _refreshButton = null!;
        private Button _openButton = null!;
        private Button _deleteButton = null!;
        private Button _renameButton = null!;
        private Button _saveButton = null!;
        private Button _approveButton = null!;
        private Button _withdrawButton = null!;
        private Button _replaceAssigneesButton = null!;
        private TextBox _filterBox = null!;
        private FlowLayoutPanel _catalogFiltersPanel = null!;
        private TableLayoutPanel _catalogLayout = null!;
        private Button _catalogFiltersToggle = null!;
        private bool _catalogFiltersExpanded;
        private TextBox _nameBox = null!;
        private GroupBox _editorGroup = null!;
        private GroupBox _catalogGroup = null!;
        private TableLayoutPanel _rootLayout = null!;
        private TableLayoutPanel _editorLayout = null!;
        private Panel _labelsBody = null!;
        private Button _labelsToggle = null!;
        private bool _labelsExpanded;
        private const int LabelsBodyHeightPx = 204;
        private const int EditorSectionCollapsedPx = 206;
        private const int EditorSectionExpandedPx = 410;
        private const int CatalogFiltersHeightPx = 58;
        private Label _statusLabel = null!;
        private Label _activeLabel = null!;
        private ComboBox _userCombo = null!;
        private ComboBox _categoryCombo = null!;
        private ComboBox _brandCombo = null!;
        private ComboBox _modelCombo = null!;
        private ComboBox _perforationCombo = null!;
        private ComboBox _edgeCombo = null!;
        private ComboBox _sizeCombo = null!;
        private Button _addPerforationButton = null!;
        private Button _addEdgeButton = null!;
        private Button _addSizeButton = null!;
        private ProgressBar _progress = null!;
        private List<DwgFileInfo> _allFiles = new List<DwgFileInfo>();
        private List<FilterEntity> _filters = new List<FilterEntity>();
        private List<FilterEntity> _catalogFilters = new List<FilterEntity>();
        private readonly Dictionary<string, string> _catalogFilterValues =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _displayNameById =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _labelsCts;
        private CancellationTokenSource? _labelCascadeCts;
        private string? _labelsRequestId;
        private bool _busy;
        private bool _sessionChecked;
        private bool _catalogHadSelection;
        private bool _suppressLabelCascade;
        private bool _suppressCatalogFilterEvents;
        private readonly Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventHandler _onDocActivated;

        public DwgBrowserControl()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(0);
            PluginTheme.ApplyPage(this);

            _loginPanel = BuildLoginPanel();
            _catalogPanel = BuildCatalogPanel();

            Controls.Add(_catalogPanel);
            Controls.Add(_loginPanel);

            ShowLogin(true);

            _onDocActivated = (_, __) => BeginInvokeSafe(UpdateActiveLabel);
            try
            {
                AcadDocumentService.SubscribeDocumentActivated(_onDocActivated);
            }
            catch
            {
                // AutoCAD may not be ready during design/load
            }

            VisibleChanged += (_, __) =>
            {
                if (Visible)
                    UpdateActiveLabel();
            };
        }

        private Panel BuildLoginPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            PluginTheme.ApplyPage(panel);

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 12, 16, 12)
            };
            PluginTheme.ApplyPage(body);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 10
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 12));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            body.Controls.Add(layout);

            var title = new Label
            {
                Text = "Вход в ConstrTodo",
                Dock = DockStyle.Fill,
                Font = PluginTheme.TitleFont,
                TextAlign = ContentAlignment.MiddleLeft
            };
            PluginTheme.ApplyLabel(title);
            title.Font = PluginTheme.TitleFont;
            layout.Controls.Add(title, 0, 0);

            var apiCaption = new Label
            {
                Text = "Адрес API (ApiBaseUrl)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            PluginTheme.ApplyLabel(apiCaption, muted: true);
            layout.Controls.Add(apiCaption, 0, 1);

            _apiUrlBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = string.Empty
            };
            PluginTheme.ApplyTextBox(_apiUrlBox);
            layout.Controls.Add(_apiUrlBox, 0, 2);

            var emailCaption = new Label
            {
                Text = "Email",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            PluginTheme.ApplyLabel(emailCaption, muted: true);
            layout.Controls.Add(emailCaption, 0, 3);

            _emailBox = new TextBox { Dock = DockStyle.Fill };
            PluginTheme.ApplyTextBox(_emailBox);
            layout.Controls.Add(_emailBox, 0, 4);

            var passwordCaption = new Label
            {
                Text = "Пароль",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft
            };
            PluginTheme.ApplyLabel(passwordCaption, muted: true);
            layout.Controls.Add(passwordCaption, 0, 5);

            _passwordBox = new TextBox
            {
                Dock = DockStyle.Fill,
                UseSystemPasswordChar = true
            };
            PluginTheme.ApplyTextBox(_passwordBox);
            _passwordBox.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await LoginAsync();
                }
            };
            layout.Controls.Add(_passwordBox, 0, 6);

            layout.Controls.Add(new Label { Dock = DockStyle.Fill }, 0, 7);

            _loginButton = new Button
            {
                Text = "Войти",
                Dock = DockStyle.Left,
                Width = 120,
                Height = 32
            };
            PluginTheme.ApplyPrimaryButton(_loginButton);
            _loginButton.Click += async (_, __) => await LoginAsync();
            layout.Controls.Add(_loginButton, 0, 8);

            _loginStatus = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Text = "Введите email и пароль, затем «Войти»."
            };
            PluginTheme.ApplyLabel(_loginStatus, muted: true);
            layout.Controls.Add(_loginStatus, 0, 9);

            panel.Controls.Add(body);
            panel.Controls.Add(PluginTheme.CreateHeader("DWG dB"));
            return panel;
        }

        private Panel BuildCatalogPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Visible = false };
            PluginTheme.ApplyPage(panel);

            _rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(0)
            };
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, EditorSectionCollapsedPx));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.Controls.Add(_rootLayout);

            var header = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PluginTheme.Header,
                Padding = new Padding(12, 6, 8, 6)
            };
            _rootLayout.Controls.Add(header, 0, 0);

            _logoutButton = new Button
            {
                Text = "Выйти",
                Dock = DockStyle.Right,
                Width = 78,
                Margin = new Padding(8, 2, 0, 2)
            };
            PluginTheme.ApplyGhostButton(_logoutButton);
            _logoutButton.Click += async (_, __) => await LogoutAsync();
            header.Controls.Add(_logoutButton);

            _userLabel = new Label
            {
                Text = "Пользователь",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                BackColor = PluginTheme.Header,
                Font = PluginTheme.UiFont,
                TextAlign = ContentAlignment.MiddleLeft
            };
            header.Controls.Add(_userLabel);

            // —— Section 1: active AutoCAD drawing (new OR catalog edit) ——
            _editorGroup = new GroupBox
            {
                Text = "1. Активный чертёж AutoCAD",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 8, 8, 8),
                Margin = new Padding(8, 8, 8, 4)
            };
            PluginTheme.ApplyGroup(_editorGroup);
            _rootLayout.Controls.Add(_editorGroup, 0, 1);

            _editorLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(0, 4, 0, 0)
            };
            _editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            _editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            _editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            _editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            _editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            _editorGroup.Controls.Add(_editorLayout);

            _activeLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Активный чертёж: —",
                TextAlign = ContentAlignment.MiddleLeft
            };
            PluginTheme.ApplyLabel(_activeLabel, muted: true);
            _editorLayout.Controls.Add(_activeLabel, 0, 0);

            var nameRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0)
            };
            nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            nameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            nameRow.Controls.Add(new Label
            {
                Text = "Имя чертежа *",
                Dock = DockStyle.Fill,
                Font = PluginTheme.CaptionFont,
                ForeColor = PluginTheme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);
            _nameBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 2, 0, 2)
            };
            PluginTheme.ApplyTextBox(_nameBox);
            nameRow.Controls.Add(_nameBox, 1, 0);
            _editorLayout.Controls.Add(nameRow, 0, 1);

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            _editorLayout.Controls.Add(actions, 0, 2);

            _renameButton = new Button
            {
                Text = "Задать имя…",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 4, 0),
                Enabled = false
            };
            PluginTheme.ApplyGhostButton(_renameButton);
            _renameButton.Click += async (_, __) => await RenameActiveAsync();
            actions.Controls.Add(_renameButton, 0, 0);

            _saveButton = new Button
            {
                Text = "Сохранить",
                Dock = DockStyle.Fill,
                Margin = new Padding(2, 0, 2, 0),
                Enabled = false
            };
            PluginTheme.ApplyPrimaryButton(_saveButton);
            _saveButton.Click += async (_, __) => await SaveActiveAsync();
            actions.Controls.Add(_saveButton, 1, 0);

            _approveButton = new Button
            {
                Text = "На согласование…",
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 0, 0, 0),
                Enabled = false
            };
            PluginTheme.ApplyGhostButton(_approveButton);
            _approveButton.Click += async (_, __) => await SubmitForApprovalAsync();
            actions.Controls.Add(_approveButton, 2, 0);

            _withdrawButton = new Button
            {
                Text = "Отозвать",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 4, 4, 0),
                Enabled = false
            };
            PluginTheme.ApplyDangerButton(_withdrawButton);
            _withdrawButton.Click += async (_, __) => await WithdrawFromApprovalAsync();
            actions.Controls.Add(_withdrawButton, 0, 1);

            _replaceAssigneesButton = new Button
            {
                Text = "Заменить согласующего…",
                Dock = DockStyle.Fill,
                Margin = new Padding(2, 4, 0, 0),
                Enabled = false
            };
            PluginTheme.ApplyGhostButton(_replaceAssigneesButton);
            _replaceAssigneesButton.Click += async (_, __) => await ReplaceApproversAsync();
            actions.Controls.Add(_replaceAssigneesButton, 1, 1);
            actions.SetColumnSpan(_replaceAssigneesButton, 2);

            _labelsToggle = new Button
            {
                Text = LabelsToggleText(false),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0),
                TabStop = false
            };
            PluginTheme.ApplyGhostButton(_labelsToggle);
            _labelsToggle.Click += (_, __) => SetLabelsExpanded(!_labelsExpanded);
            _editorLayout.Controls.Add(_labelsToggle, 0, 3);

            var labelsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 7,
                Padding = new Padding(0)
            };
            labelsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            labelsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            labelsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30));
            for (var i = 0; i < 7; i++)
                labelsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            _labelsBody = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(0),
                Visible = false
            };
            labelsPanel.Dock = DockStyle.Top;
            labelsPanel.Height = LabelsBodyHeightPx;
            _labelsBody.Controls.Add(labelsPanel);
            _editorLayout.Controls.Add(_labelsBody, 0, 4);

            _userCombo = AddLabelCombo(labelsPanel, 0, "Заказчик *");
            _categoryCombo = AddLabelCombo(labelsPanel, 1, "Категория *");
            _brandCombo = AddLabelCombo(labelsPanel, 2, "Бренд *");
            _modelCombo = AddLabelCombo(labelsPanel, 3, "Модель *");
            _perforationCombo = AddLabelCombo(labelsPanel, 4, "Перфорация *", out _addPerforationButton);
            _edgeCombo = AddLabelCombo(labelsPanel, 5, "Кромка *", out _addEdgeButton);
            _sizeCombo = AddLabelCombo(labelsPanel, 6, "Размер панели *", out _addSizeButton);
            _addPerforationButton.Click += async (_, __) => await CreatePerforationAsync().ConfigureAwait(true);
            _addEdgeButton.Click += async (_, __) => await CreateEdgeAsync().ConfigureAwait(true);
            _addSizeButton.Click += async (_, __) => await CreatePanelSizeAsync().ConfigureAwait(true);
            WireLabelCascade(_userCombo);
            WireLabelCascade(_categoryCombo);
            WireLabelCascade(_brandCombo);
            WireLabelCascade(_modelCombo);
            WireLabelCascade(_perforationCombo);
            WireLabelCascade(_edgeCombo);
            WireLabelCascade(_sizeCombo);
            _brandCombo.SelectedIndexChanged += (_, __) => UpdateReferenceCreateButtons();

            // —— Section 2: catalog from API ——
            _catalogGroup = new GroupBox
            {
                Text = "2. Каталог — открыть / удалить черновик",
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 8, 8, 8),
                Margin = new Padding(8, 2, 8, 4)
            };
            PluginTheme.ApplyGroup(_catalogGroup);
            _rootLayout.Controls.Add(_catalogGroup, 0, 2);

            _catalogLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(0, 4, 0, 0)
            };
            _catalogLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            _catalogLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            _catalogLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            _catalogLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _catalogLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            _catalogGroup.Controls.Add(_catalogLayout);

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 1
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            _catalogLayout.Controls.Add(toolbar, 0, 0);

            _filterBox = new TextBox { Dock = DockStyle.Fill };
            PluginTheme.ApplyTextBox(_filterBox);
            _filterBox.TextChanged += (_, __) => ApplyFilter();
            toolbar.Controls.Add(_filterBox, 0, 0);

            _refreshButton = new Button
            {
                Text = "Обновить",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 0)
            };
            PluginTheme.ApplyGhostButton(_refreshButton);
            _refreshButton.Click += async (_, __) => await ReloadAsync();
            toolbar.Controls.Add(_refreshButton, 1, 0);

            _openButton = new Button
            {
                Text = "Открыть",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 0),
                Enabled = false
            };
            PluginTheme.ApplyPrimaryButton(_openButton);
            _openButton.Click += async (_, __) => await OpenSelectedAsync();
            toolbar.Controls.Add(_openButton, 2, 0);

            _deleteButton = new Button
            {
                Text = "Удалить",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 0),
                Enabled = false
            };
            PluginTheme.ApplyDangerButton(_deleteButton);
            _deleteButton.Click += async (_, __) => await DeleteSelectedAsync();
            toolbar.Controls.Add(_deleteButton, 3, 0);

            _catalogFiltersToggle = new Button
            {
                Text = CatalogFiltersToggleText(false),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0),
                TabStop = false
            };
            PluginTheme.ApplyGhostButton(_catalogFiltersToggle);
            _catalogFiltersToggle.Click += (_, __) => SetCatalogFiltersExpanded(!_catalogFiltersExpanded);
            _catalogLayout.Controls.Add(_catalogFiltersToggle, 0, 1);

            _catalogFiltersPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(0),
                Margin = new Padding(0, 2, 0, 2),
                BackColor = PluginTheme.Page,
                Visible = false
            };
            _catalogLayout.Controls.Add(_catalogFiltersPanel, 0, 2);

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false
            };
            PluginTheme.ApplyListView(_list);
            _list.Columns.Add("Имя", 200);
            _list.Columns.Add("Статус", 120);
            _list.Columns.Add("Метки", 100);
            _list.Columns.Add("Комментарий", 160);
            _list.Columns.Add("Обновлён", 110);
            _list.SelectedIndexChanged += async (_, __) =>
            {
                UpdateCatalogActionButtons();
                await OnCatalogSelectionChangedAsync().ConfigureAwait(true);
            };
            _list.DoubleClick += async (_, __) => await OpenSelectedAsync();
            _catalogLayout.Controls.Add(_list, 0, 3);

            _progress = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };
            _catalogLayout.Controls.Add(_progress, 0, 4);

            var footer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = PluginTheme.Footer,
                Padding = new Padding(12, 4, 12, 4)
            };
            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Готово",
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = PluginTheme.Footer,
                ForeColor = PluginTheme.Text,
                Font = PluginTheme.SmallFont
            };
            footer.Controls.Add(_statusLabel);
            _rootLayout.Controls.Add(footer, 0, 3);

            SetLabelsExpanded(false);
            return panel;
        }

        private static string LabelsToggleText(bool expanded) =>
            expanded ? "▾  Метки — свернуть" : "▸  Метки — развернуть";

        private void SetLabelsExpanded(bool expanded)
        {
            _labelsExpanded = expanded;
            if (_labelsToggle != null && !_labelsToggle.IsDisposed)
                _labelsToggle.Text = LabelsToggleText(expanded);
            if (_labelsBody != null && !_labelsBody.IsDisposed)
                _labelsBody.Visible = expanded;
            if (_editorLayout != null && !_editorLayout.IsDisposed && _editorLayout.RowStyles.Count > 4)
                _editorLayout.RowStyles[4] = new RowStyle(
                    SizeType.Absolute, expanded ? LabelsBodyHeightPx : 0);
            if (_rootLayout != null && !_rootLayout.IsDisposed && _rootLayout.RowStyles.Count > 1)
            {
                _rootLayout.RowStyles[1] = new RowStyle(
                    SizeType.Absolute,
                    expanded ? EditorSectionExpandedPx : EditorSectionCollapsedPx);
            }
        }

        private static string CatalogFiltersToggleText(bool expanded) =>
            expanded ? "▾  Фильтры — свернуть" : "▸  Фильтры — развернуть";

        private void SetCatalogFiltersExpanded(bool expanded)
        {
            _catalogFiltersExpanded = expanded;
            if (_catalogFiltersToggle != null && !_catalogFiltersToggle.IsDisposed)
                _catalogFiltersToggle.Text = CatalogFiltersToggleText(expanded);
            if (_catalogFiltersPanel != null && !_catalogFiltersPanel.IsDisposed)
                _catalogFiltersPanel.Visible = expanded;
            if (_catalogLayout != null && !_catalogLayout.IsDisposed && _catalogLayout.RowStyles.Count > 2)
            {
                _catalogLayout.RowStyles[2] = new RowStyle(
                    SizeType.Absolute, expanded ? CatalogFiltersHeightPx : 0);
            }
        }

        private static ComboBox AddLabelCombo(TableLayoutPanel host, int row, string caption)
        {
            host.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                Font = PluginTheme.CaptionFont,
                ForeColor = PluginTheme.Muted,
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);

            var combo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 2, 0, 2)
            };
            PluginTheme.ApplyCombo(combo);
            host.Controls.Add(combo, 1, row);
            // column 2 left empty for rows without «+»
            return combo;
        }

        private static ComboBox AddLabelCombo(
            TableLayoutPanel host,
            int row,
            string caption,
            out Button addButton)
        {
            var combo = AddLabelCombo(host, row, caption);
            addButton = new Button
            {
                Text = "+",
                Dock = DockStyle.Fill,
                Margin = new Padding(2, 2, 0, 2),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                TabStop = false
            };
            PluginTheme.ApplyGhostButton(addButton);
            host.Controls.Add(addButton, 2, row);
            return combo;
        }

        private async Task CreatePanelSizeAsync()
        {
            if (_busy || PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
                return;
            if (!ProductionDrawingCreateDialogs.TryCreatePanelSize(this, out var request))
                return;

            SetBusy(true, "Создание размера панели…");
            try
            {
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    await client.CreatePanelSizeAsync(request).ConfigureAwait(true);
                    await ReloadLabelOptionsAndSelectAsync(
                            "prod_drawing_panel_size_code",
                            request.BuildCode())
                        .ConfigureAwait(true);
                }

                SetStatus("Размер панели создан: " + request.BuildCode());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "DWG dB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus("Ошибка создания размера: " + Short(ex.Message));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task CreatePerforationAsync()
        {
            await CreateBrandEntityAsync(
                    "Новая перфорация",
                    "prod_drawing_perforation_code",
                    create: (client, req, ct) => client.CreatePerforationAsync(req, ct))
                .ConfigureAwait(true);
        }

        private async Task CreateEdgeAsync()
        {
            await CreateBrandEntityAsync(
                    "Новая кромка",
                    "prod_drawing_edge_code",
                    create: (client, req, ct) => client.CreateEdgeAsync(req, ct))
                .ConfigureAwait(true);
        }

        private async Task CreateBrandEntityAsync(
            string title,
            string fieldCode,
            Func<DwgApiClient, BrandEntityCreateRequest, CancellationToken, Task> create)
        {
            if (_busy || PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
                return;

            var brand = GetSelectedCode(_brandCombo);
            if (string.IsNullOrWhiteSpace(brand))
            {
                MessageBox.Show(
                    this,
                    "Сначала выберите бренд.",
                    "DWG dB",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            if (!ProductionDrawingCreateDialogs.TryCreateBrandEntity(this, title, brand!, out var request))
                return;

            SetBusy(true, "Создание…");
            try
            {
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    await create(client, request, CancellationToken.None).ConfigureAwait(true);
                    await ReloadLabelOptionsAndSelectAsync(fieldCode, request.Code)
                        .ConfigureAwait(true);
                }

                SetStatus(title.Replace("Новая ", "").Replace("Новый ", "") + " создана: " + request.Code);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "DWG dB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetStatus("Ошибка создания: " + Short(ex.Message));
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ReloadLabelOptionsAndSelectAsync(string fieldCode, string newCode)
        {
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
                return;

            using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
            {
                var options = await client
                    .GetLabelOptionsAsync(CollectLabelSelections())
                    .ConfigureAwait(true);

                var preserved = new ProductionDrawingLabels
                {
                    UserUuid = GetSelectedCode(_userCombo) ?? string.Empty,
                    GlobalCategoryCode = GetSelectedCode(_categoryCombo) ?? string.Empty,
                    BrandCode = GetSelectedCode(_brandCombo) ?? string.Empty,
                    ModelCode = GetSelectedCode(_modelCombo) ?? string.Empty,
                    PerforationCode = GetSelectedCode(_perforationCombo) ?? string.Empty,
                    EdgeCode = GetSelectedCode(_edgeCombo) ?? string.Empty,
                    PanelSizeCode = GetSelectedCode(_sizeCombo) ?? string.Empty
                };

                if (string.Equals(fieldCode, "prod_drawing_perforation_code", StringComparison.OrdinalIgnoreCase))
                    preserved.PerforationCode = newCode;
                else if (string.Equals(fieldCode, "prod_drawing_edge_code", StringComparison.OrdinalIgnoreCase))
                    preserved.EdgeCode = newCode;
                else if (string.Equals(fieldCode, "prod_drawing_panel_size_code", StringComparison.OrdinalIgnoreCase))
                    preserved.PanelSizeCode = newCode;

                _suppressLabelCascade = true;
                try
                {
                    BindLabelComboOptions(options);
                    // Ensure newly created option is present even if refs cache lag.
                    EnsureOptionPresent(fieldCode, newCode);
                    ApplyLabelsToUi(preserved);
                }
                finally
                {
                    _suppressLabelCascade = false;
                }
            }

            UpdateReferenceCreateButtons();
        }

        private void EnsureOptionPresent(string fieldCode, string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return;

            ComboBox? combo = null;
            if (string.Equals(fieldCode, "prod_drawing_perforation_code", StringComparison.OrdinalIgnoreCase))
                combo = _perforationCombo;
            else if (string.Equals(fieldCode, "prod_drawing_edge_code", StringComparison.OrdinalIgnoreCase))
                combo = _edgeCombo;
            else if (string.Equals(fieldCode, "prod_drawing_panel_size_code", StringComparison.OrdinalIgnoreCase))
                combo = _sizeCombo;

            if (combo == null)
                return;

            foreach (var item in combo.Items)
            {
                if (item is FilterOption fo
                    && string.Equals(fo.Code, code, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            combo.Items.Add(new FilterOption { Code = code, Name = code });
        }

        private void UpdateReferenceCreateButtons()
        {
            var editorOn = _perforationCombo != null && _perforationCombo.Enabled;
            var hasBrand = !string.IsNullOrWhiteSpace(GetSelectedCode(_brandCombo));
            if (_addSizeButton != null && !_addSizeButton.IsDisposed)
                _addSizeButton.Enabled = editorOn && !_busy;
            if (_addPerforationButton != null && !_addPerforationButton.IsDisposed)
                _addPerforationButton.Enabled = editorOn && hasBrand && !_busy;
            if (_addEdgeButton != null && !_addEdgeButton.IsDisposed)
                _addEdgeButton.Enabled = editorOn && hasBrand && !_busy;
        }

        public void RefreshOnShow()
        {
            ReloadSettingsIntoUi();
            UpdateActiveLabel();
            _ = EnsureSessionThenLoadAsync();
        }

        private void ReloadSettingsIntoUi()
        {
            try
            {
                PluginApp.Settings = SettingsLoader.Load(PluginApp.PluginDirectory);
            }
            catch
            {
                // keep current Settings
            }

            var url = PluginApp.Settings.ApiBaseUrl;
            if (string.IsNullOrWhiteSpace(url)
                || url.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                url = "https://dev3.constrtodo.ru:3005";
                PluginApp.Settings.ApiBaseUrl = url;
                try
                {
                    SettingsLoader.SaveForPlugin(PluginApp.PluginDirectory, PluginApp.Settings);
                }
                catch
                {
                    // ignore write failures (file locked)
                }
            }

            if (_apiUrlBox != null && !_apiUrlBox.IsDisposed)
                _apiUrlBox.Text = url;

            var placeholder = url.IndexOf("example.com", StringComparison.OrdinalIgnoreCase) >= 0;
            if (_loginStatus != null && !_loginStatus.IsDisposed)
            {
                _loginStatus.ForeColor = placeholder ? PluginTheme.Danger : PluginTheme.Muted;
                if (placeholder)
                    _loginStatus.Text = "Укажите реальный адрес API выше.";
            }
        }

        private async Task EnsureSessionThenLoadAsync()
        {
            if (_busy) return;

            var saved = PluginApp.Session ?? AuthSessionStore.Load();
            if (saved != null && !string.IsNullOrWhiteSpace(saved.Email) && string.IsNullOrWhiteSpace(_emailBox.Text))
                _emailBox.Text = saved.Email;

            if (!_sessionChecked && saved != null && saved.IsAuthenticated)
            {
                SetLoginStatus("Проверка сессии…");
                SetBusy(true);
                try
                {
                    var auth = new AuthApiClient(PluginApp.Settings);
                    PluginApp.Session = await auth.GetSessionAsync(saved).ConfigureAwait(true);
                    _sessionChecked = true;
                    EnterCatalog();
                    await ReloadAsync().ConfigureAwait(true);
                    return;
                }
                catch (Exception ex)
                {
                    PluginApp.Session = null;
                    AuthSessionStore.Clear();
                    _sessionChecked = true;
                    ShowLogin(true);
                    SetLoginStatus("Сессия истекла. Войдите снова. (" + Short(ex.Message) + ")");
                }
                finally
                {
                    SetBusy(false);
                }
                return;
            }

            if (PluginApp.Session != null && PluginApp.Session.IsAuthenticated)
            {
                EnterCatalog();
                if (_allFiles.Count == 0)
                    await ReloadAsync().ConfigureAwait(true);
                return;
            }

            ShowLogin(true);
        }

        private async Task LoginAsync()
        {
            if (_busy) return;
            SetBusy(true);
            SetLoginStatus("Вход…");

            try
            {
                ApplyApiUrlFromUi();
                var auth = new AuthApiClient(PluginApp.Settings);
                PluginApp.Session = await auth.LoginAsync(_emailBox.Text, _passwordBox.Text)
                    .ConfigureAwait(true);
                _passwordBox.Clear();
                _sessionChecked = true;
                EnterCatalog();
                SetStatus("Вход выполнен. Загрузка каталога…");
                await ReloadAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                var detail = ex.Message;
                var logHint = Environment.NewLine + Environment.NewLine +
                              "Лог: %LocalAppData%\\AcadDwgBrowser\\login-debug.log";
                SetLoginStatus("Ошибка: " + detail);
                MessageBox.Show(this, detail + logHint, "Вход", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ApplyApiUrlFromUi()
        {
            var url = (_apiUrlBox.Text ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(url))
                throw new InvalidOperationException("Укажите адрес API (например https://your-server).");

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;

            PluginApp.Settings.ApiBaseUrl = url;
            SettingsLoader.SaveForPlugin(PluginApp.PluginDirectory, PluginApp.Settings);
            _apiUrlBox.Text = url;
        }

        private async Task LogoutAsync()
        {
            if (_busy) return;
            SetBusy(true, "Выход…");
            try
            {
                var session = PluginApp.Session;
                PluginApp.Session = null;
                _allFiles.Clear();
                ApplyFilter();
                if (session != null)
                {
                    var auth = new AuthApiClient(PluginApp.Settings);
                    await auth.LogoutAsync(session).ConfigureAwait(true);
                }
                else
                {
                    AuthSessionStore.Clear();
                }
            }
            catch
            {
                AuthSessionStore.Clear();
            }
            finally
            {
                SetBusy(false);
                ShowLogin(true);
                SetLoginStatus("Вы вышли из системы.");
            }
        }

        private void EnterCatalog()
        {
            var session = PluginApp.Session;
            var name = session?.User?.DisplayName ?? session?.Email ?? "пользователь";
            _userLabel.Text = name;
            PluginApp.Settings.ContentType = "production_drawings";
            ShowLogin(false);
            SetStatus("Вход выполнен: " + name);
        }

        private void ShowLogin(bool show)
        {
            _loginPanel.Visible = show;
            _catalogPanel.Visible = !show;
            if (show)
                _loginPanel.BringToFront();
            else
                _catalogPanel.BringToFront();
        }

        private async Task ReloadAsync()
        {
            if (_busy) return;
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
            {
                ShowLogin(true);
                return;
            }

            SetBusy(true, "Загрузка списка…");
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                await RefreshCatalogListAsync(_cts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                // Do not wipe the login session just because the DWG catalog endpoint failed.
                SetStatus("Ошибка API: " + ex.Message);
                MessageBox.Show(this, ex.Message, "DWG dB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task RefreshCatalogListAsync(CancellationToken cancellationToken)
        {
            PluginApp.Settings.ContentType = "production_drawings";
            using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session!))
            {
                // Web parity: list filters come from GET /content/list (not /content/filters).
                var catalog = await client
                    .ListCatalogAsync(CollectCatalogFilterParams(), cancellationToken)
                    .ConfigureAwait(true);
                BindCatalogFilters(catalog.Filters);

                _allFiles = new List<DwgFileInfo>(catalog.Files);
                ApplyDisplayNameOverrides();
                ApplyFilter();

                // Web parity: label combos use form POST + references (+ cascade query).
                var labelOptions = await client
                    .GetLabelOptionsAsync(CollectLabelSelections(), cancellationToken)
                    .ConfigureAwait(true);
                BindFilters(labelOptions);

                SetStatus($"Производственные чертежи: {_allFiles.Count}");
            }
        }

        private Dictionary<string, string> CollectCatalogFilterParams()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in _catalogFilterValues)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
                    map[pair.Key] = pair.Value;
            }

            return map;
        }

        private Dictionary<string, string> CollectLabelSelections()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Add(string code, ComboBox combo)
            {
                var value = GetSelectedCode(combo);
                if (!string.IsNullOrWhiteSpace(value))
                    map[code] = value!;
            }

            Add("user_uuid", _userCombo);
            Add("global_category_code", _categoryCombo);
            Add("global_cat_code", _categoryCombo);
            Add("brand_code", _brandCombo);
            Add("model_code", _modelCombo);
            Add("prod_drawing_perforation_code", _perforationCombo);
            Add("prod_drawing_edge_code", _edgeCombo);
            Add("prod_drawing_panel_size_code", _sizeCombo);
            return map;
        }

        private void WireLabelCascade(ComboBox combo)
        {
            combo.SelectedIndexChanged += async (_, __) =>
            {
                if (_suppressLabelCascade || _busy)
                    return;
                await OnLabelCascadeAsync().ConfigureAwait(true);
            };
        }

        private async Task OnLabelCascadeAsync()
        {
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
                return;

            _labelCascadeCts?.Cancel();
            _labelCascadeCts = new CancellationTokenSource();
            var token = _labelCascadeCts.Token;

            try
            {
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    var options = await client
                        .GetLabelOptionsAsync(CollectLabelSelections(), token)
                        .ConfigureAwait(true);
                    if (token.IsCancellationRequested)
                        return;

                    var preserved = new ProductionDrawingLabels
                    {
                        UserUuid = GetSelectedCode(_userCombo) ?? string.Empty,
                        GlobalCategoryCode = GetSelectedCode(_categoryCombo) ?? string.Empty,
                        BrandCode = GetSelectedCode(_brandCombo) ?? string.Empty,
                        ModelCode = GetSelectedCode(_modelCombo) ?? string.Empty,
                        PerforationCode = GetSelectedCode(_perforationCombo) ?? string.Empty,
                        EdgeCode = GetSelectedCode(_edgeCombo) ?? string.Empty,
                        PanelSizeCode = GetSelectedCode(_sizeCombo) ?? string.Empty
                    };

                    _suppressLabelCascade = true;
                    try
                    {
                        BindLabelComboOptions(options);
                        ApplyLabelsToUi(preserved.HasAnyValue ? preserved : null);
                    }
                    finally
                    {
                        _suppressLabelCascade = false;
                    }
                }
            }
                    catch (OperationCanceledException)
            {
                // newer cascade started
            }
            catch
            {
                // keep previous options if cascade fails
            }
        }

        private void BindCatalogFilters(IReadOnlyList<FilterEntity> filters)
        {
            _catalogFilters = filters != null
                ? new List<FilterEntity>(filters)
                : new List<FilterEntity>();

            _suppressCatalogFilterEvents = true;
            try
            {
                _catalogFiltersPanel.SuspendLayout();
                _catalogFiltersPanel.Controls.Clear();

                foreach (var filter in _catalogFilters)
                {
                    if (filter == null || string.IsNullOrWhiteSpace(filter.Code))
                        continue;
                    // Skip empty option sets — nothing to filter by.
                    if (filter.Options == null || filter.Options.Count == 0)
                        continue;

                    var combo = new ComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList,
                        Width = 150,
                        Margin = new Padding(0, 2, 6, 2),
                        Tag = filter.Code
                    };
                    PluginTheme.ApplyCombo(combo);
                    combo.Items.Add(new FilterOption
                    {
                        Code = string.Empty,
                        Name = "— " + (string.IsNullOrWhiteSpace(filter.Name) ? filter.Code : filter.Name) + " —"
                    });
                    foreach (var opt in filter.Options)
                    {
                        if (opt == null || string.IsNullOrWhiteSpace(opt.Code))
                            continue;
                        combo.Items.Add(opt);
                    }

                    var selected = 0;
                    if (_catalogFilterValues.TryGetValue(filter.Code, out var current)
                        && !string.IsNullOrWhiteSpace(current))
                    {
                        for (var i = 0; i < combo.Items.Count; i++)
                        {
                            if (combo.Items[i] is FilterOption fo
                                && string.Equals(fo.Code, current, StringComparison.OrdinalIgnoreCase))
                            {
                                selected = i;
                                break;
                            }
                        }
                    }

                    combo.SelectedIndex = combo.Items.Count > 0 ? selected : -1;
                    combo.SelectedIndexChanged += async (_, __) =>
                    {
                        if (_suppressCatalogFilterEvents || _busy)
                            return;
                        var code = combo.Tag as string;
                        if (string.IsNullOrWhiteSpace(code))
                            return;
                        var value = GetSelectedCode(combo);
                        if (string.IsNullOrWhiteSpace(value))
                            _catalogFilterValues.Remove(code!);
                        else
                            _catalogFilterValues[code!] = value!;
                        await ReloadAsync().ConfigureAwait(true);
                    };
                    _catalogFiltersPanel.Controls.Add(combo);
                }
            }
            finally
            {
                _catalogFiltersPanel.ResumeLayout();
                _suppressCatalogFilterEvents = false;
            }
        }

        private async Task OpenSelectedAsync()
        {
            if (_busy || _list.SelectedItems.Count == 0)
                return;
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
            {
                ShowLogin(true);
                return;
            }

            var file = _list.SelectedItems[0].Tag as DwgFileInfo;
            if (file == null)
                return;

            SetBusy(true, "Скачивание " + file.Name + "…");
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _progress.Value = 0;

            try
            {
                var dest = PluginApp.Settings.ResolveDownloadDirectory();
                var progress = new Progress<double>(p =>
                {
                    if (IsHandleCreated && !IsDisposed)
                        _progress.Value = Math.Max(0, Math.Min(100, (int)(p * 100)));
                });

                string localPath;
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    localPath = await client.DownloadFileAsync(file, dest, progress, _cts.Token)
                        .ConfigureAwait(true);
                }

                SetStatus("Открытие в AutoCAD…");
                var editable = CanEditContent(file.Status);
                AcadDocumentService.OpenDwg(localPath, readOnly: !editable);
                file.LocalPath = localPath;
                OpenDrawingRegistry.Register(localPath, file);

                // Bind whatever path AutoCAD reports for the active document.
                var activePath = AcadDocumentService.TryGetActiveDocumentPath();
                if (!string.IsNullOrWhiteSpace(activePath))
                    OpenDrawingRegistry.Register(activePath!, file);

                AcadDocumentService.WriteMessage(
                    editable
                        ? "Открыт файл: " + localPath
                        : "Открыт только для просмотра (" + FormatStatus(file.Status) + "): " + localPath);
                SetStatus(
                    editable
                        ? "Открыто: " + file.Name
                        : "Просмотр: " + file.Name + " [" + FormatStatus(file.Status) + "]");
                ApplyLabelsToUi(file.Labels);
                UpdateActiveLabel();

                // Open may finish slightly later when queued to the application thread.
                var captured = file;
                var capturedPath = localPath;
                var timer = new System.Windows.Forms.Timer { Interval = 300 };
                var ticks = 0;
                timer.Tick += (_, __) =>
                {
                    ticks++;
                    var path = AcadDocumentService.TryGetActiveDocumentPath();
                    if (!string.IsNullOrWhiteSpace(path))
                        OpenDrawingRegistry.Register(path!, captured);
                    else
                        OpenDrawingRegistry.Register(capturedPath, captured);
                    UpdateActiveLabel();
                    DwgFileInfo linked;
                    string linkedPath;
                    if (ticks >= 5 || OpenDrawingRegistry.TryGetCurrent(out linked, out linkedPath))
                    {
                        timer.Stop();
                        timer.Dispose();
                    }
                };
                timer.Start();
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
                MessageBox.Show(this, ex.Message, "DWG dB", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
                _progress.Value = 0;
            }
        }

        private async Task DeleteSelectedAsync()
        {
            if (_busy || _list.SelectedItems.Count == 0)
                return;
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
            {
                ShowLogin(true);
                return;
            }

            var file = _list.SelectedItems[0].Tag as DwgFileInfo;
            if (file == null || string.IsNullOrWhiteSpace(file.Id))
            {
                MessageBox.Show(
                    this,
                    "У выбранной записи нет Id — удаление через API невозможно.\n"
                    + "Нажмите «Обновить» и попробуйте снова.",
                    "Удаление",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (!CanDeleteContent(file.Status))
            {
                MessageBox.Show(
                    this,
                    "Удалять можно только конструкции в статусе «Черновик».\nТекущий статус: "
                    + FormatStatus(file.Status),
                    "Удаление",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                "Удалить конструкцию «" + file.Name + "»?\nЭто действие нельзя отменить.",
                "Удаление",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes)
                return;

            SetBusy(true, "Удаление " + file.Name + "…");
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                await EnsureWriteSessionAsync(_cts.Token).ConfigureAwait(true);
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    try
                    {
                        await client.DeleteContentAsync(file.Id, _cts.Token).ConfigureAwait(true);
                    }
                    catch (Exception ex) when (IsAlreadyDeletedError(ex))
                    {
                        // Already gone on server — still drop from UI.
                    }
                }

                _allFiles.RemoveAll(f =>
                    string.Equals(f.Id, file.Id, StringComparison.OrdinalIgnoreCase));
                ApplyFilter();

                AcadDocumentService.WriteMessage("Удалено: " + file.Name);
                SetStatus("Удалено: " + file.Name);
                await RefreshCatalogListAsync(_cts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Удаление", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static bool IsAlreadyDeletedError(Exception ex)
        {
            var msg = ex.Message ?? string.Empty;
            return msg.IndexOf("no rows", StringComparison.OrdinalIgnoreCase) >= 0
                   || msg.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0
                   || msg.IndexOf("не найден", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDraftStatus(string? status) =>
            ContentStatusUi.Normalize(status) == "draft";

        private static bool IsRejectedStatus(string? status) =>
            ContentStatusUi.Normalize(status) == "rejected";

        private static bool IsOnApprovalStatus(string? status)
        {
            var n = ContentStatusUi.Normalize(status);
            return n == "pending" || n == "in_review";
        }

        private static string FormatStatus(string? status) =>
            ContentStatusUi.ToDisplayName(status);

        /// <summary>draft / rejected — можно смотреть и редактировать.</summary>
        private static bool CanEditContent(string? status) =>
            IsDraftStatus(status) || IsRejectedStatus(status);

        /// <summary>Только draft — можно удалять.</summary>
        private static bool CanDeleteContent(string? status) =>
            IsDraftStatus(status);

        private void UpdateCatalogActionButtons()
        {
            var hasSelection = !_busy && _list.SelectedItems.Count > 0;
            _openButton.Enabled = hasSelection;

            DwgFileInfo? file = null;
            if (hasSelection)
                file = _list.SelectedItems[0].Tag as DwgFileInfo;

            _deleteButton.Enabled = file != null && CanDeleteContent(file.Status);

            if (file != null && !CanEditContent(file.Status))
                _openButton.Text = "Просмотр";
            else
                _openButton.Text = "Открыть";
        }

        private async Task RenameActiveAsync()
        {
            if (_busy)
                return;
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
            {
                ShowLogin(true);
                return;
            }

            // Prefer active catalog drawing; else selected draft/rejected in the list.
            DwgFileInfo? file = null;
            if (OpenDrawingRegistry.TryGetCurrent(out var current, out _)
                && !string.IsNullOrWhiteSpace(current.Id))
            {
                file = current;
            }
            else if (_list.SelectedItems.Count > 0
                     && _list.SelectedItems[0].Tag is DwgFileInfo selected
                     && !string.IsNullOrWhiteSpace(selected.Id)
                     && CanEditContent(selected.Status))
            {
                file = selected;
            }

            // Existing catalog drawing → rename on server (does not require an open DWG).
            if (file != null && !string.IsNullOrWhiteSpace(file.Id))
            {
                if (!CanEditContent(file.Status))
                {
                    MessageBox.Show(
                        this,
                        "Имя можно менять только в статусах «Черновик» и «На доработку» через «Задать имя…».",
                        "Переименование",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var currentName = ResolveKnownDisplayName(file);
                // Prefer the name already typed in the editor box; do not overwrite it first.
                var fromBox = GetEditorNameOrNull();
                var newName = PromptName(
                    !string.IsNullOrWhiteSpace(fromBox) ? fromBox! : currentName);
                if (newName == null)
                    return;
                if (string.Equals(newName, currentName, StringComparison.Ordinal))
                {
                    SetNameBoxText(newName);
                    SetStatus("Имя без изменений: " + newName);
                    return;
                }

                SetNameBoxText(newName);
                SetBusy(true, "Переименование…");
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                try
                {
                    await EnsureWriteSessionAsync(_cts.Token).ConfigureAwait(true);
                    DwgFileInfo updated;
                    using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                    {
                        updated = await client.UpdateContentAsync(
                                file.Id,
                                newName: newName,
                                localDwgPath: null,
                                dwgFieldCode: file.DwgFieldCode,
                                labels: file.Labels,
                                cancellationToken: _cts.Token)
                            .ConfigureAwait(true);
                    }

                    updated.LocalPath = file.LocalPath;
                    updated.Name = newName;
                    updated.Status = file.Status ?? updated.Status;
                    updated.Labels = file.Labels?.Clone() ?? updated.Labels;
                    RememberDisplayName(updated.Id, newName);

                    // Align local file + AutoCAD tab with catalog name.
                    try
                    {
                        var destDir = PluginApp.Settings.ResolveDownloadDirectory();
                        Directory.CreateDirectory(destDir);
                        var newLocal = BuildCatalogLocalPath(destDir, newName, updated.Id);
                        var activePath = AcadDocumentService.TryGetActiveDocumentPath();
                        var isActive = !string.IsNullOrWhiteSpace(file.LocalPath)
                            && !string.IsNullOrWhiteSpace(activePath)
                            && string.Equals(
                                Path.GetFullPath(file.LocalPath!),
                                Path.GetFullPath(activePath!),
                                StringComparison.OrdinalIgnoreCase);

                        if (isActive)
                        {
                            newLocal = AcadDocumentService.SaveActiveDocumentAs(newLocal);
                            updated.LocalPath = newLocal;
                        }
                        else if (!string.IsNullOrWhiteSpace(file.LocalPath)
                                 && File.Exists(file.LocalPath)
                                 && !string.Equals(
                                     Path.GetFullPath(file.LocalPath),
                                     Path.GetFullPath(newLocal),
                                     StringComparison.OrdinalIgnoreCase))
                        {
                            ClearReadOnlyIfNeeded(newLocal);
                            if (File.Exists(newLocal))
                                File.Delete(newLocal);
                            File.Move(file.LocalPath!, newLocal);
                            updated.LocalPath = newLocal;
                        }
                        else if (string.IsNullOrWhiteSpace(updated.LocalPath))
                        {
                            updated.LocalPath = newLocal;
                        }
                    }
                    catch
                    {
                        // Keep previous local path if rename on disk failed.
                    }

                    ApplyUpdatedCatalogItem(file.Id, updated);
                    UpdateListItemName(updated);
                    if (!string.IsNullOrWhiteSpace(updated.LocalPath))
                        OpenDrawingRegistry.Register(updated.LocalPath!, updated);
                    OpenDrawingRegistry.PendingNewName = null;
                    UpdateActiveLabel();
                    AcadDocumentService.WriteMessage("Переименовано на сервере: " + newName);
                    SetStatus("Переименовано в базе: " + newName);
                }
                catch (OperationCanceledException)
                {
                    SetStatus("Отменено");
                }
                catch (Exception ex)
                {
                    SetStatus("Ошибка: " + ex.Message);
                    MessageBox.Show(this, ex.Message, "Переименование", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetBusy(false);
                }

                return;
            }

            // New AutoCAD drawing → store name for the next Save (create in catalog).
            if (!AcadDocumentService.HasActiveDocument())
            {
                MessageBox.Show(
                    this,
                    "Выберите черновик или запись «На доработку» в каталоге или откройте новый чертёж в AutoCAD.",
                    "Переименование",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var suggested = GetEditorNameOrNull()
                            ?? OpenDrawingRegistry.PendingNewName
                            ?? AcadDocumentService.TryGetActiveDocumentTitle()
                            ?? "Новый чертёж";
            var name = PromptName(suggested);
            if (name == null)
                return;

            OpenDrawingRegistry.PendingNewName = name;
            SetNameBoxText(name);
            UpdateActiveLabel();
            SetStatus("Имя задано: " + name + ". Нажмите «Сохранить в каталог».");
            AcadDocumentService.WriteMessage("Имя нового чертежа: " + name);
        }

        private async Task SubmitForApprovalAsync()
        {
            if (_busy)
                return;
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
            {
                ShowLogin(true);
                return;
            }

            DwgFileInfo? file = null;
            if (OpenDrawingRegistry.TryGetCurrent(out var current, out _)
                && !string.IsNullOrWhiteSpace(current.Id)
                && IsDraftStatus(current.Status))
            {
                file = current;
            }
            else if (_list.SelectedItems.Count > 0
                     && _list.SelectedItems[0].Tag is DwgFileInfo selected
                     && !string.IsNullOrWhiteSpace(selected.Id)
                     && IsDraftStatus(selected.Status))
            {
                file = selected;
            }

            if (file == null || string.IsNullOrWhiteSpace(file.Id))
            {
                MessageBox.Show(
                    this,
                    "Отправка на согласование доступна только для черновика из каталога.",
                    "Согласование",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            SetBusy(true, "Загрузка согласующих…");
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                await EnsureWriteSessionAsync(_cts.Token).ConfigureAwait(true);

                IReadOnlyList<ApprovalPreviewStep> steps;
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    steps = await client.GetApprovalPreviewAsync(file.Id, _cts.Token)
                        .ConfigureAwait(true);
                }

                if (steps == null || steps.Count == 0)
                {
                    MessageBox.Show(
                        this,
                        "Для этого чертежа нет шагов согласования (проверьте политику подписания).",
                        "Согласование",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                StartApprovalProcessRequest? request;
                using (var dlg = ApprovalSubmitDialog.Create(steps, ResolveKnownDisplayName(file)))
                {
                    var owner = FindForm() as IWin32Window ?? this;
                    var dialogResult = dlg.ShowDialog(owner);
                    if (dialogResult != DialogResult.OK || dlg.Result == null)
                    {
                        SetStatus("Отправка на согласование отменена");
                        return;
                    }

                    request = dlg.Result;
                }

                SetStatus("Отправка на согласование…");
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    await client.StartApprovalAsync(file.Id, request, _cts.Token)
                        .ConfigureAwait(true);
                }

                file.Status = "pending";
                var match = _allFiles.Find(f =>
                    string.Equals(f.Id, file.Id, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    match.Status = "pending";
                if (!string.IsNullOrWhiteSpace(file.LocalPath))
                    OpenDrawingRegistry.Register(file.LocalPath!, file);

                AcadDocumentService.WriteMessage(
                    "Отправлено на согласование: " + ResolveKnownDisplayName(file));
                SetStatus("Отправлено на согласование: " + ResolveKnownDisplayName(file));

                await RefreshCatalogListAsync(_cts.Token).ConfigureAwait(true);
                ApplyDisplayNameOverrides();
                UpdateActiveLabel();
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Согласование",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private DwgFileInfo? TryGetTargetFile(Func<string?, bool> statusMatches)
        {
            if (OpenDrawingRegistry.TryGetCurrent(out var current, out _)
                && !string.IsNullOrWhiteSpace(current.Id)
                && statusMatches(current.Status))
            {
                return current;
            }

            if (_list.SelectedItems.Count > 0
                && _list.SelectedItems[0].Tag is DwgFileInfo selected
                && !string.IsNullOrWhiteSpace(selected.Id)
                && statusMatches(selected.Status))
            {
                return selected;
            }

            return null;
        }

        private void ApplyLocalStatus(DwgFileInfo file, string status)
        {
            file.Status = status;
            var match = _allFiles.Find(f =>
                string.Equals(f.Id, file.Id, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                match.Status = status;
            if (!string.IsNullOrWhiteSpace(file.LocalPath))
                OpenDrawingRegistry.Register(file.LocalPath!, file);
        }

        private async Task WithdrawFromApprovalAsync()
        {
            if (_busy)
                return;
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
            {
                ShowLogin(true);
                return;
            }

            var file = TryGetTargetFile(IsOnApprovalStatus);
            if (file == null || string.IsNullOrWhiteSpace(file.Id))
            {
                MessageBox.Show(
                    this,
                    "Отзыв доступен только для чертежа на согласовании.",
                    "Согласование",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var name = ResolveKnownDisplayName(file);
            var confirm = MessageBox.Show(
                this,
                "Отозвать «" + name + "» с согласования?\nЧертёж вернётся в черновики, текущие шаги и подписи будут удалены.",
                "Отзыв с согласования",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes)
                return;

            SetBusy(true, "Отзыв с согласования…");
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                await EnsureWriteSessionAsync(_cts.Token).ConfigureAwait(true);
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    await client.WithdrawApprovalAsync(file.Id, _cts.Token).ConfigureAwait(true);
                }

                ApplyLocalStatus(file, "draft");
                AcadDocumentService.WriteMessage("Отозвано с согласования: " + name);
                SetStatus("Отозвано с согласования: " + name);

                await RefreshCatalogListAsync(_cts.Token).ConfigureAwait(true);
                ApplyDisplayNameOverrides();
                UpdateActiveLabel();
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Отзыв с согласования",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ReplaceApproversAsync()
        {
            if (_busy)
                return;
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
            {
                ShowLogin(true);
                return;
            }

            var file = TryGetTargetFile(IsOnApprovalStatus);
            if (file == null || string.IsNullOrWhiteSpace(file.Id))
            {
                MessageBox.Show(
                    this,
                    "Замена согласующего доступна только для чертежа на согласовании.",
                    "Согласование",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            SetBusy(true, "Загрузка согласующих…");
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                await EnsureWriteSessionAsync(_cts.Token).ConfigureAwait(true);

                IReadOnlyList<ContentApprovalStep> steps;
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    steps = await client.GetActiveApprovalPreviewAsync(file.Id, _cts.Token)
                        .ConfigureAwait(true);
                }

                if (steps == null || steps.Count == 0)
                {
                    MessageBox.Show(
                        this,
                        "Нет незавершённых шагов: все согласующие уже приняли решение, или процесс недоступен.",
                        "Замена согласующих",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                IReadOnlyList<UpdateApprovalAssigneesRequest> requests;
                using (var dlg = ApprovalAssigneesDialog.Create(steps, ResolveKnownDisplayName(file)))
                {
                    var owner = FindForm() as IWin32Window ?? this;
                    var dialogResult = dlg.ShowDialog(owner);
                    if (dialogResult != DialogResult.OK || dlg.Result == null || dlg.Result.Count == 0)
                    {
                        SetStatus("Замена согласующих отменена");
                        return;
                    }

                    requests = dlg.Result;
                }

                SetStatus("Обновление согласующих…");
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    foreach (var request in requests)
                    {
                        await client.UpdateApprovalAssigneesAsync(file.Id, request, _cts.Token)
                            .ConfigureAwait(true);
                    }
                }

                var name = ResolveKnownDisplayName(file);
                AcadDocumentService.WriteMessage("Согласующие обновлены: " + name);
                SetStatus("Согласующие обновлены: " + name);

                await RefreshCatalogListAsync(_cts.Token).ConfigureAwait(true);
                ApplyDisplayNameOverrides();
                UpdateActiveLabel();
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Замена согласующих",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task SaveActiveAsync()
        {
            if (_busy)
                return;
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
            {
                ShowLogin(true);
                return;
            }

            TryGetSelectedLabels(out var labels, out _);
            var hasDoc = AcadDocumentService.HasActiveDocument();

            // New / unlinked AutoCAD drawing → create draft in catalog.
            if (hasDoc && OpenDrawingRegistry.IsActiveUnlinkedDocument())
            {
                await SaveNewDrawingAsync().ConfigureAwait(true);
                return;
            }

            // Existing catalog drawing (open in AutoCAD or last opened with local file).
            if (OpenDrawingRegistry.TryGetCurrent(out var file, out var localPath)
                && !string.IsNullOrWhiteSpace(file.Id))
            {
                if (!CanEditContent(file.Status))
                {
                    MessageBox.Show(
                        this,
                        "Редактирование недоступно для статуса «" + FormatStatus(file.Status)
                        + "». Доступен только просмотр.",
                        "Сохранение",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                if (hasDoc && AcadDocumentService.IsActiveDocumentReadOnly())
                {
                    MessageBox.Show(
                        this,
                        "Чертёж открыт в AutoCAD только для чтения.\n" +
                        "Закройте его и откройте снова через каталог (статус «Черновик» / «На доработку»).",
                        "Сохранение",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                SetBusy(true, "Сохранение…");
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                _progress.Value = 0;

                try
                {
                    string? uploadPath = null;
                    var oldId = file.Id;
                    if (hasDoc)
                    {
                        SetStatus("Сохранение в AutoCAD…");
                        var workPath = ResolveWritableWorkPath(file, localPath);
                        uploadPath = AcadDocumentService.SaveActiveDocument(workPath);
                        file.LocalPath = uploadPath;
                        OpenDrawingRegistry.Register(uploadPath, file);
                    }
                    else if (!string.IsNullOrWhiteSpace(file.LocalPath) && File.Exists(file.LocalPath))
                    {
                        uploadPath = file.LocalPath;
                    }
                    else if (_list.SelectedItems.Count > 0
                             && _list.SelectedItems[0].Tag is DwgFileInfo selected
                             && string.Equals(selected.Id, file.Id, StringComparison.OrdinalIgnoreCase)
                             && !string.IsNullOrWhiteSpace(selected.LocalPath)
                             && File.Exists(selected.LocalPath))
                    {
                        uploadPath = selected.LocalPath;
                    }

                    if (string.IsNullOrWhiteSpace(uploadPath) && !labels.HasAnyValue)
                    {
                        MessageBox.Show(
                            this,
                            "Нет локального DWG для отправки и не выбраны метки.\n" +
                            "Откройте чертёж из каталога, измените и нажмите «Сохранить».",
                            "Сохранение",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    SetStatus(string.IsNullOrWhiteSpace(uploadPath)
                        ? "Сохранение меток…"
                        : "Отправка на сервер…");
                    await EnsureWriteSessionAsync(_cts.Token).ConfigureAwait(true);
                    var progress = new Progress<double>(p =>
                    {
                        if (IsHandleCreated && !IsDisposed)
                            _progress.Value = Math.Max(0, Math.Min(100, (int)(p * 100)));
                    });

                    var preservedName = ResolveKnownDisplayName(file);
                    if (string.IsNullOrWhiteSpace(preservedName)
                        || preservedName.StartsWith("Без имени", StringComparison.OrdinalIgnoreCase))
                    {
                        // Fall back to editor text only for display after save; do not rename on save.
                        preservedName = GetEditorNameOrNull() ?? preservedName;
                    }

                    DwgFileInfo updated;
                    using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                    {
                        // newName: null — имя не меняем при сохранении (только через «Задать имя…»).
                        updated = await client.UpdateContentAsync(
                                oldId,
                                newName: null,
                                localDwgPath: uploadPath,
                                dwgFieldCode: file.DwgFieldCode,
                                labels: labels.HasAnyValue ? labels : null,
                                knownDisplayName: preservedName,
                                progress: progress,
                                cancellationToken: _cts.Token)
                            .ConfigureAwait(true);
                    }

                    if (!string.IsNullOrWhiteSpace(updated.Name)
                        && !updated.Name.StartsWith("tmp-", StringComparison.OrdinalIgnoreCase)
                        && !updated.Name.StartsWith("Без имени", StringComparison.OrdinalIgnoreCase))
                    {
                        preservedName = updated.Name;
                    }
                    else if (!string.IsNullOrWhiteSpace(preservedName))
                    {
                        updated.Name = preservedName!;
                    }

                    RememberDisplayName(updated.Id, updated.Name);
                    if (!string.Equals(oldId, updated.Id, StringComparison.OrdinalIgnoreCase))
                        _displayNameById.Remove(oldId);

                    if (!string.IsNullOrWhiteSpace(uploadPath))
                        updated.LocalPath = uploadPath;
                    else if (!string.IsNullOrWhiteSpace(file.LocalPath))
                        updated.LocalPath = file.LocalPath;

                    if (labels.HasAnyValue)
                        updated.Labels = labels.Clone();
                    else if (updated.Labels == null && file.Labels != null)
                        updated.Labels = file.Labels.Clone();

                    ApplyUpdatedCatalogItem(oldId, updated);
                    // DWG replace creates a new content id — refresh catalog list.
                    if (!string.Equals(oldId, updated.Id, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(uploadPath))
                    {
                        await RefreshCatalogListAsync(_cts.Token).ConfigureAwait(true);
                        ApplyDisplayNameOverrides();
                        var match = _allFiles.Find(f =>
                            string.Equals(f.Id, updated.Id, StringComparison.OrdinalIgnoreCase));
                        if (match != null)
                        {
                            match.Name = preservedName;
                            match.LocalPath = updated.LocalPath;
                            match.Labels = updated.Labels?.Clone();
                            OpenDrawingRegistry.Register(updated.LocalPath ?? match.LocalPath!, match);
                        }
                        else if (!string.IsNullOrWhiteSpace(updated.Id))
                        {
                            updated.Name = preservedName;
                            ApplyUpdatedCatalogItem(oldId, updated);
                        }

                        ApplyFilter();
                    }

                    SetNameBoxText(preservedName);
                    AcadDocumentService.WriteMessage("Сохранено на сервер: " + preservedName);
                    SetStatus("Сохранено: " + preservedName);
                    UpdateActiveLabel();
                }
                catch (OperationCanceledException)
                {
                    SetStatus("Отменено");
                }
                catch (Exception ex)
                {
                    SetStatus("Ошибка: " + ex.Message);
                    MessageBox.Show(this, ex.Message, "Сохранение", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    SetBusy(false);
                    _progress.Value = 0;
                }

                return;
            }

            // Selected catalog item (drawing closed) — save labels / local DWG if present.
            if (!hasDoc
                && _list.SelectedItems.Count > 0
                && _list.SelectedItems[0].Tag is DwgFileInfo selectedFile
                && !string.IsNullOrWhiteSpace(selectedFile.Id))
            {
                if (!CanEditContent(selectedFile.Status))
                {
                    MessageBox.Show(
                        this,
                        "Редактирование недоступно для статуса «" + FormatStatus(selectedFile.Status) + "».",
                        "Сохранение",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                await SaveCatalogItemAsync(selectedFile, labels).ConfigureAwait(true);
                return;
            }

            if (!hasDoc)
            {
                MessageBox.Show(this, "Нет активного чертежа в AutoCAD.", "Сохранение",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Create new catalog item from the active AutoCAD drawing.
            await SaveNewDrawingAsync().ConfigureAwait(true);
        }

        private async Task SaveCatalogItemAsync(DwgFileInfo file, ProductionDrawingLabels labels)
        {
            var uploadPath = !string.IsNullOrWhiteSpace(file.LocalPath) && File.Exists(file.LocalPath)
                ? file.LocalPath
                : null;

            if (string.IsNullOrWhiteSpace(uploadPath) && !labels.HasAnyValue)
            {
                MessageBox.Show(
                    this,
                    "Выберите метки или откройте чертёж, чтобы сохранить изменения.",
                    "Сохранение",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            SetBusy(true, "Сохранение…");
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _progress.Value = 0;

            try
            {
                await EnsureWriteSessionAsync(_cts.Token).ConfigureAwait(true);
                var progress = new Progress<double>(p =>
                {
                    if (IsHandleCreated && !IsDisposed)
                        _progress.Value = Math.Max(0, Math.Min(100, (int)(p * 100)));
                });

                var oldId = file.Id;
                var preservedName = ResolveKnownDisplayName(file);
                if (string.IsNullOrWhiteSpace(preservedName)
                    || preservedName.StartsWith("Без имени", StringComparison.OrdinalIgnoreCase))
                {
                    preservedName = GetEditorNameOrNull() ?? preservedName;
                }

                DwgFileInfo updated;
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    // Имя не меняем при сохранении — только метки/DWG.
                    updated = await client.UpdateContentAsync(
                            oldId,
                            newName: null,
                            localDwgPath: uploadPath,
                            dwgFieldCode: file.DwgFieldCode ?? "file_dwg",
                            labels: labels.HasAnyValue ? labels : null,
                            knownDisplayName: preservedName,
                            progress: progress,
                            cancellationToken: _cts.Token)
                        .ConfigureAwait(true);
                }

                if (!string.IsNullOrWhiteSpace(updated.Name)
                    && !updated.Name.StartsWith("tmp-", StringComparison.OrdinalIgnoreCase)
                    && !updated.Name.StartsWith("Без имени", StringComparison.OrdinalIgnoreCase))
                {
                    preservedName = updated.Name;
                }
                else if (!string.IsNullOrWhiteSpace(preservedName))
                {
                    updated.Name = preservedName!;
                }

                RememberDisplayName(updated.Id, updated.Name);
                if (!string.Equals(oldId, updated.Id, StringComparison.OrdinalIgnoreCase))
                    _displayNameById.Remove(oldId);

                if (!string.IsNullOrWhiteSpace(uploadPath))
                    updated.LocalPath = uploadPath;
                else if (!string.IsNullOrWhiteSpace(file.LocalPath))
                    updated.LocalPath = file.LocalPath;

                if (labels.HasAnyValue)
                    updated.Labels = labels.Clone();
                else if (updated.Labels == null && file.Labels != null)
                    updated.Labels = file.Labels.Clone();

                ApplyUpdatedCatalogItem(oldId, updated);
                if (!string.Equals(oldId, updated.Id, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(uploadPath))
                {
                    await RefreshCatalogListAsync(_cts.Token).ConfigureAwait(true);
                    ApplyDisplayNameOverrides();
                    var match = _allFiles.Find(f =>
                        string.Equals(f.Id, updated.Id, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.Name = preservedName;
                        match.LocalPath = updated.LocalPath;
                        match.Labels = updated.Labels?.Clone();
                        if (!string.IsNullOrWhiteSpace(updated.LocalPath))
                            OpenDrawingRegistry.Register(updated.LocalPath!, match);
                    }
                    else if (!string.IsNullOrWhiteSpace(updated.Id))
                    {
                        updated.Name = preservedName;
                        ApplyUpdatedCatalogItem(oldId, updated);
                    }

                    ApplyFilter();
                }

                SetNameBoxText(preservedName);
                SetStatus("Сохранено: " + preservedName);
                UpdateActiveLabel();
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Сохранение", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
                _progress.Value = 0;
            }
        }

        private async Task SaveNewDrawingAsync()
        {
            if (!TryGetSelectedLabels(out var labels, out var missingLabel))
            {
                MessageBox.Show(
                    this,
                    "Перед сохранением заполните все обязательные метки в разделе «Активный чертёж».\n\nНе заполнено: "
                    + missingLabel,
                    "Метки чертежа",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string suggested;
            try
            {
                suggested = RequireEditorName();
            }
            catch (Exception ex)
            {
                var named = PromptName(
                    OpenDrawingRegistry.PendingNewName
                    ?? AcadDocumentService.TryGetActiveDocumentTitle()
                    ?? "Новый чертёж");
                if (named == null)
                {
                    MessageBox.Show(this, ex.Message, "Сохранение нового чертежа",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                suggested = named;
                SetNameBoxText(suggested);
            }

            OpenDrawingRegistry.PendingNewName = suggested;

            SetBusy(true, "Сохранение нового чертежа…");
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _progress.Value = 0;

            try
            {
                var destDir = PluginApp.Settings.ResolveDownloadDirectory();
                Directory.CreateDirectory(destDir);
                var localPath = BuildCatalogLocalPath(destDir, suggested, null);
                localPath = AcadDocumentService.SaveActiveDocumentAs(localPath);

                SetStatus("Создание в каталоге…");
                await EnsureWriteSessionAsync(_cts.Token).ConfigureAwait(true);
                var progress = new Progress<double>(p =>
                {
                    if (IsHandleCreated && !IsDisposed)
                        _progress.Value = Math.Max(0, Math.Min(100, (int)(p * 100)));
                });

                DwgFileInfo created;
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    created = await client.CreateContentAsync(
                            suggested,
                            localPath,
                            labels,
                            dwgFieldCode: null,
                            progress: progress,
                            cancellationToken: _cts.Token)
                        .ConfigureAwait(true);
                }

                created.LocalPath = localPath;
                created.Status = "draft";
                created.Name = suggested;
                created.Labels = labels.Clone();
                RememberDisplayName(created.Id, suggested);
                if (!string.IsNullOrWhiteSpace(created.Id))
                    OpenDrawingRegistry.Register(localPath, created);
                OpenDrawingRegistry.PendingNewName = null;
                AcadDocumentService.WriteMessage("Новый чертёж сохранён как черновик: " + suggested);

                await RefreshCatalogListAsync(_cts.Token).ConfigureAwait(true);
                ApplyDisplayNameOverrides();

                var match = !string.IsNullOrWhiteSpace(created.Id)
                    ? _allFiles.Find(f =>
                        string.Equals(f.Id, created.Id, StringComparison.OrdinalIgnoreCase))
                    : null;
                match ??= _allFiles.Find(f =>
                    string.Equals(f.Name, suggested, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    match.Name = suggested;
                    match.LocalPath = localPath;
                    match.DwgFieldCode = created.DwgFieldCode;
                    match.Status = string.IsNullOrWhiteSpace(match.Status) ? "draft" : match.Status;
                    match.Labels = labels.Clone();
                    RememberDisplayName(match.Id, suggested);
                    OpenDrawingRegistry.Register(localPath, match);
                    var activePath = AcadDocumentService.TryGetActiveDocumentPath();
                    if (!string.IsNullOrWhiteSpace(activePath))
                        OpenDrawingRegistry.Register(activePath!, match);
                    SetStatus("Создано в каталоге (черновик): " + suggested);
                }
                else if (!string.IsNullOrWhiteSpace(created.Id))
                {
                    _allFiles.Insert(0, created);
                    ApplyFilter();
                    OpenDrawingRegistry.Register(localPath, created);
                    var activePath = AcadDocumentService.TryGetActiveDocumentPath();
                    if (!string.IsNullOrWhiteSpace(activePath))
                        OpenDrawingRegistry.Register(activePath!, created);
                    SetStatus("Создано в каталоге (черновик): " + suggested);
                }
                else
                {
                    SetStatus("Создано в каталоге (черновик): " + suggested + " — обновите список");
                }

                SetNameBoxText(suggested);
                ApplyFilter();
                UpdateActiveLabel();
            }
            catch (OperationCanceledException)
            {
                SetStatus("Отменено");
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка: " + ex.Message);
                MessageBox.Show(this, ex.Message, "Сохранение нового чертежа",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
                _progress.Value = 0;
            }
        }

        private async Task EnsureWriteSessionAsync(CancellationToken cancellationToken)
        {
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
                throw new InvalidOperationException("Нет активной сессии. Войдите снова.");

            var auth = new AuthApiClient(PluginApp.Settings);
            PluginApp.Session = await auth.EnsureFreshCsrfAsync(PluginApp.Session, cancellationToken)
                .ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(PluginApp.Session.CsrfToken))
            {
                throw new InvalidOperationException(
                    "Нет CSRF-токена в сессии. Выйдите и войдите снова.");
            }
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private string? PromptName(string currentName)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "Переименовать чертёж";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(360, 128);
                PluginTheme.ApplyForm(dialog);

                var label = new Label
                {
                    Text = "Новое имя:",
                    Left = 12,
                    Top = 14,
                    AutoSize = true
                };
                PluginTheme.ApplyLabel(label, muted: true);
                var box = new TextBox
                {
                    Left = 12,
                    Top = 38,
                    Width = 336,
                    Text = currentName ?? string.Empty
                };
                PluginTheme.ApplyTextBox(box);
                var ok = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Left = 172,
                    Top = 82,
                    Width = 84
                };
                PluginTheme.ApplyPrimaryButton(ok);
                var cancel = new Button
                {
                    Text = "Отмена",
                    DialogResult = DialogResult.Cancel,
                    Left = 264,
                    Top = 82,
                    Width = 84
                };
                PluginTheme.ApplyGhostButton(cancel);
                dialog.Controls.Add(label);
                dialog.Controls.Add(box);
                dialog.Controls.Add(ok);
                dialog.Controls.Add(cancel);
                dialog.AcceptButton = ok;
                dialog.CancelButton = cancel;
                box.SelectAll();

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return null;

                var name = (box.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show(this, "Имя не может быть пустым.", "Переименование",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }

                return name;
            }
        }

        private void UpdateListItemName(DwgFileInfo file)
        {
            foreach (ListViewItem item in _list.Items)
            {
                if (item.Tag is DwgFileInfo info && string.Equals(info.Id, file.Id, StringComparison.OrdinalIgnoreCase))
                {
                    info.Name = file.Name;
                    item.Text = file.Name;
                    break;
                }
            }

            var match = _allFiles.Find(f => string.Equals(f.Id, file.Id, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                match.Name = file.Name;
        }

        /// <summary>
        /// Work copy under Downloads\{catalogName}.dwg so AutoCAD tab matches catalog.
        /// </summary>
        private string ResolveWritableWorkPath(DwgFileInfo file, string? fallbackPath)
        {
            var preferred = BuildCatalogLocalPath(
                PluginApp.Settings.ResolveDownloadDirectory(),
                file.Name,
                file.Id);

            string? candidate = null;
            if (!string.IsNullOrWhiteSpace(file.LocalPath)
                && Path.IsPathRooted(file.LocalPath)
                && string.Equals(Path.GetExtension(file.LocalPath), ".dwg", StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.GetFullPath(file.LocalPath);
            }
            else if (!string.IsNullOrWhiteSpace(fallbackPath)
                     && Path.IsPathRooted(fallbackPath)
                     && string.Equals(Path.GetExtension(fallbackPath), ".dwg", StringComparison.OrdinalIgnoreCase))
            {
                candidate = Path.GetFullPath(fallbackPath);
            }

            if (candidate != null)
            {
                var want = Path.GetFileNameWithoutExtension(preferred);
                var have = Path.GetFileNameWithoutExtension(candidate);
                if (string.Equals(want, have, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            return preferred;
        }

        private static string BuildCatalogLocalPath(string directory, string? displayName, string? fallbackId)
        {
            var safe = MakeSafeFileName(displayName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(safe))
            {
                safe = !string.IsNullOrWhiteSpace(fallbackId)
                    ? fallbackId!.Replace("-", string.Empty)
                    : Guid.NewGuid().ToString("N");
            }

            if (!safe.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                safe += ".dwg";
            return Path.Combine(directory, safe);
        }

        private static void ClearReadOnlyIfNeeded(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return;
                var attrs = File.GetAttributes(path);
                if ((attrs & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
            }
            catch
            {
                // ignore
            }
        }

        private void ApplyUpdatedCatalogItem(string oldId, DwgFileInfo updated)
        {
            if (updated == null || string.IsNullOrWhiteSpace(updated.Id))
                return;

            if (!string.IsNullOrWhiteSpace(updated.LocalPath))
                OpenDrawingRegistry.Register(updated.LocalPath!, updated);
            else
                OpenDrawingRegistry.Update(updated);

            var activePath = AcadDocumentService.TryGetActiveDocumentPath();
            if (!string.IsNullOrWhiteSpace(activePath))
                OpenDrawingRegistry.Register(activePath!, updated);

            var idx = _allFiles.FindIndex(f =>
                string.Equals(f.Id, oldId, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                if (!string.Equals(oldId, updated.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _allFiles.RemoveAt(idx);
                    if (!_allFiles.Exists(f =>
                            string.Equals(f.Id, updated.Id, StringComparison.OrdinalIgnoreCase)))
                        _allFiles.Insert(Math.Min(idx, _allFiles.Count), updated);
                }
                else
                {
                    var existing = _allFiles[idx];
                    // Never replace a stable title with a temporary/generated one.
                    if (!string.IsNullOrWhiteSpace(updated.Name)
                        && !updated.Name.StartsWith("tmp-", StringComparison.OrdinalIgnoreCase)
                        && !updated.Name.StartsWith("Без имени", StringComparison.OrdinalIgnoreCase))
                    {
                        existing.Name = updated.Name;
                    }
                    existing.Status = updated.Status ?? existing.Status;
                    existing.DwgFieldCode = updated.DwgFieldCode ?? existing.DwgFieldCode;
                    existing.LocalPath = updated.LocalPath ?? existing.LocalPath;
                    if (updated.Labels != null)
                        existing.Labels = updated.Labels.Clone();
                }
            }
            else if (!string.IsNullOrWhiteSpace(updated.Id)
                     && !_allFiles.Exists(f =>
                         string.Equals(f.Id, updated.Id, StringComparison.OrdinalIgnoreCase)))
            {
                _allFiles.Insert(0, updated);
            }

            ApplyFilter();
            UpdateCatalogActionButtons();
        }

        private void UpdateActiveLabel()
        {
            if (_activeLabel == null || _activeLabel.IsDisposed)
                return;
            if (_renameButton == null || _renameButton.IsDisposed)
                return;
            if (_saveButton == null || _saveButton.IsDisposed)
                return;
            if (_approveButton == null || _approveButton.IsDisposed)
                return;

            var loggedIn = !_busy && PluginApp.Session != null && PluginApp.Session.IsAuthenticated;
            var hasDoc = AcadDocumentService.HasActiveDocument();

            if (OpenDrawingRegistry.TryGetCurrent(out var file, out _)
                && !string.IsNullOrWhiteSpace(file.Id))
            {
                var editable = CanEditContent(file.Status);
                var onApproval = IsOnApprovalStatus(file.Status);
                var displayName = ResolveKnownDisplayName(file);
                if (_editorGroup != null && !_editorGroup.IsDisposed)
                {
                    if (onApproval)
                        _editorGroup.Text = "1. На согласовании — отзыв или замена согласующего";
                    else
                        _editorGroup.Text = editable
                            ? "1. Редактирование из каталога (черновик / на доработку)"
                            : "1. Просмотр из каталога (только чтение)";
                }

                if (onApproval)
                    _activeLabel.Text = "На согласовании: " + displayName + " [" + FormatStatus(file.Status) + "]";
                else if (editable)
                    _activeLabel.Text = "Из каталога: " + displayName + " [" + FormatStatus(file.Status) + "]";
                else
                    _activeLabel.Text = "Просмотр: " + displayName + " [" + FormatStatus(file.Status) + "] — изменения недоступны";
                SetNameBoxText(displayName);
                // Имя меняется только через «Задать имя…» (draft / rejected).
                SetNameBoxEditable(false);
                _renameButton.Enabled = loggedIn && editable;
                _saveButton.Enabled = loggedIn && editable;
                _approveButton.Enabled = loggedIn && IsDraftStatus(file.Status);
                SetOnApprovalActionsEnabled(loggedIn && onApproval);
                _saveButton.Text = "Сохранить изменения";
                _renameButton.Text = "Задать имя…";
                SetLabelCombosEnabled(!_busy && editable);
                if (file.Labels == null)
                    _ = EnsureLabelsLoadedAsync(file);
                else
                    ApplyLabelsToUi(file.Labels);
            }
            else if (_list.SelectedItems.Count > 0
                     && _list.SelectedItems[0].Tag is DwgFileInfo selected
                     && !string.IsNullOrWhiteSpace(selected.Id)
                     && CanEditContent(selected.Status))
            {
                var displayName = ResolveKnownDisplayName(selected);
                if (_editorGroup != null && !_editorGroup.IsDisposed)
                    _editorGroup.Text = "1. Выбран из каталога (черновик / на доработку) — можно переименовать";

                _activeLabel.Text = "Выбрано в каталоге: " + displayName + " [" + FormatStatus(selected.Status) + "]";
                SetNameBoxText(displayName);
                SetNameBoxEditable(false);
                _renameButton.Enabled = loggedIn;
                _saveButton.Enabled = loggedIn;
                _approveButton.Enabled = loggedIn && IsDraftStatus(selected.Status);
                SetOnApprovalActionsEnabled(false);
                _saveButton.Text = "Сохранить изменения";
                _renameButton.Text = "Задать имя…";
                SetLabelCombosEnabled(!_busy);
                if (selected.Labels == null)
                    _ = EnsureLabelsLoadedAsync(selected);
                else
                    ApplyLabelsToUi(selected.Labels);
            }
            else if (_list.SelectedItems.Count > 0
                     && _list.SelectedItems[0].Tag is DwgFileInfo inReview
                     && !string.IsNullOrWhiteSpace(inReview.Id)
                     && IsOnApprovalStatus(inReview.Status))
            {
                var displayName = ResolveKnownDisplayName(inReview);
                if (_editorGroup != null && !_editorGroup.IsDisposed)
                    _editorGroup.Text = "1. Выбран из каталога (на согласовании)";

                _activeLabel.Text = "На согласовании: " + displayName + " [" + FormatStatus(inReview.Status) + "]";
                SetNameBoxText(displayName);
                SetNameBoxEditable(false);
                _renameButton.Enabled = false;
                _saveButton.Enabled = false;
                _approveButton.Enabled = false;
                SetOnApprovalActionsEnabled(loggedIn);
                _saveButton.Text = "Сохранить";
                _renameButton.Text = "Задать имя…";
                SetLabelCombosEnabled(false);
                if (inReview.Labels == null)
                    _ = EnsureLabelsLoadedAsync(inReview);
                else
                    ApplyLabelsToUi(inReview.Labels);
            }
            else if (hasDoc)
            {
                if (_editorGroup != null && !_editorGroup.IsDisposed)
                    _editorGroup.Text = "1. Новый чертёж — имя, метки, сохранить как черновик";

                _activeLabel.Text = "Новый чертёж AutoCAD → будет добавлен в каталог как черновик";
                if (_nameBox != null && !_nameBox.IsDisposed && !_nameBox.Focused)
                {
                    if (!string.IsNullOrWhiteSpace(OpenDrawingRegistry.PendingNewName))
                        SetNameBoxText(OpenDrawingRegistry.PendingNewName);
                    else
                        SetNameBoxText(AcadDocumentService.TryGetActiveDocumentTitle() ?? string.Empty);
                }

                SetNameBoxEditable(false);
                _renameButton.Enabled = loggedIn;
                _saveButton.Enabled = loggedIn;
                _approveButton.Enabled = false;
                SetOnApprovalActionsEnabled(false);
                _saveButton.Text = "Сохранить в каталог";
                _renameButton.Text = "Задать имя…";
                SetLabelCombosEnabled(!_busy);
            }
            else
            {
                if (_editorGroup != null && !_editorGroup.IsDisposed)
                    _editorGroup.Text = "1. Активный чертёж AutoCAD";
                _activeLabel.Text = "Нет активного чертежа — откройте файл в AutoCAD или из каталога";
                SetNameBoxText(string.Empty);
                SetNameBoxEditable(false);
                _renameButton.Enabled = false;
                _saveButton.Enabled = false;
                _approveButton.Enabled = false;
                SetOnApprovalActionsEnabled(false);
                _saveButton.Text = "Сохранить";
                _renameButton.Text = "Задать имя…";
                SetLabelCombosEnabled(false);
                ApplyLabelsToUi(null);
            }
        }

        private void SetOnApprovalActionsEnabled(bool enabled)
        {
            if (_withdrawButton != null && !_withdrawButton.IsDisposed)
                _withdrawButton.Enabled = enabled;
            if (_replaceAssigneesButton != null && !_replaceAssigneesButton.IsDisposed)
                _replaceAssigneesButton.Enabled = enabled;
        }

        private string ResolveKnownDisplayName(DwgFileInfo file)
        {
            if (file == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(file.Id)
                && _displayNameById.TryGetValue(file.Id, out var remembered)
                && !string.IsNullOrWhiteSpace(remembered))
            {
                return remembered;
            }

            return file.Name ?? string.Empty;
        }

        private void RememberDisplayName(string? contentId, string? name)
        {
            if (string.IsNullOrWhiteSpace(contentId) || string.IsNullOrWhiteSpace(name))
                return;
            _displayNameById[contentId!] = name!.Trim();
        }

        private void ApplyDisplayNameOverrides()
        {
            foreach (var file in _allFiles)
            {
                if (string.IsNullOrWhiteSpace(file.Id))
                    continue;
                if (_displayNameById.TryGetValue(file.Id, out var name) && !string.IsNullOrWhiteSpace(name))
                    file.Name = name;
            }
        }

        private string? GetEditorNameOrNull()
        {
            if (_nameBox == null || _nameBox.IsDisposed)
                return null;
            var t = (_nameBox.Text ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }

        private string RequireEditorName()
        {
            var name = GetEditorNameOrNull();
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Укажите имя чертежа в поле «Имя чертежа».");
            return name!;
        }

        private void SetNameBoxText(string? text)
        {
            if (_nameBox == null || _nameBox.IsDisposed)
                return;
            if (_nameBox.Focused)
                return;
            _nameBox.Text = text ?? string.Empty;
        }

        /// <summary>
        /// Имя в поле только для отображения. Меняется через «Задать имя…»
        /// (доступно для draft/rejected и для нового чертежа).
        /// </summary>
        private void SetNameBoxEditable(bool editable)
        {
            if (_nameBox == null || _nameBox.IsDisposed)
                return;
            _nameBox.ReadOnly = !editable;
            _nameBox.Enabled = true;
            _nameBox.BackColor = editable ? PluginTheme.Card : PluginTheme.InputDisabled;
        }

        private void SetLabelCombosEnabled(bool enabled)
        {
            if (_userCombo == null || _userCombo.IsDisposed)
                return;
            _userCombo.Enabled = enabled;
            _categoryCombo.Enabled = enabled;
            _brandCombo.Enabled = enabled;
            _modelCombo.Enabled = enabled;
            _perforationCombo.Enabled = enabled;
            _edgeCombo.Enabled = enabled;
            _sizeCombo.Enabled = enabled;
            UpdateReferenceCreateButtons();
        }

        private void BeginInvokeSafe(Action action)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                if (InvokeRequired)
                    BeginInvoke(action);
                else
                    action();
            }
            catch
            {
                // control may be disposing
            }
        }

        private void ApplyFilter()
        {
            var query = (_filterBox.Text ?? string.Empty).Trim();
            _list.BeginUpdate();
            _list.Items.Clear();

            foreach (var file in _allFiles)
            {
                if (!string.IsNullOrEmpty(query))
                {
                    var hay = (file.Name + " " + file.ContentType + " " + file.Status + " "
                               + FormatStatus(file.Status) + " " + file.Project + " "
                               + file.RejectionComment + " " + file.Id);
                    if (hay.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                var item = new ListViewItem(file.Name);
                var statusItem = item.SubItems.Add(FormatStatus(file.Status));
                statusItem.Tag = file.Status ?? string.Empty;
                item.SubItems.Add(file.Project ?? string.Empty);
                item.SubItems.Add(
                    IsRejectedStatus(file.Status) ? (file.RejectionComment ?? string.Empty) : string.Empty);
                item.SubItems.Add(file.UpdatedAt?.ToLocalTime().ToString("g") ?? string.Empty);
                item.Tag = file;
                _list.Items.Add(item);
            }

            _list.EndUpdate();
            UpdateCatalogActionButtons();
            // Filter rebuild clears selection — restore name/labels from the active drawing.
            UpdateActiveLabel();
        }

        private void RefreshListItemComment(DwgFileInfo file)
        {
            if (_list == null || _list.IsDisposed || file == null)
                return;

            foreach (ListViewItem item in _list.Items)
            {
                if (item.Tag is DwgFileInfo tagged
                    && string.Equals(tagged.Id, file.Id, StringComparison.OrdinalIgnoreCase))
                {
                    while (item.SubItems.Count < 5)
                        item.SubItems.Add(string.Empty);
                    item.SubItems[3].Text = IsRejectedStatus(file.Status)
                        ? (file.RejectionComment ?? string.Empty)
                        : string.Empty;
                    break;
                }
            }
        }

        private void BindFilters(IReadOnlyList<FilterEntity> filters)
        {
            _suppressLabelCascade = true;
            try
            {
                BindLabelComboOptions(filters);

                // Keep editor labels aligned with active drawing, or catalog selection if none.
                if (OpenDrawingRegistry.TryGetCurrent(out var current, out _))
                {
                    ApplyLabelsToUi(current.Labels);
                }
                else if (_list.SelectedItems.Count > 0
                         && _list.SelectedItems[0].Tag is DwgFileInfo selected)
                {
                    ApplyLabelsToUi(selected.Labels);
                }
                else
                {
                    ApplyLabelsToUi(null);
                }
            }
            finally
            {
                _suppressLabelCascade = false;
            }
        }

        private void BindLabelComboOptions(IReadOnlyList<FilterEntity> filters)
        {
            _filters = filters != null
                ? new List<FilterEntity>(filters)
                : new List<FilterEntity>();

            BindCombo(_userCombo, FindFilter("user_uuid", "user", "customer"), "Заказчик");
            BindCombo(_categoryCombo, FindFilter("global_cat_code", "global_category_code", "global_category", "category"), "Категория");
            BindCombo(_brandCombo, FindFilter("brand_code", "brand"), "Бренд");
            BindCombo(_modelCombo, FindFilter("model_code", "model"), "Модель");
            BindCombo(_perforationCombo, FindFilter(
                "prod_drawing_perforation_code",
                "prod_drawing_panel_perforation",
                "prod_drawing_perforation",
                "perforation"), "Перфорация");
            BindCombo(_edgeCombo, FindFilter(
                "prod_drawing_edge_code",
                "prod_drawing_edge",
                "edge"), "Кромка");
            BindCombo(_sizeCombo, FindFilter(
                "prod_drawing_panel_size_code",
                "prod_drawing_panel_size",
                "panel_size"), "Размер панели");
        }

        private async Task OnCatalogSelectionChangedAsync()
        {
            // No blue selection → name + labels come from the active AutoCAD drawing.
            if (_list.SelectedItems.Count == 0)
            {
                var leftSelection = _catalogHadSelection;
                _catalogHadSelection = false;
                UpdateActiveLabel();
                // After leaving a catalog peek with no linked drawing, drop peeked labels.
                if (leftSelection
                    && !OpenDrawingRegistry.TryGetCurrent(out _, out _))
                {
                    ApplyLabelsToUi(null);
                }

                return;
            }

            _catalogHadSelection = true;

            var file = _list.SelectedItems[0].Tag as DwgFileInfo;
            if (file == null || string.IsNullOrWhiteSpace(file.Id))
            {
                UpdateActiveLabel();
                return;
            }

            // Active catalog drawing stays the editor source; selection is only for Open/Delete.
            if (OpenDrawingRegistry.TryGetCurrent(out var current, out _)
                && !string.IsNullOrWhiteSpace(current.Id))
            {
                UpdateActiveLabel();
                return;
            }

            UpdateActiveLabel();

            if (file.Labels != null)
            {
                ApplyLabelsToUi(file.Labels);
                return;
            }

            await EnsureLabelsLoadedAsync(file).ConfigureAwait(true);
        }

        private async Task EnsureLabelsLoadedAsync(DwgFileInfo file)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.Id))
                return;
            if (PluginApp.Session == null || !PluginApp.Session.IsAuthenticated)
                return;

            var needLabels = file.Labels == null || !file.Labels.HasAnyValue;
            var needRejection = IsRejectedStatus(file.Status)
                                && string.IsNullOrWhiteSpace(file.RejectionComment);
            if (!needLabels && !needRejection)
            {
                if (file.Labels != null)
                    ApplyLabelsToUi(file.Labels);
                return;
            }

            var requestId = file.Id;
            _labelsRequestId = requestId;
            _labelsCts?.Cancel();
            _labelsCts = new CancellationTokenSource();
            var token = _labelsCts.Token;

            try
            {
                ProductionDrawingLabels? labels = file.Labels;
                string? rejectionComment = null;
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    if (needLabels && needRejection)
                    {
                        var meta = await client.GetContentMetaAsync(file.Id, token).ConfigureAwait(true);
                        labels = meta.Labels;
                        rejectionComment = meta.RejectionComment;
                    }
                    else if (needLabels)
                    {
                        labels = await client.GetContentLabelsAsync(file.Id, token).ConfigureAwait(true);
                    }
                    else if (needRejection)
                    {
                        rejectionComment = await client.GetRejectionCommentAsync(file.Id, token)
                            .ConfigureAwait(true);
                    }
                }

                if (token.IsCancellationRequested || _labelsRequestId != requestId)
                    return;

                file.Labels = labels ?? new ProductionDrawingLabels();
                if (!string.IsNullOrWhiteSpace(rejectionComment))
                    file.RejectionComment = rejectionComment;

                var match = _allFiles.Find(f =>
                    string.Equals(f.Id, file.Id, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    match.Labels = file.Labels.Clone();
                    if (!string.IsNullOrWhiteSpace(file.RejectionComment))
                        match.RejectionComment = file.RejectionComment;
                }
                if (!string.IsNullOrWhiteSpace(file.LocalPath))
                    OpenDrawingRegistry.Register(file.LocalPath!, file);

                // Refresh list row comment if this rejected item is visible.
                if (IsRejectedStatus(file.Status) && !string.IsNullOrWhiteSpace(file.RejectionComment))
                    RefreshListItemComment(file);

                // Apply only if this file is still the editor source.
                var apply = false;
                if (OpenDrawingRegistry.TryGetCurrent(out var current, out _)
                    && string.Equals(current.Id, file.Id, StringComparison.OrdinalIgnoreCase))
                {
                    apply = true;
                }
                else if (!OpenDrawingRegistry.TryGetCurrent(out _, out _)
                         && _list.SelectedItems.Count > 0
                         && _list.SelectedItems[0].Tag is DwgFileInfo selected
                         && string.Equals(selected.Id, file.Id, StringComparison.OrdinalIgnoreCase))
                {
                    apply = true;
                }

                if (apply)
                    ApplyLabelsToUi(file.Labels);
            }
            catch (OperationCanceledException)
            {
                // selection changed
            }
            catch
            {
                // keep UI usable if labels fail to load
            }
        }

        private void ApplyLabelsToUi(ProductionDrawingLabels? labels)
        {
            if (_userCombo == null || _userCombo.IsDisposed)
                return;

            var prev = _suppressLabelCascade;
            _suppressLabelCascade = true;
            try
            {
                SelectComboCode(_userCombo, labels?.UserUuid);
                SelectComboCode(_categoryCombo, labels?.GlobalCategoryCode);
                SelectComboCode(_brandCombo, labels?.BrandCode);
                SelectComboCode(_modelCombo, labels?.ModelCode);
                SelectComboCode(_perforationCombo, labels?.PerforationCode);
                SelectComboCode(_edgeCombo, labels?.EdgeCode);
                SelectComboCode(_sizeCombo, labels?.PanelSizeCode);
            }
            finally
            {
                _suppressLabelCascade = prev;
            }
        }

        private static void SelectComboCode(ComboBox combo, string? code)
        {
            if (combo == null || combo.IsDisposed)
                return;

            if (string.IsNullOrWhiteSpace(code))
            {
                if (combo.Items.Count > 0)
                    combo.SelectedIndex = 0;
                return;
            }

            for (var i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is FilterOption fo
                    && string.Equals(fo.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            // Keep unknown code visible even if it is missing from the dictionary.
            var orphan = new FilterOption { Code = code.Trim(), Name = code.Trim() };
            combo.Items.Add(orphan);
            combo.SelectedItem = orphan;
        }

        private FilterEntity? FindFilter(params string[] codes)
        {
            foreach (var code in codes)
            {
                var byCode = _filters.Find(f =>
                    string.Equals(f.Code, code, StringComparison.OrdinalIgnoreCase));
                if (byCode != null)
                    return byCode;
            }

            // Fallback by Russian/English display name fragments.
            foreach (var code in codes)
            {
                var needle = code.Replace("_", " ");
                var byName = _filters.Find(f =>
                    (f.Name ?? string.Empty).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                    || (f.Name ?? string.Empty).IndexOf(MapFilterHint(code), StringComparison.OrdinalIgnoreCase) >= 0);
                if (byName != null)
                    return byName;
            }

            return null;
        }

        private static string MapFilterHint(string code)
        {
            if (code.IndexOf("user", StringComparison.OrdinalIgnoreCase) >= 0) return "заказчик";
            if (code.IndexOf("global_cat", StringComparison.OrdinalIgnoreCase) >= 0
                || code.IndexOf("category", StringComparison.OrdinalIgnoreCase) >= 0) return "категор";
            if (code.IndexOf("brand", StringComparison.OrdinalIgnoreCase) >= 0) return "бренд";
            if (code.IndexOf("model", StringComparison.OrdinalIgnoreCase) >= 0) return "модель";
            if (code.IndexOf("perfor", StringComparison.OrdinalIgnoreCase) >= 0) return "перфор";
            if (code.IndexOf("edge", StringComparison.OrdinalIgnoreCase) >= 0) return "кромк";
            if (code.IndexOf("size", StringComparison.OrdinalIgnoreCase) >= 0
                || code.IndexOf("panel", StringComparison.OrdinalIgnoreCase) >= 0) return "размер";
            return code;
        }

        private static void BindCombo(ComboBox combo, FilterEntity? entity, string placeholder)
        {
            var previous = (combo.SelectedItem as FilterOption)?.Code;
            combo.BeginUpdate();
            combo.Items.Clear();
            combo.Items.Add(new FilterOption { Code = string.Empty, Name = "— " + placeholder + " —" });

            if (entity?.Options != null)
            {
                foreach (var opt in entity.Options)
                {
                    if (opt == null || string.IsNullOrWhiteSpace(opt.Code))
                        continue;
                    combo.Items.Add(opt);
                }
            }

            var selectedIndex = 0;
            if (!string.IsNullOrWhiteSpace(previous))
            {
                for (var i = 0; i < combo.Items.Count; i++)
                {
                    if (combo.Items[i] is FilterOption fo
                        && string.Equals(fo.Code, previous, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            combo.SelectedIndex = combo.Items.Count > 0 ? selectedIndex : -1;
            combo.EndUpdate();
        }

        private static string? GetSelectedCode(ComboBox combo)
        {
            if (combo.SelectedItem is FilterOption opt && !string.IsNullOrWhiteSpace(opt.Code))
                return opt.Code.Trim();
            return null;
        }

        private bool TryGetSelectedLabels(out ProductionDrawingLabels labels, out string missing)
        {
            labels = new ProductionDrawingLabels
            {
                UserUuid = GetSelectedCode(_userCombo) ?? string.Empty,
                GlobalCategoryCode = GetSelectedCode(_categoryCombo) ?? string.Empty,
                BrandCode = GetSelectedCode(_brandCombo) ?? string.Empty,
                ModelCode = GetSelectedCode(_modelCombo) ?? string.Empty,
                PerforationCode = GetSelectedCode(_perforationCombo) ?? string.Empty,
                EdgeCode = GetSelectedCode(_edgeCombo) ?? string.Empty,
                PanelSizeCode = GetSelectedCode(_sizeCombo) ?? string.Empty
            };

            missing = labels.MissingFieldName() ?? string.Empty;
            return labels.IsComplete;
        }

        private void SetBusy(bool busy, string? status = null)
        {
            _busy = busy;
            _loginButton.Enabled = !busy;
            _apiUrlBox.Enabled = !busy;
            _emailBox.Enabled = !busy;
            _passwordBox.Enabled = !busy;
            _refreshButton.Enabled = !busy;
            _logoutButton.Enabled = !busy;
            _filterBox.Enabled = !busy;
            UpdateCatalogActionButtons();
            UpdateReferenceCreateButtons();
            if (status != null)
                SetStatus(status);
            UpdateActiveLabel();
        }

        private void SetStatus(string text)
        {
            if (IsHandleCreated && !IsDisposed)
                _statusLabel.Text = text;
        }

        private void SetLoginStatus(string text)
        {
            if (!IsHandleCreated || IsDisposed)
                return;
            _loginStatus.Text = text;
            var error = (text ?? string.Empty).StartsWith("Ошибка", StringComparison.OrdinalIgnoreCase)
                        || (text ?? string.Empty).IndexOf("истекла", StringComparison.OrdinalIgnoreCase) >= 0;
            _loginStatus.ForeColor = error ? PluginTheme.Danger : PluginTheme.Muted;
        }

        private static string Short(string text)
        {
            text = text ?? string.Empty;
            return text.Length <= 120 ? text : text.Substring(0, 120) + "…";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    AcadDocumentService.UnsubscribeDocumentActivated(_onDocActivated);
                }
                catch
                {
                    // ignore
                }

                _cts?.Cancel();
                _cts?.Dispose();
                _labelsCts?.Cancel();
                _labelsCts?.Dispose();
                _labelCascadeCts?.Cancel();
                _labelCascadeCts?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

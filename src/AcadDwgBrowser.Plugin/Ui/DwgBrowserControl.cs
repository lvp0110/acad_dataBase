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
        private TextBox _filterBox = null!;
        private Label _statusLabel = null!;
        private Label _activeLabel = null!;
        private ComboBox _userCombo = null!;
        private ComboBox _categoryCombo = null!;
        private ComboBox _brandCombo = null!;
        private ComboBox _modelCombo = null!;
        private ComboBox _perforationCombo = null!;
        private ComboBox _edgeCombo = null!;
        private ComboBox _sizeCombo = null!;
        private ProgressBar _progress = null!;
        private List<DwgFileInfo> _allFiles = new List<DwgFileInfo>();
        private List<FilterEntity> _filters = new List<FilterEntity>();
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _labelsCts;
        private string? _labelsRequestId;
        private bool _busy;
        private bool _sessionChecked;
        private readonly Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventHandler _onDocActivated;

        public DwgBrowserControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(245, 246, 248);
            Padding = new Padding(10);

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
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 10
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.Controls.Add(layout);

            layout.Controls.Add(new Label
            {
                Text = "Вход в ConstrTodo",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 0);

            layout.Controls.Add(new Label
            {
                Text = "Адрес API (ApiBaseUrl)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 1);

            _apiUrlBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                Text = string.Empty
            };
            layout.Controls.Add(_apiUrlBox, 0, 2);

            layout.Controls.Add(new Label
            {
                Text = "Email",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 3);

            _emailBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f)
            };
            layout.Controls.Add(_emailBox, 0, 4);

            layout.Controls.Add(new Label
            {
                Text = "Пароль",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.BottomLeft
            }, 0, 5);

            _passwordBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                UseSystemPasswordChar = true
            };
            _passwordBox.KeyDown += async (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    await LoginAsync();
                }
            };
            layout.Controls.Add(_passwordBox, 0, 6);

            // spacer row kept for spacing via empty label
            layout.Controls.Add(new Label { Dock = DockStyle.Fill }, 0, 7);

            _loginButton = new Button
            {
                Text = "Войти",
                Dock = DockStyle.Left,
                Width = 120,
                Height = 32,
                Font = new Font("Segoe UI", 9.5f)
            };
            _loginButton.Click += async (_, __) => await LoginAsync();
            layout.Controls.Add(_loginButton, 0, 8);

            _loginStatus = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(70, 70, 70),
                TextAlign = ContentAlignment.TopLeft,
                Text = "Введите email и пароль, затем «Войти»."
            };
            layout.Controls.Add(_loginStatus, 0, 9);

            return panel;
        }

        private Panel BuildCatalogPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Visible = false };

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 310));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            panel.Controls.Add(root);

            // —— User bar ——
            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            root.Controls.Add(header, 0, 0);

            _userLabel = new Label
            {
                Text = "Пользователь",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f),
                TextAlign = ContentAlignment.MiddleLeft
            };
            header.Controls.Add(_userLabel, 0, 0);

            _logoutButton = new Button
            {
                Text = "Выйти",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 0)
            };
            _logoutButton.Click += async (_, __) => await LogoutAsync();
            header.Controls.Add(_logoutButton, 1, 0);

            // —— Section 1: catalog from API ——
            var catalogGroup = new GroupBox
            {
                Text = "Каталог — получение из API",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Padding = new Padding(8, 6, 8, 8),
                Margin = new Padding(0, 4, 0, 4)
            };
            root.Controls.Add(catalogGroup, 0, 1);

            var catalogLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0, 4, 0, 0)
            };
            catalogLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            catalogLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            catalogLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            catalogGroup.Controls.Add(catalogLayout);

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
            catalogLayout.Controls.Add(toolbar, 0, 0);

            _filterBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
            _filterBox.TextChanged += (_, __) => ApplyFilter();
            toolbar.Controls.Add(_filterBox, 0, 0);

            _refreshButton = new Button
            {
                Text = "Обновить",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 0),
                Font = new Font("Segoe UI", 9f)
            };
            _refreshButton.Click += async (_, __) => await ReloadAsync();
            toolbar.Controls.Add(_refreshButton, 1, 0);

            _openButton = new Button
            {
                Text = "Открыть",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 0),
                Font = new Font("Segoe UI", 9f),
                Enabled = false
            };
            _openButton.Click += async (_, __) => await OpenSelectedAsync();
            toolbar.Controls.Add(_openButton, 2, 0);

            _deleteButton = new Button
            {
                Text = "Удалить",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 0),
                Font = new Font("Segoe UI", 9f),
                Enabled = false
            };
            _deleteButton.Click += async (_, __) => await DeleteSelectedAsync();
            toolbar.Controls.Add(_deleteButton, 3, 0);

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                Font = new Font("Segoe UI", 9f)
            };
            _list.Columns.Add("Имя", 220);
            _list.Columns.Add("Статус", 100);
            _list.Columns.Add("Метки", 120);
            _list.Columns.Add("Обновлён", 120);
            _list.SelectedIndexChanged += async (_, __) =>
            {
                UpdateCatalogActionButtons();
                await OnCatalogSelectionChangedAsync().ConfigureAwait(true);
            };
            _list.DoubleClick += async (_, __) => await OpenSelectedAsync();
            catalogLayout.Controls.Add(_list, 0, 1);

            _progress = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };
            catalogLayout.Controls.Add(_progress, 0, 2);

            // —— Section 2: rename / save active AutoCAD drawing ——
            var editorGroup = new GroupBox
            {
                Text = "Активный чертёж AutoCAD — метки, переименовать / сохранить",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Padding = new Padding(8, 6, 8, 8),
                Margin = new Padding(0, 2, 0, 2)
            };
            root.Controls.Add(editorGroup, 0, 2);

            var editorLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(0, 4, 0, 0)
            };
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            editorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            editorGroup.Controls.Add(editorLayout);

            _activeLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Активный чертёж: —",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 70, 70),
                TextAlign = ContentAlignment.MiddleLeft
            };
            editorLayout.Controls.Add(_activeLabel, 0, 0);

            var labelsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 7,
                Padding = new Padding(0)
            };
            labelsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            labelsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (var i = 0; i < 7; i++)
                labelsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            var labelsScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(0)
            };
            labelsPanel.Dock = DockStyle.Top;
            labelsPanel.Height = 7 * 28 + 4;
            labelsScroll.Controls.Add(labelsPanel);
            editorLayout.Controls.Add(labelsScroll, 0, 1);

            _userCombo = AddLabelCombo(labelsPanel, 0, "Заказчик *");
            _categoryCombo = AddLabelCombo(labelsPanel, 1, "Категория *");
            _brandCombo = AddLabelCombo(labelsPanel, 2, "Бренд *");
            _modelCombo = AddLabelCombo(labelsPanel, 3, "Модель *");
            _perforationCombo = AddLabelCombo(labelsPanel, 4, "Перфорация *");
            _edgeCombo = AddLabelCombo(labelsPanel, 5, "Кромка *");
            _sizeCombo = AddLabelCombo(labelsPanel, 6, "Размер панели *");

            var actions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            editorLayout.Controls.Add(actions, 0, 2);

            _renameButton = new Button
            {
                Text = "Переименовать",
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 4, 0),
                Font = new Font("Segoe UI", 9f),
                Enabled = false
            };
            _renameButton.Click += async (_, __) => await RenameActiveAsync();
            actions.Controls.Add(_renameButton, 0, 0);

            _saveButton = new Button
            {
                Text = "Сохранить",
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 0, 0, 0),
                Font = new Font("Segoe UI", 9f),
                Enabled = false
            };
            _saveButton.Click += async (_, __) => await SaveActiveAsync();
            actions.Controls.Add(_saveButton, 1, 0);

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Готово",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f)
            };
            root.Controls.Add(_statusLabel, 0, 3);

            return panel;
        }

        private static ComboBox AddLabelCombo(TableLayoutPanel host, int row, string caption)
        {
            host.Controls.Add(new Label
            {
                Text = caption,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, row);

            var combo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 8.5f),
                Margin = new Padding(0, 2, 0, 2)
            };
            host.Controls.Add(combo, 1, row);
            return combo;
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
                _loginStatus.ForeColor = placeholder ? Color.Firebrick : Color.FromArgb(70, 70, 70);
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
                PluginApp.Settings.ContentType = "production_drawings";
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    var filtersTask = client.GetFiltersAsync(_cts.Token);
                    var filesTask = client.ListFilesAsync(_cts.Token);
                    await Task.WhenAll(filtersTask, filesTask).ConfigureAwait(true);

                    BindFilters(filtersTask.Result);
                    _allFiles = new List<DwgFileInfo>(filesTask.Result);
                    ApplyFilter();
                    SetStatus($"Производственные чертежи: {_allFiles.Count}");
                }
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
                AcadDocumentService.OpenDwg(localPath, readOnly: false);
                file.LocalPath = localPath;
                OpenDrawingRegistry.Register(localPath, file);

                // Bind whatever path AutoCAD reports for the active document.
                var activePath = AcadDocumentService.TryGetActiveDocumentPath();
                if (!string.IsNullOrWhiteSpace(activePath))
                    OpenDrawingRegistry.Register(activePath!, file);

                AcadDocumentService.WriteMessage("Открыт файл: " + localPath);
                SetStatus("Открыто: " + file.Name);
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
                return;

            if (!IsDraftStatus(file.Status))
            {
                MessageBox.Show(
                    this,
                    "Удалять можно только конструкции в статусе draft.\nТекущий статус: "
                    + (file.Status ?? "—"),
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
                    await client.DeleteContentAsync(file.Id, _cts.Token).ConfigureAwait(true);
                }

                AcadDocumentService.WriteMessage("Удалено: " + file.Name);
                SetStatus("Удалено: " + file.Name);
                await ReloadAsync().ConfigureAwait(true);
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

        private static bool IsDraftStatus(string? status) =>
            string.Equals(status?.Trim(), "draft", StringComparison.OrdinalIgnoreCase);

        private void UpdateCatalogActionButtons()
        {
            var hasSelection = !_busy && _list.SelectedItems.Count > 0;
            _openButton.Enabled = hasSelection;

            var canDelete = false;
            if (hasSelection && _list.SelectedItems[0].Tag is DwgFileInfo file)
                canDelete = IsDraftStatus(file.Status);
            _deleteButton.Enabled = canDelete;
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

            if (!AcadDocumentService.HasActiveDocument())
            {
                MessageBox.Show(this, "Нет активного чертежа в AutoCAD.", "Переименование",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Existing catalog drawing → rename on server.
            if (OpenDrawingRegistry.TryGetCurrent(out var file, out _)
                && !string.IsNullOrWhiteSpace(file.Id))
            {
                var newName = PromptName(file.Name);
                if (newName == null)
                    return;
                if (string.Equals(newName, file.Name, StringComparison.Ordinal))
                    return;

                SetBusy(true, "Переименование…");
                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                try
                {
                    await EnsureWriteSessionAsync(_cts.Token).ConfigureAwait(true);
                    using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                    {
                        await client.UpdateContentAsync(
                                file.Id,
                                newName: newName,
                                localDwgPath: null,
                                dwgFieldCode: file.DwgFieldCode,
                                cancellationToken: _cts.Token)
                            .ConfigureAwait(true);
                    }

                    file.Name = newName;
                    OpenDrawingRegistry.Update(file);
                    OpenDrawingRegistry.PendingNewName = newName;
                    UpdateListItemName(file);
                    UpdateActiveLabel();
                    AcadDocumentService.WriteMessage("Переименовано: " + newName);
                    SetStatus("Переименовано: " + newName);
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
            var suggested = OpenDrawingRegistry.PendingNewName
                            ?? AcadDocumentService.TryGetActiveDocumentTitle()
                            ?? "Новый чертёж";
            var name = PromptName(suggested);
            if (name == null)
                return;

            OpenDrawingRegistry.PendingNewName = name;
            UpdateActiveLabel();
            SetStatus("Имя задано: " + name + ". Нажмите «Сохранить», чтобы создать в каталоге.");
            AcadDocumentService.WriteMessage("Имя нового чертежа: " + name);
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

            if (!AcadDocumentService.HasActiveDocument())
            {
                MessageBox.Show(this, "Нет активного чертежа в AutoCAD.", "Сохранение",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Update existing catalog drawing.
            if (OpenDrawingRegistry.TryGetCurrent(out var file, out var localPath)
                && !string.IsNullOrWhiteSpace(file.Id))
            {
                SetBusy(true, "Сохранение в AutoCAD…");
                _cts?.Cancel();
                _cts = new CancellationTokenSource();
                _progress.Value = 0;

                try
                {
                    localPath = AcadDocumentService.SaveActiveDocument(file.LocalPath ?? localPath);
                    file.LocalPath = localPath;
                    OpenDrawingRegistry.Register(localPath, file);

                    SetStatus("Отправка на сервер…");
                    await EnsureWriteSessionAsync(_cts.Token).ConfigureAwait(true);
                    var progress = new Progress<double>(p =>
                    {
                        if (IsHandleCreated && !IsDisposed)
                            _progress.Value = Math.Max(0, Math.Min(100, (int)(p * 100)));
                    });

                    using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                    {
                        await client.UpdateContentAsync(
                                file.Id,
                                newName: null,
                                localDwgPath: localPath,
                                dwgFieldCode: file.DwgFieldCode,
                                progress: progress,
                                cancellationToken: _cts.Token)
                            .ConfigureAwait(true);
                    }

                    AcadDocumentService.WriteMessage("Сохранено на сервер: " + file.Name);
                    SetStatus("Сохранено: " + file.Name);
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

            // Create new catalog item from the active AutoCAD drawing.
            await SaveNewDrawingAsync().ConfigureAwait(true);
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

            var suggested = OpenDrawingRegistry.PendingNewName
                            ?? AcadDocumentService.TryGetActiveDocumentTitle()
                            ?? "Новый чертёж";
            if (suggested.StartsWith("Drawing", StringComparison.OrdinalIgnoreCase)
                || suggested.StartsWith("Чертеж", StringComparison.OrdinalIgnoreCase))
            {
                var named = PromptName(suggested);
                if (named == null)
                    return;
                suggested = named;
            }
            else if (string.IsNullOrWhiteSpace(OpenDrawingRegistry.PendingNewName))
            {
                var named = PromptName(suggested);
                if (named == null)
                    return;
                suggested = named;
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
                var safeFile = MakeSafeFileName(suggested);
                if (!safeFile.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                    safeFile += ".dwg";
                var localPath = Path.Combine(destDir, safeFile);

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

                if (string.IsNullOrWhiteSpace(created.Id))
                {
                    SetStatus("Создано: " + suggested + " (ищем в списке…)");
                }
                else
                {
                    SetStatus("Создано в каталоге: " + suggested);
                }

                created.LocalPath = localPath;
                if (!string.IsNullOrWhiteSpace(created.Id))
                    OpenDrawingRegistry.Register(localPath, created);
                OpenDrawingRegistry.PendingNewName = suggested;
                AcadDocumentService.WriteMessage("Новый чертёж сохранён: " + suggested);

                await ReloadAsync().ConfigureAwait(true);

                var match = _allFiles.Find(f =>
                    string.Equals(f.Name, suggested, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    match.LocalPath = localPath;
                    match.DwgFieldCode = created.DwgFieldCode;
                    OpenDrawingRegistry.Register(localPath, match);
                    SetStatus("Создано в каталоге: " + match.Name);
                }

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
                dialog.ClientSize = new Size(360, 120);
                dialog.Font = new Font("Segoe UI", 9f);

                var label = new Label
                {
                    Text = "Новое имя:",
                    Left = 12,
                    Top = 14,
                    AutoSize = true
                };
                var box = new TextBox
                {
                    Left = 12,
                    Top = 38,
                    Width = 336,
                    Text = currentName ?? string.Empty
                };
                var ok = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Left = 172,
                    Top = 78,
                    Width = 84
                };
                var cancel = new Button
                {
                    Text = "Отмена",
                    DialogResult = DialogResult.Cancel,
                    Left = 264,
                    Top = 78,
                    Width = 84
                };
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

        private void UpdateActiveLabel()
        {
            if (_activeLabel == null || _activeLabel.IsDisposed)
                return;
            if (_renameButton == null || _renameButton.IsDisposed)
                return;

            var loggedIn = !_busy && PluginApp.Session != null && PluginApp.Session.IsAuthenticated;
            var hasDoc = AcadDocumentService.HasActiveDocument();

            if (OpenDrawingRegistry.TryGetCurrent(out var file, out _)
                && !string.IsNullOrWhiteSpace(file.Id))
            {
                _activeLabel.Text = "Активный чертёж: " + file.Name;
                _renameButton.Enabled = loggedIn;
                _saveButton.Enabled = loggedIn;
                if (file.Labels != null && file.Labels.HasAnyValue)
                    ApplyLabelsToUi(file.Labels);
                else if (file.Labels == null)
                    _ = EnsureLabelsLoadedAsync(file);
            }
            else if (hasDoc)
            {
                var title = OpenDrawingRegistry.PendingNewName
                            ?? AcadDocumentService.TryGetActiveDocumentTitle()
                            ?? "новый";
                _activeLabel.Text = "Новый чертёж: " + title + " (сохранить в каталог)";
                _renameButton.Enabled = loggedIn;
                _saveButton.Enabled = loggedIn;
            }
            else
            {
                _activeLabel.Text = "Активный чертёж: —";
                _renameButton.Enabled = false;
                _saveButton.Enabled = false;
            }
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
                    var hay = (file.Name + " " + file.ContentType + " " + file.Status + " " + file.Project + " " + file.Id);
                    if (hay.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                var item = new ListViewItem(file.Name);
                item.SubItems.Add(file.Status ?? string.Empty);
                item.SubItems.Add(file.Project ?? string.Empty);
                item.SubItems.Add(file.UpdatedAt?.ToLocalTime().ToString("g") ?? string.Empty);
                item.Tag = file;
                _list.Items.Add(item);
            }

            _list.EndUpdate();
            UpdateCatalogActionButtons();
        }

        private void BindFilters(IReadOnlyList<FilterEntity> filters)
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

            // Keep current construction labels selected after options reload.
            if (OpenDrawingRegistry.TryGetCurrent(out var current, out _)
                && current.Labels != null
                && current.Labels.HasAnyValue)
            {
                ApplyLabelsToUi(current.Labels);
            }
            else if (_list.SelectedItems.Count > 0
                     && _list.SelectedItems[0].Tag is DwgFileInfo selected
                     && selected.Labels != null
                     && selected.Labels.HasAnyValue)
            {
                ApplyLabelsToUi(selected.Labels);
            }
        }

        private async Task OnCatalogSelectionChangedAsync()
        {
            if (_list.SelectedItems.Count == 0)
                return;

            var file = _list.SelectedItems[0].Tag as DwgFileInfo;
            if (file == null || string.IsNullOrWhiteSpace(file.Id))
                return;

            // Prefer already loaded labels; otherwise fetch payload codes.
            if (file.Labels != null && file.Labels.HasAnyValue)
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
            if (file.Labels != null && file.Labels.HasAnyValue)
            {
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
                ProductionDrawingLabels? labels;
                using (var client = new DwgApiClient(PluginApp.Settings, PluginApp.Session))
                {
                    labels = await client.GetContentLabelsAsync(file.Id, token).ConfigureAwait(true);
                }

                if (token.IsCancellationRequested || _labelsRequestId != requestId)
                    return;

                file.Labels = labels ?? new ProductionDrawingLabels();
                var match = _allFiles.Find(f =>
                    string.Equals(f.Id, file.Id, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    match.Labels = file.Labels.Clone();
                if (!string.IsNullOrWhiteSpace(file.LocalPath))
                    OpenDrawingRegistry.Register(file.LocalPath!, file);

                if (file.Labels.HasAnyValue)
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

            SelectComboCode(_userCombo, labels?.UserUuid);
            SelectComboCode(_categoryCombo, labels?.GlobalCategoryCode);
            SelectComboCode(_brandCombo, labels?.BrandCode);
            SelectComboCode(_modelCombo, labels?.ModelCode);
            SelectComboCode(_perforationCombo, labels?.PerforationCode);
            SelectComboCode(_edgeCombo, labels?.EdgeCode);
            SelectComboCode(_sizeCombo, labels?.PanelSizeCode);
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
            _userCombo.Enabled = !busy;
            _categoryCombo.Enabled = !busy;
            _brandCombo.Enabled = !busy;
            _modelCombo.Enabled = !busy;
            _perforationCombo.Enabled = !busy;
            _edgeCombo.Enabled = !busy;
            _sizeCombo.Enabled = !busy;
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
            if (IsHandleCreated && !IsDisposed)
                _loginStatus.Text = text;
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
            }

            base.Dispose(disposing);
        }
    }
}

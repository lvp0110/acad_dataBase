using System;
using System.Collections.Generic;
using System.Drawing;
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
        private TextBox _filterBox = null!;
        private Label _statusLabel = null!;
        private ProgressBar _progress = null!;
        private List<DwgFileInfo> _allFiles = new List<DwgFileInfo>();
        private CancellationTokenSource? _cts;
        private bool _busy;
        private bool _sessionChecked;

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

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            panel.Controls.Add(layout);

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            layout.Controls.Add(header, 0, 0);

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

            layout.Controls.Add(new Label
            {
                Text = "Производственные чертежи",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            }, 0, 1);

            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.Controls.Add(toolbar, 0, 2);

            _filterBox = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
            _filterBox.TextChanged += (_, __) => ApplyFilter();
            toolbar.Controls.Add(_filterBox, 0, 0);

            _refreshButton = new Button
            {
                Text = "Обновить",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 0)
            };
            _refreshButton.Click += async (_, __) => await ReloadAsync();
            toolbar.Controls.Add(_refreshButton, 1, 0);

            _openButton = new Button
            {
                Text = "Открыть",
                Dock = DockStyle.Fill,
                Margin = new Padding(6, 0, 0, 0),
                Enabled = false
            };
            _openButton.Click += async (_, __) => await OpenSelectedAsync();
            toolbar.Controls.Add(_openButton, 2, 0);

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
            _list.SelectedIndexChanged += (_, __) =>
                _openButton.Enabled = !_busy && _list.SelectedItems.Count > 0;
            _list.DoubleClick += async (_, __) => await OpenSelectedAsync();
            layout.Controls.Add(_list, 0, 3);

            _progress = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };
            layout.Controls.Add(_progress, 0, 4);

            _statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Готово",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 8.5f)
            };
            layout.Controls.Add(_statusLabel, 0, 5);

            return panel;
        }

        public void RefreshOnShow()
        {
            ReloadSettingsIntoUi();
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
                    var files = await client.ListFilesAsync(_cts.Token).ConfigureAwait(true);
                    _allFiles = new List<DwgFileInfo>(files);
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
                AcadDocumentService.WriteMessage("Открыт файл: " + localPath);
                SetStatus("Открыто: " + file.Name);
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
            _openButton.Enabled = !_busy && _list.SelectedItems.Count > 0;
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
            _openButton.Enabled = !busy && _list.SelectedItems.Count > 0;
            _filterBox.Enabled = !busy;
            if (status != null)
                SetStatus(status);
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
                _cts?.Cancel();
                _cts?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

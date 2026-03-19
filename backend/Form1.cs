using System;
using System.Drawing;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;

namespace WhatsAppTranscriptor
{
    public partial class Form1 : Form
    {
        private TabControl tabControlMain;
        private TabPage tabDashboard;
        private TabPage tabSettings;

        private SplitContainer splitMain;
        private DataGridView gridPrinters;
        private Panel panelCenter;
        private ListBox listWhatsApp;
        private TextBox txtConsole;

        // Form controls for Center Panel
        private ComboBox cmbWorkStatus;
        private TextBox txtWorkName;
        private TextBox txtClient;
        private TextBox txtSeller;
        private TextBox txtProduct;

        // Timer for scraper
        private System.Windows.Forms.Timer pollTimer;
        private TextBox txtWidth;
        private TextBox txtLength;
        private TextBox txtQuantity;
        private TextBox txtTotal;
        private TextBox txtAdvance;
        private Button btnSendVenta;
        private Button btnSendPago;

        // Form controls for Settings Panel
        private ListBox listPrinterUrls;
        private TextBox txtNewUrl;
        private Button btnAddUrl;
        private Button btnRemoveUrl;

        public Form1()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.gridPrinters = new System.Windows.Forms.DataGridView();
            this.panelCenter = new System.Windows.Forms.Panel();
            this.listWhatsApp = new System.Windows.Forms.ListBox();
            this.txtConsole = new System.Windows.Forms.TextBox();
            
            // Basic Form Control initializations
            this.txtWorkName = new System.Windows.Forms.TextBox();
            this.txtClient = new System.Windows.Forms.TextBox();
            this.txtSeller = new System.Windows.Forms.TextBox();
            this.txtProduct = new System.Windows.Forms.TextBox();
            this.txtTotal = new System.Windows.Forms.TextBox();
            this.btnSendVenta = new System.Windows.Forms.Button();
            this.btnSendPago = new System.Windows.Forms.Button();

            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabDashboard = new System.Windows.Forms.TabPage();
            this.tabSettings = new System.Windows.Forms.TabPage();
            this.listPrinterUrls = new System.Windows.Forms.ListBox();
            this.txtNewUrl = new System.Windows.Forms.TextBox();
            this.btnAddUrl = new System.Windows.Forms.Button();
            this.btnRemoveUrl = new System.Windows.Forms.Button();

            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridPrinters)).BeginInit();
            this.panelCenter.SuspendLayout();
            this.tabControlMain.SuspendLayout();
            this.tabDashboard.SuspendLayout();
            this.tabSettings.SuspendLayout();
            this.SuspendLayout();

            // 
            // tabControlMain
            // 
            this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControlMain.Controls.Add(this.tabDashboard);
            this.tabControlMain.Controls.Add(this.tabSettings);

            //
            // tabDashboard
            //
            this.tabDashboard.Text = "Principal";
            this.tabDashboard.Controls.Add(this.splitMain);

            //
            // tabSettings
            //
            this.tabSettings.Text = "Ajustes / URLs de Impresión";
            this.tabSettings.Padding = new Padding(20);
            
            Label lblUrlTitle = new Label { Text = "URLs / IPs de Máquinas de Impresión a Monitorear:", Location = new Point(20, 20), AutoSize = true, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
            this.tabSettings.Controls.Add(lblUrlTitle);
            
            this.listPrinterUrls.Location = new Point(20, 60);
            this.listPrinterUrls.Size = new Size(400, 200);
            this.tabSettings.Controls.Add(this.listPrinterUrls);

            this.txtNewUrl.Location = new Point(20, 280);
            this.txtNewUrl.Size = new Size(300, 25);
            this.txtNewUrl.PlaceholderText = "ej. http://192.168.1.50/status";
            this.tabSettings.Controls.Add(this.txtNewUrl);

            this.btnAddUrl.Location = new Point(330, 280);
            this.btnAddUrl.Name = "btnAddUrl";
            this.btnAddUrl.Size = new Size(90, 25);
            this.btnAddUrl.Text = "AGREGAR";
            this.btnAddUrl.Click += new EventHandler(this.btnAddUrl_Click);
            this.tabSettings.Controls.Add(this.btnAddUrl);

            this.btnRemoveUrl.Location = new Point(440, 60);
            this.btnRemoveUrl.Name = "btnRemoveUrl";
            this.btnRemoveUrl.Size = new Size(90, 30);
            this.btnRemoveUrl.Text = "ELIMINAR";
            this.btnRemoveUrl.BackColor = Color.LightCoral;
            this.btnRemoveUrl.Click += new EventHandler(this.btnRemoveUrl_Click);
            this.tabSettings.Controls.Add(this.btnRemoveUrl);

            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 0);
            this.splitMain.Name = "splitMain";
            
            // Panel 1 (Left) - Printers
            this.splitMain.Panel1.Controls.Add(this.gridPrinters);
            
            // Panel 2 (Right) - Split again for Center(Form) and Right(WA)
            SplitContainer splitRight = new SplitContainer();
            splitRight.Dock = DockStyle.Fill;
            splitRight.Orientation = Orientation.Vertical;
            splitRight.SplitterDistance = 300;
            
            // Center - Form
            splitRight.Panel1.Controls.Add(this.panelCenter);
            
            // Right - WA Context & Console
            SplitContainer splitFarRight = new SplitContainer();
            splitFarRight.Dock = DockStyle.Fill;
            splitFarRight.Orientation = Orientation.Horizontal;
            splitFarRight.Panel1.Controls.Add(this.listWhatsApp);
            splitFarRight.Panel2.Controls.Add(this.txtConsole);
            
            splitRight.Panel2.Controls.Add(splitFarRight);
            
            this.splitMain.Panel2.Controls.Add(splitRight);
            this.splitMain.SplitterDistance = 400; // Left panel width

            // 
            // gridPrinters
            // 
            this.gridPrinters.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridPrinters.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridPrinters.Location = new System.Drawing.Point(0, 0);
            this.gridPrinters.Name = "gridPrinters";
            this.gridPrinters.RowTemplate.Height = 25;
            this.gridPrinters.Size = new System.Drawing.Size(400, 450);
            this.gridPrinters.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridPrinters_CellDoubleClick);

            // 
            // panelCenter
            // 
            this.panelCenter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelCenter.Location = new System.Drawing.Point(0, 0);
            this.panelCenter.Name = "panelCenter";
            this.panelCenter.Padding = new System.Windows.Forms.Padding(10);
            
            // Build Center Form (Quick Layout)
            int yPos = 20;
            AddFormField("Trabajo/Archivo:", txtWorkName, ref yPos);
            AddFormField("Cliente:", txtClient, ref yPos);
            AddFormField("Vendedor:", txtSeller, ref yPos);
            AddFormField("Producto/Prod:", txtProduct, ref yPos);
            AddFormField("Total S/:", txtTotal, ref yPos);

            this.btnSendVenta.Location = new System.Drawing.Point(10, yPos + 20);
            this.btnSendVenta.Name = "btnSendVenta";
            this.btnSendVenta.Size = new System.Drawing.Size(120, 40);
            this.btnSendVenta.Text = "ENVIAR VENTA";
            this.btnSendVenta.BackColor = Color.LightGreen;
            
            this.btnSendPago.Location = new System.Drawing.Point(140, yPos + 20);
            this.btnSendPago.Name = "btnSendPago";
            this.btnSendPago.Size = new System.Drawing.Size(120, 40);
            this.btnSendPago.Text = "ENVIAR PAGO";
            this.btnSendPago.BackColor = Color.LightSkyBlue;
            
            this.panelCenter.Controls.Add(this.btnSendVenta);
            this.panelCenter.Controls.Add(this.btnSendPago);

            // 
            // listWhatsApp
            // 
            this.listWhatsApp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listWhatsApp.FormattingEnabled = true;
            this.listWhatsApp.ItemHeight = 15;
            this.listWhatsApp.Name = "listWhatsApp";

            // 
            // txtConsole
            // 
            this.txtConsole.BackColor = System.Drawing.Color.Black;
            this.txtConsole.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtConsole.Font = new System.Drawing.Font("Consolas", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtConsole.ForeColor = System.Drawing.Color.LimeGreen;
            this.txtConsole.Multiline = true;
            this.txtConsole.Name = "txtConsole";
            this.txtConsole.ReadOnly = true;
            this.txtConsole.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;

            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 600); // Larger window
            this.Controls.Add(this.tabControlMain);
            this.Name = "Form1";
            this.Text = "WhatsApp Transcriptor & Print Dashboard";
            this.Load += new System.EventHandler(this.Form1_Load);
            
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.gridPrinters)).EndInit();
            this.panelCenter.ResumeLayout(false);
            this.panelCenter.PerformLayout();
            this.tabControlMain.ResumeLayout(false);
            this.tabDashboard.ResumeLayout(false);
            this.tabSettings.ResumeLayout(false);
            this.tabSettings.PerformLayout();
            this.ResumeLayout(false);
        }

        private void AddFormField(string labelText, TextBox tb, ref int yPos)
        {
            Label lbl = new Label { Text = labelText, Location = new Point(10, yPos), AutoSize = true };
            tb.Location = new Point(110, yPos - 3);
            tb.Size = new Size(150, 23);
            this.panelCenter.Controls.Add(lbl);
            this.panelCenter.Controls.Add(tb);
            yPos += 35;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                // Wait briefly for UI to map handles before directing Console output
                await Task.Delay(500);

                // Temporarily disable Console.SetOut to see if it's swallowing errors or deadlocking
                Console.SetOut(new TextBoxWriter(txtConsole));

                Console.WriteLine("[DEBUG] Form1_Load iniciado.");

                Console.WriteLine("[DEBUG] Form1_Load iniciado.");
                ConfigureGridPrinters();

                // Load URLs onto UI
                LoadUrlsIntoUI();

                // Load quick recent context from SQLite DB asynchronously (Fire and forget)
                Console.WriteLine("[DEBUG] Iniciando carga de WA Context...");
                _ = LoadRecentMessagesAsync();

                Console.WriteLine("[INFO] Iniciando Scraper de Impresoras (5 min)...");
                pollTimer = new System.Windows.Forms.Timer();
                pollTimer.Interval = 300000; // 5 minutos (300,000 ms)
                pollTimer.Tick += (s, ev) => _ = Task.Run(() => FetchAndRenderPrintersAsync());
                pollTimer.Start();

                // Run first scrape immediately
                _ = Task.Run(() => FetchAndRenderPrintersAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FATAL UI ERROR] Form1_Load falló: {ex}");
            }
        }

        private void ConfigureGridPrinters()
        {
            gridPrinters.AutoGenerateColumns = false;
            gridPrinters.AllowUserToAddRows = false;
            gridPrinters.ReadOnly = true;
            gridPrinters.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridPrinters.MultiSelect = false;

            gridPrinters.Columns.Clear();
            gridPrinters.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Máquina", DataPropertyName = "Machine", Width = 80 });
            gridPrinters.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tipo", DataPropertyName = "Type", Width = 50 });
            gridPrinters.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Archivo", DataPropertyName = "Name", Width = 150, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            gridPrinters.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ancho", DataPropertyName = "Width", Width = 50 });
            gridPrinters.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Largo", DataPropertyName = "Length", Width = 50 });
            gridPrinters.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Copias", DataPropertyName = "Copies", Width = 50 });
            gridPrinters.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hora", DataPropertyName = "TimeStr", Width = 60 });
        }

        private async Task FetchAndRenderPrintersAsync()
        {
            try
            {
                string apexUrl = ConfigManager.Settings.OracleApexBaseUrl;
                if (string.IsNullOrEmpty(apexUrl))
                {
                    Console.WriteLine("[Scraper APEX] Error: OracleApexBaseUrl no configurado.");
                    return;
                }

                // Fetch all items from APEX
                var allJobs = await PrinterScraperService.FetchFromApexAsync(apexUrl);

                // Filter to today only
                var todayStart = DateTime.Today;
                var todayEnd = todayStart.AddDays(1).AddTicks(-1);

                var filteredJobs = allJobs
                    .Where(j => j.ParsedDate.HasValue && j.ParsedDate.Value >= todayStart && j.ParsedDate.Value <= todayEnd)
                    .OrderByDescending(j => j.ParsedDate)
                    .ToList();

                // Update UI safely
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => UpdateGrid(filteredJobs)));
                }
                else
                {
                    UpdateGrid(filteredJobs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Scraper] Error de refresco: {ex.Message}");
            }
        }

        private void UpdateGrid(System.Collections.Generic.List<PrintJob> jobs)
        {
            gridPrinters.DataSource = null;
            gridPrinters.DataSource = jobs;
            
            // Apply Custom Coloring (Blue for RIP, Green for PRINT)
            foreach (DataGridViewRow row in gridPrinters.Rows)
            {
                if (row.DataBoundItem is PrintJob job)
                {
                    if (job.Type == "RIP") row.DefaultCellStyle.BackColor = Color.LightBlue;
                    else if (job.Type == "PRINT") row.DefaultCellStyle.BackColor = Color.LightGreen;
                }
            }
        }

        private void LoadUrlsIntoUI()
        {
            listPrinterUrls.Items.Clear();
            if (ConfigManager.Settings.PrinterUrls != null)
            {
                foreach (var url in ConfigManager.Settings.PrinterUrls)
                {
                    listPrinterUrls.Items.Add(url);
                }
            }
        }

        private void btnAddUrl_Click(object? sender, EventArgs e)
        {
            string newUrl = txtNewUrl.Text.Trim();
            if (!string.IsNullOrEmpty(newUrl))
            {
                ConfigManager.Settings.PrinterUrls ??= new System.Collections.Generic.List<string>();
                if (!ConfigManager.Settings.PrinterUrls.Contains(newUrl))
                {
                    ConfigManager.Settings.PrinterUrls.Add(newUrl);
                    ConfigManager.SaveConfig();
                    LoadUrlsIntoUI();
                    txtNewUrl.Clear();
                }
            }
        }

        private void btnRemoveUrl_Click(object? sender, EventArgs e)
        {
            if (listPrinterUrls.SelectedItem != null)
            {
                string selectedUrl = listPrinterUrls.SelectedItem.ToString();
                if (ConfigManager.Settings.PrinterUrls != null && ConfigManager.Settings.PrinterUrls.Contains(selectedUrl))
                {
                    ConfigManager.Settings.PrinterUrls.Remove(selectedUrl);
                    ConfigManager.SaveConfig();
                    LoadUrlsIntoUI();
                }
            }
        }

        private async Task LoadRecentMessagesAsync()
        {
            try
            {
                using (var db = new MessagesDbContext())
                {
                    await db.Database.EnsureCreatedAsync();

                    // Get last 30 messages across all chats
                    var recentMessages = await db.Messages
                        .OrderByDescending(m => m.Timestamp)
                        .Take(30)
                        .ToListAsync();

                    // Display them ascending (older top, newer bottom)
                    recentMessages.Reverse();

                    listWhatsApp.Items.Clear();
                    foreach (var m in recentMessages)
                    {
                        string prefix = m.Sender == "Me" ? "TÚ: " : $"{m.PhoneNumber}: ";
                        listWhatsApp.Items.Add($"[{m.Timestamp:HH:mm}] {prefix}{m.Text}");
                    }
                    
                    if (listWhatsApp.Items.Count > 0)
                        listWhatsApp.TopIndex = listWhatsApp.Items.Count - 1; // Auto-scroll
                }
            }
            catch (Exception ex)
            {
                listWhatsApp.Items.Add($"[Error cargando historial WA: {ex.Message}]");
            }
        }

        private void gridPrinters_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < gridPrinters.Rows.Count)
            {
                if (gridPrinters.Rows[e.RowIndex].DataBoundItem is PrintJob job)
                {
                    if (job.Type == "RIP")
                    {
                        using (var formPedido = new FormPedido(job))
                        {
                            formPedido.ShowDialog();
                        }
                    }
                }
            }
        }
    }
}

using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;

namespace WhatsAppTranscriptor
{
    public partial class FormPedido : Form
    {
        private PrintJob _job;

        // UI Controls - Pedido (Top)
        private TextBox txtVendedor;
        private TextBox txtCliente;
        private TextBox txtTelefono;
        private TextBox txtTotal;
        private TextBox txtAcuenta;
        private TextBox txtSaldo;
        private TextBox txtMaquina;

        // UI Controls - Detalle (Bottom)
        private TextBox txtProducto;
        private TextBox txtCantidad;
        private TextBox txtAncho;
        private TextBox txtLargo;
        private TextBox txtSubtotal;

        private Button btnGuardar;
        private Button btnCancelar;

        public FormPedido(PrintJob job)
        {
            _job = job;
            InitializeComponent();
            PopulateData();
        }

        private void InitializeComponent()
        {
            this.Text = "Nuevo Pedido desde RIP";
            this.Size = new Size(400, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            int yPos = 20;
            int xLabel = 20;
            int xInput = 120;

            // --- SECCIÓN PEDIDO ---
            Label lblSeccion1 = new Label { Text = "--- DATOS DEL PEDIDO ---", Location = new Point(xLabel, yPos), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            this.Controls.Add(lblSeccion1);
            yPos += 30;

            this.txtVendedor = AddField("Vendedor:", xLabel, xInput, ref yPos);
            this.txtCliente = AddField("Cliente:", xLabel, xInput, ref yPos);
            this.txtTelefono = AddField("Teléfono:", xLabel, xInput, ref yPos);
            this.txtTotal = AddField("Total S/:", xLabel, xInput, ref yPos);
            this.txtAcuenta = AddField("A Cuenta S/:", xLabel, xInput, ref yPos);
            this.txtSaldo = AddField("Saldo S/:", xLabel, xInput, ref yPos);
            this.txtSaldo.ReadOnly = true;
            this.txtSaldo.BackColor = SystemColors.Control;
            
            this.txtMaquina = AddField("Máquina:", xLabel, xInput, ref yPos);
            this.txtMaquina.ReadOnly = true;
            this.txtMaquina.BackColor = SystemColors.Control;

            // Events for calculation
            this.txtTotal.TextChanged += CalcularSaldo;
            this.txtAcuenta.TextChanged += CalcularSaldo;

            yPos += 20;
            // --- SECCIÓN DETALLE ---
            Label lblSeccion2 = new Label { Text = "--- DETALLE (1 ÍTEM) ---", Location = new Point(xLabel, yPos), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };
            this.Controls.Add(lblSeccion2);
            yPos += 30;

            this.txtProducto = AddField("Producto:", xLabel, xInput, ref yPos);
            this.txtCantidad = AddField("Cantidad:", xLabel, xInput, ref yPos);
            this.txtAncho = AddField("Ancho (m):", xLabel, xInput, ref yPos);
            this.txtLargo = AddField("Largo (m):", xLabel, xInput, ref yPos);
            this.txtSubtotal = AddField("Subtotal S/:", xLabel, xInput, ref yPos);

            yPos += 30;

            // --- BOTONES ---
            this.btnGuardar = new Button { Text = "ENVIAR A APEX", Location = new Point(50, yPos), Size = new Size(130, 40), BackColor = Color.LightGreen };
            this.btnGuardar.Click += BtnGuardar_Click;
            this.Controls.Add(this.btnGuardar);

            this.btnCancelar = new Button { Text = "CANCELAR", Location = new Point(200, yPos), Size = new Size(130, 40), BackColor = Color.LightCoral };
            this.btnCancelar.Click += (s, e) => this.Close();
            this.Controls.Add(this.btnCancelar);
        }

        private TextBox AddField(string labelText, int xL, int xI, ref int yP)
        {
            Label lbl = new Label { Text = labelText, Location = new Point(xL, yP), AutoSize = true };
            TextBox tb = new TextBox { Location = new Point(xI, yP - 3), Size = new Size(220, 23) };
            this.Controls.Add(lbl);
            this.Controls.Add(tb);
            yP += 30;
            return tb;
        }

        private void CalcularSaldo(object? sender, EventArgs e)
        {
            decimal total = 0;
            decimal acuenta = 0;

            decimal.TryParse(txtTotal.Text, out total);
            decimal.TryParse(txtAcuenta.Text, out acuenta);

            txtSaldo.Text = (total - acuenta).ToString("0.00");
            
            // Link subtotal easily if there's only 1 detail
            txtSubtotal.Text = total.ToString("0.00");
        }

        private async void PopulateData()
        {
            // Auto fill basics
            this.txtMaquina.Text = _job.Machine;
            this.txtCantidad.Text = _job.Copies.ToString();
            this.txtAncho.Text = _job.Width;
            this.txtLargo.Text = _job.Length;

            // Try to extract Cliente and Producto from filename
            // Example Format commonly found: "CLIENTE_NAME - PRODUCT_NAME.eps" 
            // or "CLIENTE_NAME_PRODUCT_NAME.pdf"
            string filename = _job.Name;
            
            // Remove extension
            int extIdx = filename.LastIndexOf('.');
            if (extIdx > 0) filename = filename.Substring(0, extIdx);

            string clientGuess = filename;
            string productGuess = "Impresión";

            if (filename.Contains(" - "))
            {
                var parts = filename.Split(new[] { " - " }, StringSplitOptions.None);
                clientGuess = parts[0].Trim();
                if (parts.Length > 1) productGuess = parts[1].Trim();
            }
            else if (filename.Contains("_"))
            {
                var parts = filename.Split('_');
                clientGuess = parts[0].Trim();
                if (parts.Length > 1) productGuess = parts[1].Trim();
            }

            this.txtCliente.Text = clientGuess;
            this.txtProducto.Text = productGuess;

            // Search DB for Phone Number based on Client guess
            await BuscarTelefonoAsync(clientGuess);
        }

        private async Task BuscarTelefonoAsync(string clientName)
        {
            if (string.IsNullOrWhiteSpace(clientName)) return;

            try
            {
                string lowerQuery = clientName.ToLower();

                using (var db = new MessagesDbContext())
                {
                    // Look in Messages where Sender was the Client and PhoneNumber (pushname) contains our guess
                    var match = await db.Messages
                        .Where(m => m.Sender == "Client" && m.PhoneNumber.ToLower().Contains(lowerQuery))
                        .OrderByDescending(m => m.Timestamp)
                        .FirstOrDefaultAsync();

                    if (match != null && this.IsHandleCreated)
                    {
                        // Some systems store the raw phone in PhoneNumber and pushed name in Text. 
                        // Typically, PhoneNumber here is the pushname/phone. We'll populate it.
                        // Ideally we'd have the digit-only phone, but this uses what the UI has.
                        this.Invoke(new Action(() => {
                            this.txtTelefono.Text = match.PhoneNumber;
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FormPedido] Error al buscar teléfono: {ex.Message}");
            }
        }

        private async void BtnGuardar_Click(object? sender, EventArgs e)
        {
            try
            {
                this.btnGuardar.Enabled = false;
                this.btnGuardar.Text = "Enviando...";

                var apexSync = new ApexSyncService();
                
                string telefono = txtTelefono.Text.Trim();
                string clienteNombre = txtCliente.Text.Trim();
                string vendedor = txtVendedor.Text.Trim();
                
                string total = txtTotal.Text.Trim();
                string acuenta = txtAcuenta.Text.Trim();
                
                string producto = txtProducto.Text.Trim();
                string cantidad = txtCantidad.Text.Trim();
                string ancho = txtAncho.Text.Trim();
                string largo = txtLargo.Text.Trim();
                string subtotal = txtSubtotal.Text.Trim();
                string maquina = txtMaquina.Text.Trim();

                if (string.IsNullOrEmpty(clienteNombre))
                {
                    MessageBox.Show("El nombre del cliente es obligatorio.");
                    return;
                }

                // Create or Get Client
                int? clientId = await apexSync.GetOrCreateClienteIdAsync(telefono, clienteNombre);
                if (!clientId.HasValue)
                {
                    MessageBox.Show("Falla al crear/obtener Cliente en Oracle APEX.");
                    return;
                }

                int? productoId = await apexSync.GetProductoIdAsync(producto);

                DateTime now = DateTime.Now;

                // Create Pedido
                int? pedidoId = await apexSync.GetPedidoIdForTodayAsync(clientId.Value, now);
                bool isNewPedido = false;

                if (!pedidoId.HasValue)
                {
                    pedidoId = await apexSync.CreatePedidoAsync(clientId.Value, total, acuenta, vendedor, now);
                    isNewPedido = true;
                }

                if (!pedidoId.HasValue)
                {
                    MessageBox.Show("Falla al crear Pedido en Oracle APEX.");
                    return;
                }

                // Add Detail
                if (productoId.HasValue)
                {
                    bool detOk = await apexSync.CreateDetallePedidoAsync(pedidoId.Value, productoId.Value, cantidad, subtotal, largo, ancho, maquina);
                    if (!detOk)
                    {
                        MessageBox.Show("Falla al crear Detalles del Pedido.");
                        return;
                    }

                    decimal.TryParse(total, out decimal currentTotal);
                    decimal.TryParse(acuenta, out decimal currentAcuenta);

                    if (!isNewPedido)
                    {
                        var exist = await apexSync.GetPedidoAsync(pedidoId.Value);
                        if (exist != null)
                        {
                            await apexSync.UpdatePedidoTotalsAsync(pedidoId.Value, exist.total + currentTotal, exist.acuenta + currentAcuenta);
                        }
                    }

                    if (currentAcuenta > 0)
                    {
                        // Use default pago Efectivo since we don't have a field for it here, or could add later
                        int pmtId = apexSync.GetTipoPagoId("Efectivo");
                        await apexSync.CreateCajaChicaAsync(pedidoId.Value, currentAcuenta, pmtId, vendedor, now);
                    }

                    MessageBox.Show("Venta sincronizada correctamente en Oracle APEX.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error interno: {ex.Message}");
            }
            finally
            {
                this.btnGuardar.Enabled = true;
                this.btnGuardar.Text = "ENVIAR A APEX";
            }
        }
    }
}

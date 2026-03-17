using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Timers;
using System.Linq;

namespace WhatsAppTranscriptor
{
    // 1. Define the Database Models
    public class Message
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty; // "Me" or "Client"
        public DateTime Timestamp { get; set; } // Server time of capture
        public string MessageTimestamp { get; set; } = string.Empty; // Native WhatsApp display time (used to prevent duplicates)
    }

    public class Sale
    {
        public int Id { get; set; }
        public string SellerName { get; set; } = string.Empty; // Tracked seller who triggered the save
        public string ChatContactName { get; set; } = string.Empty; // Pushname / Contact display name from WhatsApp
        public string Client { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public string Width { get; set; } = string.Empty;
        public string Length { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public string Total { get; set; } = string.Empty;
        public string AdvancePayment { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } // Server time of capture
        public string MessageTimestamp { get; set; } = string.Empty; // Native WhatsApp time
        public int Synced { get; set; } = 0; // 0 = Pending, 1 = Synced to Oracle APEX Cloud
    }

    public class Payment
    {
        public int Id { get; set; }
        public string Client { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty; // Yape, Plin, Transferencia
        public string SellerName { get; set; } = string.Empty; // Who registered the payment
        public DateTime Timestamp { get; set; } // Server time of capture
        public string MessageTimestamp { get; set; } = string.Empty; // Native WhatsApp display time
        public int Synced { get; set; } = 0; // 0 = Pending, 1 = Synced to Oracle APEX Cloud
    }

    // 2. Define the Database Contexts (SQLite)
    public class MessagesDbContext : DbContext
    {
        public DbSet<Message> Messages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=whatsapp_messages.db");
        }
    }

    public class SalesDbContext : DbContext
    {
        public DbSet<Sale> Sales { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=whatsapp_sales.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sale>().ToTable("Sales");
            modelBuilder.Entity<Payment>().ToTable("Payments");
        }
    }

    // 3. Define the External Configuration Engine (Dual-Mode Parser)
    public class VentaTags
    {
        public string Vendedor { get; set; } = "Vendedor:";
        public string Cliente { get; set; } = "Cliente:";
        public string Tlf { get; set; } = "Tlf:";
        public string Tipo { get; set; } = "Tipo:";
        public string Ancho { get; set; } = "Ancho:";
        public string Largo { get; set; } = "Largo:";
        public string Cant { get; set; } = "Cant:";
        public string Tot { get; set; } = "Tot:";
        public string Ade { get; set; } = "Ade:";
        public string Est { get; set; } = "Est:";
        public string Maq { get; set; } = "Maq:";
        public string Med { get; set; } = "Med:";
    }

    public class PagoTags
    {
        public string Vendedor { get; set; } = "Vendedor:";
        public string Cliente { get; set; } = "Cliente:";
        public string Monto { get; set; } = "Monto:";
        public string Medio { get; set; } = "Medio:";
    }

    public class ConfigSettings
    {
        public string OracleApexBaseUrl { get; set; } = "https://g0b98d8ee45b90d-db5ncy1.adb.us-sanjose-1.oraclecloudapps.com/ords/fullcolor";
        public int PuertoServidorLocal { get; set; } = 5000;
        public int MinutosReintentoSync { get; set; } = 30;
        public string Separator { get; set; } = " - ";
        public string VentaPrefix { get; set; } = "#VENTA";
        public string PagoPrefix { get; set; } = "#PAGO";
        public VentaTags VentaTags { get; set; } = new VentaTags();
        public PagoTags PagoTags { get; set; } = new PagoTags();
    }

    public static class ConfigManager
    {
        public static ConfigSettings Settings { get; private set; } = new ConfigSettings();

        public static void LoadConfig()
        {
            try
            {
                if (File.Exists("config.json"))
                {
                    string json = File.ReadAllText("config.json");
                    var loaded = JsonSerializer.Deserialize<ConfigSettings>(json);
                    if (loaded != null)
                    {
                        Settings = loaded;
                        Console.WriteLine("[INFO] External config.json loaded successfully. Running in DYNAMIC mode.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Could not parse config.json. Using legacy hardcoded defaults. Reason: {ex.Message}");
            }
            Console.WriteLine("[INFO] config.json not found. Running in LEGACY STRICT mode.");
        }
    }

    // 4. Input Payload Model
    public class IncomingMessagePayload
    {
        public string id { get; set; } = string.Empty; // Native unique WhatsApp ID
        public string phoneNumber { get; set; } = string.Empty; // Holds Chat Name/Pushname for backwards compatibility
        public string rawPhoneNumber { get; set; } = string.Empty; // Holds extracted digits like +51999888777
        public string text { get; set; } = string.Empty;
        public string sender { get; set; } = string.Empty;
        public string timestamp { get; set; } = string.Empty;
    }

    class Program
    {
        private static System.Timers.Timer? _syncTimer;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Loading Configuration...");
            ConfigManager.LoadConfig();

            Console.WriteLine("Initializing Databases...");
            using (var db = new MessagesDbContext())
            {
                db.Database.EnsureCreated();
            }
            using (var salesDb = new SalesDbContext())
            {
                salesDb.Database.EnsureCreated();
            }
            Console.WriteLine(@"
=========================================================
      WHATSAPP TRANSCRIPTOR & SALES EXTRACTOR V2.2            
=========================================================

FORMATOS ESPERADOS PARA EXTRAER VENTAS:
---------------------------------------------------------
El vendedor debe enviar un mensaje a su propio chat local
o al chat del cliente con la siguiente estructura:

#VENTA - #NOMBRE_VENDEDOR - Cliente: Nombre Apellido - Tipo: UV DTF - Ancho: 10 - Largo: 20 - Cant: 50 - Tot: 150 - Ade: 50 - Est: Proceso - Maq: Fortune - Med: Yape

* Maq (Máquina) y Med (Medio de Pago) son obligatorios 
  para sincronizar bien a Oracle APEX.
* IMPORTANTE: Ancho y Largo deben estar en METROS.
* El Teléfono (Tlf:) se extrae automáticamente del chat.
  (Solo inclúyelo a mano si el cliente usa otro número).
* ¡El sistema agrupará ventas del mismo día bajo una 
  sola Proforma automáticamente!

FORMATOS ESPERADOS PARA EXTRAER PAGOS AISLADOS:
---------------------------------------------------------
Cuando un cliente hace un abono de una Venta de horas o 
días anteriores, envía el siguiente formato:

#PAGO - #NOMBRE_VENDEDOR - Cliente: Nombre - Monto: 50 - Medio: Plin
=========================================================
");
            
            // Start the HTTP Listener using the Config port
            string url = $"http://localhost:{ConfigManager.Settings.PuertoServidorLocal}/api/messages/";
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(url);
            
            // Setup Background Retry Timer from Config
            _syncTimer = new System.Timers.Timer(ConfigManager.Settings.MinutosReintentoSync * 60 * 1000); 
            _syncTimer.Elapsed += async (sender, e) => await ProcessPendingSyncsAsync();
            _syncTimer.AutoReset = true;
            _syncTimer.Enabled = true;
            
            try
            {
                listener.Start();
                Console.WriteLine($"Server listening on {url}");
                Console.WriteLine("Background SQLite APEX Sync Retry Job activated (Runs every 30m).");
                Console.WriteLine("Waiting for messages from Chrome Extension...");

                while (true)
                {
                    // Wait for an incoming request
                    HttpListenerContext context = await listener.GetContextAsync();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;

                    try
                    {
                        if (request.HttpMethod == "POST" && request.HasEntityBody)
                        {
                            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                            {
                                string body = await reader.ReadToEndAsync();
                                
                                // Deserialize JSON
                                var payload = JsonSerializer.Deserialize<IncomingMessagePayload>(body);
                                
                                if (payload != null)
                                {
                                    Console.WriteLine($"\n[New Message from {payload.phoneNumber}]");
                                    Console.WriteLine($"{payload.sender}: {payload.text}");

                                    // Save every message to the Messages Database
                                    using (var db = new MessagesDbContext())
                                    {
                                        // Deduplication check: Do not save if we already recorded this exact unique ID
                                        bool isDuplicateMsg = db.Messages.Any(m => m.MessageTimestamp == payload.id);
                                        
                                        if (!isDuplicateMsg)
                                        {
                                            var newMessage = new Message
                                            {
                                                PhoneNumber = payload.phoneNumber,
                                                Text = payload.text,
                                                Sender = payload.sender,
                                                Timestamp = DateTime.UtcNow,
                                                MessageTimestamp = payload.id
                                            };
                                            db.Messages.Add(newMessage);
                                            await db.SaveChangesAsync();
                                        }
                                        else 
                                        {
                                            // Optional: Uncomment below if you want to see duplicate skips in the console
                                            // Console.WriteLine($"[Deduplication] Message originally sent at {payload.timestamp} already in DB.");
                                        }
                                    }
                                        
                                    // Hybrid AI Algorithm: Check if it's a formatted Sale Command
                                    if (payload.sender == "Me" && payload.text.Contains(ConfigManager.Settings.VentaPrefix))
                                    {
                                         Console.WriteLine(">>> VENTA DETECTADA! Procesando algoritmo de extracción dinámico...");
                                         try 
                                         {
                                             var parts = payload.text.Split(new[] { ConfigManager.Settings.Separator }, StringSplitOptions.None);
                                             
                                             var sale = new Sale 
                                             { 
                                                 Timestamp = DateTime.Now,
                                                 ChatContactName = payload.phoneNumber
                                             };
                                             
                                             string maquinaName = "";
                                             string paymentMethod = "";
                                             var tags = ConfigManager.Settings.VentaTags;

                                             foreach (var part in parts)
                                             {
                                                 var cleanPart = part.Trim();
                                                 // Tag parsing based on external configuration
                                                 if (cleanPart.StartsWith("#") && cleanPart.ToUpper() != ConfigManager.Settings.VentaPrefix.ToUpper()) sale.SellerName = cleanPart.Substring(1).Trim();
                                                 else if (cleanPart.StartsWith(tags.Vendedor, StringComparison.OrdinalIgnoreCase)) sale.SellerName = cleanPart.Substring(tags.Vendedor.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Cliente, StringComparison.OrdinalIgnoreCase)) sale.Client = cleanPart.Substring(tags.Cliente.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Tlf, StringComparison.OrdinalIgnoreCase)) sale.PhoneNumber = cleanPart.Substring(tags.Tlf.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Tipo, StringComparison.OrdinalIgnoreCase)) sale.ProductType = cleanPart.Substring(tags.Tipo.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Ancho, StringComparison.OrdinalIgnoreCase)) sale.Width = cleanPart.Substring(tags.Ancho.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Largo, StringComparison.OrdinalIgnoreCase)) sale.Length = cleanPart.Substring(tags.Largo.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Cant, StringComparison.OrdinalIgnoreCase)) sale.Quantity = cleanPart.Substring(tags.Cant.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Tot, StringComparison.OrdinalIgnoreCase)) sale.Total = cleanPart.Substring(tags.Tot.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Ade, StringComparison.OrdinalIgnoreCase)) sale.AdvancePayment = cleanPart.Substring(tags.Ade.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Est, StringComparison.OrdinalIgnoreCase)) sale.Status = cleanPart.Substring(tags.Est.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Maq, StringComparison.OrdinalIgnoreCase)) maquinaName = cleanPart.Substring(tags.Maq.Length).Trim();
                                                 else if (cleanPart.StartsWith(tags.Med, StringComparison.OrdinalIgnoreCase)) paymentMethod = cleanPart.Substring(tags.Med.Length).Trim();
                                             }
                                             
                                             // Implicit fallback if salesperson didn't type it explicitly
                                             if (string.IsNullOrEmpty(sale.Client)) {
                                                 sale.Client = sale.ChatContactName; // Use chat name / pushname
                                             }
                                             if (string.IsNullOrEmpty(sale.PhoneNumber)) {
                                                 // Try to fall back to the actual phone number the extension extracted from WhatsApp's UI
                                                 sale.PhoneNumber = payload.rawPhoneNumber; 
                                             }

                                             // Save ONLY the extracted sales data to the Sales Database
                                             using (var salesDb = new SalesDbContext())
                                             {
                                                 // Deduplication check for Sales
                                                 bool isDuplicateSale = salesDb.Sales.Any(s => s.MessageTimestamp == payload.id);

                                                 if (!isDuplicateSale)
                                                 {
                                                     sale.MessageTimestamp = payload.id; 
                                                     salesDb.Sales.Add(sale);
                                                     await salesDb.SaveChangesAsync();
                                                     Console.WriteLine($">>> EXTRACCIÓN EXITOSA: Vendedor: {sale.SellerName} | Cliente: {sale.Client} | Tlf: {sale.PhoneNumber} | Tipo: {sale.ProductType} | Area: {sale.Width}x{sale.Length} | Cant: {sale.Quantity} | Tot: {sale.Total}");
                                                     
                                                     // Oracle APEX Cloud: Real-time Synchronization
                                                     Console.WriteLine(">>> Iniciando Sincronización con Oracle APEX Cloud...");
                                                     var apexSync = new ApexSyncService();
                                                     
                                                     int? clientId = await apexSync.GetOrCreateClienteIdAsync(sale.PhoneNumber, sale.ChatContactName);
                                                     if (clientId.HasValue)
                                                     {
                                                         int? productoId = await apexSync.GetProductoIdAsync(sale.ProductType);
                                                         
                                                         // 1. Check if the client already has an active Pedido today
                                                         bool isNewPedido = false;
                                                         int? pedidoId = await apexSync.GetPedidoIdForTodayAsync(clientId.Value, sale.Timestamp);
                                                         
                                                         if (!pedidoId.HasValue)
                                                         {
                                                             pedidoId = await apexSync.CreatePedidoAsync(clientId.Value, sale.Total, sale.AdvancePayment, sale.SellerName, sale.Timestamp);
                                                             isNewPedido = true;
                                                         }
                                                         else
                                                         {
                                                             Console.WriteLine($">>> Proforma Diaria Existente Encontrada: ID {pedidoId.Value}. Agregando detalles...");
                                                         }
                                                         
                                                         if (pedidoId.HasValue && productoId.HasValue)
                                                         {
                                                             bool isDetalleOk = await apexSync.CreateDetallePedidoAsync(
                                                                 pedidoId.Value, productoId.Value, sale.Quantity, sale.Total, sale.Length, sale.Width, maquinaName);
                                                                 
                                                             if (isDetalleOk)
                                                             {
                                                                 decimal.TryParse(sale.Total, out decimal currentSaleTotal);
                                                                 decimal.TryParse(sale.AdvancePayment, out decimal currentSaleAcuenta);
                                                                 
                                                                 // If we attached to an existing Pedido, update its Totals
                                                                 if (!isNewPedido)
                                                                 {
                                                                     var existingTotals = await apexSync.GetPedidoAsync(pedidoId.Value);
                                                                     if (existingTotals != null)
                                                                     {
                                                                         decimal newTotal = existingTotals.total + currentSaleTotal;
                                                                         decimal newAcuenta = existingTotals.acuenta + currentSaleAcuenta;
                                                                         await apexSync.UpdatePedidoTotalsAsync(pedidoId.Value, newTotal, newAcuenta);
                                                                         Console.WriteLine($">>> Proforma Actualizada: Nuevo Total {newTotal} | Nuevo Acuenta {newAcuenta}");
                                                                     }
                                                                 }

                                                                 bool pagoOk = true;
                                                                 
                                                                 if (currentSaleAcuenta > 0)
                                                                 {
                                                                     int pmtId = apexSync.GetTipoPagoId(paymentMethod);
                                                                     pagoOk = await apexSync.CreateCajaChicaAsync(pedidoId.Value, currentSaleAcuenta, pmtId, sale.SellerName, sale.Timestamp);
                                                                 }
                                                                 
                                                                 if (pagoOk)
                                                                 {
                                                                     // Mark as synced locally
                                                                     sale.Synced = 1;
                                                                     salesDb.Sales.Update(sale);
                                                                     await salesDb.SaveChangesAsync();
                                                                     Console.WriteLine(">>> ✅ VENTA SINCRONIZADA EXITOSAMENTE EN ORACLE APEX.");
                                                                 }
                                                                 else
                                                                 {
                                                                     Console.WriteLine(">>> ⚠️ Sincronización Parcial: Caja Chica falló.");
                                                                 }
                                                             }
                                                         }
                                                     }
                                                     if (sale.Synced == 0) Console.WriteLine(">>> ⚠️ FALLO SINCRONIZACIÓN APEX. Se guardó localmente, se reintentará en el batch schedule.");
                                                 }
                                                 else
                                                 {
                                                     Console.WriteLine($">>> VENTA DUPLICADA IGNORADA (Esta venta de las {payload.timestamp} ya fue extraida anteriormente).");
                                                 }
                                             }
                                         }
                                         catch (Exception ex)
                                         {
                                             Console.WriteLine($">>> ERROR EXTRACCIÓN DE VENTA: Formato incorrecto. {ex.Message}");
                                         }
                                    }
                                    // Parse Payment Command
                                    else if (payload.sender == "Me" && payload.text.StartsWith(ConfigManager.Settings.PagoPrefix, StringComparison.OrdinalIgnoreCase))
                                    {
                                         try
                                         {
                                             // Normalize separators
                                             string textToParse = payload.text;
                                             textToParse = textToParse.Replace("\n", ConfigManager.Settings.Separator).Replace("-", ConfigManager.Settings.Separator).Replace("  ", " ");
                                             
                                             var parts = textToParse.Split(ConfigManager.Settings.Separator, StringSplitOptions.RemoveEmptyEntries);
                                             
                                             var payment = new Payment
                                             {
                                                 Timestamp = DateTime.Now,
                                                 MessageTimestamp = payload.timestamp,
                                                 PhoneNumber = !string.IsNullOrEmpty(payload.rawPhoneNumber) ? payload.rawPhoneNumber : payload.phoneNumber
                                             };

                                             var pTags = ConfigManager.Settings.PagoTags;

                                             foreach (var p in parts)
                                             {
                                                 var piece = p.Trim();
                                                 if (piece.StartsWith("#") && piece.ToUpper() != ConfigManager.Settings.PagoPrefix.ToUpper()) {
                                                     payment.SellerName = piece.Substring(1).Trim();
                                                 } else if (piece.StartsWith(pTags.Vendedor, StringComparison.OrdinalIgnoreCase)) {
                                                     payment.SellerName = piece.Substring(pTags.Vendedor.Length).Trim();
                                                 } else if (piece.StartsWith(pTags.Cliente, StringComparison.OrdinalIgnoreCase)) {
                                                     payment.Client = piece.Substring(pTags.Cliente.Length).Trim();
                                                 } else if (piece.StartsWith("Tlf:", StringComparison.OrdinalIgnoreCase) || piece.StartsWith("Telefono:", StringComparison.OrdinalIgnoreCase)) {
                                                     payment.PhoneNumber = piece.Split(':')[1].Trim();
                                                 } else if (piece.StartsWith(pTags.Monto, StringComparison.OrdinalIgnoreCase)) {
                                                     string amtStr = piece.Substring(pTags.Monto.Length).Trim();
                                                     if (decimal.TryParse(amtStr, out decimal parsedAmt)) payment.Amount = parsedAmt;
                                                 } else if (piece.StartsWith(pTags.Medio, StringComparison.OrdinalIgnoreCase)) {
                                                     payment.PaymentMethod = piece.Substring(pTags.Medio.Length).Trim();
                                                 }
                                             }

                                             // Use chat defaults if explicit info was not provided
                                             if (string.IsNullOrEmpty(payment.Client)) payment.Client = payload.phoneNumber;

                                             using (var salesDb = new SalesDbContext())
                                             {
                                                 bool isDuplicatePayment = salesDb.Payments.Any(p => p.MessageTimestamp == payload.id);

                                                 if (!isDuplicatePayment)
                                                 {
                                                     payment.MessageTimestamp = payload.id;
                                                     salesDb.Payments.Add(payment);
                                                     await salesDb.SaveChangesAsync();
                                                     Console.WriteLine($">>> PAGO REGISTRADO: Vendedor: {payment.SellerName} | Cliente: {payment.Client} | Monto: {payment.Amount} | Medio: {payment.PaymentMethod}");
                                                     
                                                     // Oracle APEX Cloud: Real-time Synchronization
                                                     Console.WriteLine(">>> Iniciando Sincronización de PAGO con Oracle APEX Cloud...");
                                                     var apexSync = new ApexSyncService();
                                                     
                                                     int? clientId = await apexSync.GetOrCreateClienteIdAsync(payment.PhoneNumber, payment.Client);
                                                     if (clientId.HasValue)
                                                     {
                                                         int? pedidoId = await apexSync.GetPedidoIdForTodayAsync(clientId.Value, payment.Timestamp);
                                                         if (pedidoId.HasValue)
                                                         {
                                                             var existingTotals = await apexSync.GetPedidoAsync(pedidoId.Value);
                                                             if (existingTotals != null)
                                                             {
                                                                 decimal newAcuenta = existingTotals.acuenta + payment.Amount;
                                                                 await apexSync.UpdatePedidoTotalsAsync(pedidoId.Value, existingTotals.total, newAcuenta);
                                                                 
                                                                 int pmtId = apexSync.GetTipoPagoId(payment.PaymentMethod);
                                                                 bool pagoOk = await apexSync.CreateCajaChicaAsync(pedidoId.Value, payment.Amount, pmtId, payment.SellerName, payment.Timestamp);
                                                                 
                                                                 if (pagoOk)
                                                                 {
                                                                     payment.Synced = 1;
                                                                     salesDb.Payments.Update(payment);
                                                                     await salesDb.SaveChangesAsync();
                                                                     Console.WriteLine(">>> ✅ PAGO SINCRONIZADO EXITOSAMENTE EN ORACLE APEX.");
                                                                 }
                                                                 else
                                                                 {
                                                                     Console.WriteLine(">>> ⚠️ Sincronización de Pago falló en Caja Chica.");
                                                                 }
                                                             }
                                                         }
                                                         else
                                                         {
                                                             Console.WriteLine(">>> ⚠️ No se encontró una Proforma de hoy para asociar el pago en APEX. Se guardó localmente.");
                                                         }
                                                     }
                                                 }
                                                 else
                                                 {
                                                     Console.WriteLine($">>> PAGO DUPLICADO IGNORADO (Este pago de las {payload.timestamp} ya fue registrado).");
                                                 }
                                             }
                                         }
                                         catch (Exception ex)
                                         {
                                             Console.WriteLine($">>> ERROR EXTRACCIÓN DE PAGO: Formato incorrecto. {ex.Message}");
                                         }
                                    }
                                }
                            }
                            
                            // Send Success Response
                            response.StatusCode = (int)HttpStatusCode.OK;
                            string responseString = "{\"status\": \"success\"}";
                            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                            response.ContentLength64 = buffer.Length;
                            response.OutputStream.Write(buffer, 0, buffer.Length);
                        }
                        else if (request.HttpMethod == "OPTIONS")
                        {
                            // Handle CORS Preflight request so the Chrome extension can connect
                            response.AddHeader("Access-Control-Allow-Origin", "https://web.whatsapp.com");
                            response.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
                            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
                            response.StatusCode = (int)HttpStatusCode.OK;
                        }
                        else
                        {
                            response.StatusCode = (int)HttpStatusCode.BadRequest;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing request: {ex.Message}");
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                    finally
                    {
                        // Enable CORS for regular POST requests too
                        if (request.HttpMethod != "OPTIONS")
                        {
                           response.AddHeader("Access-Control-Allow-Origin", "https://web.whatsapp.com");
                        }
                        response.OutputStream.Close();
                    }
                }
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"Error starting server (You might need Administrator privileges): {ex.Message}");
                Console.WriteLine("Try running Visual Studio / Command Prompt / PowerShell as Administrator.");
            }
        }

        private static async Task ProcessPendingSyncsAsync()
        {
            try 
            {
                using (var salesDb = new SalesDbContext())
                {
                    var pendingSales = await salesDb.Sales.Where(s => s.Synced == 0).ToListAsync();
                    var pendingPayments = await salesDb.Payments.Where(p => p.Synced == 0).ToListAsync();

                    if (pendingSales.Count == 0 && pendingPayments.Count == 0) return;

                    Console.WriteLine($"\n[RETRY JOB] Iniciando sincronización de {pendingSales.Count} Ventas y {pendingPayments.Count} Pagos pendientes...");
                    var apexSync = new ApexSyncService();

                    // Re-sync Sales
                    foreach (var sale in pendingSales)
                    {
                        try 
                        {
                            int? clientId = await apexSync.GetOrCreateClienteIdAsync(sale.PhoneNumber, sale.ChatContactName);
                            if (clientId.HasValue)
                            {
                                int? productoId = await apexSync.GetProductoIdAsync(sale.ProductType);
                                
                                bool isNewPedido = false;
                                int? pedidoId = await apexSync.GetPedidoIdForTodayAsync(clientId.Value, sale.Timestamp);
                                
                                if (!pedidoId.HasValue)
                                {
                                    pedidoId = await apexSync.CreatePedidoAsync(clientId.Value, sale.Total, sale.AdvancePayment, sale.SellerName, sale.Timestamp);
                                    isNewPedido = true;
                                }
                                
                                if (pedidoId.HasValue && productoId.HasValue)
                                {
                                    // Extract machine name from somewhere if needed, currently fallback to general machine in DB
                                    bool isDetalleOk = await apexSync.CreateDetallePedidoAsync(
                                        pedidoId.Value, productoId.Value, sale.Quantity, sale.Total, sale.Length, sale.Width, "");
                                        
                                    if (isDetalleOk)
                                    {
                                        decimal.TryParse(sale.Total, out decimal currentSaleTotal);
                                        decimal.TryParse(sale.AdvancePayment, out decimal currentSaleAcuenta);
                                        
                                        if (!isNewPedido)
                                        {
                                            var existingTotals = await apexSync.GetPedidoAsync(pedidoId.Value);
                                            if (existingTotals != null)
                                            {
                                                decimal newTotal = existingTotals.total + currentSaleTotal;
                                                decimal newAcuenta = existingTotals.acuenta + currentSaleAcuenta;
                                                await apexSync.UpdatePedidoTotalsAsync(pedidoId.Value, newTotal, newAcuenta);
                                            }
                                        }

                                        bool pagoOk = true;
                                        if (currentSaleAcuenta > 0)
                                        {
                                            // Fallback payment method for retries if we didn't save it tightly
                                            int pmtId = apexSync.GetTipoPagoId("Efectivo"); 
                                            pagoOk = await apexSync.CreateCajaChicaAsync(pedidoId.Value, currentSaleAcuenta, pmtId, sale.SellerName, sale.Timestamp);
                                        }
                                        
                                        if (pagoOk)
                                        {
                                            sale.Synced = 1;
                                            salesDb.Sales.Update(sale);
                                        }
                                    }
                                }
                            }
                        } catch (Exception) { /* Skip and try again next half hour */ }
                    }

                    // Re-sync Payments
                    foreach (var payment in pendingPayments)
                    {
                        try 
                        {
                            int? clientId = await apexSync.GetOrCreateClienteIdAsync(payment.PhoneNumber, payment.Client);
                            if (clientId.HasValue)
                            {
                                int? pedidoId = await apexSync.GetPedidoIdForTodayAsync(clientId.Value, payment.Timestamp);
                                if (pedidoId.HasValue)
                                {
                                    var existingTotals = await apexSync.GetPedidoAsync(pedidoId.Value);
                                    if (existingTotals != null)
                                    {
                                        decimal newAcuenta = existingTotals.acuenta + payment.Amount;
                                        await apexSync.UpdatePedidoTotalsAsync(pedidoId.Value, existingTotals.total, newAcuenta);
                                        
                                        int pmtId = apexSync.GetTipoPagoId(payment.PaymentMethod);
                                        bool pagoOk = await apexSync.CreateCajaChicaAsync(pedidoId.Value, payment.Amount, pmtId, payment.SellerName, payment.Timestamp);
                                        
                                        if (pagoOk)
                                        {
                                            payment.Synced = 1;
                                            salesDb.Payments.Update(payment);
                                        }
                                    }
                                }
                            }
                        } catch (Exception) { /* Skip and try again next half hour */ }
                    }

                    // Final commit for this batch
                    await salesDb.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RETRY JOB ERROR]: {ex.Message}");
            }
        }
    }
}

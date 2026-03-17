using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace WhatsAppTranscriptor
{
    // Oracle ORDS typical response wrapper for GET array
    public class OrdsResponse<T>
    {
        public T[] items { get; set; } = Array.Empty<T>();
    }

    public class OrdsCliente
    {
        public int cliente_id { get; set; }
        public string telefono { get; set; } = string.Empty;
        public string nombre { get; set; } = string.Empty;
    }

    public class OrdsProducto
    {
        public int producto_id { get; set; }
        public string nombre { get; set; } = string.Empty;
    }

    public class OrdsPedido
    {
        public int pedido_id { get; set; }
        public string fecha { get; set; } = string.Empty;
    }

    public class OrdsPedidoRecord
    {
        public int pedido_id { get; set; }
        public int cliente_id { get; set; }
        public string fecha { get; set; } = string.Empty;
        public decimal total { get; set; }
        public decimal acuenta { get; set; }
        public decimal saldo { get; set; }
        public string usuario { get; set; } = string.Empty;
        public int anulado { get; set; }
    }

    public class ApexSyncService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApexSyncService()
        {
            _httpClient = new HttpClient();
            _baseUrl = ConfigManager.Settings.OracleApexBaseUrl;
        }

        public async Task<int?> GetOrCreateClienteIdAsync(string phoneNumber, string pushName)
        {
            try
            {
                // 1. Try to find the client by phone number
                string query = $"{{\"telefono\": \"{phoneNumber}\"}}";
                string getUrl = $"{_baseUrl}/cliente/?q={Uri.EscapeDataString(query)}";
                
                var getResponse = await _httpClient.GetAsync(getUrl);
                if (getResponse.IsSuccessStatusCode)
                {
                    string jsonInfo = await getResponse.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OrdsResponse<OrdsCliente>>(jsonInfo);
                    
                    if (result != null && result.items.Length > 0)
                    {
                        return result.items[0].cliente_id;
                    }
                }

                // 2. If not found, create a new client
                var newClient = new
                {
                    nombre = string.IsNullOrEmpty(pushName) ? "Cliente WhatsApp" : pushName,
                    telefono = phoneNumber,
                    direccion = "",
                    correo_electronico = ""
                };

                var content = new StringContent(JsonSerializer.Serialize(newClient), Encoding.UTF8, "application/json");
                var postResponse = await _httpClient.PostAsync($"{_baseUrl}/cliente/", content);
                
                if (postResponse.IsSuccessStatusCode)
                {
                    string jsonInfo = await postResponse.Content.ReadAsStringAsync();
                    var created = JsonSerializer.Deserialize<OrdsCliente>(jsonInfo);
                    return created?.cliente_id;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APEX ERROR - Cliente]: {ex.Message}");
            }

            return null;
        }

        public async Task<int?> GetProductoIdAsync(string productName)
        {
            try
            {
                if (string.IsNullOrEmpty(productName)) return 1; // Default fallback if no product specified

                string query = $"{{\"nombre\": \"{productName}\"}}";
                string getUrl = $"{_baseUrl}/producto/?q={Uri.EscapeDataString(query)}";
                
                var response = await _httpClient.GetAsync(getUrl);
                if (response.IsSuccessStatusCode)
                {
                    string jsonInfo = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OrdsResponse<OrdsProducto>>(jsonInfo);
                    
                    if (result != null && result.items.Length > 0)
                    {
                        return result.items[0].producto_id;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APEX ERROR - Producto]: {ex.Message}");
            }
            // Fallback product ID if not found so the sale doesn't fail completely
            return 1; 
        }

        public int GetMaquinaId(string maquinaName)
        {
            if (string.IsNullOrWhiteSpace(maquinaName)) return 1; // 1 = Ninguno
            
            maquinaName = maquinaName.ToUpper();
            if (maquinaName.Contains("MIMAKI") && maquinaName.Contains("GALAXY")) return 7;
            if (maquinaName.Contains("MIMAKI")) return 28;
            if (maquinaName.Contains("QUILCA") && maquinaName.Contains("UV")) return 8;
            if (maquinaName.Contains("QUILCA")) return 70;
            if (maquinaName.Contains("KEANGRAFIC")) return 69;
            if (maquinaName.Contains("FORTUNE")) return 2;
            if (maquinaName.Contains("PUNO")) return 3;
            if (maquinaName.Contains("TEXTIL")) return 4;
            if (maquinaName.Contains("KONICA")) return 5;
            if (maquinaName.Contains("ROLLO")) return 6;
            if (maquinaName.Contains("CAMA PLANA")) return 48;
            if (maquinaName.Contains("DELIVERY")) return 68;
            if (maquinaName.Contains("NINGUNO")) return 1;

            return 1; // Default
        }

        public int GetTipoPagoId(string paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod)) return 2; // EFECTIVO
            
            paymentMethod = paymentMethod.ToUpper();
            if (paymentMethod.Contains("BCP")) return 1;
            if (paymentMethod.Contains("YAPE")) return 3;
            if (paymentMethod.Contains("PLIN")) return 4;
            if (paymentMethod.Contains("INTERBANK")) return 5;
            if (paymentMethod.Contains("BBVA")) return 25;
            if (paymentMethod.Contains("CONCILIACION")) return 45;
            if (paymentMethod.Contains("EFECTIVO")) return 2;

            return 2; // Default to Efectivo
        }

        public async Task<int?> GetPedidoIdForTodayAsync(int clienteId, DateTime localDate)
        {
            try
            {
                // Query orders specifically for this client
                string query = $"{{\"cliente_id\": {clienteId}}}";
                string getUrl = $"{_baseUrl}/pedido/?q={Uri.EscapeDataString(query)}";
                
                var response = await _httpClient.GetAsync(getUrl);
                if (response.IsSuccessStatusCode)
                {
                    string jsonInfo = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OrdsResponse<OrdsPedido>>(jsonInfo);
                    
                    if (result != null && result.items.Length > 0)
                    {
                        // Oracle usually returns dates starting with YYYY-MM-DD
                        string targetDatePrefix = localDate.ToString("yyyy-MM-dd");
                        
                        var todayOrders = result.items
                            .Where(p => !string.IsNullOrEmpty(p.fecha) && p.fecha.StartsWith(targetDatePrefix))
                            .OrderByDescending(p => p.pedido_id)
                            .ToList();
                            
                        if (todayOrders.Count > 0)
                        {
                            return todayOrders.First().pedido_id;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APEX ERROR - GetPedidoForToday]: {ex.Message}");
            }
            return null;
        }

        public async Task<int?> CreatePedidoAsync(int clienteId, string total, string acuenta, string sellerName, DateTime date)
        {
            try
            {
                // Format date for Oracle APEX using EXACT local time but appending 'Z' to trick 
                // ORDS into accepting the format without shifting it internally.
                string formattedDate = date.ToString("yyyy-MM-ddTHH:mm:ss") + "Z";
                
                // Parse numbers safely
                decimal.TryParse(total, out decimal dTotal);
                decimal.TryParse(acuenta, out decimal dAcuenta);

                var newPedido = new
                {
                    cliente_id = clienteId,
                    fecha = formattedDate,
                    total = dTotal,
                    acuenta = dAcuenta,
                    usuario = sellerName,
                    anulado = 0
                };

                var content = new StringContent(JsonSerializer.Serialize(newPedido), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/pedido/", content);
                
                if (response.IsSuccessStatusCode)
                {
                    string jsonInfo = await response.Content.ReadAsStringAsync();
                    var created = JsonSerializer.Deserialize<OrdsPedido>(jsonInfo);
                    return created?.pedido_id;
                }
                else
                {
                    Console.WriteLine($"[APEX ERROR - Pedido]: Failed to create, Status Code: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APEX ERROR - Pedido]: {ex.Message}");
            }
            return null;
        }

        public async Task<OrdsPedidoRecord?> GetPedidoAsync(int pedidoId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/pedido/{pedidoId}");
                if (response.IsSuccessStatusCode)
                {
                    string jsonInfo = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<OrdsPedidoRecord>(jsonInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APEX ERROR - GetPedido]: {ex.Message}");
            }
            return null;
        }

        public async Task<bool> UpdatePedidoTotalsAsync(int pedidoId, decimal newTotal, decimal newAcuenta)
        {
            try
            {
                // Only payload needed for the fields we want to update. ORDS PUT sometimes requires all non-null fields
                // but usually missing fields keep their defaults or we can PATCH. Let's use PUT and supply existing required fields.
                // Wait, if we use PUT we might override to null. Let's send the full object we got minus the ID.
                var existingPedido = await GetPedidoAsync(pedidoId);
                if (existingPedido == null) return false;
                
                existingPedido.total = newTotal;
                existingPedido.acuenta = newAcuenta;
                // Leave other fields intact
                
                var content = new StringContent(JsonSerializer.Serialize(existingPedido), Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{_baseUrl}/pedido/{pedidoId}", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[APEX ERROR - UpdatePedido]: Status Code: {response.StatusCode} - {error}");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APEX ERROR - UpdatePedido]: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> CreateDetallePedidoAsync(int pedidoId, int productoId, string cantidad, string subtotal, string longitud, string ancho, string maquinaName)
        {
            try
            {
                // Parse cleanly
                int.TryParse(cantidad, out int q);
                if (q == 0) q = 1;

                decimal.TryParse(subtotal, out decimal st);
                decimal.TryParse(longitud, out decimal l);
                decimal.TryParse(ancho, out decimal w);
                
                int maquinaId = GetMaquinaId(maquinaName);

                var newDetalle = new
                {
                    pedido_id = pedidoId,
                    producto_id = productoId,
                    cantidad = q,
                    subtotal = st,
                    largo = l,
                    ancho = w,
                    realizado = 0,
                    maquina_id = maquinaId
                };

                var content = new StringContent(JsonSerializer.Serialize(newDetalle), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/detalle_pedido/", content);
                
                if (response.IsSuccessStatusCode) return true;
                else
                {
                     Console.WriteLine($"[APEX ERROR - Detalle]: Failed to create, Status Code: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APEX ERROR - Detalle]: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> CreateCajaChicaAsync(int pedidoId, decimal monto, int tipoPagoId, string usuario, DateTime fecha)
        {
            try
            {
                var newCajaChica = new
                {
                    tipo_movimiento_id = 1, // Ingreso de dinero
                    monto = monto,
                    fecha = fecha.ToString("yyyy-MM-ddTHH:mm:ss") + "Z", // Trick ORDS to avoid +5 hours shift
                    descripcion = "Adelanto Venta",
                    usuario = usuario,
                    tipo_pago_id = tipoPagoId,
                    pedido_id = pedidoId,
                    tipo_flujo_dinero_id = 1 // INGRESO
                };

                var content = new StringContent(JsonSerializer.Serialize(newCajaChica), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/caja_chica/", content);
                
                if (response.IsSuccessStatusCode) return true;
                else Console.WriteLine($"[APEX ERROR - Caja]: Status Code {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APEX ERROR - Caja]: {ex.Message}");
            }
            return false;
        }
    }
}

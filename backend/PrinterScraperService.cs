using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace WhatsAppTranscriptor
{
    public class ApexRiplogResponse
    {
        public List<ApexRiplogItem> items { get; set; }
    }

    public class ApexRiplogItem
    {
        public int riplog_id { get; set; }
        public string filename { get; set; }
        public string estado { get; set; }
        public DateTime? fecha_hora { get; set; }
        public decimal? ancho { get; set; }
        public decimal? largo { get; set; }
        public string maquina_nombre { get; set; }
        public int detalle_id { get; set; }
    }

    public static class PrinterScraperService
    {
        private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static async Task<List<PrintJob>> FetchFromApexAsync(string apexBaseUrl)
        {
            var jobs = new List<PrintJob>();
            try
            {
                string cleanUrl = apexBaseUrl.TrimEnd('/');
                // Oracle APEX REST View URL for riplog
                string apiUrl = $"{cleanUrl}/riplog/";

                Console.WriteLine($"[Scraper APEX] Consultando APEX: {apiUrl} ...");
                string jsonResponse = await client.GetStringAsync(apiUrl);

                await Task.Run(() =>
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var payload = JsonSerializer.Deserialize<ApexRiplogResponse>(jsonResponse, options);

                    if (payload == null || payload.items == null)
                        return;

                    foreach (var item in payload.items)
                    {
                        var job = new PrintJob
                        {
                            Machine = item.maquina_nombre ?? "Unknown",
                            Type = item.estado ?? "RIP",
                            Name = item.filename ?? "-",
                            Copies = 1,
                            Width = item.ancho.HasValue ? item.ancho.Value.ToString("F2") : "-",
                            Length = item.largo.HasValue ? item.largo.Value.ToString("F2") : "-",
                            ParsedDate = item.fecha_hora.HasValue ? item.fecha_hora.Value.ToLocalTime() : null,
                            DateStr = item.fecha_hora.HasValue ? item.fecha_hora.Value.ToLocalTime().ToString("dd/MM/yyyy") : "-",
                            TimeStr = item.fecha_hora.HasValue ? item.fecha_hora.Value.ToLocalTime().ToString("HH:mm:ss") : "-"
                        };

                        jobs.Add(job);
                    }

                    Console.WriteLine($"[Scraper APEX] Éxito. {jobs.Count} trabajos extraídos de Oracle APEX.");
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Scraper APEX] Error: {ex.Message}");
            }

            return jobs;
        }
    }
}

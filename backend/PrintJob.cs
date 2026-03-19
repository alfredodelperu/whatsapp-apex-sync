using System;

namespace WhatsAppTranscriptor
{
    public class PrintJob
    {
        public string Machine { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "RIP" or "PRINT"
        public string Name { get; set; } = string.Empty;
        public string Width { get; set; } = "-";
        public string Length { get; set; } = "-";
        public int Copies { get; set; } = 1;
        public string DateStr { get; set; } = "-";
        public string TimeStr { get; set; } = "-";
        public DateTime? ParsedDate { get; set; } // For sorting internally
    }
}

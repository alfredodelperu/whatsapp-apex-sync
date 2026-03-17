# WhatsApp Transcriptor POS & APEX Sync

## Overview
This system is an automated, high-throughput B2B solution for transcribing structured sales and payment templates from WhatsApp Web into a normalized Oracle APEX database in real-time. It completely eliminates manual data entry for local point-of-sale sellers.

The project is composed of two independent modules communicating via localhost:
1. **The Web Scraper** (JavaScript Browser Extension)
2. **The Sync Engine** (C# Windows Backend Service)

---

## 🚀 1. Features
### Background Resilience
- **Zero Data Loss:** Any internet outage or Oracle APEX downtime does not drop sales. The C# Engine marks records as `Synced = 0` locally (SQLite) and a background Timer transparently re-attempts to push them every 30 minutes.
- **Deduplication:** The system captures WhatsApp's native `data-id` timestamp. A seller can spam a `#VENTA` template multiple times, or reload the page (F5), and the C# Engine will inherently reject the identical duplicates.

### Relational Mapping
- Automatically groups multiple distinct `#VENTA` messages from the same client into a single `PEDIDO` (Proforma) for the day, calculating absolute running totals.
- Distinguishes between Sales (`#VENTA`) and standalone Payments (`#PAGO`), creating `CAJA_CHICA` cash records directly mapped to the originating `PEDIDO` in Oracle.
- Primary Key lookup is done gracefully fallback-style via the Client's exact Phone Number to find missing `CLIENTE_ID`s in APEX before blindly creating duplicates.

### Multi-Tenancy Engine
- The C# Engine features a **Dual-Mode Parser**.
- By pasting a customized [config.json](file:///c:/Users/alfre/OneDrive/Desktop/whatsappweb_transcriptor/backend/config.json) next to `WhatsAppTranscriptor.exe`, the entire template schema can be changed without touching source code.
- Custom APEX Endpoints, Ports, Separators, and Template Keys (`#FACTURA` vs `#VENTA`, `Cli:` vs `Cliente:`) are fully dynamic.

---

## 🖥️ 2. Deployment Instructions

### A. The Firefox & Chrome Web Scraper
1. **Google Chrome (Managed):**
   - Head to the Chrome Developer Dashboard, upload `whatsapp-ext-v2.zip` as an **Unlisted** extension.
   - Provide the private Web Store link to your sellers. The Extension will auto-update Over-The-Air (OTA) anytime you push a new zip to the dashboard.
   
2. **Mozilla Firefox (Permanent Offline Install):**
   - Upload `whatsapp-ext-v2.zip` to the Mozilla Add-ons Hub selecting **"On my Own"**.
   - Download the digitally signed `.xpi` file.
   - Have the seller open Firefox and simply **Drag and Drop the `.xpi` file into the window** for a permanent, offline installation.

### B. The C# Backend Server
1. Navigate to the `backend/bin/Release/net8.0/win-x64/publish/` folder to find your compiled executable.
2. Copy `WhatsAppTranscriptor.exe` and `config.json` to any folder on the Seller's Windows PC.
3. Add a shortcut to the `.exe` in their `shell:startup` folder so the black console automatically turns on when they boot their computer.

---

## 📚 3. Configuration Reference (`config.json`)

If the file is deleted, the system falls back to strict hard-coded defaults.

```json
{
  "OracleApexBaseUrl": "https://g0b98d8ee45b90d-db5ncy1.adb.us-sanjose-1.oraclecloudapps.com/ords/fullcolor",
  "PuertoServidorLocal": 5000,
  "MinutosReintentoSync": 30,
  "Separator": " - ",
  "VentaPrefix": "#VENTA",
  "VentaTags": {
    "Vendedor": "Vendedor:",
    "Cliente": "Cliente:",
    "Tipo": "Tipo:",
    ...
  }
}
```

## ⚠️ Notes for IT Admins
- Google's strict "Single Purpose" policy demands that the extension requests `data_collection_permissions: ["none"]`. The custom `manifest.json` ensures the extension only acts within the local `localhost:5000` boundary to comply.
- If the port `5000` is blocked by Windows Firewall, prompt the seller to click **"Allow Access / Permitir Acceso"** the first time they open the `.exe`.

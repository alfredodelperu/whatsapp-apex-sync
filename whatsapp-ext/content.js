// Content script injected into web.whatsapp.com
console.log("WhatsApp Transcriptor Extension Loader Loaded!");

// Create a script tag to inject our real logic INTO the page itself
// This is necessary because Chrome extensions run in an 'isolated world',
// and can't easily access WhatsApp's internal JavaScript objects (like React or Store).
const script = document.createElement('script');
script.src = chrome.runtime.getURL('inject.js');
script.onload = function() {
    this.remove(); // Clean up after execution
};
(document.head || document.documentElement).appendChild(script);

// Listen for messages emitted from our injected script, and pass them to C#
window.addEventListener("WHATSAPP_TRANSCRIPTOR_MESSAGE", async function(event) {
    const data = event.detail;
    
    // Check if it's an array of messages (Batch Processing)
    if (Array.isArray(data)) {
        console.log(`WhatsApp Transcriptor: Received batch of ${data.length} messages.`);
        for (const msg of data) {
            await sendToBackend(msg);
            // 50ms artificial delay to prevent IPC flooding across the bridge
            await new Promise(r => setTimeout(r, 50)); 
        }
    } else {
        await sendToBackend(data);
    }
}, false);

async function sendToBackend(messageData) {
    console.log("WhatsApp Transcriptor [content.js]: Attempting to send message to background script...", messageData);
    
    // Wrap in a promise to correctly wait for the background script's response before sending the next one
    return new Promise((resolve) => {
        chrome.runtime.sendMessage({ type: "SEND_TO_BACKEND", data: messageData }, (response) => {
            if (chrome.runtime.lastError) {
                 console.error("WhatsApp Transcriptor [content.js]: 🚨 Error communicating with background script.", chrome.runtime.lastError);
                 resolve(false);
                 return;
            }

            if (response && response.success) {
                console.log("WhatsApp Transcriptor [content.js]: ✅ Message successfully forwarded to server!");
                resolve(true);
            } else {
                 console.error("WhatsApp Transcriptor [content.js]: ❌ Background script failed to send the message:", response ? response.error : "Unknown error");
                 resolve(false);
            }
        });
    });
}

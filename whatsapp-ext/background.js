// Background script to handle HTTP requests bypassing WhatsApp's CSP Block
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    if (request.type === "SEND_TO_BACKEND") {
        console.log("Background Script: Received message to send", request.data);
        
        fetch("http://localhost:5000/api/messages/", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(request.data)
        })
        .then(response => {
            if (response.ok) {
                 console.log("Background Script: ✅ Successfully sent to C# server.");
                 sendResponse({ success: true });
            } else {
                 console.error(`Background Script: ❌ Server responded with ${response.status}`);
                 sendResponse({ success: false, error: "Server Rejected" });
            }
        })
        .catch(error => {
            console.error("Background Script: 🚨 Fetch failed (Is C# Server running?)", error);
            sendResponse({ success: false, error: error.message });
        });

        return true; // Indicates async response
    }
});

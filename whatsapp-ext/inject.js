/* 
  Injected directly into WhatsApp Web's page context.
  This script uses a much more reliable DOM observation strategy
  by targetting WhatsApp's native data attributes.
*/
console.log("WhatsApp Transcriptor: Injected Script Running! Version 2.0");

let lastCheckedMessageText = "";

function extractMessageData(node) {
    // Attempt to find the div that truly contains the text using WhatsApp's structure
    // We look for elements that have text inside
    // Sometimes it's selectable-text copyable-text, sometimes just selectable-text
    const spanElement = node.querySelector('span.selectable-text, span.copyable-text');
    if (!spanElement) {
        // Let's print nodes that are messages but don't have the span we look for
        console.log("WhatsApp Transcriptor: Found message node, but no selectable-text span inside.", node);
        return null;
    }

    // Try to get innerText, but if it has child spans with emojis, we might need a more robust extraction later
    const text = spanElement.innerText;
    if (!text || text.trim() === '' || text === lastCheckedMessageText) return null;
    lastCheckedMessageText = text;

    let sender = "Client";
    
    // Determine if it's sent or received based on classes commonly found in outer containers
    // `.message-out` is the class for messages sent by the user
    // `.message-in` is the class for messages received
    const rowDiv = node.closest('.message-in, .message-out') || node;
    if (rowDiv && rowDiv.classList.contains('message-out')) {
        sender = "Me";
    }

    let timestamp = new Date().toISOString();
    let phoneNumber = "CurrentChat";
    
    // Try to extract timestamp from the span's closest container usually holding metadata
    const copyableTextContainer = node.closest('[data-pre-plain-text]');
    if (copyableTextContainer) {
        const metadata = copyableTextContainer.getAttribute('data-pre-plain-text');
        try {
            const timeMatch = metadata.match(/\[(.*?)\] (.*?):/);
            if (timeMatch) {
                timestamp = timeMatch[1];
                if (sender !== "Me") {
                    sender = timeMatch[2].trim();
                }
            }
        } catch(e) {}
    } else {
         console.log("WhatsApp Transcriptor: Could not find metadata [data-pre-plain-text]");
    }

    // Try to get the active chat's name/number from the header
    const mainElement = document.getElementById('main');
    const headerElement = mainElement ? mainElement.querySelector('header') : document.querySelector('header');
    
    if (headerElement) {
        const nameElements = headerElement.querySelectorAll('[dir="auto"]');
        for (const el of Array.from(nameElements)) {
            if (el.innerText && el.innerText.trim().length > 0) {
                 phoneNumber = el.innerText.trim();
                 break;
            }
        }
    }

    console.log(`WhatsApp Transcriptor: Extracted -> [${sender}] ${text}`);

    return {
        phoneNumber: phoneNumber,
        text: text,
        sender: sender,
        timestamp: timestamp
    };
}

const sentMessageHashes = new Set(); // Memory of sent messages to prevent duplicates

// Simple hash function to uniquely identify messages
function hashMessage(text, sender, timePart) {
    return `${sender}-${timePart}-${text.substring(0, 30)}`;
}

function pollForNewMessages() {
    // 1. Find all possible message bubbles
    const messageRows = document.querySelectorAll('.message-in, .message-out');
    
    let newMessagesBatch = []; // Optimize: Accumulate to prevent CustomEvent flooding

    messageRows.forEach(row => {
        // 2. Find the span that contains the actual text
        // WhatsApp currently uses various combinations of these classes for text
        const textSpans = row.querySelectorAll('span.selectable-text, span.copyable-text, span[dir="ltr"]');
        let messageText = null;

        // Find the first span that actually contains decent text length and isn't just a time or name
        for (const span of Array.from(textSpans)) {
             if (span.innerText && span.innerText.trim().length > 0 && !span.innerText.match(/^\d{1,2}:\d{2}/)) {
                 // Might be a good text
                 messageText = span.innerText.trim();
                 break;
             }
        }

        if (!messageText) return; // No text found in this bubble

        let sender = row.classList.contains('message-out') ? "Me" : "Client";
        
        // 1. Get the native WhatsApp Unique Message ID (data-id)
        // This is the most reliable way to uniquely identify a message and prevent F5 duplicates
        const dataIdNode = row.closest('[data-id]') || row.querySelector('[data-id]') || row;
        const dataId = dataIdNode.getAttribute('data-id');

        let msgHash = "";
        let rawPhoneNumber = "";

        if (dataId) {
            msgHash = dataId; // Perfect unique identifier
            // e.g. "false_51987934724@c.us_3EB0..."
            const phoneMatch = dataId.match(/_(\d+)(@c\.us|@s\.whatsapp\.net)/);
            if (phoneMatch) {
                rawPhoneNumber = '+' + phoneMatch[1];
            }
        }

        let timestamp = new Date().toISOString();
        const copyableTextContainer = row.closest('[data-pre-plain-text]') || row.querySelector('[data-pre-plain-text]');
        if (copyableTextContainer) {
            const metadata = copyableTextContainer.getAttribute('data-pre-plain-text');
            try {
                const timeMatch = metadata.match(/\[(.*?)\] (.*?):/);
                if (timeMatch) {
                    timestamp = timeMatch[1];
                    if (sender !== "Me") {
                        sender = timeMatch[2].trim();
                    }
                }
            } catch(e) {}
        }
        
        // Fallback hash if data-id is somehow missing
        if (!msgHash) {
            msgHash = hashMessage(messageText, sender, messageText.length.toString());
        }

        // If we already sent this message, skip
        if (sentMessageHashes.has(msgHash)) return;

        // Try getting phone number and name
        let chatName = "CurrentChat";
        
        // 2. WhatsApp usually puts the active chat in a <header> inside the #main div
        const mainElement = document.getElementById('main');
        const headerElement = mainElement ? mainElement.querySelector('header') : document.querySelector('header');
        
        if (headerElement) {
            // Main title is almost always the first element with text that has dir="auto"
            const nameElements = headerElement.querySelectorAll('[dir="auto"]');
            
            for (const el of Array.from(nameElements)) {
                if (el.innerText && el.innerText.trim().length > 0) {
                     chatName = el.innerText.trim(); // E.g., "Fullcolor Angelica..."
                     break;
                }
            }
            
            // Subtitle (Usually shows the phone number below the name if they aren't saved contacts)
            for(let sub of Array.from(nameElements)) {
                let text = sub.innerText || "";
                // If it looks like a phone number (e.g., +51 999 888 777 or starts with +)
                if (!rawPhoneNumber && text.startsWith('+') && /\d/.test(text)) {
                    rawPhoneNumber = text.replace(/[^\d+]/g, ''); // Extract just + and digits
                    break;
                }
            }
            
            // If the main title itself is a phone number, use it as raw phone number too
            if (!rawPhoneNumber && chatName.startsWith('+') && /\d/.test(chatName)) {
                rawPhoneNumber = chatName.replace(/[^\d+]/g, '');
            }
        }

        // Send and mark as seen
        console.log(`WhatsApp Transcriptor: [NEW] -> [${sender}] ${messageText.substring(0, 50)}...`);
        sentMessageHashes.add(msgHash);
        
        // Keep set size manageable in long sessions
        if (sentMessageHashes.size > 5000) sentMessageHashes.clear();
        
        newMessagesBatch.push({
            id: msgHash, // Pass the unique ID to C# to enforce strict deduplication
            phoneNumber: chatName, // We keep the property name 'phoneNumber' for compatibility with C# 'IncomingMessagePayload'
            rawPhoneNumber: rawPhoneNumber, // New property for the actual extracted digits
            text: messageText,
            sender: sender,
            timestamp: timestamp
        });
    });

    if (newMessagesBatch.length > 0) {
        dispathDataToExtension(newMessagesBatch);
    }
}

function dispathDataToExtension(data) {
    // Send a safe cross-script event back to our content.js
    const event = new CustomEvent("WHATSAPP_TRANSCRIPTOR_MESSAGE", { detail: data });
    window.dispatchEvent(event);
}

// Polling interval is more robust against React virtual DOM shenanigans than MutationObserver
console.log("WhatsApp Transcriptor: Starting message polling...");
setInterval(pollForNewMessages, 2000);

// Property Inspector JavaScript for XOutputRedux Stream Deck Plugin

let websocket = null;
let uuid = null;
let actionInfo = null;
let settings = {};

function connectElgatoStreamDeckSocket(inPort, inPropertyInspectorUUID, inRegisterEvent, inInfo, inActionInfo) {
    uuid = inPropertyInspectorUUID;
    actionInfo = JSON.parse(inActionInfo);
    settings = actionInfo.payload.settings || {};

    websocket = new WebSocket('ws://127.0.0.1:' + inPort);

    websocket.onopen = function() {
        // Register the Property Inspector
        websocket.send(JSON.stringify({
            event: inRegisterEvent,
            uuid: uuid
        }));

        // Update UI with current settings
        updateUI();
    };

    websocket.onmessage = function(evt) {
        const data = JSON.parse(evt.data);

        if (data.event === 'didReceiveSettings') {
            settings = data.payload.settings || {};
            updateUI();
        }

        // Handle messages from plugin (e.g., profile list)
        if (data.event === 'sendToPropertyInspector') {
            if (data.payload && data.payload.profiles) {
                if (typeof onProfilesReceived === 'function') {
                    onProfilesReceived(data.payload.profiles);
                }
            }
        }
    };
}

function updateUI() {
    // Override this function in each PI page
    if (typeof onSettingsLoaded === 'function') {
        onSettingsLoaded(settings);
    }
}

function saveSettings() {
    if (websocket && websocket.readyState === 1) {
        websocket.send(JSON.stringify({
            event: 'setSettings',
            context: uuid,
            payload: settings
        }));
    }
}

function setSetting(key, value) {
    settings[key] = value;
    saveSettings();
}

function getSetting(key, defaultValue) {
    return settings[key] !== undefined ? settings[key] : defaultValue;
}

function sendToPlugin(payload) {
    if (websocket && websocket.readyState === 1) {
        websocket.send(JSON.stringify({
            event: 'sendToPlugin',
            context: uuid,
            payload: payload
        }));
    }
}

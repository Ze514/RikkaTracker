const TEN_SECONDS_MS = 10 * 1000;
const FOCUS_CHECK_MS = 1000;
const RENOTIFY_MS = 10 * 1000;
const RECONNECT_FAIL_SLEEP = 5;
const WSURL = 'ws://127.0.0.1:8910';

let webSocket = null;
let isConnected = false;
let isChromeFocused = true;
let autoReConnectIntervalId = null;
let isSleep = false;
let activePage = null;
let reconnectFail = 0;
let notifyFailList = [];

init();

function init() {
    connect();
    startWatchFocus();
    startRenotify();
}

function connect() {
    webSocket = new WebSocket(WSURL);
    webSocket.onopen = () => {
        isConnected = true;
        isSleep = false;
        reconnectFail = 0;
        clearInterval(autoReConnectIntervalId);
        chrome.action.setIcon({ path: 'icons/active.png' });
        keepAlive();
        console.log('[RikkaTracker] Connected');
    };
    webSocket.onmessage = (event) => {
        if (event.data === 'sleep') {
            isSleep = true;
            calDuration();
        } else if (event.data === 'wake') {
            isSleep = false;
        }
    };
    webSocket.onclose = () => {
        isConnected = false;
        chrome.action.setIcon({ path: 'icons/inactive.png' });
        webSocket = null;
        startAutoReConnect();
    };
}

function startAutoReConnect() {
    clearInterval(autoReConnectIntervalId);
    autoReConnectIntervalId = setInterval(() => {
        if (!isConnected) {
            console.log('[RikkaTracker] Reconnecting...');
            connect();
            reconnectFail++;
            if (reconnectFail >= RECONNECT_FAIL_SLEEP && !isSleep) {
                isSleep = true;
            }
        }
    }, TEN_SECONDS_MS);
}

function keepAlive() {
    const keepAliveIntervalId = setInterval(() => {
        if (isConnected && webSocket) {
            webSocket.send('ping');
        } else {
            clearInterval(keepAliveIntervalId);
        }
    }, TEN_SECONDS_MS);
}

chrome.tabs.onActivated.addListener(async (e) => {
    const tab = await getTab(e.tabId);
    onActivePage(tab);
});

chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
    if (changeInfo.status === 'complete' && tab.active) {
        onActivePage(tab);
    }
});

function getTab(tabId) {
    return new Promise((resolve) => {
        chrome.tabs.get(tabId, (tab) => resolve(tab));
    });
}

function getCurrentTab() {
    return new Promise((resolve, reject) => {
        chrome.tabs.query({ active: true }, (tabs) => {
            if (tabs && tabs.length > 0) resolve(tabs[0]);
            else reject(tabs);
        });
    });
}

function isFocused() {
    return new Promise((resolve) => {
        chrome.windows.getCurrent((w) => resolve(w.focused));
    });
}

function onActivePage(tab) {
    if (isSleep) return;

    if (activePage && activePage.url) {
        if (activePage.url !== tab.url) {
            calDuration();
            setActive(tab);
        }
    } else {
        setActive(tab);
    }
}

function setActive(tab) {
    const { url, title, favIconUrl } = tab;
    if (url) {
        activePage = {
            url,
            title: title || '',
            icon: favIconUrl || '',
            startTime: new Date().getTime()
        };
    } else {
        activePage = null;
    }
}

function calDuration() {
    if (activePage && activePage.url) {
        const now = new Date().getTime();
        const duration = parseInt((now - activePage.startTime) / 1000);
        const activeTime = parseInt(activePage.startTime / 1000);
        const data = {
            Url: activePage.url,
            Title: activePage.title,
            Icon: activePage.icon,
            Duration: duration,
            ActiveTime: activeTime
        };
        activePage = null;
        notifyTai(data);
    }
}

function notifyTai(data) {
    if (isConnected && webSocket) {
        webSocket.send(JSON.stringify(data));
    } else {
        notifyFailList.push(data);
    }
}

function renotify() {
    if (isConnected && webSocket && notifyFailList.length > 0) {
        const item = notifyFailList.shift();
        notifyTai(item);
    }
}

function startRenotify() {
    setInterval(() => {
        renotify();
    }, RENOTIFY_MS);
}

function startWatchFocus() {
    setInterval(async () => {
        const focused = await isFocused();
        if (focused) {
            if (!isChromeFocused) {
                isChromeFocused = true;
                const tab = await getCurrentTab();
                onActivePage(tab);
            }
        } else {
            if (isChromeFocused) {
                isChromeFocused = false;
                calDuration();
            }
        }
    }, FOCUS_CHECK_MS);
}

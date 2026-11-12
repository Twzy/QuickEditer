window.quickEditer = window.quickEditer || {};

window.quickEditer.createReconnectHandler = function () {
    const overlay = document.getElementById('qe-reconnect-overlay');
    const message = document.getElementById('qe-reconnect-message');
    const reloadBtn = document.getElementById('qe-reconnect-reload');

    let retryCount = 0;
    const maxRetries = 5;
    const retryInterval = 3000;

    function showOverlay(text) {
        overlay.style.display = 'flex';
        message.textContent = text;
        reloadBtn.style.display = 'none';
    }

    function hideOverlay() {
        overlay.style.display = 'none';
        retryCount = 0;
    }

    function showReload() {
        message.textContent = '无法连接到服务器，请刷新页面重试。';
        reloadBtn.style.display = 'inline-flex';
        reloadBtn.onclick = () => window.location.reload();
    }

    return {
        onConnectionDown: async (options, error) => {
            showOverlay('连接已断开，正在尝试重新连接...');

            for (retryCount = 0; retryCount < maxRetries; retryCount++) {
                message.textContent = `正在尝试重新连接 (${retryCount + 1}/${maxRetries})...`;
                await new Promise(r => setTimeout(r, retryInterval));

                try {
                    const ok = await Blazor.reconnect();
                    if (ok) {
                        hideOverlay();
                        return;
                    }
                } catch {
                }
            }

            showReload();
        },
        onConnectionUp: () => {
            hideOverlay();
        }
    };
};
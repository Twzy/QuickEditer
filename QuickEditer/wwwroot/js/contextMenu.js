//文件路径：wwwroot/js/contextMenu.js


window.quickEditer = window.quickEditer || {};

window.quickEditer.registerContextMenuClose = function (dotNetRef) {
    // 先移除旧的监听，避免重复
    if (window.quickEditer._contextMenuHandler) {
        document.removeEventListener('mousedown', window.quickEditer._contextMenuHandler, true);
    }

    window.quickEditer._contextMenuHandler = function (e) {
        // 如果点击的是右键菜单内部，不关闭
        if (e.target.closest('.context-menu')) return;

        document.removeEventListener('mousedown', window.quickEditer._contextMenuHandler, true);
        window.quickEditer._contextMenuHandler = null;
        dotNetRef.invokeMethodAsync('CloseContextMenu');
    };

    // 用 capture 阶段，确保先于其它点击逻辑触发
    document.addEventListener('mousedown', window.quickEditer._contextMenuHandler, true);
};

window.quickEditer.unregisterContextMenuClose = function () {
    if (window.quickEditer._contextMenuHandler) {
        document.removeEventListener('mousedown', window.quickEditer._contextMenuHandler, true);
        window.quickEditer._contextMenuHandler = null;
    }
};

window.quickEditer.scrollManagerLogToBottom = function () {
    const el = document.querySelector('.qe-manager-mask-log-list');
    if (el) el.scrollTop = el.scrollHeight;
};